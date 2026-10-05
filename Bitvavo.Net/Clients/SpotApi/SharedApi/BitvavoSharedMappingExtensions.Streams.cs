// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using Bitvavo.Net.Objects.Models.Spot;
using Bitvavo.Net.Objects.Models.Spot.Streams;
using CryptoExchange.Net.SharedApis;

namespace Bitvavo.Net.Clients.SpotApi;

internal static partial class BitvavoSharedMappingExtensions
{
    /// <summary>A Bitvavo candle as a Shared kline. Bitvavo reports the volume in the base asset only.</summary>
    /// <param name="candle">The candle.</param>
    /// <param name="sharedSymbol">The Shared symbol the candle belongs to.</param>
    /// <param name="symbol">The Bitvavo market name.</param>
    public static SharedKline ToSharedKline(this BitvavoKline candle, SharedSymbol? sharedSymbol, string symbol) => new(
        sharedSymbol,
        symbol,
        candle.OpenTime,
        candle.ClosePrice,
        candle.HighPrice,
        candle.LowPrice,
        candle.OpenPrice,
        new SharedOrderQuantity(candle.Volume, null));

    /// <summary>A public trade event as a Shared trade.</summary>
    public static SharedTrade ToSharedTrade(this BitvavoStreamTrade trade) => new(
        trade.Market.ToSharedSymbol(),
        trade.Market,
        new SharedOrderQuantity(trade.Amount, null),
        trade.Price,
        trade.Timestamp)
    {
        Side = trade.Side.ToSharedSide(),
    };

    /// <summary>
    /// An account-channel <c>order</c> event as a Shared order update. The event carries the state of the order, not the fill that
    /// moved it (fills arrive as their own <c>fill</c> events), so <c>LastTrade</c> stays empty. A post-only limit order is a
    /// <see cref="SharedOrderType.LimitMaker"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The order side is not one this library knows (an order always has one).</exception>
    public static SharedSpotOrderUpdate ToSharedSpotOrderUpdate(this BitvavoStreamOrderUpdate order) => new(
        order.Market.ToSharedSymbol(),
        order.Market,
        order.OrderId,
        order.OrderType.ToSharedOrderType(order.PostOnly),
        order.Side.ToSharedSide() ?? throw new InvalidOperationException("Bitvavo sent an order event with an order side this library does not know"),
        order.Status.ToSharedStatus(),
        order.Created)
    {
        ClientOrderId = order.ClientOrderId,
        OrderPrice = order.Price,
        OrderQuantity = new SharedOrderQuantity(order.Amount, null),
        QuantityFilled = new SharedOrderQuantity(order.FilledAmount, order.FilledAmountQuote),
        TimeInForce = order.TimeInForce?.ToSharedTimeInForce(),
        UpdateTime = order.Updated,
        TriggerPrice = order.TriggerAmount,
        IsTriggerOrder = order.TriggerAmount != null,
    };

    /// <summary>An account-channel <c>fill</c> event as a Shared user trade.</summary>
    public static SharedUserTrade ToSharedUserTrade(this BitvavoStreamFillEvent fill) => new(
        fill.Market.ToSharedSymbol(),
        fill.Market,
        fill.OrderId,
        fill.FillId,
        fill.Side.ToSharedSide(),
        new SharedOrderQuantity(fill.Amount, null),
        fill.Price,
        fill.Timestamp)
    {
        ClientOrderId = fill.ClientOrderId,
        Fee = fill.Fee,
        FeeAsset = fill.FeeCurrency,
        Role = fill.Taker ? SharedRole.Taker : SharedRole.Maker,
    };
}
