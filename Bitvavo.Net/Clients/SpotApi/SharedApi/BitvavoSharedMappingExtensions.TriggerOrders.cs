// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using Bitvavo.Net.Enums;
using Bitvavo.Net.Objects.Internal;
using Bitvavo.Net.Objects.Models.Spot;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.SharedApis;

namespace Bitvavo.Net.Clients.SpotApi;

/// <remarks>
/// Trigger orders. Bitvavo has no trigger-order endpoint: a trigger order is an ordinary order of type <c>stopLoss</c>,
/// <c>stopLossLimit</c>, <c>takeProfit</c> or <c>takeProfitLimit</c> that waits in status <c>awaitingTrigger</c> until the
/// <c>triggerReference</c> price reaches <c>triggerAmount</c>, then moves to <c>new</c> under the same order id. The mappings below
/// translate the Shared spot trigger-order contract to that and back.
/// </remarks>
internal static partial class BitvavoSharedMappingExtensions
{
    // ── the TriggerReference exchange parameter ──────────────────────────────────────────

    /// <summary>Name of the optional <c>TriggerReference</c> exchange parameter.</summary>
    public const string TriggerReferenceParameter = "TriggerReference";

    /// <summary>
    /// The Shared spot trigger-order request has no field for the price that triggers the order (Bitvavo's <c>triggerReference</c>:
    /// the last trade, the best bid, the best ask or the mid price), so it travels in this optional exchange parameter, as a
    /// <see cref="TriggerReference"/> value. Without it the last trade triggers the order.
    /// </summary>
    public static ExchangeParameterDescription TriggerReferenceRule { get; } = ExchangeParameterRule.Optional<TriggerReference>(
        TriggerReferenceParameter,
        description: "The price that triggers a trigger order: the last trade (default), the best bid, the best ask or the mid price",
        exampleValue: TriggerReference.BestBid);

    /// <summary>The price that triggers the order of a request: its <c>TriggerReference</c> exchange parameter, else the last trade.</summary>
    /// <param name="request">The request, validated first (<see cref="GetTriggerReferenceError"/>).</param>
    public static TriggerReference GetTriggerReference(this SharedRequest request)
        => request.GetParamValue<TriggerReference?>(BitvavoExchange.ExchangeName, TriggerReferenceParameter) ?? TriggerReference.LastTrade;

    /// <summary>
    /// The reason the <c>TriggerReference</c> exchange parameter of a request cannot be used, or null when it is absent or a
    /// <see cref="TriggerReference"/>. Reading a value of another type would throw, so a request is checked with this first.
    /// </summary>
    /// <param name="request">The request.</param>
    public static Error? GetTriggerReferenceError(this SharedRequest request)
    {
        var value = request.GetParamValue<object>(BitvavoExchange.ExchangeName, TriggerReferenceParameter);
        if (value is null or TriggerReference)
        {
            return null;
        }

        return ArgumentError.Invalid(TriggerReferenceParameter, $"Exchange parameter '{TriggerReferenceParameter}' must be a {nameof(TriggerReference)} value, not a {value.GetType().Name}");
    }

    // ── the order type a trigger order is placed as ──────────────────────────────────────

