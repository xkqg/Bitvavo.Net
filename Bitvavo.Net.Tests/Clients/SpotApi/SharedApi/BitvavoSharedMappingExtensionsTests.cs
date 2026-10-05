// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Linq;
using Bitvavo.Net.Clients.SpotApi;
using Bitvavo.Net.Enums;
using CryptoExchange.Net.SharedApis;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests.Clients.SpotApi.SharedApi;

/// <summary>
/// Every arm of the enum and symbol mappings the REST and WebSocket Shared APIs share. Both directions are pinned: what Bitvavo
/// sends becomes a Shared value (it must never throw: it is server data), and what a Shared request names becomes a Bitvavo
/// value (a value Bitvavo cannot express is a caller error).
/// </summary>
public class BitvavoSharedMappingExtensionsTests
{
    // ── symbols ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("ETH-EUR", "ETH", "EUR")]
    [InlineData("BTC-USDC", "BTC", "USDC")]
    public void Market_string_splits_into_base_and_quote_and_keeps_the_symbol_name(string market, string baseAsset, string quoteAsset)
    {
        var symbol = market.ToSharedSymbol();

        symbol.TradingMode.ShouldBe(TradingMode.Spot);
        symbol.BaseAsset.ShouldBe(baseAsset);
        symbol.QuoteAsset.ShouldBe(quoteAsset);
        symbol.SymbolName.ShouldBe(market);
    }

    [Fact]
    public void Market_string_without_a_dash_becomes_a_base_only_symbol()
    {
        var symbol = "ETH".ToSharedSymbol();

        symbol.BaseAsset.ShouldBe("ETH");
        symbol.QuoteAsset.ShouldBe(string.Empty);
        symbol.SymbolName.ShouldBe("ETH");
    }

    // ── kline intervals ──────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(SharedKlineInterval.OneMinute, KlineInterval.OneMinute)]
    [InlineData(SharedKlineInterval.FiveMinutes, KlineInterval.FiveMinutes)]
    [InlineData(SharedKlineInterval.FifteenMinutes, KlineInterval.FifteenMinutes)]
    [InlineData(SharedKlineInterval.ThirtyMinutes, KlineInterval.ThirtyMinutes)]
    [InlineData(SharedKlineInterval.OneHour, KlineInterval.OneHour)]
    [InlineData(SharedKlineInterval.TwoHours, KlineInterval.TwoHours)]
    [InlineData(SharedKlineInterval.FourHours, KlineInterval.FourHours)]
    [InlineData(SharedKlineInterval.SixHours, KlineInterval.SixHours)]
    [InlineData(SharedKlineInterval.EightHours, KlineInterval.EightHours)]
    [InlineData(SharedKlineInterval.TwelveHours, KlineInterval.TwelveHours)]
    [InlineData(SharedKlineInterval.OneDay, KlineInterval.OneDay)]
    [InlineData(SharedKlineInterval.OneWeek, KlineInterval.OneWeek)]
    [InlineData(SharedKlineInterval.OneMonth, KlineInterval.OneMonth)]
    public void Kline_interval_maps_to_the_bitvavo_interval(SharedKlineInterval shared, KlineInterval expected)
    {
        shared.ToBitvavoInterval().ShouldBe(expected);
    }

