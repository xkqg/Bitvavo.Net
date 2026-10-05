// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using Bitvavo.Net.Clients.SpotApi;
using Bitvavo.Net.Enums;
using Bitvavo.Net.Objects.Internal;
using Bitvavo.Net.Objects.Models.Spot;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Errors;
using CryptoExchange.Net.SharedApis;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests.Clients.SpotApi.SharedApi;

/// <summary>
/// Every arm of the trigger-order mappings, each on its own. A Bitvavo trigger order is an ordinary order of type
/// <c>stopLoss</c>, <c>stopLossLimit</c>, <c>takeProfit</c> or <c>takeProfitLimit</c>; the direction semantics are the ones of the
/// OpenAPI specification: a stop loss fires when the market price is worse than the stop price (lower for a sell, higher for a buy),
/// a take profit when it is better (higher for a sell, lower for a buy).
/// </summary>
public class BitvavoSharedTriggerMappingTests
{
    private static SharedSymbol EthEur => new(TradingMode.Spot, "ETH", "EUR");

    private static readonly DateTime CreatedAt = new(2024, 4, 26, 12, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime UpdatedAt = new(2024, 4, 26, 12, 5, 0, DateTimeKind.Utc);

    private static PlaceSpotTriggerOrderRequest TriggerRequest(
        SharedTriggerPriceDirection direction = SharedTriggerPriceDirection.PriceBelow,
        SharedOrderSide side = SharedOrderSide.Sell,
        SharedQuantity? quantity = null,
        decimal? orderPrice = null,
        ExchangeParameters? parameters = null)
        => new(EthEur, direction, 1500m, side, quantity ?? SharedQuantity.Base(0.5m), orderPrice, parameters);

    private static ExchangeParameters WithTriggerReference(object value)
        => new(new ExchangeParameter(BitvavoExchange.ExchangeName, BitvavoSharedMappingExtensions.TriggerReferenceParameter, value));

    // ── trigger order type from side, price direction and limit price ────────────────────

    /// <summary>The mapping table: the type is a stop loss when the trigger fires on the worse price, a take profit on the better one.</summary>
    [Theory]
    [InlineData(SharedOrderSide.Sell, SharedTriggerPriceDirection.PriceBelow, false, OrderType.StopLoss)]
    [InlineData(SharedOrderSide.Sell, SharedTriggerPriceDirection.PriceBelow, true, OrderType.StopLossLimit)]
    [InlineData(SharedOrderSide.Sell, SharedTriggerPriceDirection.PriceAbove, false, OrderType.TakeProfit)]
    [InlineData(SharedOrderSide.Sell, SharedTriggerPriceDirection.PriceAbove, true, OrderType.TakeProfitLimit)]
    [InlineData(SharedOrderSide.Buy, SharedTriggerPriceDirection.PriceAbove, false, OrderType.StopLoss)]
    [InlineData(SharedOrderSide.Buy, SharedTriggerPriceDirection.PriceAbove, true, OrderType.StopLossLimit)]
    [InlineData(SharedOrderSide.Buy, SharedTriggerPriceDirection.PriceBelow, false, OrderType.TakeProfit)]
    [InlineData(SharedOrderSide.Buy, SharedTriggerPriceDirection.PriceBelow, true, OrderType.TakeProfitLimit)]
    public void Side_direction_and_limit_price_select_the_bitvavo_trigger_order_type(SharedOrderSide side, SharedTriggerPriceDirection direction, bool limit, OrderType expected)
    {
        direction.ToBitvavoTriggerOrderType(side, limit).ShouldBe(expected);
    }

    [Fact]
    public void An_unmapped_direction_or_side_is_a_caller_error()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => ((SharedTriggerPriceDirection)(-9)).ToBitvavoTriggerOrderType(SharedOrderSide.Sell, false));
        Should.Throw<ArgumentOutOfRangeException>(() => SharedTriggerPriceDirection.PriceBelow.ToBitvavoTriggerOrderType((SharedOrderSide)(-9), false));
    }

    // ── which Bitvavo order types are trigger orders ─────────────────────────────────────

    [Theory]
    [InlineData(OrderType.Market, false)]
    [InlineData(OrderType.Limit, false)]
    [InlineData(OrderType.StopLoss, true)]
    [InlineData(OrderType.StopLossLimit, true)]
    [InlineData(OrderType.TakeProfit, true)]
    [InlineData(OrderType.TakeProfitLimit, true)]
    public void Only_the_stop_loss_and_take_profit_family_is_a_trigger_order_type(OrderType type, bool expected)
    {
        type.IsTriggerOrderType().ShouldBe(expected);
    }

    [Fact]
    public void An_order_type_this_library_does_not_know_is_not_a_trigger_order_type()
    {
        ((OrderType)(-9)).IsTriggerOrderType().ShouldBeFalse();
    }

    [Fact]
    public void Every_bitvavo_order_type_is_either_a_trigger_order_type_or_one_of_the_two_plain_types()
    {
        foreach (var type in Enum.GetValues<OrderType>())
        {
            var plain = type == OrderType.Market || type == OrderType.Limit;
            type.IsTriggerOrderType().ShouldBe(!plain, type.ToString());
        }
    }

    // ── the order type a trigger order is reported with ──────────────────────────────────

    [Theory]
    [InlineData(OrderType.StopLoss, null, SharedOrderType.Market)]
    [InlineData(OrderType.TakeProfit, null, SharedOrderType.Market)]
    [InlineData(OrderType.StopLossLimit, null, SharedOrderType.Limit)]
    [InlineData(OrderType.StopLossLimit, false, SharedOrderType.Limit)]
    [InlineData(OrderType.StopLossLimit, true, SharedOrderType.LimitMaker)]
    [InlineData(OrderType.TakeProfitLimit, null, SharedOrderType.Limit)]
    [InlineData(OrderType.TakeProfitLimit, true, SharedOrderType.LimitMaker)]
    [InlineData(OrderType.Market, null, SharedOrderType.Market)]
    [InlineData(OrderType.Limit, null, SharedOrderType.Limit)]
    [InlineData(OrderType.Limit, true, SharedOrderType.LimitMaker)]
    public void A_trigger_order_is_reported_with_the_type_of_the_order_it_places(OrderType type, bool? postOnly, SharedOrderType expected)
    {
        type.ToSharedTriggerOrderType(postOnly).ShouldBe(expected);
    }

    [Fact]
    public void An_order_type_this_library_does_not_know_is_reported_as_Other()
    {
        ((OrderType)(-9)).ToSharedTriggerOrderType(null).ShouldBe(SharedOrderType.Other);
    }

    // ── trigger order status ─────────────────────────────────────────────────────────────

    /// <summary>A trigger order that fired keeps its id and moves awaitingTrigger → new (docs.bitvavo.com, Order lifecycle).</summary>
    [Theory]
    [InlineData(OrderStatus.AwaitingTrigger, SharedTriggerOrderStatus.Active)]
    [InlineData(OrderStatus.New, SharedTriggerOrderStatus.Triggered)]
    [InlineData(OrderStatus.PartiallyFilled, SharedTriggerOrderStatus.Triggered)]
    [InlineData(OrderStatus.Filled, SharedTriggerOrderStatus.Filled)]
    [InlineData(OrderStatus.Canceled, SharedTriggerOrderStatus.CanceledOrRejected)]
    [InlineData(OrderStatus.CanceledAuction, SharedTriggerOrderStatus.CanceledOrRejected)]
    [InlineData(OrderStatus.CanceledSelfTradePrevention, SharedTriggerOrderStatus.CanceledOrRejected)]
    [InlineData(OrderStatus.CanceledIoc, SharedTriggerOrderStatus.CanceledOrRejected)]
    [InlineData(OrderStatus.CanceledFok, SharedTriggerOrderStatus.CanceledOrRejected)]
    [InlineData(OrderStatus.CanceledMarketProtection, SharedTriggerOrderStatus.CanceledOrRejected)]
    [InlineData(OrderStatus.CanceledPostOnly, SharedTriggerOrderStatus.CanceledOrRejected)]
    [InlineData(OrderStatus.Expired, SharedTriggerOrderStatus.CanceledOrRejected)]
    [InlineData(OrderStatus.Rejected, SharedTriggerOrderStatus.CanceledOrRejected)]
    public void Bitvavo_order_status_maps_to_the_shared_trigger_order_status(OrderStatus status, SharedTriggerOrderStatus expected)
    {
        status.ToSharedTriggerOrderStatus().ShouldBe(expected);
    }

    [Fact]
    public void Every_bitvavo_order_status_has_a_defined_trigger_status_and_an_unknown_value_maps_to_Unknown()
    {
        foreach (var status in Enum.GetValues<OrderStatus>())
        {
            status.ToSharedTriggerOrderStatus().ShouldNotBe(SharedTriggerOrderStatus.Unknown, status.ToString());
        }

        ((OrderStatus)(-9)).ToSharedTriggerOrderStatus().ShouldBe(SharedTriggerOrderStatus.Unknown);
    }

    // ── trigger order direction ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(OrderSide.Buy, SharedTriggerOrderDirection.Enter)]
    [InlineData(OrderSide.Sell, SharedTriggerOrderDirection.Exit)]
    public void A_buy_enters_and_a_sell_exits(OrderSide side, SharedTriggerOrderDirection expected)
    {
        side.ToSharedTriggerOrderDirection().ShouldBe(expected);
    }

    [Fact]
    public void An_order_side_this_library_does_not_know_has_no_trigger_order_direction()
    {
        ((OrderSide)(-9)).ToSharedTriggerOrderDirection().ShouldBeNull();
    }

    // ── the TriggerReference exchange parameter ──────────────────────────────────────────

    [Fact]
    public void The_trigger_reference_rule_is_one_optional_exchange_parameter_of_the_bitvavo_enum()
    {
        var rule = BitvavoSharedMappingExtensions.TriggerReferenceRule;

        rule.Name.ShouldBe("TriggerReference");
        rule.Name.ShouldBe(BitvavoSharedMappingExtensions.TriggerReferenceParameter);
        rule.Requirement.ShouldBe(ExchangeParameterRequirement.Optional);
        rule.ValueType.ShouldBe(typeof(TriggerReference));
    }

    [Fact]
    public void Without_a_trigger_reference_parameter_the_last_trade_triggers_the_order()
    {
        var request = TriggerRequest();

        request.GetTriggerReference().ShouldBe(TriggerReference.LastTrade);
        request.GetTriggerReferenceError().ShouldBeNull();
    }

    [Theory]
    [InlineData(TriggerReference.LastTrade)]
    [InlineData(TriggerReference.BestBid)]
    [InlineData(TriggerReference.BestAsk)]
    [InlineData(TriggerReference.MidPrice)]
    public void A_trigger_reference_in_the_exchange_parameters_is_read_back(TriggerReference reference)
    {
        var request = TriggerRequest(parameters: WithTriggerReference(reference));

        request.GetTriggerReference().ShouldBe(reference);
        request.GetTriggerReferenceError().ShouldBeNull();
    }

    [Fact]
    public void A_trigger_reference_of_another_type_is_an_argument_error_and_not_an_exception()
    {
        var request = TriggerRequest(parameters: WithTriggerReference("bestBid"));

        var error = request.GetTriggerReferenceError().ShouldBeOfType<ArgumentError>();

        error.ErrorType.ShouldBe(ErrorType.InvalidParameter);
        error.Message.ShouldNotBeNull().ShouldContain("TriggerReference");
    }

    // ── the request ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_trigger_order_without_a_limit_price_becomes_a_market_trigger_with_the_price_trigger_and_the_last_trade_reference()
    {
        var request = TriggerRequest();

        var mapped = request.ToBitvavoTriggerOrderRequest("ETH-EUR", 7, TriggerReference.LastTrade);

        mapped.ShouldBe(new BitvavoPlaceOrderRequest(
            "ETH-EUR",
            OrderSide.Sell,
            OrderType.StopLoss,
            7,
            Amount: 0.5m,
            TriggerAmount: 1500m,
            TriggerType: TriggerType.Price,
            TriggerReference: TriggerReference.LastTrade));
    }

    [Fact]
    public void A_trigger_order_with_a_limit_price_carries_the_price_the_time_in_force_and_the_client_order_id()
    {
        var request = TriggerRequest(SharedTriggerPriceDirection.PriceAbove, SharedOrderSide.Sell, orderPrice: 1495m);
        request.ClientOrderId = "2be7d0df-d8dc-7b93-a550-8876f3b393e9";
        request.TimeInForce = SharedTimeInForce.ImmediateOrCancel;

        var mapped = request.ToBitvavoTriggerOrderRequest("ETH-EUR", 7, TriggerReference.BestBid);

        mapped.ShouldBe(new BitvavoPlaceOrderRequest(
            "ETH-EUR",
            OrderSide.Sell,
            OrderType.TakeProfitLimit,
            7,
            Amount: 0.5m,
            Price: 1495m,
            TriggerAmount: 1500m,
            TriggerType: TriggerType.Price,
            TriggerReference: TriggerReference.BestBid,
            TimeInForce: TimeInForce.ImmediateOrCancel,
            ClientOrderId: "2be7d0df-d8dc-7b93-a550-8876f3b393e9"));
    }

    [Fact]
    public void A_market_trigger_sends_no_time_in_force_because_a_market_order_executes_at_once()
    {
        var request = TriggerRequest();
        request.TimeInForce = SharedTimeInForce.FillOrKill;

        var mapped = request.ToBitvavoTriggerOrderRequest("ETH-EUR", 7, TriggerReference.LastTrade);

        mapped.TimeInForce.ShouldBeNull();
    }

    [Fact]
    public void A_market_trigger_may_take_its_quantity_in_the_quote_asset()
    {
        var request = TriggerRequest(SharedTriggerPriceDirection.PriceAbove, SharedOrderSide.Buy, SharedQuantity.Quote(100m));

        var mapped = request.ToBitvavoTriggerOrderRequest("ETH-EUR", 7, TriggerReference.MidPrice);

        mapped.Side.ShouldBe(OrderSide.Buy);
        mapped.OrderType.ShouldBe(OrderType.StopLoss);
        mapped.Amount.ShouldBeNull();
        mapped.AmountQuote.ShouldBe(100m);
        mapped.TriggerReference.ShouldBe(TriggerReference.MidPrice);
    }

    // ── what Bitvavo cannot express ──────────────────────────────────────────────────────

    [Fact]
    public void A_quantity_in_the_base_asset_or_in_the_quote_asset_of_a_market_trigger_can_be_expressed()
    {
        TriggerRequest(quantity: SharedQuantity.Base(1m)).GetBitvavoTriggerOrderError().ShouldBeNull();
        TriggerRequest(quantity: SharedQuantity.Quote(100m)).GetBitvavoTriggerOrderError().ShouldBeNull();
        TriggerRequest(quantity: SharedQuantity.Base(1m), orderPrice: 1490m).GetBitvavoTriggerOrderError().ShouldBeNull();
    }

    [Fact]
    public void A_quantity_in_both_assets_is_rejected_because_Bitvavo_takes_amount_or_amountQuote_but_not_both()
    {
        var both = new SharedQuantity { QuantityInBaseAsset = 1m, QuantityInQuoteAsset = 100m };

        var error = TriggerRequest(quantity: both).GetBitvavoTriggerOrderError().ShouldBeOfType<ArgumentError>();

        error.ErrorType.ShouldBe(ErrorType.InvalidParameter);
        error.Message.ShouldNotBeNull().ShouldContain("Quantity");
    }

    [Fact]
    public void A_quantity_in_neither_asset_is_rejected()
    {
        var error = TriggerRequest(quantity: SharedQuantity.Contracts(1m)).GetBitvavoTriggerOrderError().ShouldBeOfType<ArgumentError>();

        error.ErrorType.ShouldBe(ErrorType.MissingParameter);
        error.Message.ShouldNotBeNull().ShouldContain("Quantity");
    }

    [Fact]
    public void A_trigger_order_with_a_limit_price_cannot_take_its_quantity_in_the_quote_asset()
    {
        var error = TriggerRequest(quantity: SharedQuantity.Quote(100m), orderPrice: 1490m).GetBitvavoTriggerOrderError().ShouldBeOfType<ArgumentError>();

        error.ErrorType.ShouldBe(ErrorType.InvalidParameter);
        error.Message.ShouldNotBeNull().ShouldContain("limit price");
    }

    [Fact]
    public void Mapping_a_request_that_cannot_be_expressed_is_a_caller_error()
    {
        var both = new SharedQuantity { QuantityInBaseAsset = 1m, QuantityInQuoteAsset = 100m };

        Should.Throw<ArgumentException>(() => TriggerRequest(quantity: both).ToBitvavoTriggerOrderRequest("ETH-EUR", 7, TriggerReference.LastTrade));
        Should.Throw<ArgumentException>(() => TriggerRequest(quantity: SharedQuantity.Contracts(1m)).ToBitvavoTriggerOrderRequest("ETH-EUR", 7, TriggerReference.LastTrade));
        Should.Throw<ArgumentException>(() => TriggerRequest(quantity: SharedQuantity.Quote(1m), orderPrice: 1490m).ToBitvavoTriggerOrderRequest("ETH-EUR", 7, TriggerReference.LastTrade));
    }

    // ── recognising a trigger order in a response ────────────────────────────────────────

    [Fact]
    public void An_order_of_a_trigger_type_is_a_trigger_order()
    {
        new BitvavoOrder { OrderType = OrderType.StopLossLimit }.WasPlacedAsTriggerOrder().ShouldBeTrue();
        new BitvavoOrder { OrderType = OrderType.TakeProfit }.WasPlacedAsTriggerOrder().ShouldBeTrue();
    }

    [Fact]
    public void An_order_that_reports_a_plain_type_but_still_carries_trigger_data_is_a_trigger_order()
    {
        new BitvavoOrder { OrderType = OrderType.Limit, TriggerAmount = 1500m }.WasPlacedAsTriggerOrder().ShouldBeTrue();
        new BitvavoOrder { OrderType = OrderType.Market, TriggerPrice = 1500m }.WasPlacedAsTriggerOrder().ShouldBeTrue();
    }

    [Fact]
    public void A_plain_order_without_trigger_data_is_not_a_trigger_order()
    {
        new BitvavoOrder { OrderType = OrderType.Limit }.WasPlacedAsTriggerOrder().ShouldBeFalse();
        new BitvavoOrder { OrderType = OrderType.Market }.WasPlacedAsTriggerOrder().ShouldBeFalse();
    }

    // ── the order as a Shared trigger order ──────────────────────────────────────────────

    private static BitvavoOrder AwaitingStopLossLimit() => new()
    {
        OrderId = "ord-1",
        ClientOrderId = "2be7d0df-d8dc-7b93-a550-8876f3b393e9",
        Market = "ETH-EUR",
        Created = CreatedAt,
        Updated = UpdatedAt,
        Status = OrderStatus.AwaitingTrigger,
        Side = OrderSide.Sell,
        OrderType = OrderType.StopLossLimit,
        Amount = 0.5m,
        AmountRemaining = 0.5m,
        Price = 1490m,
        TriggerAmount = 1500m,
        TriggerPrice = 1500m,
        TriggerType = TriggerType.Price,
        TriggerReference = TriggerReference.BestBid,
        FilledAmount = 0m,
        FilledAmountQuote = 0m,
        FeePaid = 0m,
        FeeCurrency = "EUR",
        TimeInForce = TimeInForce.GoodTillCanceled,
        PostOnly = false,
    };

    [Fact]
    public void An_untriggered_stop_loss_limit_is_an_active_exit_with_its_trigger_price_and_limit_price()
    {
        var shared = AwaitingStopLossLimit().ToSharedSpotTriggerOrder(EthEur);

        shared.TriggerOrderId.ShouldBe("ord-1");
        shared.Symbol.ShouldBe("ETH-EUR");
        shared.SharedSymbol.ShouldBe(EthEur);
        shared.ClientOrderId.ShouldBe("2be7d0df-d8dc-7b93-a550-8876f3b393e9");
        shared.OrderType.ShouldBe(SharedOrderType.Limit);
        shared.OrderDirection.ShouldBe(SharedTriggerOrderDirection.Exit);
        shared.Status.ShouldBe(SharedTriggerOrderStatus.Active);
        shared.PlacedOrderId.ShouldBeNull();
        shared.TriggerPrice.ShouldBe(1500m);
        shared.OrderPrice.ShouldBe(1490m);
        shared.TimeInForce.ShouldBe(SharedTimeInForce.GoodTillCanceled);
        shared.AveragePrice.ShouldBeNull();
        shared.Fee.ShouldBe(0m);
        shared.FeeAsset.ShouldBe("EUR");
        shared.CreateTime.ShouldBe(CreatedAt);
        shared.UpdateTime.ShouldBe(UpdatedAt);
        var quantity = shared.OrderQuantity.ShouldNotBeNull();
        quantity.QuantityInBaseAsset.ShouldBe(0.5m);
        quantity.QuantityInQuoteAsset.ShouldBeNull();
        var filled = shared.QuantityFilled.ShouldNotBeNull();
        filled.QuantityInBaseAsset.ShouldBe(0m);
    }

    [Fact]
    public void A_buy_trigger_is_an_enter()
    {
        var order = AwaitingStopLossLimit() with { Side = OrderSide.Buy };

        order.ToSharedSpotTriggerOrder(EthEur).OrderDirection.ShouldBe(SharedTriggerOrderDirection.Enter);
    }

    [Fact]
    public void A_triggered_order_keeps_its_id_and_reports_it_as_the_placed_order()
    {
        var order = AwaitingStopLossLimit() with { Status = OrderStatus.New };

        var shared = order.ToSharedSpotTriggerOrder(EthEur);

        shared.Status.ShouldBe(SharedTriggerOrderStatus.Triggered);
        shared.PlacedOrderId.ShouldBe("ord-1");
    }

    [Fact]
    public void A_filled_market_trigger_reports_its_fills_average_price_and_fee()
    {
        var order = new BitvavoOrder
        {
            OrderId = "ord-2",
            Market = "ETH-EUR",
            Created = CreatedAt,
            Updated = UpdatedAt,
            Status = OrderStatus.Filled,
            Side = OrderSide.Sell,
            OrderType = OrderType.TakeProfit,
            Amount = 0.5m,
            AmountRemaining = 0m,
            TriggerAmount = 2000m,
            FilledAmount = 0.5m,
            FilledAmountQuote = 1000m,
            FeePaid = 1.5m,
            FeeCurrency = "EUR",
        };

        var shared = order.ToSharedSpotTriggerOrder(EthEur);

        shared.OrderType.ShouldBe(SharedOrderType.Market);
        shared.Status.ShouldBe(SharedTriggerOrderStatus.Filled);
        shared.PlacedOrderId.ShouldBe("ord-2");
        shared.OrderPrice.ShouldBeNull();
        shared.AveragePrice.ShouldBe(2000m);
        shared.Fee.ShouldBe(1.5m);
        var filled = shared.QuantityFilled.ShouldNotBeNull();
        filled.QuantityInBaseAsset.ShouldBe(0.5m);
        filled.QuantityInQuoteAsset.ShouldBe(1000m);
    }

    [Fact]
    public void A_fill_without_a_quote_amount_has_no_average_price()
    {
        var order = AwaitingStopLossLimit() with { Status = OrderStatus.PartiallyFilled, FilledAmount = 0.25m, FilledAmountQuote = null };

        order.ToSharedSpotTriggerOrder(EthEur).AveragePrice.ShouldBeNull();
    }

    [Fact]
    public void A_canceled_trigger_has_no_placed_order()
    {
        var order = AwaitingStopLossLimit() with { Status = OrderStatus.Canceled };

        var shared = order.ToSharedSpotTriggerOrder(EthEur);

        shared.Status.ShouldBe(SharedTriggerOrderStatus.CanceledOrRejected);
        shared.PlacedOrderId.ShouldBeNull();
    }

    [Fact]
    public void A_trigger_ordered_in_the_quote_asset_has_no_base_quantity()
    {
        var order = AwaitingStopLossLimit() with { OrderType = OrderType.StopLoss, Amount = null, AmountRemaining = null, Price = null, AmountQuote = 100m };

        var shared = order.ToSharedSpotTriggerOrder(EthEur);

        shared.OrderType.ShouldBe(SharedOrderType.Market);
        var quantity = shared.OrderQuantity.ShouldNotBeNull();
        quantity.QuantityInBaseAsset.ShouldBeNull();
        quantity.QuantityInQuoteAsset.ShouldBe(100m);
    }

    [Fact]
    public void The_trigger_price_is_the_trigger_amount_and_falls_back_to_the_trigger_price_and_then_to_zero()
    {
        var order = AwaitingStopLossLimit();

        (order with { TriggerAmount = 1500m, TriggerPrice = 1400m }).ToSharedSpotTriggerOrder(EthEur).TriggerPrice.ShouldBe(1500m);
        (order with { TriggerAmount = null, TriggerPrice = 1400m }).ToSharedSpotTriggerOrder(EthEur).TriggerPrice.ShouldBe(1400m);
        (order with { TriggerAmount = null, TriggerPrice = null }).ToSharedSpotTriggerOrder(EthEur).TriggerPrice.ShouldBe(0m);
    }

    [Fact]
    public void A_post_only_limit_trigger_is_a_limit_maker()
    {
        var order = AwaitingStopLossLimit() with { PostOnly = true };

        order.ToSharedSpotTriggerOrder(EthEur).OrderType.ShouldBe(SharedOrderType.LimitMaker);
    }

    [Fact]
    public void Without_a_shared_symbol_the_symbol_comes_from_the_market_name()
    {
        var shared = AwaitingStopLossLimit().ToSharedSpotTriggerOrder(null);

        var symbol = shared.SharedSymbol.ShouldNotBeNull();
        symbol.BaseAsset.ShouldBe("ETH");
        symbol.QuoteAsset.ShouldBe("EUR");
        shared.Symbol.ShouldBe("ETH-EUR");
    }

    [Fact]
    public void An_order_side_this_library_does_not_know_cannot_become_a_trigger_order_and_says_so()
    {
        var order = AwaitingStopLossLimit() with { Side = (OrderSide)(-9) };

        Should.Throw<InvalidOperationException>(() => order.ToSharedSpotTriggerOrder(EthEur));
    }
}