    /// <summary>
    /// The Bitvavo order type of a trigger order, from the order side, the price direction that fires it and whether it has a limit
    /// price. A stop loss fires when the market price is worse than the trigger price (lower for a sell, higher for a buy), a take
    /// profit when it is better (higher for a sell, lower for a buy) — the usual meaning of the two types. Bitvavo's documentation says
    /// only that a stop loss "prevents further losses" and a take profit "ensures profits"; it does not state the direction, so
    /// this mapping is unverified against the live API:
    /// <list type="table">
    ///   <listheader><term>Side and price direction</term><description>Without / with a limit price</description></listheader>
    ///   <item><term>Sell, <see cref="SharedTriggerPriceDirection.PriceBelow"/></term><description><c>stopLoss</c> / <c>stopLossLimit</c></description></item>
    ///   <item><term>Sell, <see cref="SharedTriggerPriceDirection.PriceAbove"/></term><description><c>takeProfit</c> / <c>takeProfitLimit</c></description></item>
    ///   <item><term>Buy, <see cref="SharedTriggerPriceDirection.PriceAbove"/></term><description><c>stopLoss</c> / <c>stopLossLimit</c></description></item>
    ///   <item><term>Buy, <see cref="SharedTriggerPriceDirection.PriceBelow"/></term><description><c>takeProfit</c> / <c>takeProfitLimit</c></description></item>
    /// </list>
    /// </summary>
    /// <param name="direction">The price movement that fires the trigger.</param>
    /// <param name="side">The side of the order the trigger places.</param>
    /// <param name="limit">Whether the order has a limit price (it is then placed as a limit order when it fires, else as a market order).</param>
    /// <exception cref="ArgumentOutOfRangeException">The direction or the side is not one Bitvavo knows.</exception>
    public static OrderType ToBitvavoTriggerOrderType(this SharedTriggerPriceDirection direction, SharedOrderSide side, bool limit)
    {
        var bitvavoSide = side.ToBitvavoSide();
        var stopLoss = direction switch
        {
            SharedTriggerPriceDirection.PriceBelow => bitvavoSide == OrderSide.Sell,
            SharedTriggerPriceDirection.PriceAbove => bitvavoSide == OrderSide.Buy,
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Unmapped SharedTriggerPriceDirection"),
        };

        return stopLoss
            ? (limit ? OrderType.StopLossLimit : OrderType.StopLoss)
            : (limit ? OrderType.TakeProfitLimit : OrderType.TakeProfit);
    }

    /// <summary>Whether a Bitvavo order type belongs to the stop-loss and take-profit family; a value this library does not know does not.</summary>
    public static bool IsTriggerOrderType(this OrderType type)
        => type is OrderType.StopLoss or OrderType.StopLossLimit or OrderType.TakeProfit or OrderType.TakeProfitLimit;

    /// <summary>
    /// Whether an order is a trigger order: it has a stop-loss or take-profit type, or it still carries trigger data (so an order
    /// that reports a plain type once it fired is recognised as well). An ordinary market or limit order has neither.
    /// </summary>
    public static bool WasPlacedAsTriggerOrder(this BitvavoOrder order)
        => order.OrderType.IsTriggerOrderType() || order.TriggerAmount != null || order.TriggerPrice != null;

    // ── the request ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The reason a Shared trigger-order request cannot be expressed on Bitvavo, or null when it can. Bitvavo takes the quantity in
    /// the base asset (<c>amount</c>) or in the quote asset (<c>amountQuote</c>), never both (error 236); the quote asset is only
    /// valid for the market variants <c>stopLoss</c> and <c>takeProfit</c>, a trigger order with a limit price needs <c>amount</c>
    /// and <c>price</c>. Request validation checks this before <see cref="ToBitvavoTriggerOrderRequest"/>.
    /// </summary>
    /// <param name="request">The request.</param>
    public static Error? GetBitvavoTriggerOrderError(this PlaceSpotTriggerOrderRequest request)
    {
        var inBase = request.Quantity.QuantityInBaseAsset != null;
        var inQuote = request.Quantity.QuantityInQuoteAsset != null;
        if (inBase && inQuote)
        {
            return ArgumentError.Invalid(nameof(request.Quantity), "Bitvavo takes the quantity in the base asset (amount) or in the quote asset (amountQuote), not in both");
        }

        if (!inBase && !inQuote)
        {
            return ArgumentError.Missing(nameof(request.Quantity), "give the quantity in the base asset or, for a trigger order without a limit price, in the quote asset");
        }

        if (inQuote && request.OrderPrice != null)
        {
            return ArgumentError.Invalid(nameof(request.Quantity), "a trigger order with a limit price (stopLossLimit, takeProfitLimit) takes its quantity in the base asset; only stopLoss and takeProfit accept the quote asset");
        }

        return null;
    }

