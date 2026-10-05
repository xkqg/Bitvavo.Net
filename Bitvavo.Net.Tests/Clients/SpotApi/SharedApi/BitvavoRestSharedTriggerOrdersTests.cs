// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Bitvavo.Net.Clients.SpotApi;
using Bitvavo.Net.Enums;
using Bitvavo.Net.Objects.Internal;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Errors;
using CryptoExchange.Net.SharedApis;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests.Clients.SpotApi.SharedApi;

/// <summary>
/// The Shared trigger-order capabilities on the REST API, [V2] only: <c>IPlaceSpotTriggerOrderRest</c>,
/// <c>IGetSpotTriggerOrderRest</c> and <c>ICancelSpotTriggerOrderRest</c>, through the real request pipeline. Bitvavo has no
/// trigger-order endpoint of its own: a trigger order is an ordinary order (<c>POST /v2/order</c>) of type <c>stopLoss</c>,
/// <c>stopLossLimit</c>, <c>takeProfit</c> or <c>takeProfitLimit</c> with <c>triggerAmount</c>, <c>triggerType</c> and
/// <c>triggerReference</c>, read with <c>GET /v2/order</c> and canceled with <c>DELETE /v2/order</c>.
/// </summary>
public class BitvavoRestSharedTriggerOrdersTests
{
    private const string TriggerOrderId = "0b4c9c3a-3d3e-4f75-9a62-6a3a5b0d7c01";

    private const string TriggerClientOrderId = "2be7d0df-d8dc-7b93-a550-8876f3b393e9";

    /// <summary>A sell stop-loss-limit that waits for its trigger (every order field of the specification).</summary>
    private const string AwaitingStopLossLimit = """{"orderId":"0b4c9c3a-3d3e-4f75-9a62-6a3a5b0d7c01","clientOrderId":"2be7d0df-d8dc-7b93-a550-8876f3b393e9","market":"ETH-EUR","created":1714132800000,"updated":1714132800000,"status":"awaitingTrigger","side":"sell","orderType":"stopLossLimit","amount":"0.5","amountRemaining":"0.5","price":"1490","onHold":"0.5","onHoldCurrency":"ETH","triggerPrice":"1500","triggerAmount":"1500","triggerType":"price","triggerReference":"lastTrade","filledAmount":"0","filledAmountQuote":"0","feePaid":"0","feeCurrency":"EUR","fills":[],"selfTradePrevention":"decrementAndCancel","visible":true,"timeInForce":"GTC","postOnly":false,"disableMarketProtection":false,"operatorId":7,"createdNs":1714132800000000000,"updatedNs":1714132800000000000}""";

    /// <summary>The same order after its trigger fired: Bitvavo keeps the order id and moves awaitingTrigger to new.</summary>
    private const string TriggeredStopLossLimit = """{"orderId":"0b4c9c3a-3d3e-4f75-9a62-6a3a5b0d7c01","market":"ETH-EUR","created":1714132800000,"updated":1714132900000,"status":"new","side":"sell","orderType":"stopLossLimit","amount":"0.5","amountRemaining":"0.5","price":"1490","triggerPrice":"1500","triggerAmount":"1500","triggerType":"price","triggerReference":"lastTrade","filledAmount":"0","filledAmountQuote":"0","feePaid":"0","feeCurrency":"EUR","fills":[],"timeInForce":"GTC","postOnly":false}""";

    /// <summary>A sell take-profit (market) that triggered and was filled in one fill.</summary>
    private const string FilledTakeProfit = """{"orderId":"0b4c9c3a-3d3e-4f75-9a62-6a3a5b0d7c01","market":"ETH-EUR","created":1714132800000,"updated":1714136400000,"status":"filled","side":"sell","orderType":"takeProfit","amount":"0.5","amountRemaining":"0","triggerPrice":"2000","triggerAmount":"2000","triggerType":"price","triggerReference":"bestBid","filledAmount":"0.5","filledAmountQuote":"1000","feePaid":"1.5","feeCurrency":"EUR","fills":[{"id":"371c6bd3-d06d-4573-9f15-18697cd210e5","timestamp":1714136400000,"amount":"0.5","price":"2000","taker":true,"fee":"1.5","feeCurrency":"EUR","settled":true}],"selfTradePrevention":"decrementAndCancel","visible":false,"postOnly":false}""";

