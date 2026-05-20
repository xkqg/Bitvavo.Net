// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Text.Json.Serialization;
using CryptoExchange.Net.Converters.SystemTextJson;

namespace Bitvavo.Net.Objects.Models.Spot;

/// <summary>
/// One MiCA-regulatory trade record as returned by <c>GET /v2/report/{market}/trades</c>.
/// The report shape follows the EU Markets-in-Crypto-Assets (MiCA) reporting schema, so
/// field names and notations (<c>MONE</c>, <c>CRYP</c>) differ from the regular
/// <see cref="BitvavoPublicTrade"/> / <see cref="BitvavoFill"/> models.
/// </summary>
public record BitvavoTradesReport
{
    /// <summary>Unique identifier of the trade.</summary>
    [JsonPropertyName("tradeId")]
    public string TradeId { get; init; } = string.Empty;

    /// <summary>Timestamp when the trade was added to the database (UTC).</summary>
    [JsonPropertyName("transactTimestamp"), JsonConverter(typeof(DateTimeConverter))]
    public DateTime TransactTimestamp { get; init; }

    /// <summary>Digital-Token-Identifier (DTI) code or symbol of the asset.</summary>
    [JsonPropertyName("assetCode")]
    public string AssetCode { get; init; } = string.Empty;

    /// <summary>Full name of the asset.</summary>
    [JsonPropertyName("assetName")]
    public string AssetName { get; init; } = string.Empty;

    /// <summary>Price per unit in the quote currency.</summary>
    [JsonPropertyName("price"), JsonConverter(typeof(DecimalConverter))]
    public decimal? Price { get; init; }

    /// <summary>Missing-price status indicator — <c>PNDG</c> (pending) or <c>NOAP</c> (not applicable). Null when a price is present.</summary>
    [JsonPropertyName("missingPrice")]
    public string? MissingPrice { get; init; }

    /// <summary>Price notation — <c>MONE</c> for a monetary value.</summary>
    [JsonPropertyName("priceNotation")]
    public string PriceNotation { get; init; } = string.Empty;

    /// <summary>Currency in which the price is expressed.</summary>
    [JsonPropertyName("priceCurrency")]
    public string PriceCurrency { get; init; } = string.Empty;

    /// <summary>Quantity of the asset traded.</summary>
    [JsonPropertyName("quantity"), JsonConverter(typeof(DecimalConverter))]
    public decimal? Quantity { get; init; }

    /// <summary>Currency the quantity is expressed in.</summary>
    [JsonPropertyName("quantityCurrency")]
    public string QuantityCurrency { get; init; } = string.Empty;

    /// <summary>Quantity notation — <c>CRYP</c> for crypto units.</summary>
    [JsonPropertyName("quantityNotation")]
    public string QuantityNotation { get; init; } = string.Empty;

    /// <summary>Market Identifier Code of the Bitvavo trading platform.</summary>
    [JsonPropertyName("venue")]
    public string Venue { get; init; } = string.Empty;

    /// <summary>Timestamp when the trade was published (UTC).</summary>
    [JsonPropertyName("publicationTimestamp"), JsonConverter(typeof(DateTimeConverter))]
    public DateTime PublicationTimestamp { get; init; }

    /// <summary>Market Identifier Code of the publishing platform.</summary>
    [JsonPropertyName("publicationVenue")]
    public string PublicationVenue { get; init; } = string.Empty;
}
