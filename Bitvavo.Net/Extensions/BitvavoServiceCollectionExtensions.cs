// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using Bitvavo.Net.Clients;
using Bitvavo.Net.Interfaces.Clients;
using Bitvavo.Net.Interfaces.Clients.SpotApi;
using Bitvavo.Net.Objects.Options;
using CryptoExchange.Net;
using CryptoExchange.Net.SharedApis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bitvavo.Net.Extensions;

/// <summary>
/// DI registration for Bitvavo.Net — mirrors <c>AddBinance()</c> / <c>AddKraken()</c> from the JKorf family. Registers
/// <see cref="IBitvavoRestClient"/> as transient (per resolution, on a typed <see cref="System.Net.Http.HttpClient"/> that carries the
/// configured timeout), <see cref="IBitvavoSocketClient"/> as a singleton (one shared WebSocket pool process-wide) unless
/// the <c>SocketClientLifeTime</c> of <see cref="BitvavoOptions"/> says otherwise, and the Shared API client with every capability interface.
/// </summary>
/// <remarks>
/// <para>Every overload registers its options through the options pipeline (<c>Configure</c>), never as a ready-made singleton, so a
/// <c>PostConfigure&lt;BitvavoRestOptions&gt;</c> of the application runs after them and wins whichever overload was used. What a
/// registration sets beats the process-wide defaults of <c>SetDefaultOptions</c>, the lowest layer.</para>
/// <para>The clients register once (<c>TryAdd</c>, as in 0.4.0): a client the application registered first stays the one that resolves,
/// and a second <c>AddBitvavo</c> adds its options — the later call's settings win — but no second set of clients, so the first call
/// decides their lifetimes. Each REST and socket client gets a copy of the options: <c>SetOptions</c> on one client does not reach
/// the container or the next client.</para>
/// <para>The capability interfaces (<c>IGetKlinesRest</c>, <c>IGetTicker</c>, …) resolve through <see cref="IBitvavoSharedApiClient"/>,
/// which holds both transports: with a scoped <c>SocketClientLifeTime</c> they resolve from a scope, not from the root.</para>
/// </remarks>
public static class BitvavoServiceCollectionExtensions
{
    /// <summary>
    /// Register Bitvavo REST + WebSocket clients with optional <see cref="BitvavoRestOptions"/> and
    /// <see cref="BitvavoSocketOptions"/> configurators (the 0.4.0 shape, unchanged). At language version C# 13 or later a lambda
    /// written as the first positional argument keeps binding here — the library-wide
    /// <see cref="AddBitvavo(IServiceCollection, Action{BitvavoOptions})"/> is chosen only by a lambda that cannot apply to the
    /// REST options (one that touches <c>Rest</c>, <c>Socket</c> or <c>SharedApi</c>). Below C# 13 — the default of a net8.0
    /// project, whichever compiler builds it — the compiler ignores the priority attribute that decides this: a lambda over a
    /// member both option types carry (<c>ApiCredentials</c>, <c>Environment</c>) binds the library-wide overload and
    /// <c>AddBitvavo(null)</c> is ambiguous. Set <c>&lt;LangVersion&gt;13.0&lt;/LangVersion&gt;</c> or type the lambda parameter.
    /// </summary>
    /// <param name="services">The DI container to extend.</param>
    /// <param name="restOptionsDelegate">Optional configurator for REST options (timeouts, credentials, etc.).</param>
    /// <param name="socketOptionsDelegate">Optional configurator for WebSocket options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    [OverloadResolutionPriority(1)]
    public static IServiceCollection AddBitvavo(
        this IServiceCollection services,
        Action<BitvavoRestOptions>? restOptionsDelegate = null,
        Action<BitvavoSocketOptions>? socketOptionsDelegate = null)
    {
        services.Configure<BitvavoRestOptions>(o =>
        {
            BitvavoRestOptions.Default.Set(o);
            restOptionsDelegate?.Invoke(o);
        });
        services.Configure<BitvavoSocketOptions>(o =>
        {
            BitvavoSocketOptions.Default.Set(o);
            socketOptionsDelegate?.Invoke(o);
        });
        services.AddOptions<BitvavoOptions>();

        return services.AddBitvavoClients(ServiceLifetime.Singleton);
    }

