// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Text.Json.Serialization;
using CryptoExchange.Net.Converters.SystemTextJson;

namespace Bitvavo.Net.Objects.Models.Spot;

/// <summary>Single market ↔ last-price pair as returned by <c>GET /v2/ticker/price</c>.</summary>
public record BitvavoTickerPrice
{
    /// <summary>Market identifier (e.g. <c>"ETH-EUR"</c>).</summary>
    [JsonPropertyName("market")]
    public string Market { get; init; } = string.Empty;

    /// <summary>Most recent traded price.</summary>
    [JsonPropertyName("price"), JsonConverter(typeof(DecimalConverter))]
    public decimal? Price { get; init; }
}
