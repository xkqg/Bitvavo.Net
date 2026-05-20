// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
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
/// Unit tests for <see cref="BitvavoRestClientSpotApiInstitutional"/> — covers all 10
/// institutional endpoints (subaccount CRUD, transfers, per-subaccount balance / history /
/// open-orders, and subaccount order cancellation). Verifies path, method, signed headers,
/// request shape, and DTO mapping.
/// </summary>
public class BitvavoRestClientSpotApiInstitutionalTests
{
    private static BitvavoRestClientSpotApiInstitutional InstitutionalClientReturning(
        string json,
        out StubHttpMessageHandler handler,
        HttpStatusCode status = HttpStatusCode.OK)
    {
        handler = new StubHttpMessageHandler(json, status);
        var http = new HttpClient(handler);
        var opts = new BitvavoRestOptions
        {
            ApiCredentials = new BitvavoCredentials("test-key", "test-secret"),
        };
        var client = new BitvavoRestClient(http, null, Options.Create(opts));
        var apiClient = (BitvavoRestClientSpotApi)client.SpotApi;
        return new BitvavoRestClientSpotApiInstitutional(apiClient);
    }

    // ── CreateSubaccountAsync ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateSubaccountAsync_hits_v2_subaccounts_with_POST_and_signed_body()
    {
        var inst = InstitutionalClientReturning("""{"id":"sub-1","type":"spot","status":"open","label":"Desk A"}""", out var handler);

        await inst.CreateSubaccountAsync(label: "Desk A", ct: TestContext.Current.CancellationToken);

        handler.Requests[0].Method.ShouldBe(HttpMethod.Post);
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/subaccounts");
        handler.Requests[0].Headers.GetValues("Bitvavo-Access-Signature").ShouldHaveSingleItem().Length.ShouldBe(64);

        var body = await handler.Requests[0].Content!.ReadAsStringAsync(cancellationToken: TestContext.Current.CancellationToken);
        body.ShouldContain("\"label\":\"Desk A\"");
    }

