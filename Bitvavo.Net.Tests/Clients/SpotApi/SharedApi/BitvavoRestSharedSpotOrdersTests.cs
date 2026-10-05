// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Bitvavo.Net.Clients.SpotApi;
using Bitvavo.Net.Enums;
using Bitvavo.Net.Interfaces.Clients.SpotApi;
using Bitvavo.Net.Objects.Models.Spot;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Errors;
using CryptoExchange.Net.SharedApis;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests.Clients.SpotApi.SharedApi;

/// <summary>
/// The Shared spot-order capabilities on the REST API, all signed: [V2] place, get (by id and by client order id), open and closed
/// orders, the trades of one order, the user trade history and cancel (by id and by client order id), plus the legacy [V1]
/// <c>ISpotOrderRestClient</c> and <c>ISpotOrderClientIdRestClient</c> on the same instance. Every test runs through the real
/// request pipeline; only the network is the canned-response transport. Payloads have the shape of docs.bitvavo.com (API 2.10.0).
/// </summary>
public class BitvavoRestSharedSpotOrdersTests
{
    private const string ClientOrderId = "11111111-1111-1111-1111-111111111111";

    // Timestamps used below (2024-04-26 UTC): 1714129200000 = 11:00:00, 1714132800000 = 12:00:00, 1714136400000 = 13:00:00.

    private const string Placed = """{"orderId":"abc-123","clientOrderId":"11111111-1111-1111-1111-111111111111","market":"ETH-EUR","created":1714132800000,"updated":1714132800000,"status":"new","side":"buy","orderType":"limit","amount":"0.5","amountRemaining":"0.5","price":"3000","filledAmount":"0","filledAmountQuote":"0","feePaid":"0","fills":[],"selfTradePrevention":"decrementAndCancel","visible":true,"timeInForce":"GTC","postOnly":false,"operatorId":7}""";

    private const string PartiallyFilledPostOnlyBuy = """{"orderId":"ord-1","clientOrderId":"11111111-1111-1111-1111-111111111111","market":"ETH-EUR","created":1714132800000,"updated":1714132900000,"status":"partiallyFilled","side":"buy","orderType":"limit","amount":"0.5","amountRemaining":"0.2","price":"3000","onHold":"600","onHoldCurrency":"EUR","filledAmount":"0.3","filledAmountQuote":"900","feePaid":"1.35","feeCurrency":"EUR","fills":[{"id":"fill-1","timestamp":1714132850000,"amount":"0.3","price":"3000","taker":false,"fee":"1.35","feeCurrency":"EUR","settled":true}],"selfTradePrevention":"decrementAndCancel","visible":true,"timeInForce":"GTC","postOnly":true,"operatorId":7}""";

    private const string QuoteSizedMarketBuy = """{"orderId":"ord-2","market":"ETH-EUR","created":1714132800000,"updated":1714132900000,"status":"filled","side":"buy","orderType":"market","amountQuote":"30","filledAmount":"0.01","filledAmountQuote":"30","feePaid":"0.075","feeCurrency":"EUR","fills":[{"id":"fill-2","timestamp":1714132850000,"amount":"0.01","price":"3000","taker":true,"fee":"0.075","feeCurrency":"EUR","settled":true}],"visible":false}""";

    private const string AwaitingTriggerStopLossLimit = """{"orderId":"ord-3","market":"ETH-EUR","created":1714132800000,"updated":1714132800000,"status":"awaitingTrigger","side":"sell","orderType":"stopLossLimit","amount":"1","price":"2890","triggerAmount":"2900","triggerType":"price","triggerReference":"lastTrade","timeInForce":"GTC"}""";

    private const string UnfilledLimitBuy = """{"orderId":"ord-4","market":"ETH-EUR","created":1714132800000,"updated":1714132800000,"status":"new","side":"buy","orderType":"limit","amount":"1","price":"3000","filledAmount":"0","filledAmountQuote":"0"}""";

    // An order placed weeks before: its fills are embedded in the order answer, one of them not settled yet (no fee, no fee asset).
    private const string OldSoldOrderWithFills = """{"orderId":"ord-old","clientOrderId":"33333333-3333-3333-3333-333333333333","market":"ETH-EUR","created":1699999200000,"updated":1700000000000,"status":"filled","side":"sell","orderType":"market","amount":"1","filledAmount":"1","filledAmountQuote":"3060","feePaid":"3","feeCurrency":"EUR","fills":[{"id":"f-1","timestamp":1699999300000,"amount":"0.4","price":"3000","taker":true,"fee":"3","feeCurrency":"EUR","settled":true},{"id":"f-2","timestamp":1699999400000,"amount":"0.6","price":"3100","taker":false,"settled":false}]}""";

    private const string OpenEthBuy = """{"orderId":"open-1","market":"ETH-EUR","created":1714132800000,"updated":1714132800000,"status":"new","side":"buy","orderType":"limit","amount":"1","price":"2900","timeInForce":"GTC","postOnly":false}""";

    private const string OpenBtcStopLoss = """{"orderId":"open-2","market":"BTC-EUR","created":1714136400000,"updated":1714136400000,"status":"awaitingTrigger","side":"sell","orderType":"stopLoss","amountQuote":"500","triggerAmount":"55000","triggerType":"price","triggerReference":"lastTrade"}""";

    // The order history of one market, newest first. It also lists orders that are still open.
    private const string ClosedFilledBuy = """{"orderId":"cl-3","market":"ETH-EUR","created":1714136400000,"updated":1714136500000,"status":"filled","side":"buy","orderType":"limit","amount":"1","price":"3000","filledAmount":"1","filledAmountQuote":"3000","timeInForce":"GTC","postOnly":false}""";
    private const string ClosedStillOpenSell = """{"orderId":"cl-2","market":"ETH-EUR","created":1714132800000,"updated":1714132800000,"status":"new","side":"sell","orderType":"limit","amount":"1","price":"3500","timeInForce":"GTC","postOnly":false}""";
    private const string ClosedCanceledBuy = """{"orderId":"cl-1","market":"ETH-EUR","created":1714129200000,"updated":1714129300000,"status":"canceled","side":"buy","orderType":"limit","amount":"1","price":"2900","timeInForce":"GTC","postOnly":false}""";
    private const string ClosedHistory = "[" + ClosedFilledBuy + "," + ClosedStillOpenSell + "," + ClosedCanceledBuy + "]";
    private const string ClosedPair = "[" + ClosedFilledBuy + "," + ClosedCanceledBuy + "]";

    // The own trades of one market, newest first (GET /trades).
    private const string FillSell = """{"id":"t-2","orderId":"o-2","clientOrderId":"22222222-2222-2222-2222-222222222222","operatorId":7,"timestamp":1714136400000,"market":"ETH-EUR","side":"sell","amount":"0.2","price":"3100","taker":true,"fee":"1.55","feeCurrency":"EUR","settled":true}""";
    private const string FillBuy = """{"id":"t-1","orderId":"o-1","timestamp":1714132800000,"market":"ETH-EUR","side":"buy","amount":"0.5","price":"3000","taker":false,"fee":"2.25","feeCurrency":"EUR","settled":true}""";
    private const string UserTrades = "[" + FillSell + "," + FillBuy + "]";

    private const string Cancelled = """{"orderId":"ord-1","operatorId":7}""";
    private const string CancelledByClientOrderId = """{"orderId":"ord-1","clientOrderId":"11111111-1111-1111-1111-111111111111","operatorId":7}""";

    private const string UnknownOrderError = """{"errorCode":240,"error":"No order found. Please be aware that simultaneously updating the same order may return this error."}""";
    private const string SignatureError = """{"errorCode":309,"error":"The signature is invalid."}""";

    private static SharedSymbol EthEur => new(TradingMode.Spot, "ETH", "EUR");

    /// <summary>The exchange parameter every order operation needs: the account-scoped operator id.</summary>
    private static ExchangeParameters Operator(long operatorId = 7) => new(new ExchangeParameter("Bitvavo", "OperatorId", operatorId));

    private static DateTime Utc(int hour, int minute = 0) => new(2024, 4, 26, hour, minute, 0, DateTimeKind.Utc);

    private static long Ms(DateTime time) => new DateTimeOffset(time).ToUnixTimeMilliseconds();

    /// <summary>A transport that answers each order endpoint with the payload of that endpoint, for the tests that call several operations on one client.</summary>
    private static StubHttpMessageHandler OrderRouter() => new(request =>
    {
        var route = $"{request.Method.Method} {request.RequestUri!.AbsolutePath}";
        var json = route switch
        {
            "POST /v2/order" => Placed,
            "GET /v2/order" => PartiallyFilledPostOnlyBuy,
            "DELETE /v2/order" => Cancelled,
            "GET /v2/ordersOpen" => "[" + OpenEthBuy + "]",
            "GET /v2/orders" => ClosedPair,
            "GET /v2/trades" => UserTrades,
            _ => "{}",
        };
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    });

