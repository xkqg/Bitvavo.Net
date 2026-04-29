// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Text.Json.Serialization;
using Bitvavo.Net.Enums;
using CryptoExchange.Net.Converters.SystemTextJson;

namespace Bitvavo.Net.Objects.Models.Spot.Streams;

/// <summary>
/// Bitvavo private <c>fill</c> WebSocket event — emitted on the <c>account</c> channel
/// after authenticating, whenever any of the user's orders trades against the book.
/// </summary>
public record BitvavoStreamFillEvent
{
    /// <summary>Always <c>"fill"</c> for this stream.</summary>
    [JsonPropertyName("event")]
    public string Event { get; init; } = string.Empty;

    /// <summary>Market the fill happened on.</summary>
    [JsonPropertyName("market")]
    public string Market { get; init; } = string.Empty;

    /// <summary>Order this fill belongs to.</summary>
    [JsonPropertyName("orderId")]
    public string OrderId { get; init; } = string.Empty;

    /// <summary>Client-assigned order id (UUID), if the order was placed with one.</summary>
    [JsonPropertyName("clientOrderId")]
    public string? ClientOrderId { get; init; }

    /// <summary>Trade-fill identifier (UUID).</summary>
    [JsonPropertyName("fillId")]
    public string FillId { get; init; } = string.Empty;

    /// <summary>Server timestamp of the fill (UTC).</summary>
    [JsonPropertyName("timestamp"), JsonConverter(typeof(DateTimeConverter))]
    public DateTime Timestamp { get; init; }

    /// <summary>Filled quantity (base-asset units).</summary>
    [JsonPropertyName("amount")]
    public decimal Amount { get; init; }

    /// <summary>Trade direction from this account's perspective.</summary>
    [JsonPropertyName("side")]
    public OrderSide Side { get; init; }

    /// <summary>Trade price.</summary>
    [JsonPropertyName("price")]
    public decimal Price { get; init; }

    /// <summary>True if this account was the taker on the fill; false if maker.</summary>
    [JsonPropertyName("taker")]
    public bool Taker { get; init; }

    /// <summary>Fee paid (positive decimal). Currency is given by <see cref="FeeCurrency"/>.</summary>
    [JsonPropertyName("fee")]
    public decimal Fee { get; init; }

    /// <summary>Currency the fee was paid in.</summary>
    [JsonPropertyName("feeCurrency")]
    public string FeeCurrency { get; init; } = string.Empty;
}
