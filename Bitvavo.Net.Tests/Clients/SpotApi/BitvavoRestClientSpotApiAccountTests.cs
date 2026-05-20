// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Bitvavo.Net.Clients;
using Bitvavo.Net.Clients.SpotApi;
using Bitvavo.Net.Objects.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests.Clients.SpotApi;

/// <summary>
/// Unit tests for <see cref="BitvavoRestClientSpotApiAccount"/> — covers the three
/// signed account-data endpoints (account info, balances, market fees). Mocks the HTTP
/// pipeline via <see cref="StubHttpMessageHandler"/>; verifies path, method, signed
/// headers, and DTO mapping for each call.
/// </summary>
public class BitvavoRestClientSpotApiAccountTests
{
    private static BitvavoRestClientSpotApiAccount AccountClientReturning(
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
        return new BitvavoRestClientSpotApiAccount(apiClient);
    }

    // ── GetAccountInfoAsync ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAccountInfoAsync_hits_v2_account_with_signature_header()
    {
        var account = AccountClientReturning("""{"fees":{"taker":"0.0025","maker":"0.0015","volume":"0"},"capabilities":["buy","sell"]}""", out var handler);

        var result = await account.GetAccountInfoAsync(ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        handler.Requests.Count.ShouldBe(1);
        handler.Requests[0].Method.ShouldBe(HttpMethod.Get);
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/account");
        handler.Requests[0].Headers.GetValues("Bitvavo-Access-Signature").ShouldHaveSingleItem().Length.ShouldBe(64);
        handler.Requests[0].Headers.GetValues("Bitvavo-Access-Key").ShouldHaveSingleItem().ShouldBe("test-key");
    }

    [Fact]
    public async Task GetAccountInfoAsync_maps_fees_and_capabilities()
    {
        const string json = """
        {
          "fees": { "taker": "0.0025", "maker": "0.0015", "volume": "1234.5" },
          "capabilities": ["buy","sell","view","withdraw"]
        }
        """;
        var account = AccountClientReturning(json, out _);

        var result = await account.GetAccountInfoAsync(ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Fees.Taker.ShouldBe(0.0025m);
        result.Data.Fees.Maker.ShouldBe(0.0015m);
        result.Data.Fees.Volume.ShouldBe(1234.5m);
        result.Data.Capabilities.ShouldBe(new[] { "buy", "sell", "view", "withdraw" });
    }

    // ── GetBalancesAsync ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetBalancesAsync_no_filter_hits_v2_balance_with_no_query()
    {
        var account = AccountClientReturning("[]", out var handler);

        var result = await account.GetBalancesAsync(ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/balance");
        handler.Requests[0].RequestUri!.Query.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetBalancesAsync_with_symbol_passes_symbol_query()
    {
        var account = AccountClientReturning("[]", out var handler);

        await account.GetBalancesAsync(symbol: "BTC", ct: TestContext.Current.CancellationToken);

        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/balance");
        handler.Requests[0].RequestUri!.Query.ShouldContain("symbol=BTC");
    }

    [Fact]
    public async Task GetBalancesAsync_maps_array_response()
    {
        const string json = """
        [
          { "symbol": "EUR", "available": "1234.5", "inOrder": "0" },
          { "symbol": "BTC", "available": "0.05", "inOrder": "0.01" }
        ]
        """;
        var account = AccountClientReturning(json, out _);

        var result = await account.GetBalancesAsync(ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        var list = result.Data.ToList();
        list.Count.ShouldBe(2);
        list[0].Symbol.ShouldBe("EUR");
        list[0].Available.ShouldBe(1234.5m);
        list[0].InOrder.ShouldBe(0m);
        list[1].Symbol.ShouldBe("BTC");
        list[1].Available.ShouldBe(0.05m);
        list[1].InOrder.ShouldBe(0.01m);
    }

    // ── GetTradingFeesAsync ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTradingFeesAsync_no_filter_hits_v2_account_fees_with_no_query()
    {
        var account = AccountClientReturning("""{"tier":"0","volume":"0","taker":"0.0025","maker":"0.0015"}""", out var handler);

        var result = await account.GetTradingFeesAsync(ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/account/fees");
        handler.Requests[0].RequestUri!.Query.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetTradingFeesAsync_with_market_passes_market_query()
    {
        var account = AccountClientReturning("""{"tier":"0","volume":"0","taker":"0.0025","maker":"0.0015"}""", out var handler);

        await account.GetTradingFeesAsync(market: "ETH-EUR", ct: TestContext.Current.CancellationToken);

        handler.Requests[0].RequestUri!.Query.ShouldContain("market=ETH-EUR");
    }

    [Fact]
    public async Task GetTradingFeesAsync_maps_tier_volume_taker_maker()
    {
        const string json = """{ "tier": "2", "volume": "5000.5", "taker": "0.0020", "maker": "0.0010" }""";
        var account = AccountClientReturning(json, out _);

        var result = await account.GetTradingFeesAsync(ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Tier.ShouldBe("2");
        result.Data.Volume.ShouldBe(5000.5m);
        result.Data.Taker.ShouldBe(0.0020m);
        result.Data.Maker.ShouldBe(0.0010m);
    }

    // ── ResetCancelOnDisconnectAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task ResetCancelOnDisconnectAsync_hits_v2_cancelOrdersAfter_with_POST_and_numeric_codGroupId_body()
    {
        var account = AccountClientReturning("""{"codGroupId":7,"timeOfExpirySeconds":1717200000}""", out var handler);

        await account.ResetCancelOnDisconnectAsync(codGroupId: 7, expiryAfterSeconds: 30, ct: TestContext.Current.CancellationToken);

        handler.Requests[0].Method.ShouldBe(HttpMethod.Post);
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/cancelOrdersAfter");
        handler.Requests[0].Headers.GetValues("Bitvavo-Access-Signature").ShouldHaveSingleItem().Length.ShouldBe(64);

        var body = await handler.Requests[0].Content!.ReadAsStringAsync(cancellationToken: TestContext.Current.CancellationToken);
        body.ShouldContain("\"codGroupId\":7");
        body.ShouldContain("\"expiryAfterSeconds\":30");
    }

    [Fact]
    public async Task ResetCancelOnDisconnectAsync_maps_numeric_codGroupId_and_timeOfExpirySeconds()
    {
        // Bitvavo's cancelOrdersAfter response field is timeOfExpirySeconds (Unix SECONDS), not
        // expiresAt — the SDK mapped the wrong name so the deadline silently deserialized to 0.
        var account = AccountClientReturning("""{"codGroupId":42,"timeOfExpirySeconds":1717209999}""", out _);

        var result = await account.ResetCancelOnDisconnectAsync(codGroupId: 42, expiryAfterSeconds: 60, ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.CodGroupId.ShouldBe(42);
        result.Data.TimeOfExpirySeconds.ShouldBe(1717209999L);
    }

    // ── GetTransactionHistoryAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTransactionHistoryAsync_hits_v2_account_history_with_optional_params()
    {
        var account = AccountClientReturning("""{"items":[],"currentPage":1,"totalPages":1,"maxItems":100}""", out var handler);
        var from = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc);

        await account.GetTransactionHistoryAsync(fromDate: from, toDate: to, page: 2, maxItems: 50, type: "deposit", ct: TestContext.Current.CancellationToken);

        handler.Requests[0].Method.ShouldBe(HttpMethod.Get);
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/account/history");
        var q = handler.Requests[0].RequestUri!.Query;
        q.ShouldContain("fromDate=1704067200000");
        q.ShouldContain("toDate=1704153600000");
        q.ShouldContain("page=2");
        q.ShouldContain("maxItems=50");
        q.ShouldContain("type=deposit");
        handler.Requests[0].Headers.GetValues("Bitvavo-Access-Signature").ShouldHaveSingleItem().Length.ShouldBe(64);
    }

    [Fact]
    public async Task GetTransactionHistoryAsync_no_filter_passes_empty_query()
    {
        var account = AccountClientReturning("""{"items":[],"currentPage":1,"totalPages":1,"maxItems":100}""", out var handler);

        await account.GetTransactionHistoryAsync(ct: TestContext.Current.CancellationToken);

        handler.Requests[0].RequestUri!.Query.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetTransactionHistoryAsync_maps_paginated_response()
    {
        const string json = """
        {
          "items": [
            {
              "transactionId": "tx-1", "executedAt": 1704067200000, "type": "buy",
              "priceCurrency": "EUR", "priceAmount": "1500.0",
              "sentCurrency": "EUR", "sentAmount": "750.0",
              "receivedCurrency": "ETH", "receivedAmount": "0.5",
              "feesCurrency": "EUR", "feesAmount": "1.875", "address": null
            }
          ],
          "currentPage": 1, "totalPages": 3, "maxItems": 100
        }
        """;
        var account = AccountClientReturning(json, out _);

        var result = await account.GetTransactionHistoryAsync(ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.CurrentPage.ShouldBe(1);
        result.Data.TotalPages.ShouldBe(3);
        result.Data.MaxItems.ShouldBe(100);
        result.Data.Items.Count.ShouldBe(1);
        var entry = result.Data.Items[0];
        entry.TransactionId.ShouldBe("tx-1");
        entry.Type.ShouldBe("buy");
        entry.PriceAmount.ShouldBe(1500.0m);
        entry.SentCurrency.ShouldBe("EUR");
        entry.SentAmount.ShouldBe(750.0m);
        entry.ReceivedCurrency.ShouldBe("ETH");
        entry.ReceivedAmount.ShouldBe(0.5m);
        entry.FeesAmount.ShouldBe(1.875m);
        entry.Address.ShouldBeNull();
    }
}
