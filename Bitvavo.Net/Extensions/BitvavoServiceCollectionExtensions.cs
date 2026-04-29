// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Net.Http;
using Bitvavo.Net.Clients;
using Bitvavo.Net.Interfaces.Clients;
using Bitvavo.Net.Interfaces.Clients.SpotApi;
using Bitvavo.Net.Objects.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bitvavo.Net.Extensions;

/// <summary>
/// DI registration helpers for Bitvavo.Net — mirrors <c>AddBinance()</c> / <c>AddKraken()</c>
/// from the JKorf family. Registers <see cref="IBitvavoRestClient"/> as transient (per-resolution)
/// and <see cref="IBitvavoSocketClient"/> as singleton (one shared WebSocket pool process-wide).
/// </summary>
public static class BitvavoServiceCollectionExtensions
{
    /// <summary>
    /// Register Bitvavo REST + WebSocket clients with optional <see cref="BitvavoRestOptions"/>
    /// and <see cref="BitvavoSocketOptions"/> configurators.
    /// </summary>
    /// <param name="services">The DI container to extend.</param>
    /// <param name="restOptionsDelegate">Optional configurator for REST options (timeouts, credentials, etc.).</param>
    /// <param name="socketOptionsDelegate">Optional configurator for WebSocket options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddBitvavo(
        this IServiceCollection services,
        Action<BitvavoRestOptions>? restOptionsDelegate = null,
        Action<BitvavoSocketOptions>? socketOptionsDelegate = null)
    {
        services.Configure<BitvavoRestOptions>(o =>
        {
            BitvavoRestOptions.Default?.Set(o);
            restOptionsDelegate?.Invoke(o);
        });
        services.Configure<BitvavoSocketOptions>(o =>
        {
            BitvavoSocketOptions.Default?.Set(o);
            socketOptionsDelegate?.Invoke(o);
        });

        services.AddHttpClient();

        // Explicit factories — both BitvavoRestClient and BitvavoSocketClient expose two
        // ctors (Action<TOptions> and DI-friendly), which the activator can't disambiguate.
        services.TryAddTransient<IBitvavoRestClient>(sp => new BitvavoRestClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(),
            sp.GetService<ILoggerFactory>(),
            sp.GetRequiredService<IOptions<BitvavoRestOptions>>()));
        services.TryAddTransient(sp => sp.GetRequiredService<IBitvavoRestClient>().SpotApi);

        services.TryAddSingleton<IBitvavoSocketClient>(sp => new BitvavoSocketClient(
            sp.GetService<ILoggerFactory>(),
            sp.GetRequiredService<IOptions<BitvavoSocketOptions>>()));
        services.TryAddSingleton(sp => sp.GetRequiredService<IBitvavoSocketClient>().SpotApi);

        return services;
    }
}
