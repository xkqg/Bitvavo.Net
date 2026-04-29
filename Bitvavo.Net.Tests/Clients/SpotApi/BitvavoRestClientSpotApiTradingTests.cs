// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Bitvavo.Net.Clients;
using Bitvavo.Net.Clients.SpotApi;
using Bitvavo.Net.Enums;
using Bitvavo.Net.Objects.Models.Spot;
using Bitvavo.Net.Objects.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests.Clients.SpotApi;

/// <summary>
/// Unit tests for <see cref="BitvavoRestClientSpotApiTrading"/> — covers all 8 signed
/// trading endpoints. Test order mirrors the spec's authoring order:
/// place → cancel → get-single → cancel-all → list-open → history → trades → update.
/// </summary>
public class BitvavoRestClientSpotApiTradingTests
{
    private const string TestKey = "test-key";
    private const string TestSecret = "test-secret";

    private static BitvavoRestClientSpotApiTrading TradingClientReturning(
        string json,
        out StubHttpMessageHandler handler,
        HttpStatusCode status = HttpStatusCode.OK)
    {
        handler = new StubHttpMessageHandler(json, status);
        var http = new HttpClient(handler);
        var opts = new BitvavoRestOptions
        {
            ApiCredentials = new BitvavoCredentials(TestKey, TestSecret),
        };
        var client = new BitvavoRestClient(http, null, Options.Create(opts));
        var apiClient = (BitvavoRestClientSpotApi)client.SpotApi;
        return new BitvavoRestClientSpotApiTrading(apiClient);
    }

    // ── PlaceOrderAsync ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task PlaceOrderAsync_hits_v2_order_with_POST_and_signature_header()
    {
        var trading = TradingClientReturning("""{"orderId":"abc","market":"ETH-EUR","status":"new","side":"buy","orderType":"limit","created":1714132800000,"updated":1714132800000}""", out var handler);

        await trading.PlaceOrderAsync(new BitvavoPlaceOrderRequest("ETH-EUR", OrderSide.Buy, OrderType.Limit, OperatorId: 1, Amount: 0.5m, Price: 1500m));

        handler.Requests[0].Method.ShouldBe(HttpMethod.Post);
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/order");
        handler.Requests[0].Headers.GetValues("Bitvavo-Access-Signature").ShouldHaveSingleItem().Length.ShouldBe(64);
    }

    [Fact]
    public async Task PlaceOrderAsync_serialises_body_as_JSON_with_required_fields()
    {
        var trading = TradingClientReturning("""{"orderId":"x","market":"ETH-EUR","status":"new","side":"buy","orderType":"limit","created":0,"updated":0}""", out var handler);

        await trading.PlaceOrderAsync(new BitvavoPlaceOrderRequest("ETH-EUR", OrderSide.Buy, OrderType.Limit, OperatorId: 42, Amount: 0.5m, Price: 1500m, TimeInForce: TimeInForce.GoodTillCanceled, PostOnly: true));

        handler.Requests[0].Content!.Headers.ContentType!.MediaType.ShouldBe("application/json");
        var body = await handler.Requests[0].Content!.ReadAsStringAsync();
        body.ShouldContain("\"market\":\"ETH-EUR\"");
        body.ShouldContain("\"side\":\"buy\"");
        body.ShouldContain("\"orderType\":\"limit\"");
        body.ShouldContain("\"operatorId\":42");
        body.ShouldContain("\"amount\":\"0.5\"");
        body.ShouldContain("\"price\":\"1500\"");
        body.ShouldContain("\"timeInForce\":\"GTC\"");
        body.ShouldContain("\"postOnly\":true");
    }

    [Fact]
    public async Task PlaceOrderAsync_signature_is_computed_over_the_exact_body_bytes_sent()
    {
        var trading = TradingClientReturning("""{"orderId":"x","market":"ETH-EUR","status":"new","side":"buy","orderType":"limit","created":0,"updated":0}""", out var handler);

        await trading.PlaceOrderAsync(new BitvavoPlaceOrderRequest("ETH-EUR", OrderSide.Sell, OrderType.Limit, OperatorId: 7, Amount: 1m, Price: 1234m));

        var capturedBody = await handler.Requests[0].Content!.ReadAsStringAsync();
        var ts = handler.Requests[0].Headers.GetValues("Bitvavo-Access-Timestamp").Single();
        var actualSig = handler.Requests[0].Headers.GetValues("Bitvavo-Access-Signature").Single();

        var expectedPayload = ts + "POST/v2/order" + capturedBody;
        var expectedSig = HmacSha256Hex(TestSecret, expectedPayload);

        actualSig.ShouldBe(expectedSig);
    }

