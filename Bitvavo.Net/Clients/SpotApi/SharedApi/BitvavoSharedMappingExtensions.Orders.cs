// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using Bitvavo.Net.Objects.Models.Spot;
using CryptoExchange.Net.SharedApis;

namespace Bitvavo.Net.Clients.SpotApi;

internal static partial class BitvavoSharedMappingExtensions
{
    /// <summary>
    /// A Bitvavo order, in any lifecycle state, as a Shared spot order. A post-only limit order is a
    /// <see cref="SharedOrderType.LimitMaker"/>; the stop and take-profit family is <see cref="SharedOrderType.Other"/> with
    /// <see cref="SharedSpotOrder.IsTriggerOrder"/> set. The average price is not set here: <see cref="SharedSpotOrder.AveragePrice"/>
    /// derives it from the filled quantities (quote over base). The order-level fee is still filled because V1 consumers read it.
    /// </summary>
    /// <param name="order">The order.</param>
    /// <param name="sharedSymbol">The Shared symbol the order belongs to; when absent it is derived from the order's market.</param>
    /// <exception cref="InvalidOperationException">The order side is not one this library knows (an order always has one).</exception>
    public static SharedSpotOrder ToSharedSpotOrder(this BitvavoOrder order, SharedSymbol? sharedSymbol = null)
    {
        var side = order.Side.ToSharedSide()
            ?? throw new InvalidOperationException("Bitvavo returned an order side this library does not know");

        return new SharedSpotOrder(
            sharedSymbol ?? order.Market.ToSharedSymbol(),
            order.Market,
            order.OrderId,
            order.OrderType.ToSharedOrderType(order.PostOnly),
            side,
            order.Status.ToSharedStatus(),
            order.Created)
        {
            ClientOrderId = order.ClientOrderId,
            OrderPrice = order.Price,
            OrderQuantity = new SharedOrderQuantity(order.Amount, order.AmountQuote),
            QuantityFilled = new SharedOrderQuantity(order.FilledAmount, order.FilledAmountQuote),
            TimeInForce = order.TimeInForce?.ToSharedTimeInForce(),
            UpdateTime = order.Updated,
#pragma warning disable CS0618 // The order-level fee is obsolete in the Shared model; V1 consumers still read it.
            Fee = order.FeePaid,
            FeeAsset = order.FeeCurrency,
#pragma warning restore CS0618
            TriggerPrice = order.TriggerAmount,
            IsTriggerOrder = order.TriggerAmount != null,
        };
    }

    /// <summary>
    /// A fill returned on its own by the trade history (<c>GET /trades</c>) as a Shared user trade. The fill names its market, side and
    /// order itself. The fee is the amount paid in <see cref="BitvavoFill.FeeCurrency"/>; an unsettled fill has neither.
    /// </summary>
    /// <param name="fill">The fill.</param>
    /// <param name="market">The market the fill was requested for, used when the fill does not name its own.</param>
    /// <param name="sharedSymbol">The Shared symbol the fill belongs to; when absent it is derived from the market.</param>
    public static SharedUserTrade ToSharedUserTrade(this BitvavoFill fill, string market, SharedSymbol? sharedSymbol = null)
        => fill.BuildSpotOrdersUserTrade(fill.Market ?? market, sharedSymbol, fill.OrderId ?? string.Empty, fill.Side?.ToSharedSide(), fill.ClientOrderId);

    /// <summary>
    /// A fill embedded in an order answer as a Shared user trade. Embedded fills carry neither market, side nor order id: the order
    /// supplies them, and also the client order id unless the fill has one of its own.
    /// </summary>
    /// <param name="fill">The embedded fill.</param>
    /// <param name="order">The order the fill is embedded in.</param>
    /// <param name="sharedSymbol">The Shared symbol the order belongs to; when absent it is derived from the order's market.</param>
    public static SharedUserTrade ToSharedUserTrade(this BitvavoFill fill, BitvavoOrder order, SharedSymbol? sharedSymbol = null)
        => fill.BuildSpotOrdersUserTrade(order.Market, sharedSymbol, order.OrderId, order.Side.ToSharedSide(), fill.ClientOrderId ?? order.ClientOrderId);

    private static SharedUserTrade BuildSpotOrdersUserTrade(this BitvavoFill fill, string market, SharedSymbol? sharedSymbol, string orderId, SharedOrderSide? side, string? clientOrderId)
        => new(
            sharedSymbol ?? market.ToSharedSymbol(),
            market,
            orderId,
            fill.Id,
            side,
            new SharedOrderQuantity(fill.Amount),
            fill.Price ?? 0m,
            fill.Timestamp)
        {
            ClientOrderId = clientOrderId,
            Fee = fill.Fee,
            FeeAsset = fill.FeeCurrency,
            Role = fill.Taker switch
            {
                true => SharedRole.Taker,
                false => SharedRole.Maker,
                null => null,
            },
        };
}
