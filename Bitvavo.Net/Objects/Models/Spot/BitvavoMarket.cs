// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Bitvavo.Net.Objects.Models.Spot;

/// <summary>
/// Bitvavo market (trading pair) descriptor as returned by the public
/// <c>GET /v2/markets</c> endpoint. One entry per spot pair.
/// </summary>
public record BitvavoMarket
{
    /// <summary>Market identifier — Bitvavo uses dash-separated pair, e.g. <c>"ETH-EUR"</c>.</summary>
    [JsonPropertyName("market")]
    public string Market { get; init; } = string.Empty;

    /// <summary>Trading status — typical values: <c>"trading"</c>, <c>"halted"</c>, <c>"auction"</c>.</summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    /// <summary>Base asset symbol (e.g. <c>"ETH"</c>).</summary>
    [JsonPropertyName("base")]
    public string BaseAsset { get; init; } = string.Empty;

    /// <summary>Quote asset symbol (e.g. <c>"EUR"</c>).</summary>
    [JsonPropertyName("quote")]
    public string QuoteAsset { get; init; } = string.Empty;

    /// <summary>Significant digits used by Bitvavo when displaying prices for this market. Bitvavo returns this as a string in the JSON envelope.</summary>
    [JsonPropertyName("pricePrecision")]
    public string PricePrecision { get; init; } = string.Empty;

    /// <summary>Minimum order size, expressed in the base asset.</summary>
    [JsonPropertyName("minOrderInBaseAsset")]
    public string MinOrderInBaseAsset { get; init; } = string.Empty;

    /// <summary>Minimum order size, expressed in the quote asset.</summary>
    [JsonPropertyName("minOrderInQuoteAsset")]
    public string MinOrderInQuoteAsset { get; init; } = string.Empty;

    /// <summary>Maximum order size, expressed in the base asset.</summary>
    [JsonPropertyName("maxOrderInBaseAsset")]
    public string MaxOrderInBaseAsset { get; init; } = string.Empty;

    /// <summary>Maximum order size, expressed in the quote asset.</summary>
    [JsonPropertyName("maxOrderInQuoteAsset")]
    public string MaxOrderInQuoteAsset { get; init; } = string.Empty;

    /// <summary>Order types supported on this market (e.g. <c>["market", "limit", "stopLoss"]</c>).</summary>
    [JsonPropertyName("orderTypes")]
    public IReadOnlyList<string> OrderTypes { get; init; } = new List<string>();
}
