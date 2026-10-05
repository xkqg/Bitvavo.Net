// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Threading;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Errors;
using CryptoExchange.Net.SharedApis;

namespace Bitvavo.Net.Objects.Internal;

/// <summary>
/// Bitvavo exchange-wide constants + symbol-formatting helper. Mirrors <c>KrakenExchange</c>'s
/// shape — referenced by client classes (<see cref="Clients.SpotApi.BitvavoRestClientSpotApi.FormatSymbol"/>)
/// and by the CryptoExchange.Net Shared-API implementations as the canonical
/// <see cref="CryptoExchange.Net.Interfaces.Clients.IBaseApiClient.Exchange"/> identifier.
/// </summary>
public static class BitvavoExchange
{
    /// <summary>
    /// Exchange identifier used by CryptoExchange.Net for logging, tracking, and as the
    /// <see cref="CryptoExchange.Net.Interfaces.Clients.IBaseApiClient.Exchange"/> value. Mirrors
    /// <c>KrakenExchange.ExchangeName</c> / <c>BinanceExchange.ExchangeName</c>.
    /// </summary>
    public const string ExchangeName = "Bitvavo";

    /// <summary>
    /// Platform metadata — identity, links and supported environments — as <c>KrakenExchange.Metadata</c>; the Shared API's
    /// <c>Discover()</c> reports it next to the capabilities each API supports.
    /// </summary>
    public static PlatformInfo Metadata { get; } = new PlatformInfo(
        ExchangeName,
        "Bitvavo",
        "https://raw.githubusercontent.com/xkqg/Bitvavo.Net/master/Bitvavo.Net/icon.png",
        "https://bitvavo.com",
        ["https://docs.bitvavo.com/"],
        PlatformType.CryptoCurrencyExchange,
        CentralizationType.Centralized,
        BitvavoEnvironment.All);

    /// <summary>Bitvavo's default weight budget per minute: 1000 points, tracked per account (authenticated) or per IP address (unauthenticated).</summary>
    public const int WeightPerMinute = 1000;

    /// <summary>The weight of the heaviest single request Bitvavo documents (open orders / cancel orders without a market, atomic cancel).</summary>
    internal const int MaxRequestWeight = 100;

    private static BitvavoRateLimiters _rateLimiter = new();

    /// <summary>
    /// The client-side rate limiter every Bitvavo REST client in the process counts its requests against. Bitvavo's budget is
    /// process-wide by nature (it belongs to the account or the IP address, not to a client instance), so this is one static,
    /// settable object — as <c>KrakenExchange.RateLimiter</c> and <c>BinanceExchange.RateLimiter</c> are. Replace it to change the
    /// headroom or the allocated limit, or to subscribe to its events; set it at start-up, since a replacement starts counting from zero.
    /// </summary>
    /// <exception cref="ArgumentNullException">The value is null.</exception>
    public static BitvavoRateLimiters RateLimiter
    {
        get => Volatile.Read(ref _rateLimiter);
        set => Volatile.Write(ref _rateLimiter, value ?? throw new ArgumentNullException(nameof(value)));
    }

    /// <summary>
    /// The ONE serialization policy of every Bitvavo request. Decimals travel as JSON strings (<c>"amount":"0.5"</c> — Bitvavo's
    /// wire form for amounts and prices), enums as their mapped wire strings and DateTimes as unix-millisecond numbers (the
    /// CryptoExchange.Net defaults). Parameters are sorted ordinally and case-insensitively, so the query string and the JSON
    /// body — which the HMAC signs byte for byte — never depend on the host culture or the invariant-globalization switch.
    /// </summary>
    internal static readonly ParameterSerializationSettings ParameterSerializationSettings = new()
    {
        Decimal = DecimalSerialization.String,
        SortComparer = System.StringComparer.OrdinalIgnoreCase,
    };

    /// <summary>
    /// Format a base+quote pair into Bitvavo's wire convention: <c>BASE-QUOTE</c>
    /// uppercase, dash-separated. E.g. <c>("eth", "eur") → "ETH-EUR"</c>.
    /// </summary>
    public static string FormatSymbol(string baseAsset, string quoteAsset, TradingMode tradingMode, System.DateTime? deliverDate = null)
        => baseAsset.ToUpperInvariant() + "-" + quoteAsset.ToUpperInvariant();
}
