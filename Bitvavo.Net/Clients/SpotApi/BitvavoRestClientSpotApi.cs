// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Bitvavo.Net.Clients.MessageHandlers;
using Bitvavo.Net.Interfaces.Clients.SpotApi;
using Bitvavo.Net.Objects.Internal;
using Bitvavo.Net.Objects.Options;
using CryptoExchange.Net.Clients;
using CryptoExchange.Net.Converters.MessageParsing.DynamicConverters;
using CryptoExchange.Net.Converters.SystemTextJson;
using CryptoExchange.Net.Interfaces;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Errors;
using CryptoExchange.Net.RateLimiting;
using CryptoExchange.Net.RateLimiting.Filters;
using CryptoExchange.Net.RateLimiting.Guards;
using CryptoExchange.Net.RateLimiting.Interfaces;
using CryptoExchange.Net.SharedApis;
using Microsoft.Extensions.Logging;

namespace Bitvavo.Net.Clients.SpotApi;

/// <inheritdoc cref="IBitvavoRestClientSpotApi" />
internal sealed class BitvavoRestClientSpotApi : RestApiClient<BitvavoEnvironment, BitvavoAuthenticationProvider, BitvavoCredentials>, IBitvavoRestClientSpotApi
{
    /// <summary>
    /// Per-host weight gate — Bitvavo enforces <see cref="BitvavoExchange.WeightPerMinute"/>
    /// (1000) per IP per rolling minute. Endpoints opt in by passing this gate to
    /// <c>_definitions.GetOrCreate(method, path, RateLimitGate, weight, authenticated)</c>;
    /// per-endpoint weight tables are tracked for v0.4.0 rollout (see CHANGELOG).
    /// </summary>
    internal static readonly IRateLimitGate RateLimitGate = new RateLimitGate("Bitvavo")
        .AddGuard(new RateLimitGuard(
            keySelector: RateLimitGuard.PerHost,
            filter: new HostFilter("api.bitvavo.com"),
            limit: BitvavoExchange.WeightPerMinute,
            timeSpan: TimeSpan.FromMinutes(1),
            windowType: RateLimitWindowType.Sliding));

    /// <inheritdoc />
    public new BitvavoRestOptions ClientOptions => (BitvavoRestOptions)base.ClientOptions;

    /// <inheritdoc />
    protected override ErrorMapping ErrorMapping => BitvavoErrors.SpotMapping;

    /// <inheritdoc />
    protected override IRestMessageHandler MessageHandler { get; } = new BitvavoRestSpotMessageHandler(BitvavoErrors.SpotMapping);

    /// <inheritdoc />
    public IBitvavoRestClientSpotApiExchangeData ExchangeData { get; }

    /// <inheritdoc />
    public IBitvavoRestClientSpotApiAccount Account { get; }

    /// <inheritdoc />
    public IBitvavoRestClientSpotApiTrading Trading { get; }

    /// <inheritdoc />
    public IBitvavoRestClientSpotApiFunding Funding { get; }

    /// <summary>Display name of the exchange — used by CryptoExchange.Net diagnostics.</summary>
    public string ExchangeName => BitvavoExchange.ExchangeName;

    internal BitvavoRestClientSpotApi(ILogger logger, HttpClient? httpClient, BitvavoRestOptions options)
        : base(logger, httpClient, options.Environment.SpotRestBaseAddress, options, options.SpotOptions)
    {
        ExchangeData = new BitvavoRestClientSpotApiExchangeData(this);
        Account = new BitvavoRestClientSpotApiAccount(this);
        Trading = new BitvavoRestClientSpotApiTrading(this);
        Funding = new BitvavoRestClientSpotApiFunding(this);
    }

    /// <inheritdoc />
    public override string FormatSymbol(string baseAsset, string quoteAsset, TradingMode tradingMode, DateTime? deliverDate = null)
        => BitvavoExchange.FormatSymbol(baseAsset, quoteAsset, tradingMode, deliverDate);

    /// <inheritdoc />
    protected override BitvavoAuthenticationProvider CreateAuthenticationProvider(BitvavoCredentials credentials)
        => new(credentials, ClientOptions.ReceiveWindowMs);

    /// <inheritdoc />
    protected override IMessageSerializer CreateSerializer() => new SystemTextJsonMessageSerializer(new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

    /// <summary>
    /// Internal SendAsync wrapper — fills the Bitvavo Spot REST base address so call sites in the *Data partials stay terse.
    /// Always passes a fresh empty additional-headers dictionary so the auth provider has a concrete <c>Headers</c> bag to
    /// add the four Bitvavo-Access-* signed-request headers to. Mirrors KrakenRestClientSpotApi.SendAsync.
    /// </summary>
    internal Task<WebCallResult<T>> SendAsync<T>(RequestDefinition definition, ParameterCollection? parameters, CancellationToken cancellationToken, int? weight = null)
        => SendAsync<T>(BaseAddress, definition, parameters, cancellationToken, new System.Collections.Generic.Dictionary<string, string>(), weight);

    /// <summary>
    /// Internal SendAsync wrapper for endpoints that need separate query + body parameter collections (signed POST/PUT).
    /// Splits the framework's two-collection overload to keep sub-client call sites terse and ensures a non-null additional-headers
    /// dictionary is always present for the auth provider to fill in.
    /// </summary>
    internal Task<WebCallResult<T>> SendAsync<T>(RequestDefinition definition, ParameterCollection? queryParameters, ParameterCollection? bodyParameters, CancellationToken cancellationToken, int? weight = null)
        => SendAsync<T>(BaseAddress, definition, queryParameters, bodyParameters, cancellationToken, new System.Collections.Generic.Dictionary<string, string>(), weight);
}
