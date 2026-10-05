// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using Bitvavo.Net.Enums;
using CryptoExchange.Net.Converters.SystemTextJson;

namespace Bitvavo.Net.Objects.Models.Spot;

/// <summary>
/// Bitvavo market (trading pair) descriptor as returned by the public
/// <c>GET /v2/markets</c> endpoint. One entry per spot pair. Optional fields are nullable because the live API
/// leaves some of them null (<see cref="PricePrecision"/> is null on every market today) and adds values over time.
/// </summary>
public record BitvavoMarket
{
    /// <summary>Market identifier — Bitvavo uses dash-separated pair, e.g. <c>"ETH-EUR"</c>.</summary>
    [JsonPropertyName("market")]
    public string Market { get; init; } = string.Empty;

    /// <summary>
    /// Trading status. Null when the field is absent or carries a value this library does not know — never
    /// <see cref="BitvavoMarketStatus.Trading"/> by default, so an unknown status cannot be mistaken for an open market.
    /// </summary>
    [JsonPropertyName("status")]
    public BitvavoMarketStatus? Status { get; init; }

    /// <summary>Base asset symbol (e.g. <c>"ETH"</c>).</summary>
    [JsonPropertyName("base")]
    public string BaseAsset { get; init; } = string.Empty;

    /// <summary>Quote asset symbol (e.g. <c>"EUR"</c>).</summary>
    [JsonPropertyName("quote")]
    public string QuoteAsset { get; init; } = string.Empty;

    /// <summary>
    /// Significant digits of prices on this market. The specification declares it an integer but the live API returns null for
    /// every market; derive the price grid from <see cref="TickSize"/> instead.
    /// </summary>
    [JsonPropertyName("pricePrecision")]
    public int? PricePrecision { get; init; }

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

    /// <summary>Number of decimals allowed in an order's base-asset <c>amount</c>.</summary>
    [JsonPropertyName("quantityDecimals")]
    public int? QuantityDecimals { get; init; }

    /// <summary>Number of decimals allowed in an order's quote-asset value (<c>amountQuote</c> and the notional).</summary>
    [JsonPropertyName("notionalDecimals")]
    public int? NotionalDecimals { get; init; }

    /// <summary>The price grid: an order's <c>price</c> must be a multiple of this (otherwise error 422).</summary>
    [JsonPropertyName("tickSize"), JsonConverter(typeof(DecimalConverter))]
    public decimal? TickSize { get; init; }

    /// <summary>Maximum number of open orders on this market; null when Bitvavo states no limit (the live API returns 100, 200, 400 or null).</summary>
    [JsonPropertyName("maxOpenOrders")]
    public int? MaxOpenOrders { get; init; }

    /// <summary>Fee category of the market (<c>"A"</c>, <c>"B"</c>, <c>"C"</c>, <c>"D"</c> today; a string so new categories never break the read).</summary>
    [JsonPropertyName("feeCategory")]
    public string? FeeCategory { get; init; }
}