    private static string OrderWithStatus(string status)
        => $$"""{"orderId":"ord-1","market":"ETH-EUR","created":1714132800000,"updated":1714132900000,"status":"{{status}}","side":"buy","orderType":"limit","amount":"1","price":"3000"}""";

    /// <summary>Calls every one of the nine order capabilities once with a request that is valid, so a failure can only come from the call itself.</summary>
    private static async Task<IHttpResult[]> RunEveryOrderCapabilityAsync(IBitvavoRestClientSpotSharedApi api)
    {
        var ct = TestContext.Current.CancellationToken;
        var symbol = EthEur;

        return
        [
            await api.PlaceSpotOrderAsync(new PlaceSpotOrderRequest(symbol, SharedOrderSide.Buy, SharedOrderType.Limit, SharedQuantity.Base(1m), 3000m, exchangeParameters: Operator()), ct),
            await api.GetSpotOrderAsync(new GetOrderRequest(symbol, "ord-1"), ct),
            await api.GetSpotOrderByClientOrderIdAsync(new GetOrderRequest(symbol, ClientOrderId), ct),
            await api.GetOpenSpotOrdersAsync(new GetOpenOrdersRequest(), ct),
            await api.GetClosedSpotOrdersAsync(new GetClosedOrdersRequest(symbol), ct: ct),
            await api.GetSpotOrderTradesAsync(new GetOrderTradesRequest(symbol, "ord-1"), ct),
            await api.GetSpotUserTradeHistoryAsync(new GetUserTradesRequest(symbol), ct: ct),
            await api.CancelSpotOrderAsync(new CancelOrderRequest(symbol, "ord-1", Operator()), ct),
            await api.CancelSpotOrderByClientOrderIdAsync(new CancelOrderRequest(symbol, ClientOrderId, Operator()), ct),
        ];
    }

