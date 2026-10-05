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
using CryptoExchange.Net.Objects.Options;
using CryptoExchange.Net.RateLimiting.Interfaces;
using CryptoExchange.Net.SharedApis;
using Microsoft.Extensions.Logging;

namespace Bitvavo.Net.Clients.SpotApi;

/// <inheritdoc cref="IBitvavoRestClientSpotApi" />
/// <remarks>
/// The CryptoExchange.Net Shared API (V1 and V2 on one instance) lives in <see cref="BitvavoRestClientSpotSharedApi"/>, which
/// this client creates and exposes as <see cref="SharedClient"/> and <see cref="SharedApi"/>. Mirrors <c>KrakenRestClientSpotApi</c>.
/// </remarks>
internal sealed class BitvavoRestClientSpotApi : RestApiClient<BitvavoEnvironment, BitvavoAuthenticationProvider, BitvavoCredentials>, IBitvavoRestClientSpotApi
{
    /// <summary>
    /// The gate every endpoint definition is created with — one stable indirection to the current
    /// <see cref="BitvavoExchange.RateLimiter"/> (the weight budgets, the headroom, the events), so replacing the limiter
    /// takes effect for endpoints that were already called. A definition is cached on first use and keeps its gate for good.
    /// </summary>
    internal static IRateLimitGate RateLimitGate => BitvavoRateLimitGate.Instance;

    private readonly BitvavoRestClientSpotSharedApi _sharedApi;

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

    /// <inheritdoc />
    public IBitvavoRestClientSpotApiReport Report { get; }

    /// <inheritdoc />
    public IBitvavoRestClientSpotApiInstitutional Institutional { get; }

    /// <inheritdoc />
    public IBitvavoRestClientSpotApiShared SharedClient => _sharedApi;

    /// <inheritdoc />
    public IBitvavoRestClientSpotSharedApi SharedApi => _sharedApi;

    internal BitvavoRestClientSpotApi(ILoggerFactory? loggerFactory, HttpClient? httpClient, BitvavoRestOptions options)
        : base(loggerFactory, BitvavoExchange.ExchangeName, httpClient, options.Environment.SpotRestBaseAddress, options, options.SpotOptions)
    {
        ExchangeData = new BitvavoRestClientSpotApiExchangeData(this);
        Account = new BitvavoRestClientSpotApiAccount(this);
        Trading = new BitvavoRestClientSpotApiTrading(this);
        Funding = new BitvavoRestClientSpotApiFunding(this);
        Report = new BitvavoRestClientSpotApiReport(this);
        Institutional = new BitvavoRestClientSpotApiInstitutional(this);
        _sharedApi = new BitvavoRestClientSpotSharedApi(this);
    }

    /// <inheritdoc />
    public override string FormatSymbol(string baseAsset, string quoteAsset, TradingMode tradingMode, DateTime? deliverDate = null)
        => BitvavoExchange.FormatSymbol(baseAsset, quoteAsset, tradingMode, deliverDate);

    /// <inheritdoc />
    protected override BitvavoAuthenticationProvider CreateAuthenticationProvider(BitvavoCredentials credentials)
        => new(credentials, ClientOptions.ReceiveWindowMs);

    private readonly object _credentialsGate = new();

    /// <summary>
    /// The signing provider, read under the lock that <see cref="SetApiCredentials"/> and <see cref="SetOptions"/> hold while they
    /// replace the key. CryptoExchange.Net 13.1.0 replaces the provider in several steps; a request that reads it in between finds
    /// none, and an authenticated call then fails as if no credentials were set.
    /// </summary>
    public override BitvavoAuthenticationProvider? AuthenticationProvider
    {
        get
        {
            lock (_credentialsGate)
            {
                return base.AuthenticationProvider;
            }
        }
    }

    /// <inheritdoc />
    public override void SetApiCredentials(BitvavoCredentials credentials)
    {
        lock (_credentialsGate)
        {
            base.SetApiCredentials(credentials);
        }
    }

    /// <inheritdoc />
    public override void SetOptions(UpdateOptions<BitvavoCredentials> options)
    {
        lock (_credentialsGate)
        {
            base.SetOptions(options);
        }
    }

    /// <summary>
    /// The exchange clock for <c>AutoTimestamp</c>: the public <c>GET /v2/time</c>. Public on purpose — a time request that
    /// had to be signed would need the very time sync it is performing, and the framework repeats the sync before signed
    /// requests whenever it fails.
    /// </summary>
    protected override async Task<HttpResult<DateTime>> GetServerTimestampAsync()
    {
        var result = await ExchangeData.GetServerTimeAsync().ConfigureAwait(false);
        return result.Success ? HttpResult.Ok(result, result.Data.Time) : HttpResult.Fail<DateTime>(result);
    }

    /// <inheritdoc />
    protected override IMessageSerializer CreateSerializer()
        => new SystemTextJsonMessageSerializer(new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

    /// <summary>
    /// Internal SendAsync wrapper — fills the Bitvavo Spot REST base address so call sites in the *Data partials stay terse.
    /// Always passes a fresh empty additional-headers dictionary so the auth provider has a concrete <c>Headers</c> bag to
    /// add the four Bitvavo-Access-* signed-request headers to. Mirrors KrakenRestClientSpotApi.SendAsync.
    /// </summary>
    internal Task<HttpResult<T>> SendAsync<T>(RequestDefinition definition, Parameters? parameters, CancellationToken cancellationToken, int? weight = null)
        => SendAsync<T>(definition, parameters, cancellationToken, new System.Collections.Generic.Dictionary<string, string>(), weight);

    /// <summary>
    /// Internal SendAsync wrapper for endpoints that need separate query + body parameter collections (signed POST/PUT).
    /// Splits the framework's two-collection overload to keep sub-client call sites terse and ensures a non-null additional-headers
    /// dictionary is always present for the auth provider to fill in.
    /// </summary>
    internal Task<HttpResult<T>> SendAsync<T>(RequestDefinition definition, Parameters? queryParameters, Parameters? bodyParameters, CancellationToken cancellationToken, int? weight = null)
        => SendAsync<T>(definition, queryParameters, bodyParameters, cancellationToken, new System.Collections.Generic.Dictionary<string, string>(), weight);
}