    /// <summary>
    /// Register Bitvavo from a configuration section (for example <c>configuration.GetSection("Bitvavo")</c>): the shared
    /// environment and credentials, the <c>Rest</c> and <c>Socket</c> sections and <c>SharedApi</c>. An unknown environment
    /// name throws here, at registration.
    /// </summary>
    /// <param name="services">The DI container to extend.</param>
    /// <param name="configuration">The configuration to bind.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddBitvavo(this IServiceCollection services, IConfiguration configuration)
        => services.AddBitvavoCore(BitvavoOptions.CreateFromConfiguration(configuration));

    /// <summary>Register Bitvavo with the library-wide <see cref="BitvavoOptions"/>.</summary>
    /// <param name="services">The DI container to extend.</param>
    /// <param name="optionsDelegate">Configures the options: shared environment and credentials, the REST and socket sections, the Shared API.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddBitvavo(this IServiceCollection services, Action<BitvavoOptions> optionsDelegate)
        => services.AddBitvavoCore(BitvavoOptions.Create(optionsDelegate));

    /// <summary>Feeds the already-normalised options into the options pipeline, then registers the clients.</summary>
    private static IServiceCollection AddBitvavoCore(this IServiceCollection services, BitvavoOptions options)
    {
        services.AddOptions<BitvavoRestOptions>().Configure(o => options.Rest.Set(o));
        services.AddOptions<BitvavoSocketOptions>().Configure(o => options.Socket.Set(o));
        services.AddOptions<BitvavoOptions>().Configure(options.CopyTo);

        return services.AddBitvavoClients(options.SocketClientLifeTime);
    }

    /// <summary>
    /// Registers the clients the way 0.4.0 did, <c>TryAdd</c>: a client the application registered first stays the one that
    /// resolves, and a second <c>AddBitvavo</c> adds its options (the later call's settings win) but no second set of clients.
    /// </summary>
    private static IServiceCollection AddBitvavoClients(this IServiceCollection services, ServiceLifetime? socketClientLifetime)
    {
        if (!services.IsRegistered<IBitvavoRestClient>())
        {
            // A typed client: the factory hands the REST client an HttpClient that carries the configured timeout and the library's
            // connection settings, and a handler that is never rotated underneath it. The client gets a copy of the options, so
            // what it does to them (SetOptions) does not reach the container or the next client.
            services.AddHttpClient<IBitvavoRestClient, BitvavoRestClient>((httpClient, provider) =>
                {
                    var options = provider.GetRequiredService<IOptions<BitvavoRestOptions>>().Value.Copy();
                    httpClient.Timeout = options.RequestTimeout;
                    return new BitvavoRestClient(httpClient, provider.GetService<ILoggerFactory>(), Microsoft.Extensions.Options.Options.Create(options));
                })
                .ConfigurePrimaryHttpMessageHandler(provider =>
                    LibraryHelpers.CreateHttpClientMessageHandler(provider.GetRequiredService<IOptions<BitvavoRestOptions>>().Value))
                .SetHandlerLifetime(Timeout.InfiniteTimeSpan);
        }

        services.TryAddTransient(provider => provider.GetRequiredService<IBitvavoRestClient>().SpotApi);

        var socketLifetime = socketClientLifetime ?? ServiceLifetime.Singleton;
        services.TryAdd(new ServiceDescriptor(
            typeof(IBitvavoSocketClient),
            provider => new BitvavoSocketClient(
                provider.GetService<ILoggerFactory>(),
                Microsoft.Extensions.Options.Options.Create(provider.GetRequiredService<IOptions<BitvavoSocketOptions>>().Value.Copy())),
            socketLifetime));
        services.TryAdd(new ServiceDescriptor(
            typeof(IBitvavoSocketClientSpotApi),
            provider => provider.GetRequiredService<IBitvavoSocketClient>().SpotApi,
            socketLifetime));

        if (!services.IsRegistered<IBitvavoSharedApiClient>())
        {
            services.RegisterSharedRestInterfaces(provider => provider.GetRequiredService<IBitvavoRestClient>().SpotApi.SharedClient);
            services.RegisterSharedSocketInterfaces(provider => provider.GetRequiredService<IBitvavoSocketClient>().SpotApi.SharedClient);
            services.RegisterSharedApiClient<IBitvavoSharedApiClient, BitvavoSharedApiClient>(sharedApis => sharedApis
                .Add(client => client.SpotRest)
                .Add(client => client.SpotSocket));
        }

        return services;
    }

    private static bool IsRegistered<TService>(this IServiceCollection services)
        => services.Any(descriptor => descriptor.ServiceType == typeof(TService));
}
