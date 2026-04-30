// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bitvavo.Net.Enums;
using CryptoExchange.Net.Converters.SystemTextJson;

namespace Bitvavo.Net.Objects.Models.Spot.Streams;

/// <summary>
/// Bitvavo public <c>trade</c> WebSocket event. Wire shape matches the official
/// Go SDK's <c>SubscriptionTrades</c> struct byte-for-byte: prices and amounts as JSON
/// strings, side as <c>"buy"</c> or <c>"sell"</c>.
/// </summary>
public record BitvavoStreamTrade
{
    /// <summary>Always <c>"trade"</c> for this stream.</summary>
    [JsonPropertyName("event")]
    public string Event { get; init; } = string.Empty;

    /// <summary>Trade timestamp (UTC). Bitvavo emits unix-millis.</summary>
    [JsonPropertyName("timestamp"), JsonConverter(typeof(DateTimeConverter))]
    public DateTime Timestamp { get; init; }

    /// <summary>Market identifier, e.g. <c>"BTC-EUR"</c>.</summary>
    [JsonPropertyName("market")]
    public string Market { get; init; } = string.Empty;

    /// <summary>Bitvavo trade GUID.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Trade size in base-asset units. Bitvavo emits decimals as JSON strings; <see cref="JsonSerializerOptions.NumberHandling"/> = <see cref="JsonNumberHandling.AllowReadingFromString"/> on the message handler does the parse.</summary>
    [JsonPropertyName("amount")]
    public decimal Amount { get; init; }

    /// <summary>Trade price in quote-asset units per base unit. Bitvavo emits decimals as JSON strings; see <see cref="Amount"/>.</summary>
    [JsonPropertyName("price")]
    public decimal Price { get; init; }

    /// <summary>Aggressor side. Wire form is <c>"buy"</c> / <c>"sell"</c>; deserialised via <c>EnumConverter</c> on <see cref="OrderSide"/>.</summary>
    [JsonPropertyName("side")]
    public OrderSide Side { get; init; }
}