    /// <summary>
    /// A Shared trigger-order request as the Bitvavo order that places it: the order type from the side, the price direction and the
    /// limit price (<see cref="ToBitvavoTriggerOrderType"/>), <c>triggerAmount</c> the trigger price, <c>triggerType</c>
    /// <c>price</c> and the given <c>triggerReference</c>. The time in force is only sent for a trigger order with a limit price: a
    /// market order executes at once and has none.
    /// </summary>
    /// <param name="request">The request.</param>
    /// <param name="market">The Bitvavo market name.</param>
    /// <param name="operatorId">The operator id Bitvavo wants on every order operation.</param>
    /// <param name="triggerReference">The price that triggers the order.</param>
    /// <exception cref="ArgumentException">The request cannot be expressed on Bitvavo (<see cref="GetBitvavoTriggerOrderError"/>).</exception>
    /// <exception cref="ArgumentOutOfRangeException">The side or the price direction is not one Bitvavo knows.</exception>
    public static BitvavoPlaceOrderRequest ToBitvavoTriggerOrderRequest(this PlaceSpotTriggerOrderRequest request, string market, long operatorId, TriggerReference triggerReference)
    {
        var error = request.GetBitvavoTriggerOrderError();
        if (error != null)
        {
            throw new ArgumentException(error.Message, nameof(request));
        }

        var limit = request.OrderPrice != null;
        return new BitvavoPlaceOrderRequest(
            market,
            request.OrderSide.ToBitvavoSide(),
            request.PriceDirection.ToBitvavoTriggerOrderType(request.OrderSide, limit),
            operatorId,
            Amount: request.Quantity.QuantityInBaseAsset,
            AmountQuote: request.Quantity.QuantityInQuoteAsset,
            Price: request.OrderPrice,
            TriggerAmount: request.TriggerPrice,
            TriggerType: TriggerType.Price,
            TriggerReference: triggerReference,
            TimeInForce: limit ? request.TimeInForce?.ToBitvavoTimeInForce() : null,
            ClientOrderId: request.ClientOrderId);
    }

    // ── the response ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The order type a trigger order is reported with: the type of the order it places when it fires, so <c>stopLoss</c> and
    /// <c>takeProfit</c> are <see cref="SharedOrderType.Market"/> and <c>stopLossLimit</c> and <c>takeProfitLimit</c> are
    /// <see cref="SharedOrderType.Limit"/> (<see cref="SharedOrderType.LimitMaker"/> when post-only). The plain types map as
    /// <c>ToSharedOrderType</c> does; a value this library does not know is <see cref="SharedOrderType.Other"/>.
    /// </summary>
    /// <param name="type">The Bitvavo order type.</param>
    /// <param name="postOnly">Whether the order is post-only, when the response says.</param>
    public static SharedOrderType ToSharedTriggerOrderType(this OrderType type, bool? postOnly) => type switch
    {
        OrderType.StopLoss or OrderType.TakeProfit => SharedOrderType.Market,
        OrderType.StopLossLimit or OrderType.TakeProfitLimit => OrderType.Limit.ToSharedOrderType(postOnly),
        _ => type.ToSharedOrderType(postOnly),
    };

