// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Text.Json.Serialization;
using Bitvavo.Net.Enums;
using CryptoExchange.Net.Converters.SystemTextJson;

namespace Bitvavo.Net.Objects.Models.Spot.Streams;

/// <summary>
/// Bitvavo private <c>order</c> WebSocket event — emitted on the <c>account</c> channel
/// after authenticating, whenever any of the user's orders changes state on the markets
/// they subscribed to.
/// </summary>
public record BitvavoStreamOrderUpdate
{
    /// <summary>Always <c>"order"</c> for this stream.</summary>
    [JsonPropertyName("event")]
    public string Event { get; init; } = string.Empty;

    /// <summary>Server-assigned order id (UUID).</summary>
    [JsonPropertyName("orderId")]
    public string OrderId { get; init; } = string.Empty;

    /// <summary>Client-assigned order id (UUID) — only present when the order was placed with one.</summary>
    [JsonPropertyName("clientOrderId")]
    public string? ClientOrderId { get; init; }

    /// <summary>Market the order belongs to.</summary>
    [JsonPropertyName("market")]
    public string Market { get; init; } = string.Empty;

    /// <summary>Server time the order was created (UTC).</summary>
    [JsonPropertyName("created"), JsonConverter(typeof(DateTimeConverter))]
    public DateTime Created { get; init; }

    /// <summary>Server time of the most recent state change (UTC).</summary>
    [JsonPropertyName("updated"), JsonConverter(typeof(DateTimeConverter))]
    public DateTime Updated { get; init; }

    /// <summary>Lifecycle state.</summary>
    [JsonPropertyName("status")]
    public OrderStatus Status { get; init; }

    /// <summary>Order direction.</summary>
    [JsonPropertyName("side")]
    public OrderSide Side { get; init; }

    /// <summary>Order placement type.</summary>
    [JsonPropertyName("orderType")]
    public OrderType OrderType { get; init; }

    /// <summary>Originally requested base-asset quantity.</summary>
    [JsonPropertyName("amount")]
    public decimal? Amount { get; init; }

    /// <summary>Base-asset quantity still outstanding.</summary>
    [JsonPropertyName("amountRemaining")]
    public decimal? AmountRemaining { get; init; }

    /// <summary>Limit price (limit / stop-limit / take-profit-limit orders).</summary>
    [JsonPropertyName("price")]
    public decimal? Price { get; init; }

    /// <summary>Quantity currently held in escrow for this open order.</summary>
    [JsonPropertyName("onHold")]
    public decimal? OnHold { get; init; }

    /// <summary>Currency of the held amount.</summary>
    [JsonPropertyName("onHoldCurrency")]
    public string? OnHoldCurrency { get; init; }

    /// <summary>Trigger amount for stop / take-profit orders.</summary>
    [JsonPropertyName("triggerAmount")]
    public decimal? TriggerAmount { get; init; }

    /// <summary>Realised trigger price (after the trigger fired).</summary>
    [JsonPropertyName("triggerPrice")]
    public decimal? TriggerPrice { get; init; }

    /// <summary>Trigger event type (currently only price-based).</summary>
    [JsonPropertyName("triggerType")]
    public TriggerType? TriggerType { get; init; }

    /// <summary>Which price reference Bitvavo evaluates the trigger against.</summary>
    [JsonPropertyName("triggerReference")]
    public TriggerReference? TriggerReference { get; init; }

    /// <summary>Time-in-force policy.</summary>
    [JsonPropertyName("timeInForce")]
    public TimeInForce? TimeInForce { get; init; }

    /// <summary>Post-only flag.</summary>
    [JsonPropertyName("postOnly")]
    public bool? PostOnly { get; init; }

    /// <summary>Self-trade-prevention rule applied to this order.</summary>
    [JsonPropertyName("selfTradePrevention")]
    public SelfTradePrevention? SelfTradePrevention { get; init; }

    /// <summary>True when the order id is visible in the public order book for the market.</summary>
    [JsonPropertyName("visible")]
    public bool? Visible { get; init; }

    /// <summary>Total base-asset quantity filled so far.</summary>
    [JsonPropertyName("filledAmount")]
    public decimal? FilledAmount { get; init; }

    /// <summary>Total quote-asset value filled so far.</summary>
    [JsonPropertyName("filledAmountQuote")]
    public decimal? FilledAmountQuote { get; init; }

    /// <summary>Why the state change happened (e.g. <c>"new"</c>, <c>"trade"</c>, <c>"canceled"</c>).</summary>
    [JsonPropertyName("executionType")]
    public string? ExecutionType { get; init; }

    /// <summary>Reason if the order was restated (e.g. trigger moved).</summary>
    [JsonPropertyName("restatementReason")]
    public string? RestatementReason { get; init; }

    /// <summary>Cancel-on-disconnect group id (1–1000).</summary>
    [JsonPropertyName("codGroupId")]
    public int? CodGroupId { get; init; }
}
