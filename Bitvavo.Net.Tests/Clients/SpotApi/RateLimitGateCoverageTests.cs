// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

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
/// P1-bis rate-limit gate coverage. Every Bitvavo REST endpoint must be scheduled against
/// <see cref="BitvavoRestClientSpotApi.RateLimitGate"/> so the client-side weight budget
/// undercuts Bitvavo's per-IP server limit — except <c>ResetCancelOnDisconnectAsync</c>
/// (<c>v2/cancelOrdersAfter</c>), which the P1-bis mini-council deliberately left ungated so
/// the CoD heartbeat can never be client-throttled.
/// <para>
/// Each gated endpoint is driven through the real CryptoExchange.Net request pipeline (only
/// the HTTP transport is stubbed). A round-trip that produces a successful
/// <c>WebCallResult</c> proves the cached, gate-bound <c>RequestDefinition</c> is well-formed
/// — the gate was threaded without breaking the call. The per-host gate instance is a single
/// <c>internal static readonly</c> field, so every endpoint shares it.
/// </para>
/// </summary>
public class RateLimitGateCoverageTests
{
    private const string TestKey = "test-key";
    private const string TestSecret = "test-secret";

    private static BitvavoRestClientSpotApi ApiReturning(string json)
    {
        var handler = new StubHttpMessageHandler(json, HttpStatusCode.OK);
        var http = new HttpClient(handler);
        var opts = new BitvavoRestOptions
        {
            ApiCredentials = new BitvavoCredentials(TestKey, TestSecret),
        };
        var client = new BitvavoRestClient(http, null, Options.Create(opts));
        return (BitvavoRestClientSpotApi)client.SpotApi;
    }

    // ── ExchangeData — six public endpoints newly gated ───────────────────────────────────