    [Fact]
    public async Task CreateSubaccountAsync_maps_response_dto()
    {
        var inst = InstitutionalClientReturning("""{"id":"sub-42","type":"spot","status":"open","label":"Algo"}""", out _);

        var result = await inst.CreateSubaccountAsync(label: "Algo", ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Id.ShouldBe("sub-42");
        result.Data.Type.ShouldBe("spot");
        result.Data.Status.ShouldBe("open");
        result.Data.Label.ShouldBe("Algo");
    }

    // ── GetSubaccountsAsync ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSubaccountsAsync_hits_v2_subaccounts_with_paging_query()
    {
        var inst = InstitutionalClientReturning("""{"items":[],"currentPage":1,"totalPages":1,"maxItems":50}""", out var handler);

        await inst.GetSubaccountsAsync(page: 2, maxItems: 25, ct: TestContext.Current.CancellationToken);

        handler.Requests[0].Method.ShouldBe(HttpMethod.Get);
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/subaccounts");
        var q = handler.Requests[0].RequestUri!.Query;
        q.ShouldContain("page=2");
        q.ShouldContain("maxItems=25");
    }

    [Fact]
    public async Task GetSubaccountsAsync_maps_paginated_response()
    {
        const string json = """
        {
          "items": [ { "id": "s1", "type": "spot", "status": "open", "label": "A" },
                     { "id": "s2", "type": "margin", "status": "closed", "label": null } ],
          "currentPage": 1, "totalPages": 4, "maxItems": 50
        }
        """;
        var inst = InstitutionalClientReturning(json, out _);

        var result = await inst.GetSubaccountsAsync(ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.CurrentPage.ShouldBe(1);
        result.Data.TotalPages.ShouldBe(4);
        result.Data.Items.Count.ShouldBe(2);
        result.Data.Items[0].Id.ShouldBe("s1");
        result.Data.Items[1].Type.ShouldBe("margin");
        result.Data.Items[1].Label.ShouldBeNull();
    }

    // ── CreateTransferAsync ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateTransferAsync_hits_v2_subaccounts_transfers_with_POST_and_signed_body()
    {
        var inst = InstitutionalClientReturning("""{"transferId":"t-1","subaccountId":"sub-1","direction":"masterToSub","symbol":"EUR","amount":"100","status":"pending","createdAt":1717200000000}""", out var handler);

        await inst.CreateTransferAsync(new BitvavoCreateTransferRequest(
            SubaccountId: "sub-1", Direction: SubaccountTransferDirection.MasterToSub, Symbol: "EUR", Amount: 100m), ct: TestContext.Current.CancellationToken);

        handler.Requests[0].Method.ShouldBe(HttpMethod.Post);
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/subaccounts/transfers");
        handler.Requests[0].Headers.GetValues("Bitvavo-Access-Signature").ShouldHaveSingleItem().Length.ShouldBe(64);

        var body = await handler.Requests[0].Content!.ReadAsStringAsync(cancellationToken: TestContext.Current.CancellationToken);
        body.ShouldContain("\"subaccountId\":\"sub-1\"");
        body.ShouldContain("\"direction\":\"masterToSub\"");
        body.ShouldContain("\"symbol\":\"EUR\"");
        body.ShouldContain("\"amount\":\"100\"");
    }

    [Fact]
    public async Task CreateTransferAsync_maps_response_dto()
    {
        var inst = InstitutionalClientReturning("""{"transferId":"t-9","clientRequestId":"c-9","subaccountId":"sub-2","direction":"subToMaster","symbol":"BTC","amount":"0.5","status":"completed","createdAt":1717200000000}""", out _);

        var result = await inst.CreateTransferAsync(new BitvavoCreateTransferRequest(
            SubaccountId: "sub-2", Direction: SubaccountTransferDirection.SubToMaster, Symbol: "BTC", Amount: 0.5m, ClientRequestId: "c-9"), ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.TransferId.ShouldBe("t-9");
        result.Data.ClientRequestId.ShouldBe("c-9");
        result.Data.Direction.ShouldBe("subToMaster");
        result.Data.Symbol.ShouldBe("BTC");
        result.Data.Amount.ShouldBe(0.5m);
        result.Data.Status.ShouldBe("completed");
    }

    // ── GetTransferAsync ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTransferAsync_hits_v2_subaccounts_transfers_with_id_in_path()
    {
        var inst = InstitutionalClientReturning("""{"transferId":"t-1","subaccountId":"sub-1","direction":"masterToSub","symbol":"EUR","amount":"100","status":"completed","createdAt":1717200000000}""", out var handler);

        await inst.GetTransferAsync("t-1", ct: TestContext.Current.CancellationToken);

        handler.Requests[0].Method.ShouldBe(HttpMethod.Get);
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/subaccounts/transfers/t-1");
    }

    [Fact]
    public async Task GetTransferAsync_maps_response_dto()
    {
        var inst = InstitutionalClientReturning("""{"transferId":"t-1","subaccountId":"sub-3","direction":"masterToSub","symbol":"ETH","amount":"2.5","status":"completed","createdAt":1717200000000}""", out _);

        var result = await inst.GetTransferAsync("t-1", ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.TransferId.ShouldBe("t-1");
        result.Data.SubaccountId.ShouldBe("sub-3");
        result.Data.Symbol.ShouldBe("ETH");
        result.Data.Amount.ShouldBe(2.5m);
    }

    // ── GetTransfersAsync ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTransfersAsync_hits_v2_subaccounts_transfers_with_filter_query()
    {
        var inst = InstitutionalClientReturning("""{"items":[],"start":0,"end":0,"limit":25}""", out var handler);
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc);

        await inst.GetTransfersAsync(subaccountId: "sub-1", clientRequestId: "c-1", startTime: start, endTime: end, limit: 100, ct: TestContext.Current.CancellationToken);

        handler.Requests[0].Method.ShouldBe(HttpMethod.Get);
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/subaccounts/transfers");
        var q = handler.Requests[0].RequestUri!.Query;
        q.ShouldContain("subaccountId=sub-1");
        q.ShouldContain("clientRequestId=c-1");
        q.ShouldContain("start=1704067200000");
        q.ShouldContain("end=1704153600000");
        q.ShouldContain("limit=100");
    }

    [Fact]
    public async Task GetTransfersAsync_maps_list_response()
    {
        const string json = """
        {
          "items": [ { "transferId": "t-1", "subaccountId": "sub-1", "direction": "masterToSub", "symbol": "EUR", "amount": "100", "status": "completed", "createdAt": 1717200000000 } ],
          "start": 1704067200000, "end": 1704153600000, "limit": 25
        }
        """;
        var inst = InstitutionalClientReturning(json, out _);

        var result = await inst.GetTransfersAsync(subaccountId: "sub-1", ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Limit.ShouldBe(25);
        result.Data.Items.Count.ShouldBe(1);
        result.Data.Items[0].TransferId.ShouldBe("t-1");
        result.Data.Items[0].Amount.ShouldBe(100m);
    }

    // ── GetSubaccountBalancesAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetSubaccountBalancesAsync_hits_v2_institutional_subaccounts_balance_with_query()
    {
        var inst = InstitutionalClientReturning("""{"balances":[]}""", out var handler);

        await inst.GetSubaccountBalancesAsync(subaccountId: "sub-1", symbol: "BTC", ct: TestContext.Current.CancellationToken);

        handler.Requests[0].Method.ShouldBe(HttpMethod.Get);
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/institutional/subaccounts/balance");
        var q = handler.Requests[0].RequestUri!.Query;
        q.ShouldContain("subaccountId=sub-1");
        q.ShouldContain("symbol=BTC");
    }

    [Fact]
    public async Task GetSubaccountBalancesAsync_no_filter_passes_empty_query()
    {
        var inst = InstitutionalClientReturning("""{"balances":[]}""", out var handler);

        await inst.GetSubaccountBalancesAsync(ct: TestContext.Current.CancellationToken);

        handler.Requests[0].RequestUri!.Query.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetSubaccountBalancesAsync_maps_balances_array()
    {
        const string json = """
        { "balances": [ { "symbol": "EUR", "available": "1000.0", "inOrder": "0" },
                        { "symbol": "BTC", "available": "0.5", "inOrder": "0.1" } ] }
        """;
        var inst = InstitutionalClientReturning(json, out _);

        var result = await inst.GetSubaccountBalancesAsync(subaccountId: "sub-1", ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Balances.Count.ShouldBe(2);
        result.Data.Balances[0].Symbol.ShouldBe("EUR");
        result.Data.Balances[0].Available.ShouldBe(1000.0m);
        result.Data.Balances[1].Symbol.ShouldBe("BTC");
        result.Data.Balances[1].InOrder.ShouldBe(0.1m);
    }

    // ── GetSubaccountTransactionHistoryAsync ──────────────────────────────────────────────

    [Fact]
    public async Task GetSubaccountTransactionHistoryAsync_hits_v2_institutional_subaccounts_history_with_query()
    {
        var inst = InstitutionalClientReturning("""{"items":[],"currentPage":1,"totalPages":1,"maxItems":100}""", out var handler);
        var from = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc);

        await inst.GetSubaccountTransactionHistoryAsync(subaccountId: "sub-1", fromDate: from, toDate: to, page: 1, maxItems: 50, type: "buy", ct: TestContext.Current.CancellationToken);

        handler.Requests[0].Method.ShouldBe(HttpMethod.Get);
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/institutional/subaccounts/history");
        var q = handler.Requests[0].RequestUri!.Query;
        q.ShouldContain("subaccountId=sub-1");
        q.ShouldContain("fromDate=1704067200000");
        q.ShouldContain("toDate=1704153600000");
        q.ShouldContain("page=1");
        q.ShouldContain("maxItems=50");
        q.ShouldContain("type=buy");
    }

    [Fact]
    public async Task GetSubaccountTransactionHistoryAsync_maps_paginated_response()
    {
        const string json = """
        {
          "items": [ { "transactionId": "tx-1", "executedAt": 1704067200000, "type": "deposit",
                       "receivedCurrency": "EUR", "receivedAmount": "500" } ],
          "currentPage": 1, "totalPages": 2, "maxItems": 100
        }
        """;
        var inst = InstitutionalClientReturning(json, out _);

        var result = await inst.GetSubaccountTransactionHistoryAsync(subaccountId: "sub-1", ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.TotalPages.ShouldBe(2);
        result.Data.Items.Count.ShouldBe(1);
        result.Data.Items[0].TransactionId.ShouldBe("tx-1");
        result.Data.Items[0].Type.ShouldBe("deposit");
        result.Data.Items[0].ReceivedAmount.ShouldBe(500m);
    }

    // ── GetSubaccountOpenOrdersAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetSubaccountOpenOrdersAsync_hits_v2_institutional_subaccounts_orders_open_with_query()
    {
        var inst = InstitutionalClientReturning("[]", out var handler);

        await inst.GetSubaccountOpenOrdersAsync(subaccountId: "sub-1", market: "BTC-EUR", baseAsset: "BTC", ct: TestContext.Current.CancellationToken);

        handler.Requests[0].Method.ShouldBe(HttpMethod.Get);
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/institutional/subaccounts/orders/open");
        var q = handler.Requests[0].RequestUri!.Query;
        q.ShouldContain("subaccountId=sub-1");
        q.ShouldContain("market=BTC-EUR");
        q.ShouldContain("base=BTC");
    }

    [Fact]
    public async Task GetSubaccountOpenOrdersAsync_maps_order_array()
    {
        const string json = """
        [ { "orderId": "o-1", "market": "BTC-EUR", "created": 0, "updated": 0, "status": "new", "side": "buy", "orderType": "limit", "amount": "1", "price": "60000" } ]
        """;
        var inst = InstitutionalClientReturning(json, out _);

        var result = await inst.GetSubaccountOpenOrdersAsync(subaccountId: "sub-1", ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        var orders = result.Data.ToList();
        orders.Count.ShouldBe(1);
        orders[0].OrderId.ShouldBe("o-1");
        orders[0].Status.ShouldBe(OrderStatus.New);
    }

    // ── CancelSubaccountOrderAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task CancelSubaccountOrderAsync_hits_v2_institutional_subaccounts_order_with_DELETE_and_signed_body()
    {
        var inst = InstitutionalClientReturning("""{"orderId":"o-1"}""", out var handler);

        await inst.CancelSubaccountOrderAsync(new BitvavoSubaccountCancelOrderRequest(
            Market: "BTC-EUR", OrderId: "o-1", OperatorId: 5, SubaccountId: "sub-1"), ct: TestContext.Current.CancellationToken);

        handler.Requests[0].Method.ShouldBe(HttpMethod.Delete);
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/institutional/subaccounts/order");
        handler.Requests[0].Headers.GetValues("Bitvavo-Access-Signature").ShouldHaveSingleItem().Length.ShouldBe(64);

        var body = await handler.Requests[0].Content!.ReadAsStringAsync(cancellationToken: TestContext.Current.CancellationToken);
        body.ShouldContain("\"subaccountId\":\"sub-1\"");
        body.ShouldContain("\"market\":\"BTC-EUR\"");
        body.ShouldContain("\"orderId\":\"o-1\"");
        body.ShouldContain("\"operatorId\":5");
    }

    [Fact]
    public async Task CancelSubaccountOrderAsync_maps_response_dto()
    {
        var inst = InstitutionalClientReturning("""{"orderId":"o-99","market":"BTC-EUR"}""", out _);

        var result = await inst.CancelSubaccountOrderAsync(new BitvavoSubaccountCancelOrderRequest(
            Market: "BTC-EUR", OrderId: "o-99", OperatorId: 1), ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.OrderId.ShouldBe("o-99");
    }

    // ── CancelSubaccountOrdersAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task CancelSubaccountOrdersAsync_hits_v2_institutional_subaccounts_orders_with_DELETE_and_signed_body()
    {
        var inst = InstitutionalClientReturning("[]", out var handler);

        await inst.CancelSubaccountOrdersAsync(operatorId: 7, subaccountId: "sub-1", market: "ETH-EUR", ct: TestContext.Current.CancellationToken);

        handler.Requests[0].Method.ShouldBe(HttpMethod.Delete);
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/institutional/subaccounts/orders");
        handler.Requests[0].Headers.GetValues("Bitvavo-Access-Signature").ShouldHaveSingleItem().Length.ShouldBe(64);

        var body = await handler.Requests[0].Content!.ReadAsStringAsync(cancellationToken: TestContext.Current.CancellationToken);
        body.ShouldContain("\"operatorId\":7");
        body.ShouldContain("\"subaccountId\":\"sub-1\"");
        body.ShouldContain("\"market\":\"ETH-EUR\"");
    }

    [Fact]
    public async Task CancelSubaccountOrdersAsync_maps_array_response()
    {
        var inst = InstitutionalClientReturning("""[{"orderId":"a"},{"orderId":"b"}]""", out _);

        var result = await inst.CancelSubaccountOrdersAsync(operatorId: 1, ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        var ids = result.Data.ToList();
        ids.Count.ShouldBe(2);
        ids[0].OrderId.ShouldBe("a");
        ids[1].OrderId.ShouldBe("b");
    }
}
