// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bitvavo.Net.Clients.SpotApi;
using Bitvavo.Net.Extensions;
using Bitvavo.Net.Interfaces.Clients;
using Bitvavo.Net.Interfaces.Clients.SpotApi;
using Bitvavo.Net.Objects.Options;
using CryptoExchange.Net.Objects;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests;

/// <summary>
/// The DI registration as 0.4.0 consumers call it (<c>AddBitvavo()</c> and the REST/socket options delegates): the clients
/// resolve with the right lifetime, <c>PostConfigure</c> keeps working, and the typed HTTP client of the REST client carries the
/// configured timeout and a handler that is never rotated underneath it. The library-options overloads are in
/// <see cref="BitvavoLibraryOptionsRegistrationTests"/>, the Shared API client in <see cref="BitvavoSharedApiClientRegistrationTests"/>.
/// </summary>
public class BitvavoServiceCollectionExtensionsTests
{
    [Fact]
    public void The_rest_client_is_registered_per_resolution_and_the_socket_client_as_a_singleton()
    {
        var services = new ServiceCollection().AddBitvavo();

        services.Last(d => d.ServiceType == typeof(IBitvavoRestClient)).Lifetime.ShouldBe(ServiceLifetime.Transient);
        services.Last(d => d.ServiceType == typeof(IBitvavoSocketClient)).Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }

    [Fact]
    public void Both_clients_and_their_spot_apis_resolve()
    {
        using var provider = new ServiceCollection().AddBitvavo().BuildServiceProvider();

        provider.GetRequiredService<IBitvavoRestClient>().SpotApi.ShouldNotBeNull();
        provider.GetRequiredService<IBitvavoSocketClient>().SpotApi.ShouldNotBeNull();
        provider.GetRequiredService<IBitvavoRestClientSpotApi>().ShouldNotBeNull();
        provider.GetRequiredService<IBitvavoSocketClientSpotApi>().ShouldNotBeNull();
    }

    [Fact]
    public void Named_arguments_bind_the_rest_and_socket_options_overload()
    {
        var services = new ServiceCollection();

        services.AddBitvavo(restOptionsDelegate: o => o.ReceiveWindowMs = 4_000, socketOptionsDelegate: o => o.ReceiveWindowMs = 5_000);

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IOptions<BitvavoRestOptions>>().Value.ReceiveWindowMs.ShouldBe(4_000);
        provider.GetRequiredService<IOptions<BitvavoSocketOptions>>().Value.ReceiveWindowMs.ShouldBe(5_000);
    }

    /// <summary>
    /// A consumer's <c>PostConfigure</c> (the execution package makes the rate limiter fail fast that way) runs after every
    /// <c>Configure</c>, so it must win over the options the registration made.
    /// </summary>
    [Fact]
    public void PostConfigure_wins_over_the_options_of_the_legacy_overload()
    {
        var services = new ServiceCollection().AddBitvavo(o => o.ReceiveWindowMs = 8_000, o => o.ReceiveWindowMs = 8_000);
        services.PostConfigure<BitvavoRestOptions>(o => o.RateLimitingBehaviour = RateLimitingBehaviour.Fail);
        services.PostConfigure<BitvavoSocketOptions>(o => o.SocketNoDataTimeout = TimeSpan.FromSeconds(11));
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<BitvavoRestOptions>>().Value.RateLimitingBehaviour.ShouldBe(RateLimitingBehaviour.Fail);
        provider.GetRequiredService<IOptions<BitvavoSocketOptions>>().Value.SocketNoDataTimeout.ShouldBe(TimeSpan.FromSeconds(11));
        ((BitvavoRestClientSpotApi)provider.GetRequiredService<IBitvavoRestClient>().SpotApi).ClientOptions.RateLimitingBehaviour
            .ShouldBe(RateLimitingBehaviour.Fail);
    }

    /// <summary>
    /// The factory-made default <c>HttpClient</c> waits 100 s and ignores the library's connection settings; the typed client
    /// must enforce <see cref="BitvavoRestOptions.RequestTimeout"/>. The wire is the test seam: a transport that never answers.
    /// </summary>
    [Fact]
    public async Task Di_rest_client_timeout_equals_RequestTimeout()
    {
        var services = new ServiceCollection().AddBitvavo(o => o.RequestTimeout = TimeSpan.FromMilliseconds(250));
        services.ConfigureHttpClientDefaults(builder => builder.ConfigurePrimaryHttpMessageHandler(() => new HangingHttpMessageHandler()));
        services.AddHttpClient(typeof(IBitvavoRestClient).Name).ConfigurePrimaryHttpMessageHandler(() => new HangingHttpMessageHandler());
        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IBitvavoRestClient>();
        using var guard = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        guard.CancelAfter(TimeSpan.FromSeconds(5));

        var watch = Stopwatch.StartNew();
        var result = await client.SpotApi.ExchangeData.GetServerTimeAsync(guard.Token);
        watch.Stop();

        result.Success.ShouldBeFalse();
        watch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(3), "the 250 ms request timeout must end the call, not the 5 s guard");
    }

    [Fact]
    public void The_rest_http_handler_is_never_rotated_underneath_the_client()
    {
        using var provider = new ServiceCollection().AddBitvavo().BuildServiceProvider();

        var options = provider.GetRequiredService<IOptionsMonitor<HttpClientFactoryOptions>>().Get(typeof(IBitvavoRestClient).Name);

        options.HandlerLifetime.ShouldBe(Timeout.InfiniteTimeSpan);
    }
}
