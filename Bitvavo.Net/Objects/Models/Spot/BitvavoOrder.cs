// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Bitvavo.Net.Enums;
using CryptoExchange.Net.Converters.SystemTextJson;

namespace Bitvavo.Net.Objects.Models.Spot;

/// <summary>
/// An order on Bitvavo, in any lifecycle state. The same record covers the response
/// shape of <c>POST /v2/order</c>, <c>PUT /v2/order</c>, <c>GET /v2/order</c>, and
/// element of the list responses <c>GET /v2/ordersOpen</c> and <c>GET /v2/orders</c>.
/// </summary>
public record BitvavoOrder
{
    /// <summary>Server-assigned order id (UUID).</summary>
    [JsonPropertyName("orderId")]
    public string OrderId { get; init; } = string.Empty;

    /// <summary>Client-assigned order id (UUID), if the order was placed with one.</summary>
    [JsonPropertyName("clientOrderId")]
    public string? ClientOrderId { get; init; }

    /// <summary>Market the order belongs to (e.g. <c>"ETH-EUR"</c>).</summary>
    [JsonPropertyName("market")]
    public string Market { get; init; } = string.Empty;

    /// <summary>Server timestamp when the order was created (UTC).</summary>
    [JsonPropertyName("created"), JsonConverter(typeof(DateTimeConverter))]
    public DateTime Created { get; init; }

    /// <summary>Server timestamp of the most recent state change (UTC).</summary>
    [JsonPropertyName("updated"), JsonConverter(typeof(DateTimeConverter))]
    public DateTime Updated { get; init; }

    /// <summary>Lifecycle state — see <see cref="OrderStatus"/>.</summary>
    [JsonPropertyName("status")]
    public OrderStatus Status { get; init; }

    /// <summary>Direction of the order.</summary>
    [JsonPropertyName("side")]
    public OrderSide Side { get; init; }

    /// <summary>Order placement type.</summary>
    [JsonPropertyName("orderType")]
    public OrderType OrderType { get; init; }

    /// <summary>Originally requested base-asset quantity. Null for amount-quote orders.</summary>
    [JsonPropertyName("amount"), JsonConverter(typeof(DecimalConverter))]
    public decimal? Amount { get; init; }

    /// <summary>Base-asset quantity still outstanding (= <see cref="Amount"/> − <see cref="FilledAmount"/>).</summary>
    [JsonPropertyName("amountRemaining"), JsonConverter(typeof(DecimalConverter))]
    public decimal? AmountRemaining { get; init; }

    /// <summary>Limit price for limit / stop-limit / take-profit-limit orders.</summary>
    [JsonPropertyName("price"), JsonConverter(typeof(DecimalConverter))]
    public decimal? Price { get; init; }

    /// <summary>Originally requested quote-asset budget. Used by quote-amount market buys.</summary>
    [JsonPropertyName("amountQuote"), JsonConverter(typeof(DecimalConverter))]
    public decimal? AmountQuote { get; init; }

    /// <summary>Quote-asset budget still outstanding.</summary>
    [JsonPropertyName("amountQuoteRemaining"), JsonConverter(typeof(DecimalConverter))]
    public decimal? AmountQuoteRemaining { get; init; }

    /// <summary>Total base-asset quantity filled so far.</summary>
    [JsonPropertyName("filledAmount"), JsonConverter(typeof(DecimalConverter))]
    public decimal? FilledAmount { get; init; }

    /// <summary>Total quote-asset value filled so far.</summary>
    [JsonPropertyName("filledAmountQuote"), JsonConverter(typeof(DecimalConverter))]
    public decimal? FilledAmountQuote { get; init; }

    /// <summary>Total fees paid so far on this order.</summary>
    [JsonPropertyName("feePaid"), JsonConverter(typeof(DecimalConverter))]
    public decimal? FeePaid { get; init; }

    /// <summary>Currency the fee was paid in.</summary>
    [JsonPropertyName("feeCurrency")]
    public string? FeeCurrency { get; init; }

    /// <summary>Trade fills that have happened against this order so far.</summary>
    [JsonPropertyName("fills")]
    public IReadOnlyList<BitvavoFill> Fills { get; init; } = new List<BitvavoFill>();

    /// <summary>Time-in-force policy.</summary>
    [JsonPropertyName("timeInForce")]
    public TimeInForce? TimeInForce { get; init; }

    /// <summary>If true, the order will only post liquidity (will not cross the spread).</summary>
    [JsonPropertyName("postOnly")]
    public bool? PostOnly { get; init; }

    /// <summary>Self-trade-prevention rule applied to this order.</summary>
    [JsonPropertyName("selfTradePrevention")]
    public SelfTradePrevention? SelfTradePrevention { get; init; }

    /// <summary>True when the order id is visible in the public order book for the market.</summary>
    [JsonPropertyName("visible")]
    public bool? Visible { get; init; }

    /// <summary>Trigger amount for stop / take-profit orders.</summary>
    [JsonPropertyName("triggerAmount"), JsonConverter(typeof(DecimalConverter))]
    public decimal? TriggerAmount { get; init; }

    /// <summary>Realised trigger price (after the trigger fired). Sometimes returned alongside <see cref="TriggerAmount"/>.</summary>
    [JsonPropertyName("triggerPrice"), JsonConverter(typeof(DecimalConverter))]
    public decimal? TriggerPrice { get; init; }

    /// <summary>Trigger event type (currently only price-based).</summary>
    [JsonPropertyName("triggerType")]
    public TriggerType? TriggerType { get; init; }

    /// <summary>Which price reference Bitvavo evaluates the trigger against.</summary>
    [JsonPropertyName("triggerReference")]
    public TriggerReference? TriggerReference { get; init; }

    /// <summary>Per-account integer used by Bitvavo to route order operations to a specific operator.</summary>
    [JsonPropertyName("operatorId")]
    public long? OperatorId { get; init; }

    /// <summary>Cancel-on-disconnect group id (1–1000).</summary>
    [JsonPropertyName("codGroupId")]
    public int? CodGroupId { get; init; }

    /// <summary>Quantity currently held in escrow for this open order.</summary>
    [JsonPropertyName("onHold"), JsonConverter(typeof(DecimalConverter))]
    public decimal? OnHold { get; init; }

    /// <summary>Currency of the held amount.</summary>
    [JsonPropertyName("onHoldCurrency")]
    public string? OnHoldCurrency { get; init; }
}