    [Fact]
    public async Task PlaceOrderAsync_maps_response_dto()
    {
        const string json = """
        {
          "orderId": "abc-123", "clientOrderId": "ext-1", "market": "ETH-EUR",
          "created": 1714132800000, "updated": 1714132900000,
          "status": "filled", "side": "buy", "orderType": "limit",
          "amount": "0.5", "amountRemaining": "0", "price": "1500",
          "filledAmount": "0.5", "filledAmountQuote": "750",
          "feePaid": "0.6", "feeCurrency": "EUR",
          "fills": [
            { "id": "f-1", "timestamp": 1714132850000, "amount": "0.5", "price": "1500", "taker": false, "fee": "0.6", "feeCurrency": "EUR", "settled": true }
          ],
          "timeInForce": "GTC", "postOnly": false
        }
        """;
        var trading = TradingClientReturning(json, out _);

        var result = await trading.PlaceOrderAsync(new BitvavoPlaceOrderRequest("ETH-EUR", OrderSide.Buy, OrderType.Limit, OperatorId: 1, Amount: 0.5m, Price: 1500m));

        result.Success.ShouldBeTrue();
        result.Data.OrderId.ShouldBe("abc-123");
        result.Data.ClientOrderId.ShouldBe("ext-1");
        result.Data.Status.ShouldBe(OrderStatus.Filled);
        result.Data.Side.ShouldBe(OrderSide.Buy);
        result.Data.OrderType.ShouldBe(OrderType.Limit);
        result.Data.Amount.ShouldBe(0.5m);
        result.Data.Price.ShouldBe(1500m);
        result.Data.FeePaid.ShouldBe(0.6m);
        result.Data.TimeInForce.ShouldBe(TimeInForce.GoodTillCanceled);
        result.Data.PostOnly.ShouldBe(false);
        result.Data.Fills.Count.ShouldBe(1);
        result.Data.Fills[0].Id.ShouldBe("f-1");
        result.Data.Fills[0].Amount.ShouldBe(0.5m);
    }

    // ── CancelOrderAsync ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CancelOrderAsync_hits_v2_order_with_DELETE_and_query()
    {
        var trading = TradingClientReturning("""{"orderId":"abc","market":"ETH-EUR"}""", out var handler);

        await trading.CancelOrderAsync(market: "ETH-EUR", operatorId: 9, orderId: "abc-123");

        handler.Requests[0].Method.ShouldBe(HttpMethod.Delete);
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/order");
        handler.Requests[0].RequestUri!.Query.ShouldContain("market=ETH-EUR");
        handler.Requests[0].RequestUri!.Query.ShouldContain("orderId=abc-123");
        handler.Requests[0].RequestUri!.Query.ShouldContain("operatorId=9");
        handler.Requests[0].Content.ShouldBeNull();
    }

    [Fact]
    public async Task CancelOrderAsync_signs_query_string_with_empty_body()
    {
        var trading = TradingClientReturning("""{"orderId":"abc","market":"ETH-EUR"}""", out var handler);

        await trading.CancelOrderAsync(market: "ETH-EUR", operatorId: 1, orderId: "abc");

        var ts = handler.Requests[0].Headers.GetValues("Bitvavo-Access-Timestamp").Single();
        var actualSig = handler.Requests[0].Headers.GetValues("Bitvavo-Access-Signature").Single();
        var query = handler.Requests[0].RequestUri!.Query.TrimStart('?');

        var expectedPayload = ts + "DELETE/v2/order?" + query;
        var expectedSig = HmacSha256Hex(TestSecret, expectedPayload);

        actualSig.ShouldBe(expectedSig);
    }

    [Fact]
    public async Task CancelOrderAsync_maps_response_dto()
    {
        var trading = TradingClientReturning("""{"orderId":"abc-123","market":"ETH-EUR"}""", out _);

        var result = await trading.CancelOrderAsync(market: "ETH-EUR", operatorId: 1, orderId: "abc-123");

        result.Success.ShouldBeTrue();
        result.Data.OrderId.ShouldBe("abc-123");
        result.Data.Market.ShouldBe("ETH-EUR");
    }

