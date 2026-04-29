// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

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

        var result = await account.GetAccountInfoAsync();

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

        var result = await account.GetAccountInfoAsync();

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

        var result = await account.GetBalancesAsync();

        result.Success.ShouldBeTrue();
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/balance");
        handler.Requests[0].RequestUri!.Query.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetBalancesAsync_with_symbol_passes_symbol_query()
    {
        var account = AccountClientReturning("[]", out var handler);

        await account.GetBalancesAsync(symbol: "BTC");

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

        var result = await account.GetBalancesAsync();

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

        var result = await account.GetTradingFeesAsync();

        result.Success.ShouldBeTrue();
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/account/fees");
        handler.Requests[0].RequestUri!.Query.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetTradingFeesAsync_with_market_passes_market_query()
    {
        var account = AccountClientReturning("""{"tier":"0","volume":"0","taker":"0.0025","maker":"0.0015"}""", out var handler);

        await account.GetTradingFeesAsync(market: "ETH-EUR");

        handler.Requests[0].RequestUri!.Query.ShouldContain("market=ETH-EUR");
    }

    [Fact]
    public async Task GetTradingFeesAsync_maps_tier_volume_taker_maker()
    {
        const string json = """{ "tier": "2", "volume": "5000.5", "taker": "0.0020", "maker": "0.0010" }""";
        var account = AccountClientReturning(json, out _);

        var result = await account.GetTradingFeesAsync();

        result.Success.ShouldBeTrue();
        result.Data.Tier.ShouldBe("2");
        result.Data.Volume.ShouldBe(5000.5m);
        result.Data.Taker.ShouldBe(0.0020m);
        result.Data.Maker.ShouldBe(0.0010m);
    }
}
