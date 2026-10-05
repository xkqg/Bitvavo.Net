// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using Bitvavo.Net.Enums;
using CryptoExchange.Net.SharedApis;

namespace Bitvavo.Net.Clients.SpotApi;

/// <summary>
/// The mappings between Bitvavo's types and CryptoExchange.Net's Shared types, written once for the REST and the WebSocket
/// Shared API alike. Two rules decide how each one treats a value it cannot map:
/// <list type="bullet">
///   <item><description>Data that came from Bitvavo (a status, a side, a time in force) never throws: it maps to <c>Unknown</c> or to nothing, so one new server value cannot break a whole response.</description></item>
///   <item><description>A value a caller put in a Shared request that Bitvavo cannot express is a caller error and throws; request validation rejects those first, so a throw here is a bug in the options.</description></item>
/// </list>
/// Each capability adds its own mappings in its own partial of this class (<c>BitvavoSharedMappingExtensions.&lt;Capability&gt;.cs</c>).
/// </summary>
internal static partial class BitvavoSharedMappingExtensions
{
    /// <summary>The candle intervals Bitvavo offers; Bitvavo has no 3-minute candle.</summary>
    public static readonly SharedKlineInterval[] SupportedKlineIntervals =
    [
        SharedKlineInterval.OneMinute,
        SharedKlineInterval.FiveMinutes,
        SharedKlineInterval.FifteenMinutes,
        SharedKlineInterval.ThirtyMinutes,
        SharedKlineInterval.OneHour,
        SharedKlineInterval.TwoHours,
        SharedKlineInterval.FourHours,
        SharedKlineInterval.SixHours,
        SharedKlineInterval.EightHours,
        SharedKlineInterval.TwelveHours,
        SharedKlineInterval.OneDay,
        SharedKlineInterval.OneWeek,
        SharedKlineInterval.OneMonth,
    ];

    /// <summary>
    /// A Bitvavo market name (<c>BASE-QUOTE</c>) as a <see cref="SharedSymbol"/>. Bitvavo's market names are always dash-separated,
    /// so this is exact; a name without a dash becomes a base-only symbol.
    /// </summary>
    public static SharedSymbol ToSharedSymbol(this string market)
    {
        var dash = market.IndexOf('-');
        return dash > 0
            ? new SharedSymbol(TradingMode.Spot, market[..dash], market[(dash + 1)..]) { SymbolName = market }
            : new SharedSymbol(TradingMode.Spot, market, string.Empty) { SymbolName = market };
    }

    /// <summary>
    /// A Shared kline interval as Bitvavo's <see cref="KlineInterval"/>. The two enums share no integer values (the Shared one
    /// is seconds, Bitvavo's is ordinal), so the map is explicit; a direct cast would silently corrupt.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The interval is not one Bitvavo offers (for example 3 minutes).</exception>
    public static KlineInterval ToBitvavoInterval(this SharedKlineInterval interval) => interval switch
    {
        SharedKlineInterval.OneMinute => KlineInterval.OneMinute,
        SharedKlineInterval.FiveMinutes => KlineInterval.FiveMinutes,
        SharedKlineInterval.FifteenMinutes => KlineInterval.FifteenMinutes,
        SharedKlineInterval.ThirtyMinutes => KlineInterval.ThirtyMinutes,
        SharedKlineInterval.OneHour => KlineInterval.OneHour,
        SharedKlineInterval.TwoHours => KlineInterval.TwoHours,
        SharedKlineInterval.FourHours => KlineInterval.FourHours,
        SharedKlineInterval.SixHours => KlineInterval.SixHours,
        SharedKlineInterval.EightHours => KlineInterval.EightHours,
        SharedKlineInterval.TwelveHours => KlineInterval.TwelveHours,
        SharedKlineInterval.OneDay => KlineInterval.OneDay,
        SharedKlineInterval.OneWeek => KlineInterval.OneWeek,
        SharedKlineInterval.OneMonth => KlineInterval.OneMonth,
        _ => throw new ArgumentOutOfRangeException(nameof(interval), interval, "Interval not supported by Bitvavo"),
    };

    /// <summary>A Bitvavo order side as a Shared side; null for a value this library does not know.</summary>
    public static SharedOrderSide? ToSharedSide(this OrderSide side) => side switch
    {
        OrderSide.Buy => SharedOrderSide.Buy,
        OrderSide.Sell => SharedOrderSide.Sell,
        _ => null,
    };

    /// <summary>A Shared order side as Bitvavo's side.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The side is not one Bitvavo knows.</exception>
    public static OrderSide ToBitvavoSide(this SharedOrderSide side) => side switch
    {
        SharedOrderSide.Buy => OrderSide.Buy,
        SharedOrderSide.Sell => OrderSide.Sell,
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Unmapped SharedOrderSide"),
    };

    /// <summary>
    /// A Bitvavo order type as a Shared type. A post-only limit order is <see cref="SharedOrderType.LimitMaker"/>; the stop and
    /// take-profit family has no Shared equivalent and is <see cref="SharedOrderType.Other"/> (it is placed and read through the
    /// trigger-order capabilities).
    /// </summary>
    /// <param name="type">The Bitvavo order type.</param>
    /// <param name="postOnly">Whether the order is post-only, when the response says.</param>
    public static SharedOrderType ToSharedOrderType(this OrderType type, bool? postOnly) => type switch
    {
        OrderType.Market => SharedOrderType.Market,
        OrderType.Limit => postOnly == true ? SharedOrderType.LimitMaker : SharedOrderType.Limit,
        _ => SharedOrderType.Other,
    };