    // ── GetOrderAsync ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrderAsync_hits_v2_order_with_GET_and_query()
    {
        var trading = TradingClientReturning("""{"orderId":"x","market":"ETH-EUR","status":"new","side":"buy","orderType":"limit","created":0,"updated":0}""", out var handler);

        await trading.GetOrderAsync(market: "ETH-EUR", orderId: "x");

        handler.Requests[0].Method.ShouldBe(HttpMethod.Get);
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/order");
        handler.Requests[0].RequestUri!.Query.ShouldContain("market=ETH-EUR");
        handler.Requests[0].RequestUri!.Query.ShouldContain("orderId=x");
    }

    [Fact]
    public async Task GetOrderAsync_with_clientOrderId_passes_clientOrderId_query()
    {
        var trading = TradingClientReturning("""{"orderId":"x","market":"ETH-EUR","status":"new","side":"buy","orderType":"limit","created":0,"updated":0}""", out var handler);

        await trading.GetOrderAsync(market: "ETH-EUR", clientOrderId: "client-1");

        handler.Requests[0].RequestUri!.Query.ShouldContain("clientOrderId=client-1");
    }

    // ── CancelOrdersAsync (batch) ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CancelOrdersAsync_no_market_cancels_all_with_no_query()
    {
        var trading = TradingClientReturning("[]", out var handler);

        await trading.CancelOrdersAsync();

        handler.Requests[0].Method.ShouldBe(HttpMethod.Delete);
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/orders");
        handler.Requests[0].RequestUri!.Query.ShouldBeEmpty();
    }

    [Fact]
    public async Task CancelOrdersAsync_with_market_passes_market_query()
    {
        var trading = TradingClientReturning("[]", out var handler);

        await trading.CancelOrdersAsync(market: "ETH-EUR");

        handler.Requests[0].RequestUri!.Query.ShouldContain("market=ETH-EUR");
    }

    [Fact]
    public async Task CancelOrdersAsync_maps_array_response()
    {
        var trading = TradingClientReturning("""[{"orderId":"a","market":"ETH-EUR"},{"orderId":"b","market":"ETH-EUR"}]""", out _);

        var result = await trading.CancelOrdersAsync(market: "ETH-EUR");

        result.Success.ShouldBeTrue();
        var ids = result.Data.ToList();
        ids.Count.ShouldBe(2);
        ids[0].OrderId.ShouldBe("a");
        ids[1].OrderId.ShouldBe("b");
    }

    // ── GetOpenOrdersAsync ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOpenOrdersAsync_hits_v2_ordersOpen()
    {
        var trading = TradingClientReturning("[]", out var handler);

        await trading.GetOpenOrdersAsync();

        handler.Requests[0].Method.ShouldBe(HttpMethod.Get);
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/ordersOpen");
    }

    [Fact]
    public async Task GetOpenOrdersAsync_with_market_passes_market_query()
    {
        var trading = TradingClientReturning("[]", out var handler);

        await trading.GetOpenOrdersAsync(market: "BTC-EUR");

        handler.Requests[0].RequestUri!.Query.ShouldContain("market=BTC-EUR");
    }

    [Fact]
    public async Task GetOpenOrdersAsync_with_baseAsset_passes_base_query()
    {
        var trading = TradingClientReturning("[]", out var handler);

        await trading.GetOpenOrdersAsync(baseAsset: "ETH");

        handler.Requests[0].RequestUri!.Query.ShouldContain("base=ETH");
    }

    // ── GetOrderHistoryAsync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrderHistoryAsync_hits_v2_orders_with_market_and_paging_params()
    {
        var trading = TradingClientReturning("[]", out var handler);
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc);

        await trading.GetOrderHistoryAsync(market: "ETH-EUR", limit: 100, startTime: start, endTime: end, orderIdFrom: "from-1", orderIdTo: "to-1");

