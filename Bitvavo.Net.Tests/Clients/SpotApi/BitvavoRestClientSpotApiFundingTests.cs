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
/// Unit tests for <see cref="BitvavoRestClientSpotApiFunding"/> — covers the three
/// signed funding endpoints (deposit address, deposit history, withdrawal history).
/// </summary>
public class BitvavoRestClientSpotApiFundingTests
{
    private static BitvavoRestClientSpotApiFunding FundingClientReturning(
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
        return new BitvavoRestClientSpotApiFunding(apiClient);
    }

    // ── GetDepositAddressAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDepositAddressAsync_hits_v2_deposit_with_signature_and_symbol_query()
    {
        var funding = FundingClientReturning("""{"address":"bc1q...","paymentId":null}""", out var handler);

        await funding.GetDepositAddressAsync("BTC");

        handler.Requests[0].Method.ShouldBe(HttpMethod.Get);
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/deposit");
        handler.Requests[0].RequestUri!.Query.ShouldContain("symbol=BTC");
        handler.Requests[0].Headers.GetValues("Bitvavo-Access-Signature").ShouldHaveSingleItem().Length.ShouldBe(64);
    }

    [Fact]
    public async Task GetDepositAddressAsync_maps_crypto_address_response()
    {
        var funding = FundingClientReturning("""{"address":"bc1qxy2kgdygjrsqtzq2n0yrf2493p83kkfjhx0wlh","paymentId":null}""", out _);

        var result = await funding.GetDepositAddressAsync("BTC");

        result.Success.ShouldBeTrue();
        result.Data.Address.ShouldBe("bc1qxy2kgdygjrsqtzq2n0yrf2493p83kkfjhx0wlh");
        result.Data.PaymentReference.ShouldBeNull();
        result.Data.Iban.ShouldBeNull();
        result.Data.Bic.ShouldBeNull();
    }

    [Fact]
    public async Task GetDepositAddressAsync_maps_fiat_iban_response()
    {
        var funding = FundingClientReturning("""{"iban":"NL12BITV1234567890","bic":"BITVNL2A","paymentId":"REF-123"}""", out _);

        var result = await funding.GetDepositAddressAsync("EUR");

        result.Success.ShouldBeTrue();
        result.Data.Iban.ShouldBe("NL12BITV1234567890");
        result.Data.Bic.ShouldBe("BITVNL2A");
        result.Data.PaymentReference.ShouldBe("REF-123");
        result.Data.Address.ShouldBeNull();
    }

    // ── GetDepositHistoryAsync ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetDepositHistoryAsync_hits_v2_depositHistory_with_optional_params()
    {
        var funding = FundingClientReturning("[]", out var handler);
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc);

        await funding.GetDepositHistoryAsync(symbol: "EUR", limit: 50, startTime: start, endTime: end);

