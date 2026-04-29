// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Text.Json.Serialization;
using Bitvavo.Net.Enums;
using CryptoExchange.Net.Converters.SystemTextJson;

namespace Bitvavo.Net.Objects.Models.Spot;

/// <summary>
/// A single trade fill — used both as the standalone response of <c>GET /v2/trades</c>
/// (the user's trade-fill history) and as the nested <c>fills[]</c> array element on
/// <see cref="BitvavoOrder"/>.
/// </summary>
/// <remarks>
/// Some fields (<see cref="OrderId"/>, <see cref="Market"/>, <see cref="Side"/>) are
/// only emitted when this fill is returned standalone from <c>/v2/trades</c>; they are
/// omitted when the fill is embedded inside an order response (the order already carries
/// those values). All optional in this DTO so the same record covers both shapes.
/// </remarks>
public record BitvavoFill
{
    /// <summary>Trade-fill identifier (UUID).</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Order this fill belongs to. Present in standalone <c>/v2/trades</c> responses; omitted in embedded fills.</summary>
    [JsonPropertyName("orderId")]
    public string? OrderId { get; init; }

    /// <summary>Client-assigned order id (UUID), if the order was placed with one.</summary>
    [JsonPropertyName("clientOrderId")]
    public string? ClientOrderId { get; init; }

    /// <summary>Server timestamp of the fill (UTC).</summary>
    [JsonPropertyName("timestamp"), JsonConverter(typeof(DateTimeConverter))]
    public DateTime Timestamp { get; init; }

    /// <summary>Market the fill happened on. Present in standalone <c>/v2/trades</c> responses; omitted in embedded fills.</summary>
    [JsonPropertyName("market")]
    public string? Market { get; init; }

    /// <summary>Direction of the fill from this account's perspective.</summary>
    [JsonPropertyName("side")]
    public OrderSide? Side { get; init; }

    /// <summary>Filled quantity, base-asset units.</summary>
    [JsonPropertyName("amount"), JsonConverter(typeof(DecimalConverter))]
    public decimal? Amount { get; init; }

    /// <summary>Trade price.</summary>
    [JsonPropertyName("price"), JsonConverter(typeof(DecimalConverter))]
    public decimal? Price { get; init; }

    /// <summary>True if this account was the taker on the fill; false if maker.</summary>
    [JsonPropertyName("taker")]
    public bool? Taker { get; init; }

    /// <summary>Fee paid (positive decimal). Currency is given by <see cref="FeeCurrency"/>.</summary>
    [JsonPropertyName("fee"), JsonConverter(typeof(DecimalConverter))]
    public decimal? Fee { get; init; }

    /// <summary>Currency the fee was paid in (e.g. <c>"EUR"</c>).</summary>
    [JsonPropertyName("feeCurrency")]
    public string FeeCurrency { get; init; } = string.Empty;

    /// <summary>True once the fill is fully settled on Bitvavo's books.</summary>
    [JsonPropertyName("settled")]
    public bool? Settled { get; init; }
}
