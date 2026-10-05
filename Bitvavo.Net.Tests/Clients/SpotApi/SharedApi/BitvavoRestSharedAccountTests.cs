// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Bitvavo.Net.Clients.SpotApi;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Errors;
using CryptoExchange.Net.SharedApis;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests.Clients.SpotApi.SharedApi;

/// <summary>
/// The Shared account capabilities on the REST API: balances, fees and the ledger ([V2] <c>IGetBalancesRest</c>, <c>IGetFeesRest</c>
/// and <c>IGetLedgerRest</c>; the legacy [V1] <c>IBalanceRestClient</c> and <c>IFeeRestClient</c> on the same instance), through the
/// real request pipeline. All three are signed calls: without credentials nothing is sent.
/// </summary>
public class BitvavoRestSharedAccountTests
{
    private const string Balances = """[{"symbol":"EUR","available":"1000","inOrder":"250"},{"symbol":"ETH","available":"2","inOrder":"0"}]""";

    private const string FeeTier = """{"tier":"0","volume":"0","taker":"0.0025","maker":"0.0015"}""";

    /// <summary>Page 1 of 2: a buy (two balances move: EUR out, BTC in) and a deposit (EUR in).</summary>
    private const string LedgerFirstPage = """
        {"items":[
          {"transactionId":"tx-buy","executedAt":"2026-10-05T12:00:00.000Z","type":"buy","priceCurrency":"EUR","priceAmount":"100.00","sentCurrency":"EUR","sentAmount":"100.00","receivedCurrency":"BTC","receivedAmount":"0.0015","feesCurrency":"EUR","feesAmount":"0.25","address":null},
          {"transactionId":"tx-dep","executedAt":"2026-10-04T08:30:00.000Z","type":"deposit","priceCurrency":"EUR","priceAmount":"500.00","receivedCurrency":"EUR","receivedAmount":"500.00","address":null}
        ],"currentPage":1,"totalPages":2,"maxItems":100}
        """;

    /// <summary>Page 2 of 2: a withdrawal (EUR out).</summary>
    private const string LedgerLastPage = """
        {"items":[
          {"transactionId":"tx-wd","executedAt":"2026-10-03T10:00:00.000Z","type":"withdrawal","priceCurrency":"EUR","priceAmount":"50.00","sentCurrency":"EUR","sentAmount":"50.00","feesCurrency":"EUR","feesAmount":"0.5","address":"NL89BANK0123456789"}
        ],"currentPage":2,"totalPages":2,"maxItems":100}
        """;

    /// <summary>One trade on the only page.</summary>
    private const string LedgerSingleTrade = """
        {"items":[
          {"transactionId":"tx-sell","executedAt":"2026-10-05T12:00:00.000Z","type":"sell","priceCurrency":"EUR","priceAmount":"60.00","sentCurrency":"BTC","sentAmount":"0.001","receivedCurrency":"EUR","receivedAmount":"59.85","feesCurrency":"EUR","feesAmount":"0.15","address":null}
        ],"currentPage":1,"totalPages":1,"maxItems":1}
        """;

    private static SharedSymbol EthEur => new(TradingMode.Spot, "ETH", "EUR");

    private static HttpResponseMessage Json(string body)
        => new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    /// <summary>A handler that answers every request the way Bitvavo answers a bad signature: HTTP 403 with error code 309.</summary>
    private static StubHttpMessageHandler BadSignature()
        => new("""{"errorCode":309,"error":"The signature is invalid."}""", HttpStatusCode.Forbidden);

    private static void ShouldBeTheBadSignatureError(Error? error)
    {
        var serverError = error.ShouldBeOfType<ServerError>();
        serverError.ErrorType.ShouldBe(ErrorType.Unauthorized);
        serverError.Code.ShouldBe(309);
    }

