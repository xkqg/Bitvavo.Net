// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bitvavo.Net.Clients;
using Bitvavo.Net.Clients.SpotApi;
using Bitvavo.Net.Extensions;
using Bitvavo.Net.Interfaces.Clients;
using Bitvavo.Net.Interfaces.Clients.SpotApi;
using Bitvavo.Net.Objects.Options;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Options;
using CryptoExchange.Net.SharedApis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests;

/// <summary>
/// The library-wide registration overloads — <c>AddBitvavo(IConfiguration)</c> and <c>AddBitvavo(Action&lt;BitvavoOptions&gt;)</c> —
/// next to the legacy REST/socket options overload. Every one of them registers through the options pipeline, so a consumer's
/// <c>PostConfigure</c> wins; the old positional call shapes keep binding to the old overload.
/// </summary>
public class BitvavoLibraryOptionsRegistrationTests
{
    public enum OverloadKind
    {
        LegacyNoArguments,
        LegacyDelegates,
        LibraryOptionsDelegate,
        Configuration,
    }

    public static IEnumerable<object[]> Overloads() => Enum.GetValues<OverloadKind>().Select(kind => new object[] { kind });

    private static IServiceCollection Register(IServiceCollection services, OverloadKind kind) => kind switch
    {
        OverloadKind.LegacyNoArguments => services.AddBitvavo(),
        OverloadKind.LegacyDelegates => services.AddBitvavo(
            restOptionsDelegate: o => o.ReceiveWindowMs = 8_000,
            socketOptionsDelegate: o => o.ReceiveWindowMs = 8_000),
        OverloadKind.LibraryOptionsDelegate => services.AddBitvavo((BitvavoOptions o) => o.Rest.ReceiveWindowMs = 8_000),
        OverloadKind.Configuration => services.AddBitvavo(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Rest:ReceiveWindowMs"] = "8000" })
            .Build()),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    [Theory]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Transient)]
    public void The_socket_client_lifetime_follows_SocketClientLifeTime(ServiceLifetime lifetime)
    {
        var services = new ServiceCollection().AddBitvavo(o => o.SocketClientLifeTime = lifetime);

        services.Last(d => d.ServiceType == typeof(IBitvavoSocketClient)).Lifetime.ShouldBe(lifetime);
        services.Last(d => d.ServiceType == typeof(IBitvavoSocketClientSpotApi)).Lifetime.ShouldBe(lifetime);
    }

    /// <summary>
    /// A consumer's <c>PostConfigure</c> runs after every <c>Configure</c>, so it must win whichever overload registered the
    /// options — which a pre-built options singleton would not allow.
    /// </summary>
    [Theory]
    [MemberData(nameof(Overloads))]
    public void PostConfigure_wins_for_every_AddBitvavo_overload(OverloadKind kind)
    {
        var services = Register(new ServiceCollection(), kind);
        services.PostConfigure<BitvavoRestOptions>(o => o.RateLimitingBehaviour = RateLimitingBehaviour.Fail);
        services.PostConfigure<BitvavoSocketOptions>(o => o.SocketNoDataTimeout = TimeSpan.FromSeconds(11));
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOptions<BitvavoRestOptions>>().Value.RateLimitingBehaviour.ShouldBe(RateLimitingBehaviour.Fail);
        provider.GetRequiredService<IOptions<BitvavoSocketOptions>>().Value.SocketNoDataTimeout.ShouldBe(TimeSpan.FromSeconds(11));
        ((BitvavoRestClientSpotApi)provider.GetRequiredService<IBitvavoRestClient>().SpotApi).ClientOptions.RateLimitingBehaviour
            .ShouldBe(RateLimitingBehaviour.Fail);
    }

    /// <summary>
    /// 0.4.0 took one positional <c>Action&lt;BitvavoRestOptions&gt;</c>. Over a member both option types carry (the credentials),
    /// a lambda would silently rebind to the new library-options overload — and hand the credentials to the socket client too.
    /// </summary>
    [Fact]
    public void Positional_rest_lambda_binds_the_rest_options_overload()
    {
        var services = new ServiceCollection();

        services.AddBitvavo(o => o.ApiCredentials = new BitvavoCredentials("rest-key", "rest-secret"));

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IOptions<BitvavoRestOptions>>().Value.ApiCredentials.ShouldNotBeNull();
        provider.GetRequiredService<IOptions<BitvavoSocketOptions>>().Value.ApiCredentials.ShouldBeNull();
    }

    [Fact]
    public void A_lambda_over_the_library_options_binds_the_library_options_overload_by_itself()
    {
        var services = new ServiceCollection();

        services.AddBitvavo(o => o.SharedApi.PreferredTransport = SharedTransport.Socket);

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IOptions<BitvavoOptions>>().Value.SharedApi.PreferredTransport.ShouldBe(SharedTransport.Socket);
    }

    [Fact]
    public void The_library_options_delegate_flows_to_the_rest_the_socket_and_the_library_options()
    {
        var services = new ServiceCollection();

        services.AddBitvavo((BitvavoOptions o) =>
        {
            o.ApiCredentials = new BitvavoCredentials("lib-key", "lib-secret");
            o.Rest.ReceiveWindowMs = 3_333;
            o.Socket.ReceiveWindowMs = 6_666;
        });

        using var provider = services.BuildServiceProvider();
        var rest = provider.GetRequiredService<IOptions<BitvavoRestOptions>>().Value;
        var socket = provider.GetRequiredService<IOptions<BitvavoSocketOptions>>().Value;
        var library = provider.GetRequiredService<IOptions<BitvavoOptions>>().Value;
        rest.ReceiveWindowMs.ShouldBe(3_333);
        rest.ApiCredentials.ShouldNotBeNull().Spot.ShouldNotBeNull().Key.ShouldBe("lib-key");
        socket.ReceiveWindowMs.ShouldBe(6_666);
        socket.ApiCredentials.ShouldNotBeNull().Spot.ShouldNotBeNull().Key.ShouldBe("lib-key");
        library.Rest.ReceiveWindowMs.ShouldBe(3_333, "the registered library options carry the whole Bitvavo-specific rest section");
        library.Socket.ReceiveWindowMs.ShouldBe(6_666);
    }

    [Fact]
    public void The_configuration_overload_binds_the_sections_and_normalises_them()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Rest:ReceiveWindowMs"] = "7000",
                ["Socket:ReceiveWindowMs"] = "9000",
                ["ApiCredentials:Spot:Key"] = "cfg-key",
                ["ApiCredentials:Spot:Secret"] = "cfg-secret",
                ["Environment:Name"] = "Live",
            })
            .Build();

        using var provider = new ServiceCollection().AddBitvavo(configuration).BuildServiceProvider();

        var rest = provider.GetRequiredService<IOptions<BitvavoRestOptions>>().Value;
        var socket = provider.GetRequiredService<IOptions<BitvavoSocketOptions>>().Value;
        rest.ReceiveWindowMs.ShouldBe(7_000);
        rest.Environment.ShouldBeSameAs(BitvavoEnvironment.Live);
        rest.ApiCredentials.ShouldNotBeNull().Spot.ShouldNotBeNull().Key.ShouldBe("cfg-key");
        socket.ReceiveWindowMs.ShouldBe(9_000);
        socket.ApiCredentials.ShouldNotBeNull().Spot.ShouldNotBeNull().Key.ShouldBe("cfg-key");
    }

    [Fact]
    public void The_configuration_overload_rejects_an_unknown_environment_when_registering()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Environment:Name"] = "staging" })
            .Build();

        Should.Throw<InvalidOperationException>(() => new ServiceCollection().AddBitvavo(configuration));
    }

    private static IServiceCollection RegisterWithKey(IServiceCollection services, OverloadKind kind, string key) => kind switch
    {
        OverloadKind.LegacyDelegates => services.AddBitvavo(
            restOptionsDelegate: o => o.ApiCredentials = new BitvavoCredentials(key, "secret"),
            socketOptionsDelegate: o => o.ApiCredentials = new BitvavoCredentials(key, "secret")),
        OverloadKind.LibraryOptionsDelegate => services.AddBitvavo((BitvavoOptions o) => o.ApiCredentials = new BitvavoCredentials(key, "secret")),
        OverloadKind.Configuration => services.AddBitvavo(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ApiCredentials:Spot:Key"] = key, ["ApiCredentials:Spot:Secret"] = "secret" })
            .Build()),
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    /// <summary>
    /// What a registration sets beats the process-wide defaults of <c>SetDefaultOptions</c> on every overload that sets credentials —
    /// the legacy overload always did; the library-wide ones start from the same defaults and must not let them win.
    /// </summary>
    [Theory]
    [InlineData(OverloadKind.LegacyDelegates)]
    [InlineData(OverloadKind.LibraryOptionsDelegate)]
    [InlineData(OverloadKind.Configuration)]
    public void The_credentials_of_a_registration_beat_the_process_wide_defaults(OverloadKind kind)
    {
        var previousRest = BitvavoRestOptions.Default;
        var previousSocket = BitvavoSocketOptions.Default;
        try
        {
            BitvavoRestClient.SetDefaultOptions(o => o.ApiCredentials = new BitvavoCredentials("default-key", "default-secret"));
            BitvavoSocketClient.SetDefaultOptions(o => o.ApiCredentials = new BitvavoCredentials("default-key", "default-secret"));

            using var provider = RegisterWithKey(new ServiceCollection(), kind, "registered-key").BuildServiceProvider();

            provider.GetRequiredService<IOptions<BitvavoRestOptions>>().Value.ApiCredentials.ShouldNotBeNull().Spot.ShouldNotBeNull().Key
                .ShouldBe("registered-key");
            provider.GetRequiredService<IOptions<BitvavoSocketOptions>>().Value.ApiCredentials.ShouldNotBeNull().Spot.ShouldNotBeNull().Key
                .ShouldBe("registered-key");
        }
        finally
        {
            BitvavoRestOptions.Default = previousRest;
            BitvavoSocketOptions.Default = previousSocket;
        }
    }

    /// <summary>
    /// <c>IBitvavoRestClient</c> is transient: what one client does to its own options (<c>SetOptions</c> writes the timeout and the
    /// proxy) must not change the options of the container or of the next client.
    /// </summary>
    [Fact]
    public void SetOptions_on_one_rest_client_leaves_the_container_options_and_the_next_client_alone()
    {
        var services = new ServiceCollection().AddBitvavo(restOptionsDelegate: o => o.RequestTimeout = TimeSpan.FromSeconds(20));
        services.PostConfigure<BitvavoRestOptions>(o => o.Proxy = new ApiProxy("http://127.0.0.1", 8888));
        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<IBitvavoRestClient>();

        first.SetOptions(new UpdateOptions<BitvavoCredentials> { RequestTimeout = TimeSpan.FromSeconds(1) });

        first.ClientOptions.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(1), "the client itself takes the change");
        var shared = provider.GetRequiredService<IOptions<BitvavoRestOptions>>().Value;
        shared.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(20));
        shared.Proxy.ShouldNotBeNull();
        var next = provider.GetRequiredService<IBitvavoRestClient>();
        next.ClientOptions.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(20));
        next.ClientOptions.Proxy.ShouldNotBeNull();
    }

    [Fact]
    public void SetOptions_on_one_transient_socket_client_leaves_the_container_options_and_the_next_client_alone()
    {
        var services = new ServiceCollection().AddBitvavo((BitvavoOptions o) =>
        {
            o.SocketClientLifeTime = ServiceLifetime.Transient;
            o.Socket.RequestTimeout = TimeSpan.FromSeconds(20);
        });
        using var provider = services.BuildServiceProvider();
        var first = provider.GetRequiredService<IBitvavoSocketClient>();

        first.SetOptions(new UpdateOptions<BitvavoCredentials> { RequestTimeout = TimeSpan.FromSeconds(1) });

        first.ClientOptions.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(1), "the client itself takes the change");
        provider.GetRequiredService<IOptions<BitvavoSocketOptions>>().Value.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(20));
        provider.GetRequiredService<IBitvavoSocketClient>().ClientOptions.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(20));
    }

    /// <summary>The 0.4.0 registration was <c>TryAdd</c>: an application's own client, registered first, is the one that resolves.</summary>
    [Fact]
    public void A_socket_client_registered_before_AddBitvavo_stays_the_one_resolved()
    {
        using var mine = new BitvavoSocketClient();
        using var provider = new ServiceCollection().AddSingleton<IBitvavoSocketClient>(mine).AddBitvavo().BuildServiceProvider();

        provider.GetRequiredService<IBitvavoSocketClient>().ShouldBeSameAs(mine);
        provider.GetRequiredService<IBitvavoSocketClientSpotApi>().ShouldBeSameAs(mine.SpotApi);
    }

    [Fact]
    public void A_rest_client_registered_before_AddBitvavo_stays_the_one_resolved()
    {
        using var mine = new BitvavoRestClient();
        using var provider = new ServiceCollection().AddSingleton<IBitvavoRestClient>(mine).AddBitvavo().BuildServiceProvider();

        provider.GetRequiredService<IBitvavoRestClient>().ShouldBeSameAs(mine);
        provider.GetRequiredService<IBitvavoRestClientSpotApi>().ShouldBeSameAs(mine.SpotApi);
    }

    /// <summary>Two modules that both call <c>AddBitvavo</c> must not give a multi-exchange consumer the venue twice, nor two socket pools.</summary>
    [Theory]
    [MemberData(nameof(Overloads))]
    public void Registering_twice_registers_the_clients_once(OverloadKind kind)
    {
        using var provider = Register(Register(new ServiceCollection(), kind), kind).BuildServiceProvider();

        provider.GetServices<IBitvavoRestClient>().Count().ShouldBe(1);
        provider.GetServices<IBitvavoSocketClient>().Count().ShouldBe(1);
        provider.GetServices<IBitvavoSharedApiClient>().Count().ShouldBe(1);
        provider.GetServices<ISharedApiClientBase>().Count().ShouldBe(1);
        provider.GetServices<IGetTickerRest>().Count().ShouldBe(1);
        provider.GetServices<ISpotTickerRestClient>().Count().ShouldBe(1);
    }

    /// <summary>The clients register once, the options do not: the later call's settings win, as in every options pipeline.</summary>
    [Fact]
    public void A_second_registration_still_applies_its_options()
    {
        using var provider = new ServiceCollection()
            .AddBitvavo(restOptionsDelegate: o => o.ReceiveWindowMs = 1_000)
            .AddBitvavo(restOptionsDelegate: o => o.ReceiveWindowMs = 2_000)
            .BuildServiceProvider();

        provider.GetRequiredService<IOptions<BitvavoRestOptions>>().Value.ReceiveWindowMs.ShouldBe(2_000);
        ((BitvavoRestClientSpotApi)provider.GetRequiredService<IBitvavoRestClient>().SpotApi).ClientOptions.ReceiveWindowMs.ShouldBe(2_000);
    }

    /// <summary>
    /// The capability interfaces resolve through the Shared API client, which holds both transports: with a scoped socket client
    /// they resolve from a scope (the container refuses a scoped service from the root with scope validation on).
    /// </summary>
    [Fact]
    public void With_a_scoped_socket_client_the_capability_interfaces_resolve_from_a_scope()
    {
        using var provider = new ServiceCollection()
            .AddBitvavo((BitvavoOptions o) => o.SocketClientLifeTime = ServiceLifetime.Scoped)
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        using var scope = provider.CreateScope();

        scope.ServiceProvider.GetRequiredService<IGetKlinesRest>().ShouldNotBeNull();
        scope.ServiceProvider.GetRequiredService<IGetTicker>().ShouldNotBeNull();
        scope.ServiceProvider.GetRequiredService<IBitvavoSharedApiClient>().ShouldNotBeNull();
        provider.GetRequiredService<IBitvavoRestClient>().ShouldNotBeNull();
    }

    /// <summary>The typed HTTP client of the library-options overload enforces the timeout like the legacy one: same registration path.</summary>
    [Fact]
    public async Task Di_rest_client_timeout_equals_RequestTimeout_for_the_library_options_overload()
    {
        var services = new ServiceCollection().AddBitvavo((BitvavoOptions o) => o.Rest.RequestTimeout = TimeSpan.FromMilliseconds(250));
        services.ConfigureHttpClientDefaults(builder => builder.ConfigurePrimaryHttpMessageHandler(() => new HangingHttpMessageHandler()));
        services.AddHttpClient(typeof(IBitvavoRestClient).Name).ConfigurePrimaryHttpMessageHandler(() => new HangingHttpMessageHandler());
        using var provider = services.BuildServiceProvider();
        var client = provider.GetRequiredService<IBitvavoRestClient>();
        using var guard = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        guard.CancelAfter(TimeSpan.FromSeconds(5));

        var watch = System.Diagnostics.Stopwatch.StartNew();
        var result = await client.SpotApi.ExchangeData.GetServerTimeAsync(guard.Token);
        watch.Stop();

        result.Success.ShouldBeFalse();
        watch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(3));
    }
}
