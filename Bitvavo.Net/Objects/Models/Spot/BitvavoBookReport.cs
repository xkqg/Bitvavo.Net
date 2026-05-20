// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using CryptoExchange.Net.Converters.SystemTextJson;

namespace Bitvavo.Net.Objects.Models.Spot;

/// <summary>
/// MiCA-regulatory order-book snapshot as returned by <c>GET /v2/report/{market}/book</c>.
/// Follows the EU Markets-in-Crypto-Assets reporting schema — the <see cref="Bids"/> /
/// <see cref="Asks"/> arrays carry per-price-level aggregates, not the raw order book.
/// </summary>
public record BitvavoBookReport
{
    /// <summary>Timestamp when the order book was submitted to the database (UTC).</summary>
    [JsonPropertyName("submissionTimestamp"), JsonConverter(typeof(DateTimeConverter))]
    public DateTime SubmissionTimestamp { get; init; }

    /// <summary>Digital-Token-Identifier (DTI) code or symbol of the asset.</summary>
    [JsonPropertyName("assetCode")]
    public string AssetCode { get; init; } = string.Empty;

    /// <summary>Full name of the asset.</summary>
    [JsonPropertyName("assetName")]
    public string AssetName { get; init; } = string.Empty;

    /// <summary>DTI code or symbol of the currency the prices are expressed in.</summary>
    [JsonPropertyName("priceCurrency")]
    public string PriceCurrency { get; init; } = string.Empty;

    /// <summary>Price notation — <c>MONE</c> for a monetary value.</summary>
    [JsonPropertyName("priceNotation")]
    public string PriceNotation { get; init; } = string.Empty;

    /// <summary>Currency the quantities are expressed in.</summary>
    [JsonPropertyName("quantityCurrency")]
    public string QuantityCurrency { get; init; } = string.Empty;

    /// <summary>Quantity notation — <c>CRYP</c> for crypto units.</summary>
    [JsonPropertyName("quantityNotation")]
    public string QuantityNotation { get; init; } = string.Empty;

    /// <summary>Market Identifier Code of the Bitvavo trading platform.</summary>
    [JsonPropertyName("venue")]
    public string Venue { get; init; } = string.Empty;

    /// <summary>Identifier of the trading system.</summary>
    [JsonPropertyName("tradingSystem")]
    public string TradingSystem { get; init; } = string.Empty;

    /// <summary>Timestamp when the book snapshot was added to the database (UTC).</summary>
    [JsonPropertyName("publicationTimestamp"), JsonConverter(typeof(DateTimeConverter))]
    public DateTime PublicationTimestamp { get; init; }

    /// <summary>Aggregated buy-side price levels, best price first.</summary>
    [JsonPropertyName("bids")]
    public IReadOnlyList<BitvavoBookReportEntry> Bids { get; init; } = new List<BitvavoBookReportEntry>();

    /// <summary>Aggregated sell-side price levels, best price first.</summary>
    [JsonPropertyName("asks")]
    public IReadOnlyList<BitvavoBookReportEntry> Asks { get; init; } = new List<BitvavoBookReportEntry>();
}

/// <summary>
/// One aggregated price level in a <see cref="BitvavoBookReport"/> — the total quantity
/// resting at a single price plus the count of contributing orders.
/// </summary>
public record BitvavoBookReportEntry
{
    /// <summary>Order direction — <c>BUYI</c> for bids, <c>SELL</c> for asks.</summary>
    [JsonPropertyName("side")]
    public string Side { get; init; } = string.Empty;

    /// <summary>Quote-currency price per base-currency unit at this level.</summary>
    [JsonPropertyName("price"), JsonConverter(typeof(DecimalConverter))]
    public decimal? Price { get; init; }

    /// <summary>Total asset quantity resting at this price level.</summary>
    [JsonPropertyName("quantity"), JsonConverter(typeof(DecimalConverter))]
    public decimal? Quantity { get; init; }

    /// <summary>Number of individual orders aggregated into this price level.</summary>
    [JsonPropertyName("numOrders")]
    public int NumOrders { get; init; }
}
