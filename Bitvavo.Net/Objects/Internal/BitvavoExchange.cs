// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using CryptoExchange.Net.Objects.Errors;
using CryptoExchange.Net.SharedApis;

namespace Bitvavo.Net.Objects.Internal;

/// <summary>
/// Bitvavo exchange-wide constants + symbol-formatting helper. Mirrors KrakenExchange's
/// shape — referenced by client classes (<see cref="Clients.SpotApi.BitvavoRestClientSpotApi.FormatSymbol"/>).
/// </summary>
internal static class BitvavoExchange
{
    /// <summary>Display name used by CryptoExchange.Net for logging + tracking.</summary>
    public const string ExchangeName = "Bitvavo";

    /// <summary>Bitvavo public-tier weight budget per minute (per source IP). 1000 weight/min.</summary>
    public const int WeightPerMinute = 1000;

    /// <summary>
    /// Format a base+quote pair into Bitvavo's wire convention: <c>BASE-QUOTE</c>
    /// uppercase, dash-separated. E.g. <c>("eth", "eur") → "ETH-EUR"</c>.
    /// </summary>
    public static string FormatSymbol(string baseAsset, string quoteAsset, TradingMode tradingMode, System.DateTime? deliverDate = null)
        => baseAsset.ToUpperInvariant() + "-" + quoteAsset.ToUpperInvariant();
}

/// <summary>
/// Bitvavo error mappings consumed by the message handler. v0.1 ships an empty mapping
/// (Bitvavo's <c>{ "errorCode": int, "error": "msg" }</c> envelope is parsed generically);
/// later releases will populate per-code <see cref="ErrorInfo"/> entries.
/// </summary>
internal static class BitvavoErrors
{
    public static readonly ErrorMapping SpotMapping = new(System.Array.Empty<ErrorInfo>(), System.Array.Empty<ErrorEvaluator>());
}
