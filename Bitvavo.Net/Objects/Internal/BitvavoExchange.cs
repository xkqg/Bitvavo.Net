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
/// Bitvavo error mappings consumed by the REST + WebSocket message handlers. The Bitvavo
/// error envelope shape is uniform — <c>{ "errorCode": int, "error": "msg" }</c> — so the
/// generic handler can parse every response without per-code metadata; this static class
/// is the seam for populating <see cref="ErrorInfo"/> entries (e.g. <c>IsTransient</c>,
/// <see cref="ErrorType"/>) per-code as the table is built up.
/// </summary>
internal static class BitvavoErrors
{
    /// <summary>
    /// Spot-API error mapping. v0.3 ships empty: every server error funnels through
    /// <see cref="ErrorMapping.GetErrorInfo(string, string)"/> which returns
    /// <see cref="ErrorInfo.Unknown"/> for missing codes — perfectly safe, just less
    /// granular for retry/transient classification. Populate per-code entries before v1.0.
    /// </summary>
    public static readonly ErrorMapping SpotMapping = new(System.Array.Empty<ErrorInfo>(), System.Array.Empty<ErrorEvaluator>());
}