    /// <summary>
    /// A Bitvavo order status as a Shared trigger-order status. The order lifecycle (docs.bitvavo.com) moves a trigger order from
    /// <c>awaitingTrigger</c> to <c>new</c> when it fires, so <c>awaitingTrigger</c> is <see cref="SharedTriggerOrderStatus.Active"/>,
    /// <c>new</c> and <c>partiallyFilled</c> are <see cref="SharedTriggerOrderStatus.Triggered"/>, <c>filled</c> is
    /// <see cref="SharedTriggerOrderStatus.Filled"/> and every canceled status, <c>expired</c> and <c>rejected</c> are
    /// <see cref="SharedTriggerOrderStatus.CanceledOrRejected"/>. A status this library does not know is
    /// <see cref="SharedTriggerOrderStatus.Unknown"/>.
    /// </summary>
    public static SharedTriggerOrderStatus ToSharedTriggerOrderStatus(this OrderStatus status) => status switch
    {
        OrderStatus.AwaitingTrigger => SharedTriggerOrderStatus.Active,
        OrderStatus.New => SharedTriggerOrderStatus.Triggered,
        OrderStatus.PartiallyFilled => SharedTriggerOrderStatus.Triggered,
        OrderStatus.Filled => SharedTriggerOrderStatus.Filled,
        OrderStatus.Canceled => SharedTriggerOrderStatus.CanceledOrRejected,
        OrderStatus.CanceledAuction => SharedTriggerOrderStatus.CanceledOrRejected,
        OrderStatus.CanceledSelfTradePrevention => SharedTriggerOrderStatus.CanceledOrRejected,
        OrderStatus.CanceledIoc => SharedTriggerOrderStatus.CanceledOrRejected,
        OrderStatus.CanceledFok => SharedTriggerOrderStatus.CanceledOrRejected,
        OrderStatus.CanceledMarketProtection => SharedTriggerOrderStatus.CanceledOrRejected,
        OrderStatus.CanceledPostOnly => SharedTriggerOrderStatus.CanceledOrRejected,
        OrderStatus.Expired => SharedTriggerOrderStatus.CanceledOrRejected,
        OrderStatus.Rejected => SharedTriggerOrderStatus.CanceledOrRejected,
        _ => SharedTriggerOrderStatus.Unknown,
    };

    /// <summary>A Bitvavo order side as the direction of a trigger order: a buy enters, a sell exits; null for a value this library does not know.</summary>
    public static SharedTriggerOrderDirection? ToSharedTriggerOrderDirection(this OrderSide side) => side switch
    {
        OrderSide.Buy => SharedTriggerOrderDirection.Enter,
        OrderSide.Sell => SharedTriggerOrderDirection.Exit,
        _ => null,
    };

    /// <summary>
    /// A Bitvavo order that is a trigger order as a Shared trigger order. The trigger price is <c>triggerAmount</c> (what the caller
    /// set; <c>triggerPrice</c> when only that is present, else zero). A trigger order that fired keeps its order id, so once it is
    /// triggered or filled the order it placed is itself and <see cref="SharedSpotTriggerOrder.PlacedOrderId"/> names it. The
    /// average price is the filled quote amount over the filled base amount; the fee is the amount paid, negative for a rebate.
    /// </summary>
    /// <param name="order">The order.</param>
    /// <param name="sharedSymbol">The Shared symbol the order belongs to; when absent it is derived from the order's market.</param>
    /// <exception cref="InvalidOperationException">The order side is not one this library knows (an order always has one).</exception>
    public static SharedSpotTriggerOrder ToSharedSpotTriggerOrder(this BitvavoOrder order, SharedSymbol? sharedSymbol = null)
    {
        var direction = order.Side.ToSharedTriggerOrderDirection()
            ?? throw new InvalidOperationException("Bitvavo returned an order side this library does not know");
        var status = order.Status.ToSharedTriggerOrderStatus();
        var fired = status is SharedTriggerOrderStatus.Triggered or SharedTriggerOrderStatus.Filled;

        return new SharedSpotTriggerOrder(
            sharedSymbol ?? order.Market.ToSharedSymbol(),
            order.Market,
            order.OrderId,
            order.OrderType.ToSharedTriggerOrderType(order.PostOnly),
            direction,
            status,
            order.TriggerAmount ?? order.TriggerPrice ?? 0m,
            order.Created)
        {
            PlacedOrderId = fired ? order.OrderId : null,
            ClientOrderId = order.ClientOrderId,
            TimeInForce = order.TimeInForce?.ToSharedTimeInForce(),
            OrderQuantity = new SharedOrderQuantity(order.Amount, order.AmountQuote),
            QuantityFilled = new SharedOrderQuantity(order.FilledAmount, order.FilledAmountQuote),
            OrderPrice = order.Price,
            AveragePrice = order.FilledAmount is > 0m && order.FilledAmountQuote is > 0m ? order.FilledAmountQuote / order.FilledAmount : null,
            Fee = order.FeePaid,
            FeeAsset = order.FeeCurrency,
            UpdateTime = order.Updated,
        };
    }
}
