// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Text.Json.Serialization;
using Bitvavo.Net.Enums;
using CryptoExchange.Net.Converters.SystemTextJson;

namespace Bitvavo.Net.Objects.Models.Spot;

/// <summary>
/// Public trade-tape entry as returned by <c>GET /v2/{market}/trades</c>. Distinct from
/// <see cref="BitvavoFill"/> which is the user's own trade-fill (private endpoint, with
/// fee + taker info).
/// </summary>
public record BitvavoPublicTrade
{
    /// <summary>Trade identifier (UUID).</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Trade timestamp (UTC).</summary>
    [JsonPropertyName("timestamp"), JsonConverter(typeof(DateTimeConverter))]
    public DateTime Timestamp { get; init; }

    /// <summary>Quantity traded (base-asset units).</summary>
    [JsonPropertyName("amount"), JsonConverter(typeof(DecimalConverter))]
    public decimal? Amount { get; init; }

    /// <summary>Trade price (quote-asset units).</summary>
    [JsonPropertyName("price"), JsonConverter(typeof(DecimalConverter))]
    public decimal? Price { get; init; }

    /// <summary>Aggressor side — which side hit the resting order.</summary>
    [JsonPropertyName("side")]
    public OrderSide Side { get; init; }
}