    // ── balances ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Balances_are_mapped_and_the_total_is_available_plus_in_order()
    {
        var handler = new StubHttpMessageHandler(Balances);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.GetBalancesAsync(new GetBalancesRequest(SharedAccountType.Spot), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Length.ShouldBe(2);
        var eur = result.Data[0];
        eur.Asset.ShouldBe("EUR");
        eur.Available.ShouldBe(1000m);
        eur.Total.ShouldBe(1250m);
        eur.TradingModes.ShouldBe(new[] { TradingMode.Spot });
        var eth = result.Data[1];
        eth.Asset.ShouldBe("ETH");
        eth.Available.ShouldBe(2m);
        eth.Total.ShouldBe(2m);
    }

    [Fact]
    public async Task A_balance_without_an_available_or_an_in_order_amount_counts_the_missing_one_as_zero()
    {
        var api = new StubHttpMessageHandler("""[{"symbol":"BTC","available":"0.5"},{"symbol":"XRP","inOrder":"7"}]""").RestClient().SpotApi.SharedApi;

        var result = await api.GetBalancesAsync(new GetBalancesRequest(), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data[0].Available.ShouldBe(0.5m);
        result.Data[0].Total.ShouldBe(0.5m);
        result.Data[1].Available.ShouldBe(0m);
        result.Data[1].Total.ShouldBe(7m);
    }

    [Fact]
    public async Task Balances_are_requested_for_every_asset_and_the_call_is_signed()
    {
        var handler = new StubHttpMessageHandler(Balances);
        var api = handler.RestClient().SpotApi.SharedApi;

        await api.GetBalancesAsync(new GetBalancesRequest(), TestContext.Current.CancellationToken);

        var request = handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Get);
        request.RequestUri!.PathAndQuery.ShouldBe("/v2/balance");
        request.Headers.GetValues("Bitvavo-Access-Signature").ShouldHaveSingleItem().Length.ShouldBe(64);
    }

    [Fact]
    public async Task A_balance_request_for_an_account_type_Bitvavo_does_not_have_is_rejected_before_anything_is_sent()
    {
        var handler = new StubHttpMessageHandler(Balances);
        var api = handler.RestClient().SpotApi.SharedApi;

        var funding = await api.GetBalancesAsync(new GetBalancesRequest(SharedAccountType.Funding), TestContext.Current.CancellationToken);
        var margin = await api.GetBalancesAsync(new GetBalancesRequest(SharedAccountType.CrossMargin), TestContext.Current.CancellationToken);
        var futures = await api.GetBalancesAsync(new GetBalancesRequest(TradingMode.PerpetualLinear), TestContext.Current.CancellationToken);

        funding.Error.ShouldBeOfType<ArgumentError>();
        margin.Error.ShouldBeOfType<ArgumentError>();
        futures.Error.ShouldBeOfType<ArgumentError>();
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Balances_without_credentials_fail_with_NoApiCredentialsError_and_send_nothing()
    {
        var handler = new StubHttpMessageHandler(Balances);
        var api = handler.RestClient(withCredentials: false).SpotApi.SharedApi;

        var result = await api.GetBalancesAsync(new GetBalancesRequest(), TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.Error.ShouldBeOfType<NoApiCredentialsError>();
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_failed_balance_call_passes_the_server_error_through()
    {
        var api = BadSignature().RestClient().SpotApi.SharedApi;

        var result = await api.GetBalancesAsync(new GetBalancesRequest(), TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        ShouldBeTheBadSignatureError(result.Error);
    }

    [Fact]
    public void The_balance_options_offer_the_one_spot_account_and_need_credentials()
    {
        var options = new StubHttpMessageHandler("[]").RestClient().SpotApi.SharedApi.GetBalancesOptions;

        options.NeedsAuthentication.ShouldBeTrue();
        options.SupportedAccountTypes.ShouldBe(new[] { AccountTypeFilter.Spot });
        options.IsValid(SharedAccountType.Spot).ShouldBeTrue();
        options.IsValid(SharedAccountType.Funding).ShouldBeFalse();
    }

    [Fact]
    public async Task The_legacy_interface_serves_balances()
    {
        var legacy = new StubHttpMessageHandler(Balances).RestClient().SpotApi.SharedClient;

        var result = await legacy.GetBalancesAsync(new GetBalancesRequest(SharedAccountType.Spot), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data[0].Total.ShouldBe(1250m);
    }

    [Fact]
    public void The_legacy_balance_options_are_the_V2_options_object()
    {
        var client = new StubHttpMessageHandler("[]").RestClient();

        client.SpotApi.SharedClient.GetBalancesOptions.ShouldBeSameAs(client.SpotApi.SharedApi.GetBalancesOptions);
    }

    // ── fees ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Fee_rates_are_percentages_Bitvavo_reports_fractions()
    {
        var handler = new StubHttpMessageHandler(FeeTier);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.GetFeesAsync(new GetFeeRequest(EthEur), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.MakerFee.ShouldBe(0.15m);
        result.Data.TakerFee.ShouldBe(0.25m);
    }

    [Fact]
    public async Task The_fee_request_names_the_market_and_is_signed()
    {
        var handler = new StubHttpMessageHandler(FeeTier);
        var api = handler.RestClient().SpotApi.SharedApi;

        await api.GetFeesAsync(new GetFeeRequest(EthEur), TestContext.Current.CancellationToken);

        var request = handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Get);
        request.RequestUri!.PathAndQuery.ShouldBe("/v2/account/fees?market=ETH-EUR");
        request.Headers.GetValues("Bitvavo-Access-Signature").ShouldHaveSingleItem().Length.ShouldBe(64);
    }

    [Fact]
    public async Task A_fee_response_without_rates_maps_to_zero_fees()
    {
        var api = new StubHttpMessageHandler("""{"tier":"0"}""").RestClient().SpotApi.SharedApi;

        var result = await api.GetFeesAsync(new GetFeeRequest(EthEur), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.MakerFee.ShouldBe(0m);
        result.Data.TakerFee.ShouldBe(0m);
    }

    [Fact]
    public async Task Fees_without_credentials_fail_with_NoApiCredentialsError_and_send_nothing()
    {
        var handler = new StubHttpMessageHandler(FeeTier);
        var api = handler.RestClient(withCredentials: false).SpotApi.SharedApi;

        var result = await api.GetFeesAsync(new GetFeeRequest(EthEur), TestContext.Current.CancellationToken);

        result.Error.ShouldBeOfType<NoApiCredentialsError>();
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_failed_fee_call_passes_the_server_error_through()
    {
        var api = BadSignature().RestClient().SpotApi.SharedApi;

        var result = await api.GetFeesAsync(new GetFeeRequest(EthEur), TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        ShouldBeTheBadSignatureError(result.Error);
    }

    [Fact]
    public async Task The_legacy_interface_serves_fees()
    {
        var legacy = new StubHttpMessageHandler(FeeTier).RestClient().SpotApi.SharedClient;

        var result = await legacy.GetFeesAsync(new GetFeeRequest(EthEur), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.TakerFee.ShouldBe(0.25m);
    }

    [Fact]
    public void The_legacy_fee_options_are_the_V2_options_object_and_need_credentials()
    {
        var client = new StubHttpMessageHandler("{}").RestClient();

        client.SpotApi.SharedClient.GetFeeOptions.ShouldBeSameAs(client.SpotApi.SharedApi.GetFeeOptions);
        client.SpotApi.SharedApi.GetFeeOptions.NeedsAuthentication.ShouldBeTrue();
    }

    // ── ledger ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_trade_is_one_entry_per_balance_leg_with_signed_quantities_and_a_deposit_is_one_entry()
    {
        var api = new StubHttpMessageHandler(LedgerFirstPage).RestClient().SpotApi.SharedApi;

        var result = await api.GetLedgerAsync(new GetLedgerRequest(), ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Length.ShouldBe(3);

        var sent = result.Data[0];
        sent.Id.ShouldBe("tx-buy:sent");
        sent.RelationId.ShouldBe("tx-buy");
        sent.Asset.ShouldBe("EUR");
        sent.DeltaQuantity.ShouldBe(-100m);
        sent.Type.ShouldBe(SharedLedgerEntryType.Trade);
        sent.TypeString.ShouldBe("buy");
        sent.Timestamp.ShouldBe(new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc));

        var received = result.Data[1];
        received.Id.ShouldBe("tx-buy:received");
        received.RelationId.ShouldBe("tx-buy");
        received.Asset.ShouldBe("BTC");
        received.DeltaQuantity.ShouldBe(0.0015m);
        received.Type.ShouldBe(SharedLedgerEntryType.Trade);
        received.TypeString.ShouldBe("buy");
        received.Timestamp.ShouldBe(new DateTime(2026, 10, 5, 12, 0, 0, DateTimeKind.Utc));

        var deposit = result.Data[2];
        deposit.Id.ShouldBe("tx-dep:received");
        deposit.RelationId.ShouldBe("tx-dep");
        deposit.Asset.ShouldBe("EUR");
        deposit.DeltaQuantity.ShouldBe(500m);
        deposit.Type.ShouldBe(SharedLedgerEntryType.Deposit);
        deposit.TypeString.ShouldBe("deposit");
        deposit.Timestamp.ShouldBe(new DateTime(2026, 10, 4, 8, 30, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task The_fee_is_no_entry_of_its_own_because_Bitvavo_does_not_say_whether_the_amounts_include_it()
    {
        var api = new StubHttpMessageHandler(LedgerSingleTrade).RestClient().SpotApi.SharedApi;

        var result = await api.GetLedgerAsync(new GetLedgerRequest(), ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue(result.Error?.ToString());
        result.Data.ShouldAllBe(entry => entry.Type != SharedLedgerEntryType.Fee);
        result.Data.Length.ShouldBe(2);
    }

    [Fact]
    public async Task The_ledger_request_sends_the_time_window_and_the_page_size_and_is_signed()
    {
        var handler = new StubHttpMessageHandler(LedgerFirstPage);
        var api = handler.RestClient().SpotApi.SharedApi;
        var start = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc);

        await api.GetLedgerAsync(new GetLedgerRequest(startTime: start, endTime: end, limit: 50), ct: TestContext.Current.CancellationToken);

        var request = handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Get);
        request.RequestUri!.AbsolutePath.ShouldBe("/v2/account/history");
        request.RequestUri.Query.ShouldContain("fromDate=" + new DateTimeOffset(start).ToUnixTimeMilliseconds());
        request.RequestUri.Query.ShouldContain("toDate=" + new DateTimeOffset(end).ToUnixTimeMilliseconds());
        request.RequestUri.Query.ShouldContain("maxItems=50");
        request.RequestUri.Query.ShouldNotContain("page=");
        request.Headers.GetValues("Bitvavo-Access-Signature").ShouldHaveSingleItem().Length.ShouldBe(64);
    }

    [Fact]
    public async Task A_bare_ledger_request_asks_for_the_first_page_of_100_and_invents_no_bound_or_filter()
    {
        var handler = new StubHttpMessageHandler(LedgerFirstPage);
        var api = handler.RestClient().SpotApi.SharedApi;

        await api.GetLedgerAsync(new GetLedgerRequest(), ct: TestContext.Current.CancellationToken);

        var query = handler.Requests.ShouldHaveSingleItem().RequestUri!.Query;
        query.ShouldContain("maxItems=100");
        query.ShouldNotContain("fromDate=");
        query.ShouldNotContain("toDate=");
        query.ShouldNotContain("page=");
        query.ShouldNotContain("type=");
    }

    [Fact]
    public async Task A_page_that_is_not_the_last_returns_the_request_for_the_next_page_and_the_token_continues_there()
    {
        var handler = new StubHttpMessageHandler(request => Json(request.RequestUri!.Query.Contains("page=2") ? LedgerLastPage : LedgerFirstPage));
        var api = handler.RestClient().SpotApi.SharedApi;
        var request = new GetLedgerRequest(limit: 100);

        var first = await api.GetLedgerAsync(request, ct: TestContext.Current.CancellationToken);

        first.NextPageRequest.ShouldNotBeNull().Page.ShouldBe(2);

        var second = await api.GetLedgerAsync(request, first.NextPageRequest, TestContext.Current.CancellationToken);

        handler.Requests[1].RequestUri!.Query.ShouldContain("page=2");
        second.Data.ShouldHaveSingleItem().Id.ShouldBe("tx-wd:sent");
        second.Data[0].DeltaQuantity.ShouldBe(-50m);
        second.Data[0].Type.ShouldBe(SharedLedgerEntryType.Withdrawal);
        second.NextPageRequest.ShouldBeNull();
    }

    [Fact]
    public async Task The_last_page_has_no_next_page()
    {
        var api = new StubHttpMessageHandler(LedgerLastPage).RestClient().SpotApi.SharedApi;

        var result = await api.GetLedgerAsync(new GetLedgerRequest(), ct: TestContext.Current.CancellationToken);

        result.NextPageRequest.ShouldBeNull();
    }

    [Fact]
    public async Task A_page_token_that_carries_a_window_keeps_it()
    {
        var handler = new StubHttpMessageHandler(LedgerLastPage);
        var api = handler.RestClient().SpotApi.SharedApi;
        var start = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc);
        var token = new PageRequest { Page = 2, StartTime = start, EndTime = end };

        await api.GetLedgerAsync(new GetLedgerRequest(), token, TestContext.Current.CancellationToken);

        var query = handler.Requests.ShouldHaveSingleItem().RequestUri!.Query;
        query.ShouldContain("page=2");
        query.ShouldContain("fromDate=" + new DateTimeOffset(start).ToUnixTimeMilliseconds());
        query.ShouldContain("toDate=" + new DateTimeOffset(end).ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task The_limit_counts_Bitvavo_transactions_so_a_trade_page_of_one_holds_two_entries()
    {
        var handler = new StubHttpMessageHandler(LedgerSingleTrade);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.GetLedgerAsync(new GetLedgerRequest(limit: 1), ct: TestContext.Current.CancellationToken);

        handler.Requests.ShouldHaveSingleItem().RequestUri!.Query.ShouldContain("maxItems=1");
        result.Success.ShouldBeTrue(result.Error?.ToString());
        result.Data.Length.ShouldBe(2);
        result.Data[0].Asset.ShouldBe("BTC");
        result.Data[0].DeltaQuantity.ShouldBe(-0.001m);
        result.Data[1].Asset.ShouldBe("EUR");
        result.Data[1].DeltaQuantity.ShouldBe(59.85m);
    }

    [Fact]
    public async Task The_asset_filter_is_applied_to_the_fetched_entries_because_Bitvavo_cannot_filter_by_asset()
    {
        var handler = new StubHttpMessageHandler(LedgerFirstPage);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.GetLedgerAsync(new GetLedgerRequest("btc"), ct: TestContext.Current.CancellationToken);

        var entry = result.Data.ShouldHaveSingleItem();
        entry.Asset.ShouldBe("BTC");
        entry.DeltaQuantity.ShouldBe(0.0015m);
        var query = handler.Requests.ShouldHaveSingleItem().RequestUri!.Query;
        query.ShouldNotContain("symbol=");
        query.ShouldNotContain("asset=");
        query.ShouldNotContain("currency=");
        result.NextPageRequest.ShouldNotBeNull();
    }

    [Fact]
    public async Task A_transaction_that_moved_no_balance_has_no_entry_and_a_leg_needs_both_its_currency_and_its_amount()
    {
        const string page = """
            {"items":[
              {"transactionId":"tx-none","executedAt":"2026-10-05T12:00:00.000Z","type":"manually_assigned"},
              {"transactionId":"tx-half","executedAt":"2026-10-05T11:00:00.000Z","type":"sell","sentAmount":"1","receivedCurrency":"EUR"},
              {"transactionId":"tx-ok","executedAt":"2026-10-05T10:00:00.000Z","type":"staking","receivedCurrency":"ADA","receivedAmount":"0.2"}
            ],"currentPage":1,"totalPages":1,"maxItems":100}
            """;
        var api = new StubHttpMessageHandler(page).RestClient().SpotApi.SharedApi;

        var result = await api.GetLedgerAsync(new GetLedgerRequest(), ct: TestContext.Current.CancellationToken);

        var entry = result.Data.ShouldHaveSingleItem();
        entry.Id.ShouldBe("tx-ok:received");
        entry.Asset.ShouldBe("ADA");
        entry.DeltaQuantity.ShouldBe(0.2m);
    }

    [Fact]
    public async Task An_unlisted_transaction_type_is_still_returned_with_its_wire_value_and_the_Unknown_type()
    {
        const string page = """
            {"items":[
              {"transactionId":"tx-new","executedAt":"2026-10-05T12:00:00.000Z","type":"a_type_bitvavo_adds_later","receivedCurrency":"EUR","receivedAmount":"1"}
            ],"currentPage":1,"totalPages":1,"maxItems":100}
            """;
        var api = new StubHttpMessageHandler(page).RestClient().SpotApi.SharedApi;

        var result = await api.GetLedgerAsync(new GetLedgerRequest(), ct: TestContext.Current.CancellationToken);

        var entry = result.Data.ShouldHaveSingleItem();
        entry.Type.ShouldBe(SharedLedgerEntryType.Unknown);
        entry.TypeString.ShouldBe("a_type_bitvavo_adds_later");
    }

    /// <summary>The fifteen types the documentation lists (docs.bitvavo.com, GET /account/history); the reason for each Shared type is written at the mapping.</summary>
    [Theory]
    [InlineData("buy", SharedLedgerEntryType.Trade)]
    [InlineData("sell", SharedLedgerEntryType.Trade)]
    [InlineData("deposit", SharedLedgerEntryType.Deposit)]
    [InlineData("withdrawal", SharedLedgerEntryType.Withdrawal)]
    [InlineData("withdrawal_cancelled", SharedLedgerEntryType.Withdrawal)]
    [InlineData("internal_transfer", SharedLedgerEntryType.Transfer)]
    [InlineData("rebate", SharedLedgerEntryType.Rebate)]
    [InlineData("staking", SharedLedgerEntryType.Unknown)]
    [InlineData("fixed_staking", SharedLedgerEntryType.Unknown)]
    [InlineData("affiliate", SharedLedgerEntryType.Unknown)]
    [InlineData("distribution", SharedLedgerEntryType.Unknown)]
    [InlineData("loan", SharedLedgerEntryType.Unknown)]
    [InlineData("external_transferred_funds", SharedLedgerEntryType.Unknown)]
    [InlineData("manually_assigned", SharedLedgerEntryType.Unknown)]
    [InlineData("manually_assigned_bitvavo", SharedLedgerEntryType.Unknown)]
    public void Every_documented_transaction_type_maps_to_its_shared_ledger_type(string wire, SharedLedgerEntryType expected)
    {
        wire.ToSharedLedgerEntryType().ShouldBe(expected);
    }

    [Theory]
    [InlineData("a type Bitvavo adds later")]
    [InlineData("")]
    [InlineData(null)]
    public void An_unlisted_transaction_type_maps_to_Unknown(string? wire)
    {
        wire.ToSharedLedgerEntryType().ShouldBe(SharedLedgerEntryType.Unknown);
    }

    [Fact]
    public void The_wire_type_is_matched_without_regard_to_case()
    {
        "BUY".ToSharedLedgerEntryType().ShouldBe(SharedLedgerEntryType.Trade);
        "Withdrawal_Cancelled".ToSharedLedgerEntryType().ShouldBe(SharedLedgerEntryType.Withdrawal);
    }

    [Fact]
    public async Task Ledger_requests_the_options_forbid_are_rejected_before_anything_is_sent()
    {
        var handler = new StubHttpMessageHandler(LedgerFirstPage);
        var api = handler.RestClient().SpotApi.SharedApi;

        var ascending = await api.GetLedgerAsync(new GetLedgerRequest(direction: DataDirection.Ascending), ct: TestContext.Current.CancellationToken);
        var tooMany = await api.GetLedgerAsync(new GetLedgerRequest(limit: 101), ct: TestContext.Current.CancellationToken);

        ascending.Error.ShouldBeOfType<ArgumentError>();
        tooMany.Error.ShouldBeOfType<ArgumentError>();
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_ledger_without_credentials_fails_with_NoApiCredentialsError_and_sends_nothing()
    {
        var handler = new StubHttpMessageHandler(LedgerFirstPage);
        var api = handler.RestClient(withCredentials: false).SpotApi.SharedApi;

        var result = await api.GetLedgerAsync(new GetLedgerRequest(), ct: TestContext.Current.CancellationToken);

        result.Error.ShouldBeOfType<NoApiCredentialsError>();
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_failed_ledger_call_passes_the_server_error_through()
    {
        var handler = new StubHttpMessageHandler("""{"errorCode":205,"error":"The page parameter is invalid."}""", HttpStatusCode.BadRequest);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.GetLedgerAsync(new GetLedgerRequest(), ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        var error = result.Error.ShouldBeOfType<ServerError>();
        error.ErrorType.ShouldBe(ErrorType.InvalidParameter);
        error.Code.ShouldBe(205);
    }

    [Fact]
    public void The_ledger_options_describe_what_Bitvavo_supports()
    {
        var options = new StubHttpMessageHandler("{}").RestClient().SpotApi.SharedApi.GetLedgerOptions;

        options.SupportsAscending.ShouldBeFalse();
        options.SupportsDescending.ShouldBeTrue();
        options.TimePeriodFilterSupport.ShouldBeTrue();
        options.MaxLimit.ShouldBe(100);
        options.NeedsAuthentication.ShouldBeTrue();
    }
}