    [Fact]
    public void Bitvavo_has_no_three_minute_candle()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => SharedKlineInterval.ThreeMinutes.ToBitvavoInterval());
        BitvavoSharedMappingExtensions.SupportedKlineIntervals.ShouldNotContain(SharedKlineInterval.ThreeMinutes);
    }

    [Fact]
    public void Every_supported_interval_maps_and_the_supported_set_has_one_entry_per_bitvavo_interval()
    {
        BitvavoSharedMappingExtensions.SupportedKlineIntervals.Length.ShouldBe(Enum.GetValues<KlineInterval>().Length);
        foreach (var interval in BitvavoSharedMappingExtensions.SupportedKlineIntervals)
        {
            Should.NotThrow(() => interval.ToBitvavoInterval());
        }
    }

    // ── order side ───────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(OrderSide.Buy, SharedOrderSide.Buy)]
    [InlineData(OrderSide.Sell, SharedOrderSide.Sell)]
    public void Order_side_maps_both_ways(OrderSide bitvavo, SharedOrderSide shared)
    {
        bitvavo.ToSharedSide().ShouldBe(shared);
        shared.ToBitvavoSide().ShouldBe(bitvavo);
    }

    [Fact]
    public void Unknown_wire_side_maps_to_no_shared_side_and_an_unsupported_shared_side_is_rejected()
    {
        ((OrderSide)(-9)).ToSharedSide().ShouldBeNull();
        Should.Throw<ArgumentOutOfRangeException>(() => ((SharedOrderSide)(-9)).ToBitvavoSide());
    }

    // ── order type ───────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(OrderType.Market, null, SharedOrderType.Market)]
    [InlineData(OrderType.Limit, null, SharedOrderType.Limit)]
    [InlineData(OrderType.Limit, false, SharedOrderType.Limit)]
    [InlineData(OrderType.Limit, true, SharedOrderType.LimitMaker)]
    [InlineData(OrderType.StopLoss, null, SharedOrderType.Other)]
    [InlineData(OrderType.StopLossLimit, null, SharedOrderType.Other)]
    [InlineData(OrderType.TakeProfit, null, SharedOrderType.Other)]
    [InlineData(OrderType.TakeProfitLimit, true, SharedOrderType.Other)]
    public void Bitvavo_order_type_maps_to_the_shared_type(OrderType type, bool? postOnly, SharedOrderType expected)
    {
        type.ToSharedOrderType(postOnly).ShouldBe(expected);
    }

    [Fact]
    public void Every_bitvavo_order_type_maps_without_throwing()
    {
        foreach (var type in Enum.GetValues<OrderType>())
        {
            Should.NotThrow(() => type.ToSharedOrderType(null));
        }
    }

    [Theory]
    [InlineData(SharedOrderType.Market, OrderType.Market)]
    [InlineData(SharedOrderType.Limit, OrderType.Limit)]
    [InlineData(SharedOrderType.LimitMaker, OrderType.Limit)]
    public void Shared_order_type_maps_to_the_bitvavo_type(SharedOrderType shared, OrderType expected)
    {
        shared.ToBitvavoOrderType().ShouldBe(expected);
    }

    [Fact]
    public void A_shared_order_type_bitvavo_cannot_place_is_rejected()
    {
        Should.Throw<ArgumentException>(() => SharedOrderType.Other.ToBitvavoOrderType());
    }

    // ── order status ─────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(OrderStatus.New, SharedOrderStatus.Open)]
    [InlineData(OrderStatus.AwaitingTrigger, SharedOrderStatus.Open)]
    [InlineData(OrderStatus.PartiallyFilled, SharedOrderStatus.Open)]
    [InlineData(OrderStatus.Filled, SharedOrderStatus.Filled)]
    [InlineData(OrderStatus.Canceled, SharedOrderStatus.Canceled)]
    [InlineData(OrderStatus.CanceledAuction, SharedOrderStatus.Canceled)]
    [InlineData(OrderStatus.CanceledSelfTradePrevention, SharedOrderStatus.Canceled)]
    [InlineData(OrderStatus.CanceledIoc, SharedOrderStatus.Canceled)]
    [InlineData(OrderStatus.CanceledFok, SharedOrderStatus.Canceled)]
    [InlineData(OrderStatus.CanceledMarketProtection, SharedOrderStatus.Canceled)]
    [InlineData(OrderStatus.CanceledPostOnly, SharedOrderStatus.Canceled)]
    [InlineData(OrderStatus.Expired, SharedOrderStatus.Canceled)]
    [InlineData(OrderStatus.Rejected, SharedOrderStatus.Canceled)]
    public void Bitvavo_order_status_maps_to_the_shared_status(OrderStatus status, SharedOrderStatus expected)
    {
        status.ToSharedStatus().ShouldBe(expected);
    }

    [Fact]
    public void Every_bitvavo_order_status_has_a_defined_shared_status_and_an_unknown_value_maps_to_Unknown()
    {
        foreach (var status in Enum.GetValues<OrderStatus>())
        {
            status.ToSharedStatus().ShouldNotBe(SharedOrderStatus.Unknown, status.ToString());
        }

        ((OrderStatus)(-9)).ToSharedStatus().ShouldBe(SharedOrderStatus.Unknown);
    }

    // ── time in force ────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(TimeInForce.GoodTillCanceled, SharedTimeInForce.GoodTillCanceled)]
    [InlineData(TimeInForce.ImmediateOrCancel, SharedTimeInForce.ImmediateOrCancel)]
    [InlineData(TimeInForce.FillOrKill, SharedTimeInForce.FillOrKill)]
    public void Time_in_force_maps_both_ways(TimeInForce bitvavo, SharedTimeInForce shared)
    {
        bitvavo.ToSharedTimeInForce().ShouldBe(shared);
        shared.ToBitvavoTimeInForce().ShouldBe(bitvavo);
    }

    [Fact]
    public void Unknown_wire_time_in_force_maps_to_none_and_an_unsupported_shared_value_is_rejected()
    {
        ((TimeInForce)(-9)).ToSharedTimeInForce().ShouldBeNull();
        Should.Throw<ArgumentOutOfRangeException>(() => ((SharedTimeInForce)(-9)).ToBitvavoTimeInForce());
    }

    // ── transfer status (deposits and withdrawals) ───────────────────────────────────────

    /// <summary>The nine documented withdrawal statuses (docs.bitvavo.com, GET /withdrawalHistory), plus the one deposit status Bitvavo documents.</summary>
    [Theory]
    [InlineData("awaiting_processing", SharedTransferStatus.InProgress)]
    [InlineData("awaiting_email_confirmation", SharedTransferStatus.InProgress)]
    [InlineData("awaiting_bitvavo_inspection", SharedTransferStatus.InProgress)]
    [InlineData("approved", SharedTransferStatus.InProgress)]
    [InlineData("sending", SharedTransferStatus.InProgress)]
    [InlineData("in_mempool", SharedTransferStatus.InProgress)]
    [InlineData("processed", SharedTransferStatus.InProgress)]
    [InlineData("completed", SharedTransferStatus.Completed)]
    [InlineData("canceled", SharedTransferStatus.Failed)]
    [InlineData("COMPLETED", SharedTransferStatus.Completed)]
    public void Transfer_status_string_maps_to_the_shared_status(string wire, SharedTransferStatus expected)
    {
        wire.ToSharedTransferStatus().ShouldBe(expected);
    }

    [Theory]
    [InlineData("a status Bitvavo adds later")]
    [InlineData("")]
    [InlineData(null)]
    public void Unlisted_transfer_status_maps_to_Unknown(string? wire)
    {
        wire.ToSharedTransferStatus().ShouldBe(SharedTransferStatus.Unknown);
    }

    // ── tick size → decimals ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("1.00", 0)]
    [InlineData("1", 0)]
    [InlineData("0.1", 1)]
    [InlineData("0.01", 2)]
    [InlineData("0.00001", 5)]
    [InlineData("0.00000001", 8)]
    [InlineData("10", 0)]
    [InlineData("0.50", 1)]
    public void Tick_size_gives_the_number_of_price_decimals(string tick, int decimals)
    {
        decimal.Parse(tick, System.Globalization.CultureInfo.InvariantCulture).DecimalPlaces().ShouldBe(decimals);
    }
}