    /// <summary>A trigger order the user canceled before it triggered.</summary>
    private const string CanceledStopLoss = """{"orderId":"0b4c9c3a-3d3e-4f75-9a62-6a3a5b0d7c01","market":"ETH-EUR","created":1714132800000,"updated":1714133000000,"status":"canceled","side":"sell","orderType":"stopLoss","amount":"0.5","amountRemaining":"0.5","triggerPrice":"1500","triggerAmount":"1500","triggerType":"price","triggerReference":"lastTrade","filledAmount":"0","filledAmountQuote":"0","fills":[],"postOnly":false}""";

    /// <summary>An order that reports a plain type but still carries its trigger data (what Bitvavo may report once it fired).</summary>
    private const string TriggeredReportedAsLimit = """{"orderId":"0b4c9c3a-3d3e-4f75-9a62-6a3a5b0d7c01","market":"ETH-EUR","created":1714132800000,"updated":1714132900000,"status":"new","side":"sell","orderType":"limit","amount":"0.5","amountRemaining":"0.5","price":"1490","triggerPrice":"1500","triggerAmount":"1500","triggerType":"price","triggerReference":"lastTrade","filledAmount":"0","filledAmountQuote":"0","fills":[],"timeInForce":"GTC","postOnly":false}""";

    /// <summary>An ordinary resting limit order: no trigger type and no trigger data.</summary>
    private const string PlainLimitOrder = """{"orderId":"0b4c9c3a-3d3e-4f75-9a62-6a3a5b0d7c01","market":"ETH-EUR","created":1714132800000,"updated":1714132800000,"status":"new","side":"buy","orderType":"limit","amount":"1","amountRemaining":"1","price":"1000","filledAmount":"0","filledAmountQuote":"0","feePaid":"0","feeCurrency":"EUR","fills":[],"timeInForce":"GTC","postOnly":false}""";

    /// <summary>DELETE /order answers with the order id, the client order id and the operator.</summary>
    private const string CanceledOrderId = """{"orderId":"0b4c9c3a-3d3e-4f75-9a62-6a3a5b0d7c01","clientOrderId":"2be7d0df-d8dc-7b93-a550-8876f3b393e9","operatorId":7}""";

    private static SharedSymbol EthEur => new(TradingMode.Spot, "ETH", "EUR");

    private static ExchangeParameters Operator(long id = 7)
        => new(new ExchangeParameter(BitvavoExchange.ExchangeName, BitvavoSharedParameters.OperatorId, id));

    private static ExchangeParameters OperatorAndTriggerReference(object triggerReference)
        => new(
            new ExchangeParameter(BitvavoExchange.ExchangeName, BitvavoSharedParameters.OperatorId, 7L),
            new ExchangeParameter(BitvavoExchange.ExchangeName, BitvavoSharedMappingExtensions.TriggerReferenceParameter, triggerReference));

    private static PlaceSpotTriggerOrderRequest SellStopLoss(SharedQuantity? quantity = null, decimal? limitPrice = null, ExchangeParameters? parameters = null)
        => new(EthEur, SharedTriggerPriceDirection.PriceBelow, 1500m, SharedOrderSide.Sell, quantity ?? SharedQuantity.Base(0.5m), limitPrice, parameters ?? Operator());

