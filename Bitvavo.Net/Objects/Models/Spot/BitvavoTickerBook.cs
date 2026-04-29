// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Text.Json.Serialization;
using CryptoExchange.Net.Converters.SystemTextJson;

namespace Bitvavo.Net.Objects.Models.Spot;

/// <summary>Top-of-book quote for a single market as returned by <c>GET /v2/ticker/book</c>.</summary>
public record BitvavoTickerBook
{
    /// <summary>Market identifier.</summary>
    [JsonPropertyName("market")]
    public string Market { get; init; } = string.Empty;

    /// <summary>Best bid (highest buy) price, in the quote currency.</summary>
    [JsonPropertyName("bid"), JsonConverter(typeof(DecimalConverter))]
    public decimal? Bid { get; init; }

    /// <summary>Quantity available at the best bid, in base-asset units.</summary>
    [JsonPropertyName("bidSize"), JsonConverter(typeof(DecimalConverter))]
    public decimal? BidSize { get; init; }

    /// <summary>Best ask (lowest sell) price, in the quote currency.</summary>
    [JsonPropertyName("ask"), JsonConverter(typeof(DecimalConverter))]
    public decimal? Ask { get; init; }

    /// <summary>Quantity available at the best ask, in base-asset units.</summary>
    [JsonPropertyName("askSize"), JsonConverter(typeof(DecimalConverter))]
    public decimal? AskSize { get; init; }
}