    /// <summary>
    /// A Shared order type as Bitvavo's type. <see cref="SharedOrderType.LimitMaker"/> is a limit order the caller also sends as
    /// post-only.
    /// </summary>
    /// <exception cref="ArgumentException">The type is <see cref="SharedOrderType.Other"/>: name a concrete type, or use a trigger order.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The type is not one Bitvavo knows.</exception>
    public static OrderType ToBitvavoOrderType(this SharedOrderType type) => type switch
    {
        SharedOrderType.Market => OrderType.Market,
        SharedOrderType.Limit => OrderType.Limit,
        SharedOrderType.LimitMaker => OrderType.Limit,
        SharedOrderType.Other => throw new ArgumentException("SharedOrderType.Other cannot be placed on Bitvavo: name a concrete order type, or use a trigger order", nameof(type)),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unmapped SharedOrderType"),
    };

    /// <summary>A Bitvavo order status as a Shared status; a value this library does not know is <see cref="SharedOrderStatus.Unknown"/>.</summary>
    public static SharedOrderStatus ToSharedStatus(this OrderStatus status) => status switch
    {
        OrderStatus.New => SharedOrderStatus.Open,
        OrderStatus.AwaitingTrigger => SharedOrderStatus.Open,
        OrderStatus.PartiallyFilled => SharedOrderStatus.Open,
        OrderStatus.Filled => SharedOrderStatus.Filled,
        OrderStatus.Canceled => SharedOrderStatus.Canceled,
        OrderStatus.CanceledAuction => SharedOrderStatus.Canceled,
        OrderStatus.CanceledSelfTradePrevention => SharedOrderStatus.Canceled,
        OrderStatus.CanceledIoc => SharedOrderStatus.Canceled,
        OrderStatus.CanceledFok => SharedOrderStatus.Canceled,
        OrderStatus.CanceledMarketProtection => SharedOrderStatus.Canceled,
        OrderStatus.CanceledPostOnly => SharedOrderStatus.Canceled,
        OrderStatus.Expired => SharedOrderStatus.Canceled,
        OrderStatus.Rejected => SharedOrderStatus.Canceled,
        _ => SharedOrderStatus.Unknown,
    };

    /// <summary>A Bitvavo time in force as a Shared one; null for a value this library does not know.</summary>
    public static SharedTimeInForce? ToSharedTimeInForce(this TimeInForce timeInForce) => timeInForce switch
    {
        TimeInForce.GoodTillCanceled => SharedTimeInForce.GoodTillCanceled,
        TimeInForce.ImmediateOrCancel => SharedTimeInForce.ImmediateOrCancel,
        TimeInForce.FillOrKill => SharedTimeInForce.FillOrKill,
        _ => null,
    };

    /// <summary>A Shared time in force as Bitvavo's.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not one Bitvavo knows.</exception>
    public static TimeInForce ToBitvavoTimeInForce(this SharedTimeInForce timeInForce) => timeInForce switch
    {
        SharedTimeInForce.GoodTillCanceled => TimeInForce.GoodTillCanceled,
        SharedTimeInForce.ImmediateOrCancel => TimeInForce.ImmediateOrCancel,
        SharedTimeInForce.FillOrKill => TimeInForce.FillOrKill,
        _ => throw new ArgumentOutOfRangeException(nameof(timeInForce), timeInForce, "Unmapped SharedTimeInForce"),
    };

    /// <summary>
    /// A Bitvavo deposit or withdrawal status as a Shared transfer status. The withdrawal statuses are the nine the documentation
    /// lists; <c>completed</c> is the one finished state and <c>canceled</c> the one failed state, everything between is in
    /// progress. A status this library does not know is <see cref="SharedTransferStatus.Unknown"/>.
    /// </summary>
    public static SharedTransferStatus ToSharedTransferStatus(this string? status) => status?.ToLowerInvariant() switch
    {
        "completed" => SharedTransferStatus.Completed,
        "awaiting_processing" => SharedTransferStatus.InProgress,
        "awaiting_email_confirmation" => SharedTransferStatus.InProgress,
        "awaiting_bitvavo_inspection" => SharedTransferStatus.InProgress,
        "approved" => SharedTransferStatus.InProgress,
        "sending" => SharedTransferStatus.InProgress,
        "in_mempool" => SharedTransferStatus.InProgress,
        "processed" => SharedTransferStatus.InProgress,
        "canceled" => SharedTransferStatus.Failed,
        _ => SharedTransferStatus.Unknown,
    };

    /// <summary>
    /// The number of decimal places a price grid step needs: <c>1.00</c> is 0, <c>0.50</c> is 1, <c>0.00001</c> is 5. Trailing zeros
    /// do not count. Used to turn a market's <c>tickSize</c> into the price decimals the Shared symbol model asks for.
    /// </summary>
    public static int DecimalPlaces(this decimal step)
    {
        // Dividing by 1.000…0 (28 zeros) strips trailing zeros from the decimal's scale without changing its value.
        var normalized = step / 1.0000000000000000000000000000m;
        return (decimal.GetBits(normalized)[3] >> 16) & 0xFF;
    }
}