        handler.Requests[0].Method.ShouldBe(HttpMethod.Get);
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/orders");
        var q = handler.Requests[0].RequestUri!.Query;
        q.ShouldContain("market=ETH-EUR");
        q.ShouldContain("limit=100");
        q.ShouldContain("start=1704067200000");
        q.ShouldContain("end=1704153600000");
        q.ShouldContain("orderIdFrom=from-1");
        q.ShouldContain("orderIdTo=to-1");
    }

    [Fact]
    public async Task GetOrderHistoryAsync_maps_array_with_status_enum()
    {
        const string json = """
        [
          { "orderId":"a", "market":"ETH-EUR", "created":0, "updated":0, "status":"filled", "side":"buy", "orderType":"limit", "amount":"1", "price":"1000" },
          { "orderId":"b", "market":"ETH-EUR", "created":0, "updated":0, "status":"canceled", "side":"sell", "orderType":"market" }
        ]
        """;
        var trading = TradingClientReturning(json, out _);

        var result = await trading.GetOrderHistoryAsync(market: "ETH-EUR");

        result.Success.ShouldBeTrue();
        var orders = result.Data.ToList();
        orders.Count.ShouldBe(2);
        orders[0].Status.ShouldBe(OrderStatus.Filled);
        orders[1].Status.ShouldBe(OrderStatus.Canceled);
    }

    // ── GetUserTradesAsync ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetUserTradesAsync_hits_v2_trades_with_paging_params()
    {
        var trading = TradingClientReturning("[]", out var handler);
        var start = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        await trading.GetUserTradesAsync(market: "ETH-EUR", limit: 50, startTime: start, tradeIdFrom: "tf-1");

        handler.Requests[0].Method.ShouldBe(HttpMethod.Get);
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/trades");
        var q = handler.Requests[0].RequestUri!.Query;
        q.ShouldContain("market=ETH-EUR");
        q.ShouldContain("limit=50");
        q.ShouldContain("start=1717200000000");
        q.ShouldContain("tradeIdFrom=tf-1");
    }

    [Fact]
    public async Task GetUserTradesAsync_maps_fill_array_with_side_enum_and_taker_flag()
    {
        const string json = """
        [
          { "id":"t1", "orderId":"o1", "timestamp":1717200000000, "market":"ETH-EUR", "side":"buy",
            "amount":"0.5", "price":"3000", "taker":true, "fee":"0.75", "feeCurrency":"EUR", "settled":true }
        ]
        """;
        var trading = TradingClientReturning(json, out _);

        var result = await trading.GetUserTradesAsync(market: "ETH-EUR");

        result.Success.ShouldBeTrue();
        var fills = result.Data.ToList();
        fills.Count.ShouldBe(1);
        fills[0].Id.ShouldBe("t1");
        fills[0].OrderId.ShouldBe("o1");
        fills[0].Market.ShouldBe("ETH-EUR");
        fills[0].Side.ShouldBe(OrderSide.Buy);
        fills[0].Amount.ShouldBe(0.5m);
        fills[0].Price.ShouldBe(3000m);
        fills[0].Taker.ShouldBe(true);
        fills[0].Settled.ShouldBe(true);
    }

    // ── UpdateOrderAsync ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateOrderAsync_hits_v2_order_with_PUT_and_signed_body()
    {
        var trading = TradingClientReturning("""{"orderId":"abc","market":"ETH-EUR","status":"new","side":"buy","orderType":"limit","created":0,"updated":0}""", out var handler);

        await trading.UpdateOrderAsync(new BitvavoUpdateOrderRequest("ETH-EUR", OperatorId: 5, OrderId: "abc-123", Amount: 0.6m, Price: 1600m));

        handler.Requests[0].Method.ShouldBe(HttpMethod.Put);
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/order");
        handler.Requests[0].Headers.GetValues("Bitvavo-Access-Signature").ShouldHaveSingleItem().Length.ShouldBe(64);

        var body = await handler.Requests[0].Content!.ReadAsStringAsync();
        body.ShouldContain("\"market\":\"ETH-EUR\"");
        body.ShouldContain("\"orderId\":\"abc-123\"");
        body.ShouldContain("\"operatorId\":5");
        body.ShouldContain("\"amount\":\"0.6\"");
        body.ShouldContain("\"price\":\"1600\"");
    }

    [Fact]
    public async Task UpdateOrderAsync_signature_matches_HMAC_of_body()
    {
        var trading = TradingClientReturning("""{"orderId":"abc","market":"ETH-EUR","status":"new","side":"buy","orderType":"limit","created":0,"updated":0}""", out var handler);

        await trading.UpdateOrderAsync(new BitvavoUpdateOrderRequest("ETH-EUR", OperatorId: 1, ClientOrderId: "client-1", Price: 1234m));

        var capturedBody = await handler.Requests[0].Content!.ReadAsStringAsync();
        var ts = handler.Requests[0].Headers.GetValues("Bitvavo-Access-Timestamp").Single();
        var actualSig = handler.Requests[0].Headers.GetValues("Bitvavo-Access-Signature").Single();

        var expectedPayload = ts + "PUT/v2/order" + capturedBody;
        var expectedSig = HmacSha256Hex(TestSecret, expectedPayload);

        actualSig.ShouldBe(expectedSig);
    }

    private static string HmacSha256Hex(string secret, string payload)
    {
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexStringLower(h.ComputeHash(Encoding.UTF8.GetBytes(payload)));
    }
}