    private static async Task<string> BodyOf(HttpRequestMessage request)
        => await request.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);

    private static void ShouldBeSigned(HttpRequestMessage request)
    {
        request.Headers.GetValues("Bitvavo-Access-Key").ShouldHaveSingleItem().ShouldBe("test-key");
        request.Headers.GetValues("Bitvavo-Access-Signature").ShouldHaveSingleItem().Length.ShouldBe(64);
    }

    // ── place ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_sell_stop_loss_without_a_limit_price_posts_a_signed_stopLoss_triggered_by_the_price_of_the_last_trade()
    {
        var handler = new StubHttpMessageHandler(AwaitingStopLossLimit);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.PlaceSpotTriggerOrderAsync(SellStopLoss(), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Id.ShouldBe(TriggerOrderId);
        var request = handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Post);
        request.RequestUri!.PathAndQuery.ShouldBe("/v2/order");
        (await BodyOf(request)).ShouldBe("""{"amount":"0.5","market":"ETH-EUR","operatorId":7,"orderType":"stopLoss","side":"sell","triggerAmount":"1500","triggerReference":"lastTrade","triggerType":"price"}""");
        ShouldBeSigned(request);
    }

    [Fact]
    public async Task A_stop_loss_with_a_limit_price_posts_stopLossLimit_with_price_time_in_force_and_client_order_id()
    {
        var handler = new StubHttpMessageHandler(AwaitingStopLossLimit);
        var api = handler.RestClient().SpotApi.SharedApi;
        var order = SellStopLoss(limitPrice: 1490m);
        order.ClientOrderId = TriggerClientOrderId;
        order.TimeInForce = SharedTimeInForce.GoodTillCanceled;

        var result = await api.PlaceSpotTriggerOrderAsync(order, TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        var request = handler.Requests.ShouldHaveSingleItem();
        (await BodyOf(request)).ShouldBe("""{"amount":"0.5","clientOrderId":"2be7d0df-d8dc-7b93-a550-8876f3b393e9","market":"ETH-EUR","operatorId":7,"orderType":"stopLossLimit","price":"1490","side":"sell","timeInForce":"GTC","triggerAmount":"1500","triggerReference":"lastTrade","triggerType":"price"}""");
    }

    [Fact]
    public async Task A_sell_that_triggers_on_a_rising_price_posts_takeProfit_and_with_a_limit_price_takeProfitLimit()
    {
        var handler = new StubHttpMessageHandler(AwaitingStopLossLimit);
        var api = handler.RestClient().SpotApi.SharedApi;
        var market = new PlaceSpotTriggerOrderRequest(EthEur, SharedTriggerPriceDirection.PriceAbove, 2000m, SharedOrderSide.Sell, SharedQuantity.Base(0.5m), null, Operator());
        var limit = new PlaceSpotTriggerOrderRequest(EthEur, SharedTriggerPriceDirection.PriceAbove, 2000m, SharedOrderSide.Sell, SharedQuantity.Base(0.5m), 1995m, Operator());

        await api.PlaceSpotTriggerOrderAsync(market, TestContext.Current.CancellationToken);
        await api.PlaceSpotTriggerOrderAsync(limit, TestContext.Current.CancellationToken);

        handler.Requests.Count.ShouldBe(2);
        (await BodyOf(handler.Requests[0])).ShouldBe("""{"amount":"0.5","market":"ETH-EUR","operatorId":7,"orderType":"takeProfit","side":"sell","triggerAmount":"2000","triggerReference":"lastTrade","triggerType":"price"}""");
        (await BodyOf(handler.Requests[1])).ShouldBe("""{"amount":"0.5","market":"ETH-EUR","operatorId":7,"orderType":"takeProfitLimit","price":"1995","side":"sell","triggerAmount":"2000","triggerReference":"lastTrade","triggerType":"price"}""");
    }

    /// <summary>Worse means lower for a sell and higher for a buy (stop loss); better means higher for a sell and lower for a buy (take profit).</summary>
    [Theory]
    [InlineData(SharedOrderSide.Sell, SharedTriggerPriceDirection.PriceBelow, false, "sell", "stopLoss")]
    [InlineData(SharedOrderSide.Sell, SharedTriggerPriceDirection.PriceBelow, true, "sell", "stopLossLimit")]
    [InlineData(SharedOrderSide.Sell, SharedTriggerPriceDirection.PriceAbove, false, "sell", "takeProfit")]
    [InlineData(SharedOrderSide.Sell, SharedTriggerPriceDirection.PriceAbove, true, "sell", "takeProfitLimit")]
    [InlineData(SharedOrderSide.Buy, SharedTriggerPriceDirection.PriceAbove, false, "buy", "stopLoss")]
    [InlineData(SharedOrderSide.Buy, SharedTriggerPriceDirection.PriceAbove, true, "buy", "stopLossLimit")]
    [InlineData(SharedOrderSide.Buy, SharedTriggerPriceDirection.PriceBelow, false, "buy", "takeProfit")]
    [InlineData(SharedOrderSide.Buy, SharedTriggerPriceDirection.PriceBelow, true, "buy", "takeProfitLimit")]
    public async Task The_order_side_price_direction_and_limit_price_pick_the_order_type_on_the_wire(SharedOrderSide side, SharedTriggerPriceDirection direction, bool limit, string wireSide, string wireType)
    {
        var handler = new StubHttpMessageHandler(AwaitingStopLossLimit);
        var api = handler.RestClient().SpotApi.SharedApi;
        var order = new PlaceSpotTriggerOrderRequest(EthEur, direction, 1500m, side, SharedQuantity.Base(0.5m), limit ? 1490m : (decimal?)null, Operator());

        await api.PlaceSpotTriggerOrderAsync(order, TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(await BodyOf(handler.Requests.ShouldHaveSingleItem()));
        body.RootElement.GetProperty("side").GetString().ShouldBe(wireSide);
        body.RootElement.GetProperty("orderType").GetString().ShouldBe(wireType);
    }

    [Theory]
    [InlineData(TriggerReference.LastTrade, "lastTrade")]
    [InlineData(TriggerReference.BestBid, "bestBid")]
    [InlineData(TriggerReference.BestAsk, "bestAsk")]
    [InlineData(TriggerReference.MidPrice, "midPrice")]
    public async Task The_trigger_reference_exchange_parameter_selects_the_price_that_triggers_the_order(TriggerReference reference, string wire)
    {
        var handler = new StubHttpMessageHandler(AwaitingStopLossLimit);
        var api = handler.RestClient().SpotApi.SharedApi;

        await api.PlaceSpotTriggerOrderAsync(SellStopLoss(parameters: OperatorAndTriggerReference(reference)), TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(await BodyOf(handler.Requests.ShouldHaveSingleItem()));
        body.RootElement.GetProperty("triggerReference").GetString().ShouldBe(wire);
        body.RootElement.GetProperty("triggerType").GetString().ShouldBe("price");
    }

    [Fact]
    public async Task A_market_trigger_in_the_quote_asset_sends_amountQuote_and_no_amount()
    {
        var handler = new StubHttpMessageHandler(AwaitingStopLossLimit);
        var api = handler.RestClient().SpotApi.SharedApi;
        var order = new PlaceSpotTriggerOrderRequest(EthEur, SharedTriggerPriceDirection.PriceAbove, 2000m, SharedOrderSide.Buy, SharedQuantity.Quote(100m), null, Operator());

        var result = await api.PlaceSpotTriggerOrderAsync(order, TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        (await BodyOf(handler.Requests.ShouldHaveSingleItem())).ShouldBe("""{"amountQuote":"100","market":"ETH-EUR","operatorId":7,"orderType":"stopLoss","side":"buy","triggerAmount":"2000","triggerReference":"lastTrade","triggerType":"price"}""");
    }

    [Fact]
    public async Task A_market_trigger_sends_no_time_in_force_and_a_limit_trigger_does()
    {
        var handler = new StubHttpMessageHandler(AwaitingStopLossLimit);
        var api = handler.RestClient().SpotApi.SharedApi;
        var market = SellStopLoss();
        market.TimeInForce = SharedTimeInForce.ImmediateOrCancel;
        var limit = SellStopLoss(limitPrice: 1490m);
        limit.TimeInForce = SharedTimeInForce.ImmediateOrCancel;

        await api.PlaceSpotTriggerOrderAsync(market, TestContext.Current.CancellationToken);
        await api.PlaceSpotTriggerOrderAsync(limit, TestContext.Current.CancellationToken);

        (await BodyOf(handler.Requests[0])).ShouldNotContain("timeInForce");
        (await BodyOf(handler.Requests[1])).ShouldContain("\"timeInForce\":\"IOC\"");
    }

    [Fact]
    public async Task Placing_a_trigger_order_without_an_operator_id_is_rejected_before_anything_is_sent()
    {
        var handler = new StubHttpMessageHandler(AwaitingStopLossLimit);
        var api = handler.RestClient().SpotApi.SharedApi;
        var order = new PlaceSpotTriggerOrderRequest(EthEur, SharedTriggerPriceDirection.PriceBelow, 1500m, SharedOrderSide.Sell, SharedQuantity.Base(0.5m));

        var result = await api.PlaceSpotTriggerOrderAsync(order, TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        var error = result.Error.ShouldBeOfType<ArgumentError>();
        error.ErrorType.ShouldBe(ErrorType.InvalidParameter);
        error.Message.ShouldNotBeNull().ShouldContain("OperatorId");
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Placing_a_trigger_order_without_credentials_is_a_NoApiCredentialsError_and_sends_nothing()
    {
        var handler = new StubHttpMessageHandler(AwaitingStopLossLimit);
        var api = handler.RestClient(withCredentials: false).SpotApi.SharedApi;

        var result = await api.PlaceSpotTriggerOrderAsync(SellStopLoss(), TestContext.Current.CancellationToken);

        result.Error.ShouldBeOfType<NoApiCredentialsError>();
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Placing_a_trigger_order_on_a_symbol_that_is_not_spot_is_rejected_before_anything_is_sent()
    {
        var handler = new StubHttpMessageHandler(AwaitingStopLossLimit);
        var api = handler.RestClient().SpotApi.SharedApi;
        var perpetual = new SharedSymbol(TradingMode.PerpetualLinear, "ETH", "EUR");
        var order = new PlaceSpotTriggerOrderRequest(perpetual, SharedTriggerPriceDirection.PriceBelow, 1500m, SharedOrderSide.Sell, SharedQuantity.Base(0.5m), null, Operator());

        var result = await api.PlaceSpotTriggerOrderAsync(order, TestContext.Current.CancellationToken);

        result.Error.ShouldBeOfType<ArgumentError>();
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_trigger_reference_of_the_wrong_type_is_an_argument_error_and_sends_nothing()
    {
        var handler = new StubHttpMessageHandler(AwaitingStopLossLimit);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.PlaceSpotTriggerOrderAsync(SellStopLoss(parameters: OperatorAndTriggerReference("bestBid")), TestContext.Current.CancellationToken);

        result.Error.ShouldBeOfType<ArgumentError>().Message.ShouldNotBeNull().ShouldContain("TriggerReference");
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_quantity_in_both_assets_a_quantity_in_neither_and_a_limit_trigger_in_the_quote_asset_are_rejected_before_anything_is_sent()
    {
        var handler = new StubHttpMessageHandler(AwaitingStopLossLimit);
        var api = handler.RestClient().SpotApi.SharedApi;
        var both = new SharedQuantity { QuantityInBaseAsset = 1m, QuantityInQuoteAsset = 100m };

        var bothAssets = await api.PlaceSpotTriggerOrderAsync(SellStopLoss(both), TestContext.Current.CancellationToken);
        var neitherAsset = await api.PlaceSpotTriggerOrderAsync(SellStopLoss(SharedQuantity.Contracts(1m)), TestContext.Current.CancellationToken);
        var limitInQuote = await api.PlaceSpotTriggerOrderAsync(SellStopLoss(SharedQuantity.Quote(100m), 1490m), TestContext.Current.CancellationToken);

        bothAssets.Error.ShouldBeOfType<ArgumentError>().ErrorType.ShouldBe(ErrorType.InvalidParameter);
        neitherAsset.Error.ShouldBeOfType<ArgumentError>().ErrorType.ShouldBe(ErrorType.MissingParameter);
        limitInQuote.Error.ShouldBeOfType<ArgumentError>().ErrorType.ShouldBe(ErrorType.InvalidParameter);
        handler.Requests.ShouldBeEmpty();
    }

    /// <summary>216: insufficient balance; 238: the required parameters of a stopLossLimit are missing.</summary>
    [Theory]
    [InlineData(216, ErrorType.InsufficientBalance)]
    [InlineData(238, ErrorType.InvalidStopParameters)]
    public async Task The_server_refusing_a_trigger_order_reaches_the_caller_with_its_code_and_mapped_type(int code, ErrorType expected)
    {
        var handler = new StubHttpMessageHandler($$"""{"errorCode":{{code}},"error":"server message"}""", HttpStatusCode.BadRequest);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.PlaceSpotTriggerOrderAsync(SellStopLoss(), TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        var error = result.Error.ShouldBeOfType<ServerError>();
        error.ErrorType.ShouldBe(expected);
        error.Code.ShouldBe(code);
    }

    // ── get ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_untriggered_trigger_order_is_read_with_a_signed_GET_of_market_and_order_id_and_needs_no_operator_id()
    {
        var handler = new StubHttpMessageHandler(AwaitingStopLossLimit);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.GetSpotTriggerOrderAsync(new GetOrderRequest(EthEur, TriggerOrderId), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        var request = handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Get);
        request.RequestUri!.PathAndQuery.ShouldBe("/v2/order?market=ETH-EUR&orderId=0b4c9c3a-3d3e-4f75-9a62-6a3a5b0d7c01");
        request.Content.ShouldBeNull();
        ShouldBeSigned(request);

        var order = result.Data;
        order.TriggerOrderId.ShouldBe(TriggerOrderId);
        order.Symbol.ShouldBe("ETH-EUR");
        order.SharedSymbol.ShouldBe(EthEur);
        order.ClientOrderId.ShouldBe(TriggerClientOrderId);
        order.Status.ShouldBe(SharedTriggerOrderStatus.Active);
        order.PlacedOrderId.ShouldBeNull();
        order.OrderType.ShouldBe(SharedOrderType.Limit);
        order.OrderDirection.ShouldBe(SharedTriggerOrderDirection.Exit);
        order.TriggerPrice.ShouldBe(1500m);
        order.OrderPrice.ShouldBe(1490m);
        order.TimeInForce.ShouldBe(SharedTimeInForce.GoodTillCanceled);
        order.AveragePrice.ShouldBeNull();
        order.Fee.ShouldBe(0m);
        order.FeeAsset.ShouldBe("EUR");
        order.CreateTime.ShouldBe(new DateTime(2024, 4, 26, 12, 0, 0, DateTimeKind.Utc));
        order.UpdateTime.ShouldBe(new DateTime(2024, 4, 26, 12, 0, 0, DateTimeKind.Utc));
        var quantity = order.OrderQuantity.ShouldNotBeNull();
        quantity.QuantityInBaseAsset.ShouldBe(0.5m);
        var filled = order.QuantityFilled.ShouldNotBeNull();
        filled.QuantityInBaseAsset.ShouldBe(0m);
    }

    [Fact]
    public async Task A_trigger_order_that_fired_is_Triggered_and_names_its_own_id_as_the_placed_order()
    {
        var api = new StubHttpMessageHandler(TriggeredStopLossLimit).RestClient().SpotApi.SharedApi;

        var result = await api.GetSpotTriggerOrderAsync(new GetOrderRequest(EthEur, TriggerOrderId), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Status.ShouldBe(SharedTriggerOrderStatus.Triggered);
        result.Data.PlacedOrderId.ShouldBe(TriggerOrderId);
    }

    [Fact]
    public async Task A_filled_take_profit_reports_its_fill_average_price_and_fee()
    {
        var api = new StubHttpMessageHandler(FilledTakeProfit).RestClient().SpotApi.SharedApi;

        var result = await api.GetSpotTriggerOrderAsync(new GetOrderRequest(EthEur, TriggerOrderId), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        var order = result.Data;
        order.Status.ShouldBe(SharedTriggerOrderStatus.Filled);
        order.OrderType.ShouldBe(SharedOrderType.Market);
        order.OrderPrice.ShouldBeNull();
        order.TriggerPrice.ShouldBe(2000m);
        order.AveragePrice.ShouldBe(2000m);
        order.Fee.ShouldBe(1.5m);
        order.FeeAsset.ShouldBe("EUR");
        var filled = order.QuantityFilled.ShouldNotBeNull();
        filled.QuantityInBaseAsset.ShouldBe(0.5m);
        filled.QuantityInQuoteAsset.ShouldBe(1000m);
    }

    [Fact]
    public async Task A_canceled_trigger_order_is_CanceledOrRejected_and_has_no_placed_order()
    {
        var api = new StubHttpMessageHandler(CanceledStopLoss).RestClient().SpotApi.SharedApi;

        var result = await api.GetSpotTriggerOrderAsync(new GetOrderRequest(EthEur, TriggerOrderId), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Status.ShouldBe(SharedTriggerOrderStatus.CanceledOrRejected);
        result.Data.PlacedOrderId.ShouldBeNull();
        result.Data.OrderType.ShouldBe(SharedOrderType.Market);
    }

    [Fact]
    public async Task An_order_that_reports_a_plain_type_but_still_carries_its_trigger_data_is_read_as_a_trigger_order()
    {
        var api = new StubHttpMessageHandler(TriggeredReportedAsLimit).RestClient().SpotApi.SharedApi;

        var result = await api.GetSpotTriggerOrderAsync(new GetOrderRequest(EthEur, TriggerOrderId), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.OrderType.ShouldBe(SharedOrderType.Limit);
        result.Data.TriggerPrice.ShouldBe(1500m);
        result.Data.Status.ShouldBe(SharedTriggerOrderStatus.Triggered);
    }

    [Fact]
    public async Task An_ordinary_order_is_not_a_trigger_order_and_is_reported_as_an_UnknownOrder_error()
    {
        var handler = new StubHttpMessageHandler(PlainLimitOrder);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.GetSpotTriggerOrderAsync(new GetOrderRequest(EthEur, TriggerOrderId), TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        var error = result.Error.ShouldBeOfType<ServerError>();
        error.ErrorType.ShouldBe(ErrorType.UnknownOrder);
        error.ErrorDescription.ShouldNotBeNull().ShouldContain("not a trigger order");
        error.ToString().ShouldContain("not a trigger order");
        handler.Requests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task An_unknown_order_id_is_an_UnknownOrder_error_with_the_servers_code()
    {
        var handler = new StubHttpMessageHandler("""{"errorCode":240,"error":"The order does not exist or is no longer active."}""", HttpStatusCode.NotFound);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.GetSpotTriggerOrderAsync(new GetOrderRequest(EthEur, TriggerOrderId), TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        var error = result.Error.ShouldBeOfType<ServerError>();
        error.ErrorType.ShouldBe(ErrorType.UnknownOrder);
        error.Code.ShouldBe(240);
    }

    [Fact]
    public async Task Reading_a_trigger_order_without_credentials_is_a_NoApiCredentialsError_and_sends_nothing()
    {
        var handler = new StubHttpMessageHandler(AwaitingStopLossLimit);
        var api = handler.RestClient(withCredentials: false).SpotApi.SharedApi;

        var result = await api.GetSpotTriggerOrderAsync(new GetOrderRequest(EthEur, TriggerOrderId), TestContext.Current.CancellationToken);

        result.Error.ShouldBeOfType<NoApiCredentialsError>();
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Reading_a_trigger_order_without_an_order_id_is_rejected_before_anything_is_sent()
    {
        var handler = new StubHttpMessageHandler(AwaitingStopLossLimit);
        var api = handler.RestClient().SpotApi.SharedApi;
        var request = new GetOrderRequest(EthEur, TriggerOrderId) { OrderId = null! };

        var result = await api.GetSpotTriggerOrderAsync(request, TestContext.Current.CancellationToken);

        result.Error.ShouldBeOfType<ArgumentError>().Message.ShouldNotBeNull().ShouldContain("OrderId");
        handler.Requests.ShouldBeEmpty();
    }

    // ── cancel ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Canceling_a_trigger_order_sends_a_signed_DELETE_with_market_operator_and_order_id_in_the_query()
    {
        var handler = new StubHttpMessageHandler(CanceledOrderId);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.CancelSpotTriggerOrderAsync(new CancelOrderRequest(EthEur, TriggerOrderId, Operator()), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Id.ShouldBe(TriggerOrderId);
        var request = handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Delete);
        request.RequestUri!.PathAndQuery.ShouldBe("/v2/order?market=ETH-EUR&operatorId=7&orderId=0b4c9c3a-3d3e-4f75-9a62-6a3a5b0d7c01");
        request.Content.ShouldBeNull();
        ShouldBeSigned(request);
    }

    [Fact]
    public async Task Canceling_a_trigger_order_without_an_operator_id_is_rejected_before_anything_is_sent()
    {
        var handler = new StubHttpMessageHandler(CanceledOrderId);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.CancelSpotTriggerOrderAsync(new CancelOrderRequest(EthEur, TriggerOrderId), TestContext.Current.CancellationToken);

        result.Error.ShouldBeOfType<ArgumentError>().Message.ShouldNotBeNull().ShouldContain("OperatorId");
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Canceling_a_trigger_order_without_credentials_is_a_NoApiCredentialsError_and_sends_nothing()
    {
        var handler = new StubHttpMessageHandler(CanceledOrderId);
        var api = handler.RestClient(withCredentials: false).SpotApi.SharedApi;

        var result = await api.CancelSpotTriggerOrderAsync(new CancelOrderRequest(EthEur, TriggerOrderId, Operator()), TestContext.Current.CancellationToken);

        result.Error.ShouldBeOfType<NoApiCredentialsError>();
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Canceling_a_trigger_order_without_an_order_id_is_rejected_before_anything_is_sent()
    {
        var handler = new StubHttpMessageHandler(CanceledOrderId);
        var api = handler.RestClient().SpotApi.SharedApi;
        var request = new CancelOrderRequest(EthEur, TriggerOrderId, Operator()) { OrderId = null! };

        var result = await api.CancelSpotTriggerOrderAsync(request, TestContext.Current.CancellationToken);

        result.Error.ShouldBeOfType<ArgumentError>().Message.ShouldNotBeNull().ShouldContain("OrderId");
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Canceling_an_order_that_is_filled_canceled_or_unknown_is_an_UnknownOrder_error()
    {
        var handler = new StubHttpMessageHandler("""{"errorCode":240,"error":"The order does not exist or is no longer active."}""", HttpStatusCode.NotFound);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.CancelSpotTriggerOrderAsync(new CancelOrderRequest(EthEur, TriggerOrderId, Operator()), TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        var error = result.Error.ShouldBeOfType<ServerError>();
        error.ErrorType.ShouldBe(ErrorType.UnknownOrder);
        error.Code.ShouldBe(240);
    }

    // ── the transport-agnostic interfaces ────────────────────────────────────────────────

    [Fact]
    public async Task The_transport_agnostic_interfaces_reach_the_same_calls()
    {
        var handler = new StubHttpMessageHandler(request => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(request.Method == HttpMethod.Delete ? CanceledOrderId : AwaitingStopLossLimit, System.Text.Encoding.UTF8, "application/json"),
        });
        var api = handler.RestClient().SpotApi.SharedApi;
        IPlaceSpotTriggerOrder place = api;
        IGetSpotTriggerOrder get = api;
        ICancelSpotTriggerOrder cancel = api;
        var ct = TestContext.Current.CancellationToken;

        var placed = await place.PlaceSpotTriggerOrderAsync(SellStopLoss(), ct);
        var read = await get.GetSpotTriggerOrderAsync(new GetOrderRequest(EthEur, TriggerOrderId), ct);
        var canceled = await cancel.CancelSpotTriggerOrderAsync(new CancelOrderRequest(EthEur, TriggerOrderId, Operator()), ct);

        placed.Success.ShouldBeTrue();
        read.Success.ShouldBeTrue();
        canceled.Success.ShouldBeTrue();
        placed.Data.Id.ShouldBe(TriggerOrderId);
        read.Data.Status.ShouldBe(SharedTriggerOrderStatus.Active);
        canceled.Data.Id.ShouldBe(TriggerOrderId);
        handler.Requests.Select(x => x.Method.Method + " " + x.RequestUri!.AbsolutePath).ToArray()
            .ShouldBe(new[] { "POST /v2/order", "GET /v2/order", "DELETE /v2/order" });
    }

    // ── the options ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Placing_a_trigger_order_needs_credentials_the_operator_id_and_optionally_a_trigger_reference_and_holds_the_funds()
    {
        var options = new StubHttpMessageHandler("[]").RestClient().SpotApi.SharedApi.PlaceSpotTriggerOrderOptions;

        options.NeedsAuthentication.ShouldBeTrue();
        options.HoldsFunds.ShouldBeTrue();
        options.ExchangeParameterRules.Select(x => x.Name).ToArray().ShouldBe(new[] { "OperatorId", "TriggerReference" });
        options.ExchangeParameterRules.Single(x => x.Name == "OperatorId").Requirement.ShouldBe(ExchangeParameterRequirement.Required);
        options.ExchangeParameterRules.Single(x => x.Name == "TriggerReference").Requirement.ShouldBe(ExchangeParameterRequirement.Optional);
    }

    [Fact]
    public void The_place_options_state_the_order_type_table_and_what_the_limit_price_and_the_time_in_force_do()
    {
        var options = new StubHttpMessageHandler("[]").RestClient().SpotApi.SharedApi.PlaceSpotTriggerOrderOptions;

        var notes = options.RequestNotes.ShouldNotBeNull();
        notes.ShouldContain("stopLoss");
        notes.ShouldContain("takeProfit");
        notes.ShouldContain("stopLossLimit");
        notes.ShouldContain("takeProfitLimit");
        notes.ShouldContain("awaitingTrigger");
        notes.ShouldContain("TriggerReference");
        options.RequestParameterRules.Single(x => x.Name == "PriceDirection").Description.ShouldContain("stopLoss");
        options.RequestParameterRules.Single(x => x.Name == "OrderPrice").Description.ShouldContain("takeProfitLimit");
        options.RequestParameterRules.Single(x => x.Name == "TimeInForce").Description.ShouldContain("limit price");
        options.RequestParameterRules.Single(x => x.Name == "Quantity").Description.ShouldContain("amountQuote");
    }

    [Fact]
    public void Reading_a_trigger_order_needs_credentials_but_no_exchange_parameter()
    {
        var options = new StubHttpMessageHandler("[]").RestClient().SpotApi.SharedApi.GetSpotTriggerOrderOptions;

        options.NeedsAuthentication.ShouldBeTrue();
        options.ExchangeParameterRules.ShouldBeEmpty();
        options.RequestNotes.ShouldNotBeNull().ShouldContain("awaitingTrigger");
    }

    [Fact]
    public void Canceling_a_trigger_order_needs_credentials_and_the_operator_id()
    {
        var options = new StubHttpMessageHandler("[]").RestClient().SpotApi.SharedApi.CancelSpotTriggerOrderOptions;

        options.NeedsAuthentication.ShouldBeTrue();
        var rule = options.ExchangeParameterRules.ShouldHaveSingleItem();
        rule.Name.ShouldBe("OperatorId");
        rule.Requirement.ShouldBe(ExchangeParameterRequirement.Required);
    }
}