    [Fact]
    public async Task GetServerTimeAsync_GatedEndpoint_RoundTrips()
    {
        var api = ApiReturning("""{"time":1714132800000,"timeNs":1714132800000000000}""");
        var data = new BitvavoRestClientSpotApiExchangeData(api);

        var result = await data.GetServerTimeAsync(TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task GetAssetsAsync_GatedEndpoint_RoundTrips()
    {
        var api = ApiReturning("[]");
        var data = new BitvavoRestClientSpotApiExchangeData(api);

        var result = await data.GetAssetsAsync(ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task GetTickerPricesAsync_GatedEndpoint_RoundTrips()
    {
        var api = ApiReturning("[]");
        var data = new BitvavoRestClientSpotApiExchangeData(api);

        var result = await data.GetTickerPricesAsync(ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task GetTickerBookAsync_GatedEndpoint_RoundTrips()
    {
        var api = ApiReturning("[]");
        var data = new BitvavoRestClientSpotApiExchangeData(api);

        var result = await data.GetTickerBookAsync(ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task GetOrderBookAsync_GatedEndpoint_RoundTrips()
    {
        var api = ApiReturning("""{"market":"ETH-EUR","nonce":1,"bids":[],"asks":[],"timestamp":0}""");
        var data = new BitvavoRestClientSpotApiExchangeData(api);

        var result = await data.GetOrderBookAsync("ETH-EUR", ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task GetPublicTradesAsync_GatedEndpoint_RoundTrips()
    {
        var api = ApiReturning("[]");
        var data = new BitvavoRestClientSpotApiExchangeData(api);

        var result = await data.GetPublicTradesAsync("ETH-EUR", ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
    }

    // ── Account — three signed endpoints newly gated ──────────────────────────────────────

    [Fact]
    public async Task GetAccountInfoAsync_GatedEndpoint_RoundTrips()
    {
        var api = ApiReturning("""{"fees":{"taker":"0.0025","maker":"0.0015","volume":"0"},"capabilities":[]}""");
        var account = new BitvavoRestClientSpotApiAccount(api);

        var result = await account.GetAccountInfoAsync(TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task GetBalancesAsync_GatedEndpoint_RoundTrips()
    {
        var api = ApiReturning("[]");
        var account = new BitvavoRestClientSpotApiAccount(api);

        var result = await account.GetBalancesAsync(ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task GetTradingFeesAsync_GatedEndpoint_RoundTrips()
    {
        var api = ApiReturning("""{"taker":"0.0025","maker":"0.0015","volume":"0"}""");
        var account = new BitvavoRestClientSpotApiAccount(api);

        var result = await account.GetTradingFeesAsync(ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task GetTransactionHistoryAsync_GatedEndpoint_RoundTrips()
    {
        var api = ApiReturning("""{"items":[],"currentPage":1,"totalPages":1,"maxItems":25}""");
        var account = new BitvavoRestClientSpotApiAccount(api);

        var result = await account.GetTransactionHistoryAsync(ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
    }

    // ── Account — CoD endpoint deliberately left UNGATED (council exception) ───────────────

    [Fact]
    public async Task ResetCancelOnDisconnectAsync_UngatedCoDEndpoint_StillRoundTrips()
    {
        // The CoD heartbeat must never be client-side rate-limited — the P1-bis mini-council
        // left v2/cancelOrdersAfter on the 3-arg GetOrCreate (no gate). The endpoint must still
        // function exactly like a gated one; this round-trip pins that.
        var api = ApiReturning("""{"expiresAt":1714132830000}""");
        var account = new BitvavoRestClientSpotApiAccount(api);

        var result = await account.ResetCancelOnDisconnectAsync(
            codGroupId: 1, expiryAfterSeconds: 30, TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
    }

    // ── Trading — six endpoints newly gated (PlaceOrderAsync was already gated) ────────────

    [Fact]
    public async Task UpdateOrderAsync_GatedEndpoint_RoundTrips()
    {
        var api = ApiReturning("""{"orderId":"x","market":"ETH-EUR","status":"new","side":"buy","orderType":"limit","created":0,"updated":0}""");
        var trading = new BitvavoRestClientSpotApiTrading(api);

        var result = await trading.UpdateOrderAsync(
            new BitvavoUpdateOrderRequest("ETH-EUR", OperatorId: 1, OrderId: "x", Price: 1500m),
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task GetOrderAsync_GatedEndpoint_RoundTrips()
    {
        var api = ApiReturning("""{"orderId":"x","market":"ETH-EUR","status":"new","side":"buy","orderType":"limit","created":0,"updated":0}""");
        var trading = new BitvavoRestClientSpotApiTrading(api);

        var result = await trading.GetOrderAsync("ETH-EUR", orderId: "x", ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task CancelOrderAsync_GatedInUriEndpoint_RoundTrips()
    {
        var api = ApiReturning("""{"orderId":"x","market":"ETH-EUR"}""");
        var trading = new BitvavoRestClientSpotApiTrading(api);

        var result = await trading.CancelOrderAsync(
            "ETH-EUR", operatorId: 1, orderId: "x", ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("ETH-EUR")]
    public async Task CancelOrdersAsync_GatedInUriEndpoint_RoundTripsForBothWeightBranches(string? market)
    {
        var api = ApiReturning("[]");
        var trading = new BitvavoRestClientSpotApiTrading(api);

        var result = await trading.CancelOrdersAsync(
            operatorId: 1, market: market, ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("ETH-EUR")]
    public async Task GetOpenOrdersAsync_GatedEndpoint_RoundTripsForBothWeightBranches(string? market)
    {
        var api = ApiReturning("[]");
        var trading = new BitvavoRestClientSpotApiTrading(api);

        var result = await trading.GetOpenOrdersAsync(market: market, ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task GetOrderHistoryAsync_GatedEndpoint_RoundTrips()
    {
        var api = ApiReturning("[]");
        var trading = new BitvavoRestClientSpotApiTrading(api);

        var result = await trading.GetOrderHistoryAsync("ETH-EUR", ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task GetUserTradesAsync_GatedEndpoint_RoundTrips()
    {
        var api = ApiReturning("[]");
        var trading = new BitvavoRestClientSpotApiTrading(api);

        var result = await trading.GetUserTradesAsync("ETH-EUR", ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
    }

    // ── Funding — four signed endpoints newly gated ───────────────────────────────────────

    [Fact]
    public async Task GetDepositAddressAsync_GatedEndpoint_RoundTrips()
    {
        var api = ApiReturning("""{"address":"bc1q...","paymentId":null}""");
        var funding = new BitvavoRestClientSpotApiFunding(api);

        var result = await funding.GetDepositAddressAsync("BTC", TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task GetDepositHistoryAsync_GatedEndpoint_RoundTrips()
    {
        var api = ApiReturning("[]");
        var funding = new BitvavoRestClientSpotApiFunding(api);

        var result = await funding.GetDepositHistoryAsync(ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task GetWithdrawalHistoryAsync_GatedEndpoint_RoundTrips()
    {
        var api = ApiReturning("[]");
        var funding = new BitvavoRestClientSpotApiFunding(api);

        var result = await funding.GetWithdrawalHistoryAsync(ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task WithdrawAsync_GatedEndpoint_RoundTrips()
    {
        var api = ApiReturning("""{"success":true,"symbol":"BTC","amount":"0.001"}""");
        var funding = new BitvavoRestClientSpotApiFunding(api);

        var result = await funding.WithdrawAsync(
            new BitvavoWithdrawRequest("BTC", 0.001m, "bc1q..."), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
    }

    // ── Report — two signed endpoints newly gated ─────────────────────────────────────────

    [Fact]
    public async Task GetTradesReportAsync_GatedEndpoint_RoundTrips()
    {
        var api = ApiReturning("[]");
        var report = new BitvavoRestClientSpotApiReport(api);

        var result = await report.GetTradesReportAsync("ETH-EUR", ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task GetBookReportAsync_GatedEndpoint_RoundTrips()
    {
        const string json = """
        {"submissionTimestamp":0,"assetCode":"BTC","assetName":"Bitcoin","priceCurrency":"EUR",
         "priceNotation":"MONE","quantityCurrency":"BTC","quantityNotation":"CRYP","venue":"VAVO",
         "tradingSystem":"VAVO","publicationTimestamp":0,"bids":[],"asks":[]}
        """;
        var api = ApiReturning(json);
        var report = new BitvavoRestClientSpotApiReport(api);

        var result = await report.GetBookReportAsync("ETH-EUR", ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
    }

    // ── Institutional — ten signed endpoints newly gated ──────────────────────────────────

    [Fact]
    public async Task CreateSubaccountAsync_GatedEndpoint_RoundTrips()
    {
        var api = ApiReturning("""{"id":"sub-1","type":"spot","status":"open","label":"A"}""");
        var inst = new BitvavoRestClientSpotApiInstitutional(api);

        var result = await inst.CreateSubaccountAsync(ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task GetSubaccountsAsync_GatedEndpoint_RoundTrips()
    {
        var api = ApiReturning("""{"items":[],"currentPage":1,"totalPages":1,"maxItems":50}""");
        var inst = new BitvavoRestClientSpotApiInstitutional(api);

        var result = await inst.GetSubaccountsAsync(ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateTransferAsync_GatedEndpoint_RoundTrips()
    {
        var api = ApiReturning("""{"transferId":"t-1","subaccountId":"sub-1","direction":"masterToSub","symbol":"EUR","amount":"100","status":"pending","createdAt":1717200000000}""");
        var inst = new BitvavoRestClientSpotApiInstitutional(api);

        var result = await inst.CreateTransferAsync(
            new BitvavoCreateTransferRequest("sub-1", SubaccountTransferDirection.MasterToSub, "EUR", 100m),
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task GetTransferAsync_GatedEndpoint_RoundTrips()
    {
        var api = ApiReturning("""{"transferId":"t-1","subaccountId":"sub-1","direction":"masterToSub","symbol":"EUR","amount":"100","status":"completed","createdAt":1717200000000}""");
        var inst = new BitvavoRestClientSpotApiInstitutional(api);

        var result = await inst.GetTransferAsync("t-1", TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task GetTransfersAsync_GatedEndpoint_RoundTrips()
    {
        var api = ApiReturning("""{"items":[],"start":0,"end":0,"limit":25}""");
        var inst = new BitvavoRestClientSpotApiInstitutional(api);

        var result = await inst.GetTransfersAsync("sub-1", ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task GetSubaccountBalancesAsync_GatedEndpoint_RoundTrips()
    {
        var api = ApiReturning("""{"balances":[]}""");
        var inst = new BitvavoRestClientSpotApiInstitutional(api);

        var result = await inst.GetSubaccountBalancesAsync(ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task GetSubaccountTransactionHistoryAsync_GatedEndpoint_RoundTrips()
    {
        var api = ApiReturning("""{"items":[],"currentPage":1,"totalPages":1,"maxItems":25}""");
        var inst = new BitvavoRestClientSpotApiInstitutional(api);

        var result = await inst.GetSubaccountTransactionHistoryAsync(ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("ETH-EUR")]
    public async Task GetSubaccountOpenOrdersAsync_GatedEndpoint_RoundTripsForBothWeightBranches(string? market)
    {
        var api = ApiReturning("[]");
        var inst = new BitvavoRestClientSpotApiInstitutional(api);

        var result = await inst.GetSubaccountOpenOrdersAsync(market: market, ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task CancelSubaccountOrderAsync_GatedEndpoint_RoundTrips()
    {
        var api = ApiReturning("""{"orderId":"o-1"}""");
        var inst = new BitvavoRestClientSpotApiInstitutional(api);

        var result = await inst.CancelSubaccountOrderAsync(
            new BitvavoSubaccountCancelOrderRequest("ETH-EUR", "o-1", OperatorId: 1, SubaccountId: "sub-1"),
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("ETH-EUR")]
    public async Task CancelSubaccountOrdersAsync_GatedEndpoint_RoundTripsForBothWeightBranches(string? market)
    {
        var api = ApiReturning("[]");
        var inst = new BitvavoRestClientSpotApiInstitutional(api);

        var result = await inst.CancelSubaccountOrdersAsync(
            operatorId: 1, market: market, ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
    }
}