        handler.Requests[0].Method.ShouldBe(HttpMethod.Get);
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/depositHistory");
        var q = handler.Requests[0].RequestUri!.Query;
        q.ShouldContain("symbol=EUR");
        q.ShouldContain("limit=50");
        q.ShouldContain("start=1704067200000");
        q.ShouldContain("end=1704153600000");
    }

    [Fact]
    public async Task GetDepositHistoryAsync_no_filter_passes_empty_query()
    {
        var funding = FundingClientReturning("[]", out var handler);

        await funding.GetDepositHistoryAsync();

        handler.Requests[0].RequestUri!.Query.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetDepositHistoryAsync_maps_array_response()
    {
        const string json = """
        [
          { "timestamp": 1704067200000, "symbol": "BTC", "amount": "0.05", "address": "bc1q...", "txId": "abc...", "fee": "0", "status": "completed" }
        ]
        """;
        var funding = FundingClientReturning(json, out _);

        var result = await funding.GetDepositHistoryAsync();

        result.Success.ShouldBeTrue();
        var entries = result.Data.ToList();
        entries.Count.ShouldBe(1);
        entries[0].Symbol.ShouldBe("BTC");
        entries[0].Amount.ShouldBe(0.05m);
        entries[0].Address.ShouldBe("bc1q...");
        entries[0].TxId.ShouldBe("abc...");
        entries[0].Fee.ShouldBe(0m);
        entries[0].Status.ShouldBe("completed");
    }

    // ── GetWithdrawalHistoryAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetWithdrawalHistoryAsync_hits_v2_withdrawalHistory_with_optional_params()
    {
        var funding = FundingClientReturning("[]", out var handler);
        var start = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        await funding.GetWithdrawalHistoryAsync(symbol: "BTC", limit: 25, startTime: start);

        handler.Requests[0].Method.ShouldBe(HttpMethod.Get);
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/withdrawalHistory");
        var q = handler.Requests[0].RequestUri!.Query;
        q.ShouldContain("symbol=BTC");
        q.ShouldContain("limit=25");
        q.ShouldContain("start=1717200000000");
    }

    [Fact]
    public async Task GetWithdrawalHistoryAsync_maps_array_response()
    {
        const string json = """
        [
          { "timestamp": 1717200000000, "symbol": "EUR", "amount": "100.0", "fee": "0.5", "status": "completed" }
        ]
        """;
        var funding = FundingClientReturning(json, out _);

        var result = await funding.GetWithdrawalHistoryAsync();

        result.Success.ShouldBeTrue();
        var entries = result.Data.ToList();
        entries.Count.ShouldBe(1);
        entries[0].Symbol.ShouldBe("EUR");
        entries[0].Amount.ShouldBe(100.0m);
        entries[0].Fee.ShouldBe(0.5m);
        entries[0].Status.ShouldBe("completed");
    }

    // ── WithdrawAsync ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task WithdrawAsync_hits_v2_withdrawal_with_POST_and_signed_body()
    {
        var funding = FundingClientReturning("""{"success":true,"symbol":"BTC","amount":"0.001"}""", out var handler);

        await funding.WithdrawAsync(new Bitvavo.Net.Objects.Models.Spot.BitvavoWithdrawRequest(
            Symbol: "BTC", Amount: 0.001m, Address: "bc1qxy2kgdygjrsqtzq2n0yrf2493p83kkfjhx0wlh"));

        handler.Requests[0].Method.ShouldBe(HttpMethod.Post);
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/withdrawal");
        handler.Requests[0].Headers.GetValues("Bitvavo-Access-Signature").ShouldHaveSingleItem().Length.ShouldBe(64);

        var body = await handler.Requests[0].Content!.ReadAsStringAsync();
        body.ShouldContain("\"symbol\":\"BTC\"");
        body.ShouldContain("\"amount\":\"0.001\"");
        body.ShouldContain("\"address\":\"bc1qxy2kgdygjrsqtzq2n0yrf2493p83kkfjhx0wlh\"");
    }

    [Fact]
    public async Task WithdrawAsync_passes_optional_paymentId_and_addWithdrawalFee()
    {
        var funding = FundingClientReturning("""{"success":true,"symbol":"XRP","amount":"50"}""", out var handler);

        await funding.WithdrawAsync(new Bitvavo.Net.Objects.Models.Spot.BitvavoWithdrawRequest(
            Symbol: "XRP", Amount: 50m, Address: "rXY...", PaymentId: "12345", AddWithdrawalFee: true));

        var body = await handler.Requests[0].Content!.ReadAsStringAsync();
        body.ShouldContain("\"paymentId\":\"12345\"");
        body.ShouldContain("\"addWithdrawalFee\":true");
    }

    [Fact]
    public async Task WithdrawAsync_maps_response_dto()
    {
        var funding = FundingClientReturning("""{"success":true,"symbol":"BTC","amount":"0.0012"}""", out _);

        var result = await funding.WithdrawAsync(new Bitvavo.Net.Objects.Models.Spot.BitvavoWithdrawRequest(
            Symbol: "BTC", Amount: 0.001m, Address: "bc1q..."));

        result.Success.ShouldBeTrue();
        result.Data.Success.ShouldBeTrue();
        result.Data.Symbol.ShouldBe("BTC");
        result.Data.Amount.ShouldBe(0.0012m);
    }
}