    // ── [V2] place ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_limit_order_is_placed_with_a_signed_POST_and_a_sorted_body_and_returns_the_order_id()
    {
        var handler = new StubHttpMessageHandler(Placed);
        var api = handler.RestClient().SpotApi.SharedApi;
        var request = new PlaceSpotOrderRequest(
            EthEur, SharedOrderSide.Buy, SharedOrderType.Limit, SharedQuantity.Base(0.5m), 3000m, SharedTimeInForce.GoodTillCanceled, ClientOrderId, Operator());

        var result = await api.PlaceSpotOrderAsync(request, TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Id.ShouldBe("abc-123");
        var sent = handler.Requests.ShouldHaveSingleItem();
        sent.Method.ShouldBe(HttpMethod.Post);
        sent.RequestUri!.AbsolutePath.ShouldBe("/v2/order");
        sent.Headers.GetValues("Bitvavo-Access-Signature").ShouldHaveSingleItem().Length.ShouldBe(64);
        var body = await sent.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldBe("""{"amount":"0.5","clientOrderId":"11111111-1111-1111-1111-111111111111","market":"ETH-EUR","operatorId":7,"orderType":"limit","price":"3000","side":"buy","timeInForce":"GTC"}""");
    }

    [Fact]
    public async Task A_post_only_limit_order_is_a_limit_order_with_postOnly_true()
    {
        var handler = new StubHttpMessageHandler(Placed);
        var api = handler.RestClient().SpotApi.SharedApi;
        var request = new PlaceSpotOrderRequest(
            EthEur, SharedOrderSide.Sell, SharedOrderType.LimitMaker, SharedQuantity.Base(0.5m), 3000m, exchangeParameters: Operator());

        await api.PlaceSpotOrderAsync(request, TestContext.Current.CancellationToken);

        var body = await handler.Requests.ShouldHaveSingleItem().Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldBe("""{"amount":"0.5","market":"ETH-EUR","operatorId":7,"orderType":"limit","postOnly":true,"price":"3000","side":"sell"}""");
    }

    [Theory]
    [InlineData(SharedTimeInForce.GoodTillCanceled, "GTC")]
    [InlineData(SharedTimeInForce.ImmediateOrCancel, "IOC")]
    [InlineData(SharedTimeInForce.FillOrKill, "FOK")]
    public async Task Time_in_force_is_sent_in_Bitvavo_notation(SharedTimeInForce timeInForce, string wire)
    {
        var handler = new StubHttpMessageHandler(Placed);
        var api = handler.RestClient().SpotApi.SharedApi;
        var request = new PlaceSpotOrderRequest(
            EthEur, SharedOrderSide.Buy, SharedOrderType.Limit, SharedQuantity.Base(1m), 3000m, timeInForce, exchangeParameters: Operator());

        await api.PlaceSpotOrderAsync(request, TestContext.Current.CancellationToken);

        var body = await handler.Requests.ShouldHaveSingleItem().Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain($"\"timeInForce\":\"{wire}\"");
    }

    [Fact]
    public async Task A_market_buy_sized_in_the_quote_asset_sends_amountQuote_and_no_amount_or_price()
    {
        var handler = new StubHttpMessageHandler(Placed);
        var api = handler.RestClient().SpotApi.SharedApi;
        var request = new PlaceSpotOrderRequest(
            EthEur, SharedOrderSide.Buy, SharedOrderType.Market, SharedQuantity.Quote(25m), exchangeParameters: Operator());

        await api.PlaceSpotOrderAsync(request, TestContext.Current.CancellationToken);

        var body = await handler.Requests.ShouldHaveSingleItem().Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldBe("""{"amountQuote":"25","market":"ETH-EUR","operatorId":7,"orderType":"market","side":"buy"}""");
    }

    [Fact]
    public async Task A_market_sell_sized_in_the_base_asset_sends_amount_and_no_amountQuote()
    {
        var handler = new StubHttpMessageHandler(Placed);
        var api = handler.RestClient().SpotApi.SharedApi;
        var request = new PlaceSpotOrderRequest(
            EthEur, SharedOrderSide.Sell, SharedOrderType.Market, SharedQuantity.Base(0.1m), exchangeParameters: Operator());

        await api.PlaceSpotOrderAsync(request, TestContext.Current.CancellationToken);

        var body = await handler.Requests.ShouldHaveSingleItem().Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldBe("""{"amount":"0.1","market":"ETH-EUR","operatorId":7,"orderType":"market","side":"sell"}""");
    }

    [Fact]
    public async Task A_placement_without_the_OperatorId_exchange_parameter_is_rejected_before_anything_is_sent()
    {
        var handler = new StubHttpMessageHandler(Placed);
        var api = handler.RestClient().SpotApi.SharedApi;
        var request = new PlaceSpotOrderRequest(EthEur, SharedOrderSide.Buy, SharedOrderType.Limit, SharedQuantity.Base(1m), 3000m);

        var result = await api.PlaceSpotOrderAsync(request, TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        var error = result.Error.ShouldBeOfType<ArgumentError>();
        error.Message.ShouldNotBeNull().ShouldContain("OperatorId");
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_default_OperatorId_set_on_the_client_serves_an_order_that_names_none()
    {
        var handler = new StubHttpMessageHandler(Placed);
        var api = handler.RestClient().SpotApi.SharedApi;
        var request = new PlaceSpotOrderRequest(EthEur, SharedOrderSide.Buy, SharedOrderType.Limit, SharedQuantity.Base(1m), 3000m);
        try
        {
            api.SetDefaultExchangeParameter("OperatorId", 42L);

            var result = await api.PlaceSpotOrderAsync(request, TestContext.Current.CancellationToken);

            result.Success.ShouldBeTrue();
            var body = await handler.Requests.ShouldHaveSingleItem().Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);
            body.ShouldContain("\"operatorId\":42");
        }
        finally
        {
            api.ResetDefaultExchangeParameters();
        }
    }

    [Fact]
    public async Task An_order_type_or_time_in_force_the_options_do_not_list_is_rejected_before_anything_is_sent()
    {
        var handler = new StubHttpMessageHandler(Placed);
        var api = handler.RestClient().SpotApi.SharedApi;
        var ct = TestContext.Current.CancellationToken;

        // Stop-loss and take-profit orders have no Shared order type of their own: they surface as Other and are never placed here.
        await Should.ThrowAsync<ArgumentException>(() => api.PlaceSpotOrderAsync(
            new PlaceSpotOrderRequest(EthEur, SharedOrderSide.Buy, SharedOrderType.Other, SharedQuantity.Base(1m), 3000m, exchangeParameters: Operator()), ct));
        var timeInForce = await api.PlaceSpotOrderAsync(
            new PlaceSpotOrderRequest(EthEur, SharedOrderSide.Buy, SharedOrderType.Limit, SharedQuantity.Base(1m), 3000m, (SharedTimeInForce)99, exchangeParameters: Operator()), ct);

        timeInForce.Error.ShouldBeOfType<ArgumentError>();
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_limit_order_sized_in_the_quote_asset_is_rejected_before_anything_is_sent()
    {
        var handler = new StubHttpMessageHandler(Placed);
        var api = handler.RestClient().SpotApi.SharedApi;
        var request = new PlaceSpotOrderRequest(
            EthEur, SharedOrderSide.Buy, SharedOrderType.Limit, SharedQuantity.Quote(10m), 3000m, exchangeParameters: Operator());

        var result = await api.PlaceSpotOrderAsync(request, TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.Error.ShouldBeOfType<ArgumentError>();
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public void The_place_options_describe_what_Bitvavo_supports()
    {
        var api = new StubHttpMessageHandler("{}").RestClient().SpotApi.SharedApi;

        api.SpotFeeDeductionType.ShouldBe(SharedFeeDeductionType.DeductFromOutput);
        api.SpotFeeAssetType.ShouldBe(SharedFeeAssetType.QuoteAsset);
        api.SpotSupportedOrderTypes.ShouldBe(new[] { SharedOrderType.Limit, SharedOrderType.Market, SharedOrderType.LimitMaker }, ignoreOrder: true);
        api.SpotSupportedTimeInForce.ShouldBe(new[] { SharedTimeInForce.GoodTillCanceled, SharedTimeInForce.ImmediateOrCancel, SharedTimeInForce.FillOrKill }, ignoreOrder: true);
        var quantities = api.SpotSupportedOrderQuantity;
        quantities.GetSupportedQuantityType(SharedOrderSide.Buy, SharedOrderType.Limit).ShouldBe(SharedQuantityType.BaseAsset);
        quantities.GetSupportedQuantityType(SharedOrderSide.Sell, SharedOrderType.Limit).ShouldBe(SharedQuantityType.BaseAsset);
        quantities.GetSupportedQuantityType(SharedOrderSide.Buy, SharedOrderType.LimitMaker).ShouldBe(SharedQuantityType.BaseAsset);
        quantities.GetSupportedQuantityType(SharedOrderSide.Buy, SharedOrderType.Market).ShouldBe(SharedQuantityType.BaseAndQuoteAsset);
        quantities.GetSupportedQuantityType(SharedOrderSide.Sell, SharedOrderType.Market).ShouldBe(SharedQuantityType.BaseAndQuoteAsset);
        var rule = api.PlaceSpotOrderOptions.ExchangeParameterRules.ShouldHaveSingleItem();
        rule.Name.ShouldBe("OperatorId");
        rule.Requirement.ShouldBe(ExchangeParameterRequirement.Required);
        rule.ValueType.ShouldBe(typeof(long));
    }

    [Fact]
    public void A_generated_client_order_id_is_a_unique_UUID_as_Bitvavo_requires()
    {
        var api = new StubHttpMessageHandler("{}").RestClient().SpotApi.SharedApi;

        var first = api.GenerateClientOrderId();
        var second = api.GenerateClientOrderId();

        Guid.TryParse(first, out _).ShouldBeTrue();
        Guid.TryParse(second, out _).ShouldBeTrue();
        first.ShouldNotBe(second);
    }

    // ── [V2] get one order ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_post_only_limit_order_is_mapped_field_by_field()
    {
        var api = new StubHttpMessageHandler(PartiallyFilledPostOnlyBuy).RestClient().SpotApi.SharedApi;
        var symbol = EthEur;

        var result = await api.GetSpotOrderAsync(new GetOrderRequest(symbol, "ord-1"), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        var order = result.Data;
        order.OrderId.ShouldBe("ord-1");
        order.ClientOrderId.ShouldBe(ClientOrderId);
        order.Symbol.ShouldBe("ETH-EUR");
        order.SharedSymbol.ShouldBeSameAs(symbol);
        order.OrderType.ShouldBe(SharedOrderType.LimitMaker);
        order.Side.ShouldBe(SharedOrderSide.Buy);
        order.Status.ShouldBe(SharedOrderStatus.Open);
        order.TimeInForce.ShouldBe(SharedTimeInForce.GoodTillCanceled);
        order.CreateTime.ShouldBe(Utc(12));
        order.UpdateTime.ShouldBe(Utc(12, 1).AddSeconds(40));
        order.OrderPrice.ShouldBe(3000m);
        var quantity = order.OrderQuantity.ShouldNotBeNull();
        quantity.QuantityInBaseAsset.ShouldBe(0.5m);
        quantity.QuantityInQuoteAsset.ShouldBeNull();
        var filled = order.QuantityFilled.ShouldNotBeNull();
        filled.QuantityInBaseAsset.ShouldBe(0.3m);
        filled.QuantityInQuoteAsset.ShouldBe(900m);
        order.AveragePrice.ShouldBe(3000m);
        order.IsTriggerOrder.ShouldBeFalse();
        order.TriggerPrice.ShouldBeNull();
    }

    [Fact]
    public async Task The_order_level_fee_is_still_filled_for_the_consumers_that_read_it()
    {
        var api = new StubHttpMessageHandler(PartiallyFilledPostOnlyBuy).RestClient().SpotApi.SharedApi;

        var result = await api.GetSpotOrderAsync(new GetOrderRequest(EthEur, "ord-1"), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
#pragma warning disable CS0618 // SharedSpotOrder.Fee / FeeAsset are obsolete in CryptoExchange.Net 13, but V1 consumers still read them: this pins that they are filled.
        result.Data.Fee.ShouldBe(1.35m);
        result.Data.FeeAsset.ShouldBe("EUR");
#pragma warning restore CS0618
    }

    [Theory]
    [InlineData("new", SharedOrderStatus.Open)]
    [InlineData("awaitingTrigger", SharedOrderStatus.Open)]
    [InlineData("partiallyFilled", SharedOrderStatus.Open)]
    [InlineData("filled", SharedOrderStatus.Filled)]
    [InlineData("canceled", SharedOrderStatus.Canceled)]
    [InlineData("canceledAuction", SharedOrderStatus.Canceled)]
    [InlineData("canceledSelfTradePrevention", SharedOrderStatus.Canceled)]
    [InlineData("canceledIOC", SharedOrderStatus.Canceled)]
    [InlineData("canceledFOK", SharedOrderStatus.Canceled)]
    [InlineData("canceledMarketProtection", SharedOrderStatus.Canceled)]
    [InlineData("canceledPostOnly", SharedOrderStatus.Canceled)]
    [InlineData("expired", SharedOrderStatus.Canceled)]
    [InlineData("rejected", SharedOrderStatus.Canceled)]
    [InlineData("a status Bitvavo adds later", SharedOrderStatus.Unknown)]
    public async Task The_wire_status_maps_to_the_shared_status_and_an_unknown_one_never_fails_the_call(string wire, SharedOrderStatus expected)
    {
        var api = new StubHttpMessageHandler(OrderWithStatus(wire)).RestClient().SpotApi.SharedApi;

        var result = await api.GetSpotOrderAsync(new GetOrderRequest(EthEur, "ord-1"), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Status.ShouldBe(expected);
    }

    [Fact]
    public async Task A_market_order_sized_in_the_quote_asset_has_no_price_and_gets_its_average_price_from_the_fills()
    {
        var api = new StubHttpMessageHandler(QuoteSizedMarketBuy).RestClient().SpotApi.SharedApi;

        var result = await api.GetSpotOrderAsync(new GetOrderRequest(EthEur, "ord-2"), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        var order = result.Data;
        order.OrderType.ShouldBe(SharedOrderType.Market);
        order.Status.ShouldBe(SharedOrderStatus.Filled);
        order.OrderPrice.ShouldBeNull();
        order.TimeInForce.ShouldBeNull();
        var quantity = order.OrderQuantity.ShouldNotBeNull();
        quantity.QuantityInBaseAsset.ShouldBeNull();
        quantity.QuantityInQuoteAsset.ShouldBe(30m);
        var filled = order.QuantityFilled.ShouldNotBeNull();
        filled.QuantityInBaseAsset.ShouldBe(0.01m);
        filled.QuantityInQuoteAsset.ShouldBe(30m);
        order.AveragePrice.ShouldBe(3000m);
    }

    [Fact]
    public async Task A_trigger_order_is_the_other_order_type_and_carries_its_trigger_price()
    {
        var api = new StubHttpMessageHandler(AwaitingTriggerStopLossLimit).RestClient().SpotApi.SharedApi;

        var result = await api.GetSpotOrderAsync(new GetOrderRequest(EthEur, "ord-3"), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        var order = result.Data;
        order.OrderType.ShouldBe(SharedOrderType.Other);
        order.Side.ShouldBe(SharedOrderSide.Sell);
        order.Status.ShouldBe(SharedOrderStatus.Open);
        order.IsTriggerOrder.ShouldBeTrue();
        order.TriggerPrice.ShouldBe(2900m);
        order.OrderPrice.ShouldBe(2890m);
    }

    [Fact]
    public async Task An_order_without_fills_has_no_average_price()
    {
        var api = new StubHttpMessageHandler(UnfilledLimitBuy).RestClient().SpotApi.SharedApi;

        var result = await api.GetSpotOrderAsync(new GetOrderRequest(EthEur, "ord-4"), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.AveragePrice.ShouldBeNull();
        result.Data.TimeInForce.ShouldBeNull();
        result.Data.ClientOrderId.ShouldBeNull();
    }

    [Fact]
    public async Task An_order_is_requested_by_its_id_with_a_signed_GET()
    {
        var handler = new StubHttpMessageHandler(UnfilledLimitBuy);
        var api = handler.RestClient().SpotApi.SharedApi;

        await api.GetSpotOrderAsync(new GetOrderRequest(EthEur, "ord-4"), TestContext.Current.CancellationToken);

        var sent = handler.Requests.ShouldHaveSingleItem();
        sent.Method.ShouldBe(HttpMethod.Get);
        sent.RequestUri!.PathAndQuery.ShouldBe("/v2/order?market=ETH-EUR&orderId=ord-4");
        sent.Headers.GetValues("Bitvavo-Access-Signature").ShouldHaveSingleItem().Length.ShouldBe(64);
    }

    // ── [V2] get one order by client order id ────────────────────────────────────────────

    [Fact]
    public async Task An_order_is_requested_by_its_client_order_id_and_not_by_its_order_id()
    {
        var handler = new StubHttpMessageHandler(PartiallyFilledPostOnlyBuy);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.GetSpotOrderByClientOrderIdAsync(new GetOrderRequest(EthEur, ClientOrderId), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.ClientOrderId.ShouldBe(ClientOrderId);
        result.Data.OrderId.ShouldBe("ord-1");
        var sent = handler.Requests.ShouldHaveSingleItem();
        sent.Method.ShouldBe(HttpMethod.Get);
        sent.RequestUri!.PathAndQuery.ShouldBe("/v2/order?clientOrderId=11111111-1111-1111-1111-111111111111&market=ETH-EUR");
        sent.Headers.GetValues("Bitvavo-Access-Signature").ShouldHaveSingleItem().Length.ShouldBe(64);
    }

    // ── [V2] open orders ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Open_orders_without_a_symbol_list_every_market_and_take_their_symbols_from_the_orders()
    {
        var handler = new StubHttpMessageHandler("[" + OpenEthBuy + "," + OpenBtcStopLoss + "]");
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.GetOpenSpotOrdersAsync(new GetOpenOrdersRequest(), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        handler.Requests.ShouldHaveSingleItem().RequestUri!.PathAndQuery.ShouldBe("/v2/ordersOpen");
        result.Data.Length.ShouldBe(2);
        var eth = result.Data[0];
        eth.OrderId.ShouldBe("open-1");
        eth.Symbol.ShouldBe("ETH-EUR");
        var ethSymbol = eth.SharedSymbol.ShouldNotBeNull();
        ethSymbol.BaseAsset.ShouldBe("ETH");
        ethSymbol.QuoteAsset.ShouldBe("EUR");
        ethSymbol.TradingMode.ShouldBe(TradingMode.Spot);
        eth.OrderType.ShouldBe(SharedOrderType.Limit);
        eth.TimeInForce.ShouldBe(SharedTimeInForce.GoodTillCanceled);
        var btc = result.Data[1];
        btc.Symbol.ShouldBe("BTC-EUR");
        btc.SharedSymbol.ShouldNotBeNull().BaseAsset.ShouldBe("BTC");
        btc.OrderType.ShouldBe(SharedOrderType.Other);
        btc.IsTriggerOrder.ShouldBeTrue();
        btc.TriggerPrice.ShouldBe(55000m);
        btc.TimeInForce.ShouldBeNull();
    }

    [Fact]
    public async Task Open_orders_of_one_symbol_send_the_market_filter_and_sign_the_request()
    {
        var handler = new StubHttpMessageHandler("[" + OpenEthBuy + "]");
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.GetOpenSpotOrdersAsync(new GetOpenOrdersRequest(EthEur), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.ShouldHaveSingleItem().Symbol.ShouldBe("ETH-EUR");
        var sent = handler.Requests.ShouldHaveSingleItem();
        sent.Method.ShouldBe(HttpMethod.Get);
        sent.RequestUri!.PathAndQuery.ShouldBe("/v2/ordersOpen?market=ETH-EUR");
        sent.Headers.GetValues("Bitvavo-Access-Signature").ShouldHaveSingleItem().Length.ShouldBe(64);
    }

    [Fact]
    public void The_open_orders_symbol_is_optional()
    {
        var options = new StubHttpMessageHandler("[]").RestClient().SpotApi.SharedApi.GetOpenSpotOrdersOptions;

        options.RequestParameterRules.Single(x => x.Name == nameof(GetOpenOrdersRequest.Symbol)).Support.ShouldBe(RequestParameterSupport.Optional);
        options.NeedsAuthentication.ShouldBeTrue();
    }

    // ── [V2] closed orders ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Closed_orders_are_requested_with_the_window_and_limit_and_orders_still_open_are_dropped()
    {
        var handler = new StubHttpMessageHandler(ClosedHistory);
        var api = handler.RestClient().SpotApi.SharedApi;
        var start = new DateTime(2024, 4, 26, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2024, 4, 27, 0, 0, 0, DateTimeKind.Utc);

        var result = await api.GetClosedSpotOrdersAsync(new GetClosedOrdersRequest(EthEur, start, end, limit: 5), ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Select(x => x.OrderId).ShouldBe(new[] { "cl-3", "cl-1" });
        result.Data[0].Status.ShouldBe(SharedOrderStatus.Filled);
        result.Data[1].Status.ShouldBe(SharedOrderStatus.Canceled);
        result.Data[0].Symbol.ShouldBe("ETH-EUR");
        var sent = handler.Requests.ShouldHaveSingleItem();
        sent.Method.ShouldBe(HttpMethod.Get);
        sent.RequestUri!.PathAndQuery.ShouldBe($"/v2/orders?end={Ms(end)}&limit=5&market=ETH-EUR&start={Ms(start)}");
        sent.Headers.GetValues("Bitvavo-Access-Signature").ShouldHaveSingleItem().Length.ShouldBe(64);
    }

    [Fact]
    public async Task A_closed_orders_request_without_time_bounds_sends_neither_start_nor_end_and_the_default_limit()
    {
        var handler = new StubHttpMessageHandler(ClosedPair);
        var api = handler.RestClient().SpotApi.SharedApi;

        await api.GetClosedSpotOrdersAsync(new GetClosedOrdersRequest(EthEur), ct: TestContext.Current.CancellationToken);

        handler.Requests.ShouldHaveSingleItem().RequestUri!.PathAndQuery.ShouldBe("/v2/orders?limit=500&market=ETH-EUR");
    }

    [Fact]
    public async Task A_closed_orders_request_with_only_a_start_time_does_not_invent_an_end_time()
    {
        var handler = new StubHttpMessageHandler(ClosedPair);
        var api = handler.RestClient().SpotApi.SharedApi;
        var start = new DateTime(2024, 4, 26, 0, 0, 0, DateTimeKind.Utc);

        await api.GetClosedSpotOrdersAsync(new GetClosedOrdersRequest(EthEur, start), ct: TestContext.Current.CancellationToken);

        handler.Requests.ShouldHaveSingleItem().RequestUri!.PathAndQuery.ShouldBe($"/v2/orders?limit=500&market=ETH-EUR&start={Ms(start)}");
    }

    [Fact]
    public async Task A_full_page_of_closed_orders_returns_the_request_for_the_next_older_page_and_the_token_continues_from_there()
    {
        var handler = new StubHttpMessageHandler(ClosedPair);
        var api = handler.RestClient().SpotApi.SharedApi;
        var ct = TestContext.Current.CancellationToken;

        var first = await api.GetClosedSpotOrdersAsync(new GetClosedOrdersRequest(EthEur, limit: 2), ct: ct);

        var oldestCreated = Utc(11);
        var nextPage = first.NextPageRequest.ShouldNotBeNull();
        nextPage.EndTime.ShouldBe(oldestCreated.AddMilliseconds(-1));
        nextPage.StartTime.ShouldBeNull();

        await api.GetClosedSpotOrdersAsync(new GetClosedOrdersRequest(EthEur, limit: 2), nextPage, ct);

        handler.Requests[1].RequestUri!.PathAndQuery.ShouldBe($"/v2/orders?end={Ms(oldestCreated.AddMilliseconds(-1))}&limit=2&market=ETH-EUR");
    }

    [Fact]
    public async Task A_page_that_held_open_orders_still_counts_as_full_so_the_next_page_is_offered()
    {
        var api = new StubHttpMessageHandler(ClosedHistory).RestClient().SpotApi.SharedApi;

        var result = await api.GetClosedSpotOrdersAsync(new GetClosedOrdersRequest(EthEur, limit: 3), ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Length.ShouldBe(2);
        var nextPage = result.NextPageRequest.ShouldNotBeNull();
        nextPage.EndTime.ShouldBe(Utc(11).AddMilliseconds(-1));
    }

    [Fact]
    public async Task A_short_or_empty_page_of_closed_orders_has_no_next_page()
    {
        var shortPage = await new StubHttpMessageHandler(ClosedPair).RestClient().SpotApi.SharedApi
            .GetClosedSpotOrdersAsync(new GetClosedOrdersRequest(EthEur, limit: 100), ct: TestContext.Current.CancellationToken);
        var emptyPage = await new StubHttpMessageHandler("[]").RestClient().SpotApi.SharedApi
            .GetClosedSpotOrdersAsync(new GetClosedOrdersRequest(EthEur, limit: 2), ct: TestContext.Current.CancellationToken);

        shortPage.Success.ShouldBeTrue();
        shortPage.NextPageRequest.ShouldBeNull();
        emptyPage.Success.ShouldBeTrue();
        emptyPage.Data.ShouldBeEmpty();
        emptyPage.NextPageRequest.ShouldBeNull();
    }

    [Fact]
    public async Task An_ascending_closed_orders_request_is_rejected_before_anything_is_sent()
    {
        var handler = new StubHttpMessageHandler(ClosedPair);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.GetClosedSpotOrdersAsync(new GetClosedOrdersRequest(EthEur, direction: DataDirection.Ascending), ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.Error.ShouldBeOfType<ArgumentError>();
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public void The_closed_orders_options_describe_what_Bitvavo_supports()
    {
        var options = new StubHttpMessageHandler("[]").RestClient().SpotApi.SharedApi.GetClosedSpotOrdersOptions;

        options.SupportsAscending.ShouldBeFalse();
        options.SupportsDescending.ShouldBeTrue();
        options.TimePeriodFilterSupport.ShouldBeTrue();
        options.MaxLimit.ShouldBe(1000);
        options.NeedsAuthentication.ShouldBeTrue();
    }

    // ── [V2] the trades of one order ─────────────────────────────────────────────────────

    [Fact]
    public async Task The_trades_of_an_order_are_the_fills_embedded_in_the_order_even_when_the_order_is_old()
    {
        var handler = new StubHttpMessageHandler(OldSoldOrderWithFills);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.GetSpotOrderTradesAsync(new GetOrderTradesRequest(EthEur, "ord-old"), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        var sent = handler.Requests.ShouldHaveSingleItem();
        sent.Method.ShouldBe(HttpMethod.Get);
        sent.RequestUri!.PathAndQuery.ShouldBe("/v2/order?market=ETH-EUR&orderId=ord-old");
        sent.Headers.GetValues("Bitvavo-Access-Signature").ShouldHaveSingleItem().Length.ShouldBe(64);
        result.Data.Length.ShouldBe(2);

        var settled = result.Data[0];
        settled.Id.ShouldBe("f-1");
        settled.OrderId.ShouldBe("ord-old");
        settled.ClientOrderId.ShouldBe("33333333-3333-3333-3333-333333333333");
        settled.Symbol.ShouldBe("ETH-EUR");
        settled.SharedSymbol.ShouldNotBeNull().BaseAsset.ShouldBe("ETH");
        settled.Side.ShouldBe(SharedOrderSide.Sell);
        settled.Price.ShouldBe(3000m);
        settled.Quantities.QuantityInBaseAsset.ShouldBe(0.4m);
        settled.Quantities.QuantityInQuoteAsset.ShouldBe(1200m);
        settled.Timestamp.ShouldBe(DateTimeOffset.FromUnixTimeMilliseconds(1699999300000).UtcDateTime);
        settled.Fee.ShouldBe(3m);
        settled.FeeAsset.ShouldBe("EUR");
        settled.Role.ShouldBe(SharedRole.Taker);

        var unsettled = result.Data[1];
        unsettled.Id.ShouldBe("f-2");
        unsettled.OrderId.ShouldBe("ord-old");
        unsettled.Fee.ShouldBeNull();
        unsettled.FeeAsset.ShouldBeNull();
        unsettled.Role.ShouldBe(SharedRole.Maker);
    }

    [Fact]
    public async Task An_order_without_fills_has_no_trades()
    {
        var api = new StubHttpMessageHandler(UnfilledLimitBuy).RestClient().SpotApi.SharedApi;

        var result = await api.GetSpotOrderTradesAsync(new GetOrderTradesRequest(EthEur, "ord-4"), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.ShouldBeEmpty();
    }

    // ── [V2] user trade history ──────────────────────────────────────────────────────────

    [Fact]
    public async Task User_trades_are_mapped_with_side_fee_role_order_and_client_order_id()
    {
        var api = new StubHttpMessageHandler(UserTrades).RestClient().SpotApi.SharedApi;

        var result = await api.GetSpotUserTradeHistoryAsync(new GetUserTradesRequest(EthEur), ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Length.ShouldBe(2);
        var sell = result.Data[0];
        sell.Id.ShouldBe("t-2");
        sell.OrderId.ShouldBe("o-2");
        sell.ClientOrderId.ShouldBe("22222222-2222-2222-2222-222222222222");
        sell.Symbol.ShouldBe("ETH-EUR");
        sell.SharedSymbol.ShouldNotBeNull().QuoteAsset.ShouldBe("EUR");
        sell.Side.ShouldBe(SharedOrderSide.Sell);
        sell.Price.ShouldBe(3100m);
        sell.Quantities.QuantityInBaseAsset.ShouldBe(0.2m);
        sell.Quantities.QuantityInQuoteAsset.ShouldBe(620m);
        sell.Timestamp.ShouldBe(Utc(13));
        sell.Fee.ShouldBe(1.55m);
        sell.FeeAsset.ShouldBe("EUR");
        sell.Role.ShouldBe(SharedRole.Taker);
        var buy = result.Data[1];
        buy.Id.ShouldBe("t-1");
        buy.ClientOrderId.ShouldBeNull();
        buy.Side.ShouldBe(SharedOrderSide.Buy);
        buy.Role.ShouldBe(SharedRole.Maker);
        buy.Fee.ShouldBe(2.25m);
    }

    [Fact]
    public async Task User_trades_are_requested_with_the_window_and_limit_with_a_signed_GET()
    {
        var handler = new StubHttpMessageHandler(UserTrades);
        var api = handler.RestClient().SpotApi.SharedApi;
        var start = new DateTime(2024, 4, 26, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2024, 4, 26, 23, 0, 0, DateTimeKind.Utc);

        await api.GetSpotUserTradeHistoryAsync(new GetUserTradesRequest(EthEur, start, end, limit: 5), ct: TestContext.Current.CancellationToken);

        var sent = handler.Requests.ShouldHaveSingleItem();
        sent.Method.ShouldBe(HttpMethod.Get);
        sent.RequestUri!.PathAndQuery.ShouldBe($"/v2/trades?end={Ms(end)}&limit=5&market=ETH-EUR&start={Ms(start)}");
        sent.Headers.GetValues("Bitvavo-Access-Signature").ShouldHaveSingleItem().Length.ShouldBe(64);
    }

    [Fact]
    public async Task A_user_trades_request_without_time_bounds_sends_neither_start_nor_end_and_the_default_limit()
    {
        var handler = new StubHttpMessageHandler(UserTrades);
        var api = handler.RestClient().SpotApi.SharedApi;

        await api.GetSpotUserTradeHistoryAsync(new GetUserTradesRequest(EthEur), ct: TestContext.Current.CancellationToken);

        handler.Requests.ShouldHaveSingleItem().RequestUri!.PathAndQuery.ShouldBe("/v2/trades?limit=500&market=ETH-EUR");
    }

    [Fact]
    public async Task A_full_page_of_user_trades_returns_the_request_for_the_next_older_page_and_the_token_continues_from_there()
    {
        var handler = new StubHttpMessageHandler(UserTrades);
        var api = handler.RestClient().SpotApi.SharedApi;
        var ct = TestContext.Current.CancellationToken;

        var first = await api.GetSpotUserTradeHistoryAsync(new GetUserTradesRequest(EthEur, limit: 2), ct: ct);

        var oldestTrade = Utc(12);
        var nextPage = first.NextPageRequest.ShouldNotBeNull();
        nextPage.EndTime.ShouldBe(oldestTrade.AddMilliseconds(-1));
        nextPage.StartTime.ShouldBeNull();

        await api.GetSpotUserTradeHistoryAsync(new GetUserTradesRequest(EthEur, limit: 2), nextPage, ct);

        handler.Requests[1].RequestUri!.PathAndQuery.ShouldBe($"/v2/trades?end={Ms(oldestTrade.AddMilliseconds(-1))}&limit=2&market=ETH-EUR");
    }

    [Fact]
    public async Task A_short_or_empty_page_of_user_trades_has_no_next_page()
    {
        var shortPage = await new StubHttpMessageHandler(UserTrades).RestClient().SpotApi.SharedApi
            .GetSpotUserTradeHistoryAsync(new GetUserTradesRequest(EthEur, limit: 100), ct: TestContext.Current.CancellationToken);
        var emptyPage = await new StubHttpMessageHandler("[]").RestClient().SpotApi.SharedApi
            .GetSpotUserTradeHistoryAsync(new GetUserTradesRequest(EthEur, limit: 2), ct: TestContext.Current.CancellationToken);

        shortPage.Success.ShouldBeTrue();
        shortPage.NextPageRequest.ShouldBeNull();
        emptyPage.Success.ShouldBeTrue();
        emptyPage.Data.ShouldBeEmpty();
        emptyPage.NextPageRequest.ShouldBeNull();
    }

    [Fact]
    public async Task An_ascending_user_trades_request_is_rejected_before_anything_is_sent()
    {
        var handler = new StubHttpMessageHandler(UserTrades);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.GetSpotUserTradeHistoryAsync(new GetUserTradesRequest(EthEur, direction: DataDirection.Ascending), ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.Error.ShouldBeOfType<ArgumentError>();
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public void The_user_trade_history_options_describe_what_Bitvavo_supports()
    {
        var options = new StubHttpMessageHandler("[]").RestClient().SpotApi.SharedApi.GetSpotUserTradeHistoryOptions;

        options.SupportsAscending.ShouldBeFalse();
        options.SupportsDescending.ShouldBeTrue();
        options.TimePeriodFilterSupport.ShouldBeTrue();
        options.MaxLimit.ShouldBe(1000);
        options.NeedsAuthentication.ShouldBeTrue();
    }

    // ── [V2] cancel ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_order_is_cancelled_by_id_with_a_signed_DELETE_carrying_market_orderId_and_operatorId_in_the_query()
    {
        var handler = new StubHttpMessageHandler(Cancelled);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.CancelSpotOrderAsync(new CancelOrderRequest(EthEur, "ord-1", Operator(9)), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Id.ShouldBe("ord-1");
        var sent = handler.Requests.ShouldHaveSingleItem();
        sent.Method.ShouldBe(HttpMethod.Delete);
        sent.RequestUri!.PathAndQuery.ShouldBe("/v2/order?market=ETH-EUR&operatorId=9&orderId=ord-1");
        sent.Content.ShouldBeNull();
        sent.Headers.GetValues("Bitvavo-Access-Signature").ShouldHaveSingleItem().Length.ShouldBe(64);
    }

    [Fact]
    public async Task An_order_is_cancelled_by_client_order_id_with_the_client_order_id_in_the_query()
    {
        var handler = new StubHttpMessageHandler(CancelledByClientOrderId);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.CancelSpotOrderByClientOrderIdAsync(new CancelOrderRequest(EthEur, ClientOrderId, Operator(9)), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Id.ShouldBe("ord-1");
        var sent = handler.Requests.ShouldHaveSingleItem();
        sent.Method.ShouldBe(HttpMethod.Delete);
        sent.RequestUri!.PathAndQuery.ShouldBe("/v2/order?clientOrderId=11111111-1111-1111-1111-111111111111&market=ETH-EUR&operatorId=9");
        sent.Content.ShouldBeNull();
        sent.Headers.GetValues("Bitvavo-Access-Signature").ShouldHaveSingleItem().Length.ShouldBe(64);
    }

    [Fact]
    public async Task A_cancel_without_the_OperatorId_exchange_parameter_is_rejected_before_anything_is_sent()
    {
        var handler = new StubHttpMessageHandler(Cancelled);
        var api = handler.RestClient().SpotApi.SharedApi;
        var ct = TestContext.Current.CancellationToken;

        var byId = await api.CancelSpotOrderAsync(new CancelOrderRequest(EthEur, "ord-1"), ct);
        var byClientOrderId = await api.CancelSpotOrderByClientOrderIdAsync(new CancelOrderRequest(EthEur, ClientOrderId), ct);

        byId.Success.ShouldBeFalse();
        byId.Error.ShouldBeOfType<ArgumentError>().Message.ShouldNotBeNull().ShouldContain("OperatorId");
        byClientOrderId.Success.ShouldBeFalse();
        byClientOrderId.Error.ShouldBeOfType<ArgumentError>().Message.ShouldNotBeNull().ShouldContain("OperatorId");
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public void Only_the_operations_that_change_orders_need_the_OperatorId_exchange_parameter()
    {
        var api = new StubHttpMessageHandler("{}").RestClient().SpotApi.SharedApi;

        foreach (var options in new CapabilityOptions[] { api.PlaceSpotOrderOptions, api.CancelSpotOrderOptions, api.CancelSpotOrderByClientOrderIdOptions })
        {
            var rule = options.ExchangeParameterRules.ShouldHaveSingleItem();
            rule.Name.ShouldBe("OperatorId");
            rule.Requirement.ShouldBe(ExchangeParameterRequirement.Required);
        }

        foreach (var options in new CapabilityOptions[]
        {
            api.GetSpotOrderOptions, api.GetSpotOrderByClientOrderIdOptions, api.GetOpenSpotOrdersOptions,
            api.GetClosedSpotOrdersOptions, api.GetSpotOrderTradesOptions, api.GetSpotUserTradeHistoryOptions,
        })
        {
            options.ExchangeParameterRules.ShouldBeEmpty();
        }
    }

    // ── credentials and server errors ────────────────────────────────────────────────────

    [Fact]
    public async Task Every_order_capability_refuses_to_run_without_credentials_and_sends_nothing()
    {
        var handler = new StubHttpMessageHandler("{}");
        var api = handler.RestClient(withCredentials: false).SpotApi.SharedApi;

        var results = await RunEveryOrderCapabilityAsync(api);

        results.Length.ShouldBe(9);
        foreach (var result in results)
        {
            result.Success.ShouldBeFalse();
            result.Error.ShouldBeOfType<NoApiCredentialsError>();
        }

        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public void Every_order_capability_needs_credentials()
    {
        var api = new StubHttpMessageHandler("{}").RestClient().SpotApi.SharedApi;

        foreach (var options in new CapabilityOptions[]
        {
            api.PlaceSpotOrderOptions, api.GetSpotOrderOptions, api.GetSpotOrderByClientOrderIdOptions, api.GetOpenSpotOrdersOptions,
            api.GetClosedSpotOrdersOptions, api.GetSpotOrderTradesOptions, api.GetSpotUserTradeHistoryOptions,
            api.CancelSpotOrderOptions, api.CancelSpotOrderByClientOrderIdOptions,
        })
        {
            options.NeedsAuthentication.ShouldBeTrue(options.OperationName);
        }
    }

    [Fact]
    public async Task A_server_error_is_passed_through_unchanged_by_every_order_capability()
    {
        var handler = new StubHttpMessageHandler(SignatureError, HttpStatusCode.Forbidden);
        var api = handler.RestClient().SpotApi.SharedApi;

        var results = await RunEveryOrderCapabilityAsync(api);

        results.Length.ShouldBe(9);
        handler.Requests.Count.ShouldBe(9);
        foreach (var result in results)
        {
            result.Success.ShouldBeFalse();
            var error = result.Error.ShouldNotBeNull();
            error.ErrorType.ShouldBe(ErrorType.Unauthorized);
            error.Code.ShouldBe(309);
        }
    }

    [Fact]
    public async Task An_unknown_order_is_an_UnknownOrder_error_for_lookups_order_trades_and_cancels()
    {
        var handler = new StubHttpMessageHandler(UnknownOrderError, HttpStatusCode.NotFound);
        var api = handler.RestClient().SpotApi.SharedApi;
        var ct = TestContext.Current.CancellationToken;

        var results = new IHttpResult[]
        {
            await api.GetSpotOrderAsync(new GetOrderRequest(EthEur, "nope"), ct),
            await api.GetSpotOrderByClientOrderIdAsync(new GetOrderRequest(EthEur, ClientOrderId), ct),
            await api.GetSpotOrderTradesAsync(new GetOrderTradesRequest(EthEur, "nope"), ct),
            await api.CancelSpotOrderAsync(new CancelOrderRequest(EthEur, "nope", Operator()), ct),
            await api.CancelSpotOrderByClientOrderIdAsync(new CancelOrderRequest(EthEur, ClientOrderId, Operator()), ct),
        };

        foreach (var result in results)
        {
            result.Success.ShouldBeFalse();
            var error = result.Error.ShouldNotBeNull();
            error.ErrorType.ShouldBe(ErrorType.UnknownOrder);
            error.Code.ShouldBe(240);
        }
    }

    // ── [V2] the transport-agnostic capability interfaces ────────────────────────────────

    [Fact]
    public async Task The_transport_agnostic_capability_interfaces_return_what_the_REST_ones_return()
    {
        var api = OrderRouter().RestClient().SpotApi.SharedApi;
        var ct = TestContext.Current.CancellationToken;
        var symbol = EthEur;

        var place = await ((IPlaceSpotOrder)api).PlaceSpotOrderAsync(
            new PlaceSpotOrderRequest(symbol, SharedOrderSide.Buy, SharedOrderType.Limit, SharedQuantity.Base(1m), 3000m, exchangeParameters: Operator()), ct);
        var byId = await ((IGetSpotOrder)api).GetSpotOrderAsync(new GetOrderRequest(symbol, "ord-1"), ct);
        var byClientOrderId = await ((IGetSpotOrderByClientOrderId)api).GetSpotOrderByClientOrderIdAsync(new GetOrderRequest(symbol, ClientOrderId), ct);
        var open = await ((IGetOpenSpotOrders)api).GetOpenSpotOrdersAsync(new GetOpenOrdersRequest(), ct);
        var closed = await ((IGetClosedSpotOrders)api).GetClosedSpotOrdersAsync(new GetClosedOrdersRequest(symbol), ct: ct);
        var orderTrades = await ((IGetSpotOrderTrades)api).GetSpotOrderTradesAsync(new GetOrderTradesRequest(symbol, "ord-1"), ct);
        var userTrades = await ((IGetSpotUserTradeHistory)api).GetSpotUserTradeHistoryAsync(new GetUserTradesRequest(symbol), ct: ct);
        var cancel = await ((ICancelSpotOrder)api).CancelSpotOrderAsync(new CancelOrderRequest(symbol, "ord-1", Operator()), ct);
        var cancelByClientOrderId = await ((ICancelSpotOrderByClientOrderId)api).CancelSpotOrderByClientOrderIdAsync(new CancelOrderRequest(symbol, ClientOrderId, Operator()), ct);

        place.Success.ShouldBeTrue();
        place.Data.Id.ShouldBe("abc-123");
        byId.Success.ShouldBeTrue();
        byId.Data.OrderId.ShouldBe("ord-1");
        byClientOrderId.Success.ShouldBeTrue();
        byClientOrderId.Data.ClientOrderId.ShouldBe(ClientOrderId);
        open.Success.ShouldBeTrue();
        open.Data.ShouldHaveSingleItem().OrderId.ShouldBe("open-1");
        closed.Success.ShouldBeTrue();
        closed.Data.Length.ShouldBe(2);
        orderTrades.Success.ShouldBeTrue();
        orderTrades.Data.ShouldHaveSingleItem().Id.ShouldBe("fill-1");
        userTrades.Success.ShouldBeTrue();
        userTrades.Data.Length.ShouldBe(2);
        cancel.Success.ShouldBeTrue();
        cancel.Data.Id.ShouldBe("ord-1");
        cancelByClientOrderId.Success.ShouldBeTrue();
        cancelByClientOrderId.Data.Id.ShouldBe("ord-1");
    }

    // ── [V1] the legacy interfaces on the same instance ──────────────────────────────────

    [Fact]
    public async Task The_legacy_interface_places_an_order_and_returns_its_id()
    {
        var handler = new StubHttpMessageHandler("""{"orderId":"abc-123","market":"ETH-EUR","status":"new","side":"buy","orderType":"limit","created":0,"updated":0}""");
        var legacy = handler.RestClient().SpotApi.SharedClient;
        var request = new PlaceSpotOrderRequest(
            EthEur, SharedOrderSide.Buy, SharedOrderType.Limit, quantity: SharedQuantity.Base(0.5m), price: 3000m, exchangeParameters: Operator());

        var result = await legacy.PlaceSpotOrderAsync(request, TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Id.ShouldBe("abc-123");
        handler.Requests.ShouldHaveSingleItem().RequestUri!.AbsolutePath.ShouldBe("/v2/order");
    }

    [Fact]
    public async Task The_legacy_interface_gets_an_order_and_maps_it()
    {
        var legacy = new StubHttpMessageHandler("""{"orderId":"abc-123","market":"ETH-EUR","status":"filled","side":"buy","orderType":"limit","amount":"0.5","filledAmount":"0.5","price":"3000","created":1714132800000,"updated":1714132900000}""")
            .RestClient().SpotApi.SharedClient;

        var result = await legacy.GetSpotOrderAsync(new GetOrderRequest(EthEur, "abc-123"), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.OrderId.ShouldBe("abc-123");
        result.Data.Status.ShouldBe(SharedOrderStatus.Filled);
        result.Data.Side.ShouldBe(SharedOrderSide.Buy);
    }

    [Fact]
    public async Task The_legacy_interface_cancels_an_order_and_returns_the_cancelled_id()
    {
        var legacy = new StubHttpMessageHandler("""{"orderId":"abc-123","market":"ETH-EUR"}""").RestClient().SpotApi.SharedClient;

        var result = await legacy.CancelSpotOrderAsync(new CancelOrderRequest(EthEur, "abc-123", Operator()), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Id.ShouldBe("abc-123");
    }

    [Fact]
    public async Task The_legacy_interface_lists_open_and_closed_orders()
    {
        var ct = TestContext.Current.CancellationToken;
        var open = await new StubHttpMessageHandler("[" + OpenEthBuy + "]").RestClient().SpotApi.SharedClient
            .GetOpenSpotOrdersAsync(new GetOpenOrdersRequest(EthEur), ct);
        var closed = await new StubHttpMessageHandler(ClosedHistory).RestClient().SpotApi.SharedClient
            .GetClosedSpotOrdersAsync(new GetClosedOrdersRequest(EthEur, limit: 3), ct: ct);

        open.Success.ShouldBeTrue();
        open.Data.ShouldHaveSingleItem().OrderId.ShouldBe("open-1");
        closed.Success.ShouldBeTrue();
        closed.Data.Length.ShouldBe(2);
        closed.NextPageRequest.ShouldNotBeNull();
    }

    [Fact]
    public async Task The_legacy_interface_serves_the_trades_of_an_order_and_the_user_trades_under_its_old_name()
    {
        var ct = TestContext.Current.CancellationToken;
        var orderTrades = await new StubHttpMessageHandler(OldSoldOrderWithFills).RestClient().SpotApi.SharedClient
            .GetSpotOrderTradesAsync(new GetOrderTradesRequest(EthEur, "ord-old"), ct);
        var userTradesHandler = new StubHttpMessageHandler(UserTrades);
        var userTrades = await userTradesHandler.RestClient().SpotApi.SharedClient
            .GetSpotUserTradesAsync(new GetUserTradesRequest(EthEur, limit: 2), ct: ct);

        orderTrades.Success.ShouldBeTrue();
        orderTrades.Data.Length.ShouldBe(2);
        userTrades.Success.ShouldBeTrue();
        userTrades.Data.Length.ShouldBe(2);
        userTrades.NextPageRequest.ShouldNotBeNull();
        userTradesHandler.Requests.ShouldHaveSingleItem().RequestUri!.PathAndQuery.ShouldBe("/v2/trades?limit=2&market=ETH-EUR");
    }

    [Fact]
    public async Task The_legacy_client_order_id_interface_gets_an_order_by_client_order_id_and_cancels_it()
    {
        var getHandler = new StubHttpMessageHandler("""{"orderId":"abc-123","clientOrderId":"client-1","market":"ETH-EUR","status":"new","side":"buy","orderType":"limit","created":0,"updated":0}""");
        var cancelHandler = new StubHttpMessageHandler(CancelledByClientOrderId);
        var ct = TestContext.Current.CancellationToken;

        var order = await getHandler.RestClient().SpotApi.SharedClient.GetSpotOrderByClientOrderIdAsync(new GetOrderRequest(EthEur, "client-1"), ct);
        var cancelled = await cancelHandler.RestClient().SpotApi.SharedClient.CancelSpotOrderByClientOrderIdAsync(new CancelOrderRequest(EthEur, ClientOrderId, Operator()), ct);

        order.Success.ShouldBeTrue();
        order.Data.ClientOrderId.ShouldBe("client-1");
        getHandler.Requests.ShouldHaveSingleItem().RequestUri!.Query.ShouldContain("clientOrderId=client-1");
        cancelled.Success.ShouldBeTrue();
        cancelled.Data.Id.ShouldBe("ord-1");
        cancelHandler.Requests.ShouldHaveSingleItem().RequestUri!.Query.ShouldContain("clientOrderId=" + ClientOrderId);
    }

    [Fact]
    public void The_legacy_interface_reports_the_same_order_capabilities_as_the_V2_one()
    {
        var legacy = new StubHttpMessageHandler("{}").RestClient().SpotApi.SharedClient;

        legacy.SpotFeeDeductionType.ShouldBe(SharedFeeDeductionType.DeductFromOutput);
        legacy.SpotFeeAssetType.ShouldBe(SharedFeeAssetType.QuoteAsset);
        legacy.SpotSupportedOrderTypes.ShouldBe(new[] { SharedOrderType.Limit, SharedOrderType.Market, SharedOrderType.LimitMaker }, ignoreOrder: true);
        legacy.SpotSupportedTimeInForce.ShouldBe(new[] { SharedTimeInForce.GoodTillCanceled, SharedTimeInForce.ImmediateOrCancel, SharedTimeInForce.FillOrKill }, ignoreOrder: true);
        legacy.SpotSupportedOrderQuantity.GetSupportedQuantityType(SharedOrderSide.Buy, SharedOrderType.Market).ShouldBe(SharedQuantityType.BaseAndQuoteAsset);
        Guid.TryParse(legacy.GenerateClientOrderId(), out _).ShouldBeTrue();
    }

    [Fact]
    public void The_legacy_options_are_the_V2_options_objects_and_the_renamed_user_trades_member_is_a_bridge()
    {
        var client = new StubHttpMessageHandler("{}").RestClient();
        var legacy = client.SpotApi.SharedClient;
        var api = client.SpotApi.SharedApi;

        legacy.PlaceSpotOrderOptions.ShouldBeSameAs(api.PlaceSpotOrderOptions);
        legacy.GetSpotOrderOptions.ShouldBeSameAs(api.GetSpotOrderOptions);
        legacy.GetOpenSpotOrdersOptions.ShouldBeSameAs(api.GetOpenSpotOrdersOptions);
        legacy.GetClosedSpotOrdersOptions.ShouldBeSameAs(api.GetClosedSpotOrdersOptions);
        legacy.GetSpotOrderTradesOptions.ShouldBeSameAs(api.GetSpotOrderTradesOptions);
        legacy.GetSpotUserTradesOptions.ShouldBeSameAs(api.GetSpotUserTradeHistoryOptions);
        legacy.CancelSpotOrderOptions.ShouldBeSameAs(api.CancelSpotOrderOptions);
        legacy.GetSpotOrderByClientOrderIdOptions.ShouldBeSameAs(api.GetSpotOrderByClientOrderIdOptions);
        legacy.CancelSpotOrderByClientOrderIdOptions.ShouldBeSameAs(api.CancelSpotOrderByClientOrderIdOptions);
    }

    // ── the mappings behind them (BitvavoSharedMappingExtensions.Orders) ─────────────────

    [Fact]
    public void The_order_symbol_comes_from_its_market_unless_the_caller_supplies_one()
    {
        var order = new BitvavoOrder { OrderId = "o-1", Market = "BTC-EUR", Side = OrderSide.Sell };
        var own = new SharedSymbol(TradingMode.Spot, "BTC", "EUR");

        var derived = order.ToSharedSpotOrder().SharedSymbol.ShouldNotBeNull();

        derived.BaseAsset.ShouldBe("BTC");
        derived.QuoteAsset.ShouldBe("EUR");
        derived.SymbolName.ShouldBe("BTC-EUR");
        order.ToSharedSpotOrder(own).SharedSymbol.ShouldBeSameAs(own);
    }

    [Fact]
    public void An_order_side_this_library_does_not_know_cannot_become_a_shared_order()
    {
        var order = new BitvavoOrder { OrderId = "o-1", Market = "BTC-EUR", Side = (OrderSide)(-9) };

        Should.Throw<InvalidOperationException>(() => order.ToSharedSpotOrder());
    }

    [Fact]
    public void An_order_with_a_filled_amount_but_no_filled_quote_has_no_average_price()
    {
        var order = new BitvavoOrder { OrderId = "o-1", Market = "BTC-EUR", Side = OrderSide.Buy, FilledAmount = 1m, FilledAmountQuote = null };

        order.ToSharedSpotOrder().AveragePrice.ShouldBeNull();
    }

    [Fact]
    public void A_standalone_fill_without_role_side_fee_amount_or_price_maps_to_an_empty_trade()
    {
        var fill = new BitvavoFill { Id = "f-1", Timestamp = Utc(12) };

        var trade = fill.ToSharedUserTrade("ETH-EUR");

        trade.Id.ShouldBe("f-1");
        trade.OrderId.ShouldBe(string.Empty);
        trade.Symbol.ShouldBe("ETH-EUR");
        trade.SharedSymbol.ShouldNotBeNull().BaseAsset.ShouldBe("ETH");
        trade.Side.ShouldBeNull();
        trade.Role.ShouldBeNull();
        trade.Fee.ShouldBeNull();
        trade.FeeAsset.ShouldBeNull();
        trade.ClientOrderId.ShouldBeNull();
        trade.Price.ShouldBe(0m);
        trade.Quantities.QuantityInBaseAsset.ShouldBeNull();
        trade.Timestamp.ShouldBe(Utc(12));
    }

    [Fact]
    public void A_standalone_fill_names_its_own_market_and_derives_the_quote_quantity_from_amount_and_price()
    {
        var fill = new BitvavoFill { Id = "f-1", Market = "BTC-EUR", Side = OrderSide.Buy, Amount = 2m, Price = 10m };
        var own = new SharedSymbol(TradingMode.Spot, "BTC", "EUR");

        var trade = fill.ToSharedUserTrade("ETH-EUR", own);

        trade.Symbol.ShouldBe("BTC-EUR");
        trade.SharedSymbol.ShouldBeSameAs(own);
        trade.Side.ShouldBe(SharedOrderSide.Buy);
        trade.Quantities.QuantityInBaseAsset.ShouldBe(2m);
        trade.Quantities.QuantityInQuoteAsset.ShouldBe(20m);
    }

    [Fact]
    public void An_embedded_fill_takes_order_id_side_and_client_order_id_from_its_order_but_keeps_a_client_order_id_of_its_own()
    {
        var order = new BitvavoOrder { OrderId = "o-1", ClientOrderId = "c-order", Market = "ETH-EUR", Side = OrderSide.Sell };

        var inherited = new BitvavoFill { Id = "f-1", Amount = 1m, Price = 5m, Taker = true }.ToSharedUserTrade(order);
        var own = new BitvavoFill { Id = "f-2", ClientOrderId = "c-fill" }.ToSharedUserTrade(order);

        inherited.OrderId.ShouldBe("o-1");
        inherited.Side.ShouldBe(SharedOrderSide.Sell);
        inherited.ClientOrderId.ShouldBe("c-order");
        inherited.Symbol.ShouldBe("ETH-EUR");
        inherited.SharedSymbol.ShouldNotBeNull().BaseAsset.ShouldBe("ETH");
        inherited.Role.ShouldBe(SharedRole.Taker);
        own.ClientOrderId.ShouldBe("c-fill");
    }
}
