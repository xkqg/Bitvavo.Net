// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Errors;
using CryptoExchange.Net.SharedApis;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests.Clients.SpotApi.SharedApi;

/// <summary>
/// The Shared funding capabilities on the REST API: deposit addresses and history, withdrawal history and withdrawing ([V2]
/// <c>IGetDepositAddressesRest</c>, <c>IGetDepositHistoryRest</c>, <c>IGetWithdrawalHistoryRest</c>, <c>IWithdrawRest</c>; the legacy
/// [V1] <c>IDepositRestClient</c>, <c>IWithdrawalRestClient</c> and <c>IWithdrawRestClient</c> on the same instance), through the
/// real request pipeline. Every one is a signed call: without credentials nothing is sent. Bitvavo returns the histories newest first.
/// </summary>
public class BitvavoRestSharedFundingTests
{
    private const string CryptoAddress = """{"address":"bc1qxyz","paymentId":"memo-1"}""";

    private const string FiatInstructions = """{"iban":"NL12BITV1234567890","bic":"BITVNL2A","description":"254D20CC94"}""";

    /// <summary>Two deposits, newest first: a completed one with a memo and a pending one.</summary>
    private const string Deposits = """[{"timestamp":1714136400000,"symbol":"BTC","amount":"0.5","address":"bc1qsource","paymentId":"memo-9","txId":"tx-2","fee":"0","status":"completed"},{"timestamp":1714132800000,"symbol":"BTC","amount":"0.25","txId":"tx-1","fee":"0","status":"awaiting_processing"}]""";

    /// <summary>Two withdrawals, newest first: a completed crypto one with a memo and a fiat one that waits for the e-mail confirmation.</summary>
    private const string Withdrawals = """[{"timestamp":1714136400000,"symbol":"BTC","amount":"0.25","address":"bc1qabc","paymentId":"memo-3","txId":"tx-9","fee":"0.0002","status":"completed"},{"timestamp":1714132800000,"symbol":"EUR","amount":"100","fee":"0.5","status":"awaiting_email_confirmation"}]""";

    /// <summary>The 201 Bitvavo answers an accepted crypto withdrawal with.</summary>
    private const string WithdrawalAccepted = """{"id":"wd-1","asset":"BTC","network":"Bitcoin","address":"bc1qdest","amount":"0.1","fee":"0.0001","createdAt":"2026-10-05T12:34:56.789Z"}""";

    private static readonly DateTime _newer = new(2024, 4, 26, 13, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime _older = new(2024, 4, 26, 12, 0, 0, DateTimeKind.Utc);

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

    private static string DepositWithStatus(string wire)
        => "[{\"timestamp\":1714132800000,\"symbol\":\"BTC\",\"amount\":\"0.5\",\"txId\":\"tx-1\",\"status\":\"" + wire + "\"}]";

    private static string WithdrawalWithStatus(string wire)
        => "[{\"timestamp\":1714132800000,\"symbol\":\"BTC\",\"amount\":\"0.25\",\"address\":\"bc1qabc\",\"txId\":\"tx-9\",\"status\":\"" + wire + "\"}]";

    private static async Task<string> BodyOfAsync(HttpRequestMessage request)
        => await request.Content!.ReadAsStringAsync(cancellationToken: TestContext.Current.CancellationToken);

    private static void ShouldBeSigned(HttpRequestMessage request)
        => request.Headers.GetValues("Bitvavo-Access-Signature").ShouldHaveSingleItem().Length.ShouldBe(64);

    // ── deposit addresses ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_crypto_deposit_address_is_mapped_with_its_memo_and_the_asset_is_sent_signed()
    {
        var handler = new StubHttpMessageHandler(CryptoAddress);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.GetDepositAddressesAsync(new GetDepositAddressesRequest("BTC"), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        var address = result.Data.ShouldHaveSingleItem();
        address.Asset.ShouldBe("BTC");
        address.Address.ShouldBe("bc1qxyz");
        address.TagOrMemo.ShouldBe("memo-1");
        address.Network.ShouldBeNull();
        var request = handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Get);
        request.RequestUri!.PathAndQuery.ShouldBe("/v2/deposit?symbol=BTC");
        ShouldBeSigned(request);
    }

    [Fact]
    public async Task A_crypto_deposit_address_without_a_memo_has_no_tag()
    {
        var api = new StubHttpMessageHandler("""{"address":"bc1qxyz"}""").RestClient().SpotApi.SharedApi;

        var result = await api.GetDepositAddressesAsync(new GetDepositAddressesRequest("BTC"), TestContext.Current.CancellationToken);

        result.Data.ShouldHaveSingleItem().TagOrMemo.ShouldBeNull();
    }

    /// <summary>The OpenAPI specification spells the memo <c>paymentid</c>, the deposit history and the 0.4.0 oracle <c>paymentId</c>: both are read.</summary>
    [Fact]
    public async Task The_memo_is_read_whatever_the_case_of_its_field_name()
    {
        var api = new StubHttpMessageHandler("""{"address":"rDeposit","paymentid":"10002653"}""").RestClient().SpotApi.SharedApi;

        var result = await api.GetDepositAddressesAsync(new GetDepositAddressesRequest("XRP"), TestContext.Current.CancellationToken);

        result.Data.ShouldHaveSingleItem().TagOrMemo.ShouldBe("10002653");
    }

    [Fact]
    public async Task A_fiat_asset_has_bank_details_instead_of_an_address_so_the_list_is_empty()
    {
        var api = new StubHttpMessageHandler(FiatInstructions).RestClient().SpotApi.SharedApi;

        var result = await api.GetDepositAddressesAsync(new GetDepositAddressesRequest("EUR"), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_network_cannot_be_chosen_so_a_request_naming_one_is_rejected_before_anything_is_sent()
    {
        var handler = new StubHttpMessageHandler(CryptoAddress);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.GetDepositAddressesAsync(new GetDepositAddressesRequest("BTC", "Bitcoin"), TestContext.Current.CancellationToken);

        result.Error.ShouldBeOfType<ArgumentError>();
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_deposit_address_without_credentials_fails_with_NoApiCredentialsError_and_sends_nothing()
    {
        var handler = new StubHttpMessageHandler(CryptoAddress);
        var api = handler.RestClient(withCredentials: false).SpotApi.SharedApi;

        var result = await api.GetDepositAddressesAsync(new GetDepositAddressesRequest("BTC"), TestContext.Current.CancellationToken);

        result.Error.ShouldBeOfType<NoApiCredentialsError>();
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_failed_deposit_address_call_passes_the_server_error_through()
    {
        var api = BadSignature().RestClient().SpotApi.SharedApi;

        var result = await api.GetDepositAddressesAsync(new GetDepositAddressesRequest("BTC"), TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        ShouldBeTheBadSignatureError(result.Error);
    }

    [Fact]
    public void The_deposit_address_options_need_credentials_and_state_that_a_network_is_not_supported()
    {
        var options = new StubHttpMessageHandler("{}").RestClient().SpotApi.SharedApi.GetDepositAddressesOptions;

        options.NeedsAuthentication.ShouldBeTrue();
        var network = options.RequestParameterRules.Single(rule => rule.Name == nameof(GetDepositAddressesRequest.Network));
        network.Support.ShouldBe(RequestParameterSupport.NotSupported);
        options.RequestParameterRules.Single(rule => rule.Name == nameof(GetDepositAddressesRequest.Asset)).Support.ShouldBe(RequestParameterSupport.Required);
    }

    // ── deposit history ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Deposits_are_mapped_field_by_field_newest_first()
    {
        var handler = new StubHttpMessageHandler(Deposits);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.GetDepositHistoryAsync(new GetDepositsRequest("BTC"), ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Length.ShouldBe(2);
        var completed = result.Data[0];
        completed.Asset.ShouldBe("BTC");
        completed.Quantity.ShouldBe(0.5m);
        completed.Timestamp.ShouldBe(_newer);
        completed.Completed.ShouldBeTrue();
        completed.Status.ShouldBe(SharedTransferStatus.Completed);
        completed.TransactionId.ShouldBe("tx-2");
        completed.Tag.ShouldBe("memo-9");
        var pending = result.Data[1];
        pending.Quantity.ShouldBe(0.25m);
        pending.Timestamp.ShouldBe(_older);
        pending.Completed.ShouldBeFalse();
        pending.Status.ShouldBe(SharedTransferStatus.InProgress);
        pending.TransactionId.ShouldBe("tx-1");
        pending.Tag.ShouldBeNull();
    }

    [Theory]
    [InlineData("completed", SharedTransferStatus.Completed, true)]
    [InlineData("awaiting_processing", SharedTransferStatus.InProgress, false)]
    [InlineData("awaiting_email_confirmation", SharedTransferStatus.InProgress, false)]
    [InlineData("in_mempool", SharedTransferStatus.InProgress, false)]
    [InlineData("canceled", SharedTransferStatus.Failed, false)]
    [InlineData("a_status_bitvavo_adds_later", SharedTransferStatus.Unknown, false)]
    public async Task Deposit_status_maps_end_to_end_and_only_a_completed_deposit_is_completed(string wire, SharedTransferStatus expected, bool completed)
    {
        var api = new StubHttpMessageHandler(DepositWithStatus(wire)).RestClient().SpotApi.SharedApi;

        var result = await api.GetDepositHistoryAsync(new GetDepositsRequest(), ct: TestContext.Current.CancellationToken);

        var deposit = result.Data.ShouldHaveSingleItem();
        deposit.Status.ShouldBe(expected);
        deposit.Completed.ShouldBe(completed);
    }

    [Fact]
    public async Task A_deposit_without_an_amount_counts_it_as_zero()
    {
        var api = new StubHttpMessageHandler("""[{"timestamp":1714132800000,"symbol":"BTC","status":"completed"}]""").RestClient().SpotApi.SharedApi;

        var result = await api.GetDepositHistoryAsync(new GetDepositsRequest(), ct: TestContext.Current.CancellationToken);

        result.Data.ShouldHaveSingleItem().Quantity.ShouldBe(0m);
    }

    [Fact]
    public async Task A_deposit_without_a_status_is_Unknown_and_not_completed()
    {
        var api = new StubHttpMessageHandler("""[{"timestamp":1714132800000,"symbol":"BTC","amount":"0.5"}]""").RestClient().SpotApi.SharedApi;

        var result = await api.GetDepositHistoryAsync(new GetDepositsRequest(), ct: TestContext.Current.CancellationToken);

        var deposit = result.Data.ShouldHaveSingleItem();
        deposit.Status.ShouldBe(SharedTransferStatus.Unknown);
        deposit.Completed.ShouldBeFalse();
    }

    [Fact]
    public async Task The_deposit_request_sends_the_asset_the_limit_and_the_time_window_and_is_signed()
    {
        var handler = new StubHttpMessageHandler(Deposits);
        var api = handler.RestClient().SpotApi.SharedApi;
        var start = new DateTime(2024, 4, 26, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2024, 4, 27, 0, 0, 0, DateTimeKind.Utc);

        await api.GetDepositHistoryAsync(new GetDepositsRequest("BTC", start, end, limit: 50), ct: TestContext.Current.CancellationToken);

        var request = handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Get);
        request.RequestUri!.AbsolutePath.ShouldBe("/v2/depositHistory");
        request.RequestUri.Query.ShouldContain("symbol=BTC");
        request.RequestUri.Query.ShouldContain("limit=50");
        request.RequestUri.Query.ShouldContain("start=" + new DateTimeOffset(start).ToUnixTimeMilliseconds());
        request.RequestUri.Query.ShouldContain("end=" + new DateTimeOffset(end).ToUnixTimeMilliseconds());
        ShouldBeSigned(request);
    }

    [Fact]
    public async Task A_bare_deposit_request_asks_for_Bitvavos_default_page_of_500_and_invents_no_bound()
    {
        var handler = new StubHttpMessageHandler(Deposits);
        var api = handler.RestClient().SpotApi.SharedApi;

        await api.GetDepositHistoryAsync(new GetDepositsRequest(), ct: TestContext.Current.CancellationToken);

        var query = handler.Requests.ShouldHaveSingleItem().RequestUri!.Query;
        query.ShouldContain("limit=500");
        query.ShouldNotContain("symbol=");
        query.ShouldNotContain("start=");
        query.ShouldNotContain("end=");
    }

    [Fact]
    public async Task A_full_deposit_page_returns_the_request_for_the_next_older_page_and_the_token_continues_from_there()
    {
        var handler = new StubHttpMessageHandler(Deposits);
        var api = handler.RestClient().SpotApi.SharedApi;
        var request = new GetDepositsRequest("BTC", limit: 2);

        var first = await api.GetDepositHistoryAsync(request, ct: TestContext.Current.CancellationToken);

        var next = first.NextPageRequest.ShouldNotBeNull();
        next.EndTime.ShouldBe(_older.AddMilliseconds(-1));
        next.StartTime.ShouldBeNull();

        await api.GetDepositHistoryAsync(request, next, TestContext.Current.CancellationToken);

        handler.Requests[1].RequestUri!.Query.ShouldContain("end=" + new DateTimeOffset(_older.AddMilliseconds(-1)).ToUnixTimeMilliseconds());
        handler.Requests[1].RequestUri!.Query.ShouldContain("limit=2");
    }

    [Fact]
    public async Task The_next_deposit_page_keeps_the_callers_start_time()
    {
        var handler = new StubHttpMessageHandler(Deposits);
        var api = handler.RestClient().SpotApi.SharedApi;
        var start = new DateTime(2024, 4, 26, 0, 0, 0, DateTimeKind.Utc);
        var request = new GetDepositsRequest("BTC", start, limit: 2);

        var first = await api.GetDepositHistoryAsync(request, ct: TestContext.Current.CancellationToken);

        var next = first.NextPageRequest.ShouldNotBeNull();
        next.StartTime.ShouldBe(start);

        await api.GetDepositHistoryAsync(request, next, TestContext.Current.CancellationToken);

        handler.Requests[1].RequestUri!.Query.ShouldContain("start=" + new DateTimeOffset(start).ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task A_deposit_page_that_reaches_back_to_the_start_time_has_no_next_page()
    {
        var start = new DateTime(2024, 4, 26, 12, 30, 0, DateTimeKind.Utc);
        var api = new StubHttpMessageHandler(Deposits).RestClient().SpotApi.SharedApi;

        var result = await api.GetDepositHistoryAsync(new GetDepositsRequest("BTC", start, limit: 2), ct: TestContext.Current.CancellationToken);

        result.NextPageRequest.ShouldBeNull();
    }

    [Fact]
    public async Task A_failed_deposit_history_call_passes_the_server_error_through()
    {
        var api = BadSignature().RestClient().SpotApi.SharedApi;

        var result = await api.GetDepositHistoryAsync(new GetDepositsRequest(), ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        ShouldBeTheBadSignatureError(result.Error);
    }

    [Fact]
    public async Task A_short_deposit_page_has_no_next_page()
    {
        var api = new StubHttpMessageHandler(Deposits).RestClient().SpotApi.SharedApi;

        var result = await api.GetDepositHistoryAsync(new GetDepositsRequest("BTC", limit: 100), ct: TestContext.Current.CancellationToken);

        result.NextPageRequest.ShouldBeNull();
    }

    [Fact]
    public async Task An_empty_deposit_page_has_no_next_page()
    {
        var api = new StubHttpMessageHandler("[]").RestClient().SpotApi.SharedApi;

        var result = await api.GetDepositHistoryAsync(new GetDepositsRequest("BTC", limit: 1), ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.ShouldBeEmpty();
        result.NextPageRequest.ShouldBeNull();
    }

    [Fact]
    public async Task Deposit_requests_the_options_forbid_are_rejected_before_anything_is_sent()
    {
        var handler = new StubHttpMessageHandler(Deposits);
        var api = handler.RestClient().SpotApi.SharedApi;

        var ascending = await api.GetDepositHistoryAsync(new GetDepositsRequest(direction: DataDirection.Ascending), ct: TestContext.Current.CancellationToken);
        var tooMany = await api.GetDepositHistoryAsync(new GetDepositsRequest(limit: 1001), ct: TestContext.Current.CancellationToken);

        ascending.Error.ShouldBeOfType<ArgumentError>();
        tooMany.Error.ShouldBeOfType<ArgumentError>();
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_deposit_history_without_credentials_fails_with_NoApiCredentialsError_and_sends_nothing()
    {
        var handler = new StubHttpMessageHandler(Deposits);
        var api = handler.RestClient(withCredentials: false).SpotApi.SharedApi;

        var result = await api.GetDepositHistoryAsync(new GetDepositsRequest(), ct: TestContext.Current.CancellationToken);

        result.Error.ShouldBeOfType<NoApiCredentialsError>();
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public void The_deposit_history_options_describe_what_Bitvavo_supports()
    {
        var options = new StubHttpMessageHandler("[]").RestClient().SpotApi.SharedApi.GetDepositHistoryOptions;

        options.SupportsAscending.ShouldBeFalse();
        options.SupportsDescending.ShouldBeTrue();
        options.TimePeriodFilterSupport.ShouldBeTrue();
        options.MaxLimit.ShouldBe(1000);
        options.NeedsAuthentication.ShouldBeTrue();
    }

    [Fact]
    public async Task The_legacy_interface_serves_deposit_addresses_and_deposits()
    {
        var handler = new StubHttpMessageHandler(request => Json(request.RequestUri!.AbsolutePath == "/v2/deposit"
            ? CryptoAddress
            : """[{"timestamp":1714132800000,"symbol":"BTC","amount":"0.5","txId":"tx-1","status":"completed"}]"""));
        var legacy = handler.RestClient().SpotApi.SharedClient;

        var addresses = await legacy.GetDepositAddressesAsync(new GetDepositAddressesRequest("BTC"), TestContext.Current.CancellationToken);
        var deposits = await legacy.GetDepositsAsync(new GetDepositsRequest("BTC"), ct: TestContext.Current.CancellationToken);

        addresses.Data.ShouldHaveSingleItem().Address.ShouldBe("bc1qxyz");
        var deposit = deposits.Data.ShouldHaveSingleItem();
        deposit.Asset.ShouldBe("BTC");
        deposit.Quantity.ShouldBe(0.5m);
        deposit.Completed.ShouldBeTrue();
        deposit.Status.ShouldBe(SharedTransferStatus.Completed);
    }

    [Fact]
    public void The_legacy_deposit_options_are_the_V2_options_objects_the_history_one_under_its_old_name()
    {
        var client = new StubHttpMessageHandler("{}").RestClient();
        var legacy = client.SpotApi.SharedClient;

        legacy.GetDepositAddressesOptions.ShouldBeSameAs(client.SpotApi.SharedApi.GetDepositAddressesOptions);
        legacy.GetDepositsOptions.ShouldBeSameAs(client.SpotApi.SharedApi.GetDepositHistoryOptions);
    }

    // ── withdrawal history ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Withdrawals_are_mapped_field_by_field_newest_first()
    {
        var handler = new StubHttpMessageHandler(Withdrawals);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.GetWithdrawalHistoryAsync(new GetWithdrawalsRequest(), ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Length.ShouldBe(2);
        var crypto = result.Data[0];
        crypto.Asset.ShouldBe("BTC");
        crypto.Address.ShouldBe("bc1qabc");
        crypto.Quantity.ShouldBe(0.25m);
        crypto.Fee.ShouldBe(0.0002m);
        crypto.Timestamp.ShouldBe(_newer);
        crypto.TransactionId.ShouldBe("tx-9");
        crypto.Tag.ShouldBe("memo-3");
        crypto.Completed.ShouldBeTrue();
        crypto.Status.ShouldBe(SharedTransferStatus.Completed);
        var fiat = result.Data[1];
        fiat.Asset.ShouldBe("EUR");
        fiat.Address.ShouldBe(string.Empty);
        fiat.Fee.ShouldBe(0.5m);
        fiat.TransactionId.ShouldBeNull();
        fiat.Tag.ShouldBeNull();
        fiat.Completed.ShouldBeFalse();
        fiat.Status.ShouldBe(SharedTransferStatus.InProgress);
    }

    /// <summary>The nine documented withdrawal statuses (G4: approved, sending and processed are in progress, not unknown), plus a status Bitvavo may add.</summary>
    [Theory]
    [InlineData("awaiting_processing", SharedTransferStatus.InProgress, false)]
    [InlineData("awaiting_email_confirmation", SharedTransferStatus.InProgress, false)]
    [InlineData("awaiting_bitvavo_inspection", SharedTransferStatus.InProgress, false)]
    [InlineData("approved", SharedTransferStatus.InProgress, false)]
    [InlineData("sending", SharedTransferStatus.InProgress, false)]
    [InlineData("in_mempool", SharedTransferStatus.InProgress, false)]
    [InlineData("processed", SharedTransferStatus.InProgress, false)]
    [InlineData("completed", SharedTransferStatus.Completed, true)]
    [InlineData("canceled", SharedTransferStatus.Failed, false)]
    [InlineData("a_status_bitvavo_adds_later", SharedTransferStatus.Unknown, false)]
    public async Task Withdrawal_status_maps_end_to_end_and_only_a_completed_withdrawal_is_completed(string wire, SharedTransferStatus expected, bool completed)
    {
        var api = new StubHttpMessageHandler(WithdrawalWithStatus(wire)).RestClient().SpotApi.SharedApi;

        var result = await api.GetWithdrawalHistoryAsync(new GetWithdrawalsRequest("BTC"), ct: TestContext.Current.CancellationToken);

        var withdrawal = result.Data.ShouldHaveSingleItem();
        withdrawal.Status.ShouldBe(expected);
        withdrawal.Completed.ShouldBe(completed);
    }

    [Fact]
    public async Task The_withdrawal_request_sends_the_asset_the_limit_and_the_time_window_and_is_signed()
    {
        var handler = new StubHttpMessageHandler(Withdrawals);
        var api = handler.RestClient().SpotApi.SharedApi;
        var start = new DateTime(2024, 4, 26, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2024, 4, 27, 0, 0, 0, DateTimeKind.Utc);

        await api.GetWithdrawalHistoryAsync(new GetWithdrawalsRequest("BTC", start, end, limit: 50), ct: TestContext.Current.CancellationToken);

        var request = handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Get);
        request.RequestUri!.AbsolutePath.ShouldBe("/v2/withdrawalHistory");
        request.RequestUri.Query.ShouldContain("symbol=BTC");
        request.RequestUri.Query.ShouldContain("limit=50");
        request.RequestUri.Query.ShouldContain("start=" + new DateTimeOffset(start).ToUnixTimeMilliseconds());
        request.RequestUri.Query.ShouldContain("end=" + new DateTimeOffset(end).ToUnixTimeMilliseconds());
        ShouldBeSigned(request);
    }

    [Fact]
    public async Task A_bare_withdrawal_request_asks_for_Bitvavos_default_page_of_500_and_invents_no_bound()
    {
        var handler = new StubHttpMessageHandler(Withdrawals);
        var api = handler.RestClient().SpotApi.SharedApi;

        await api.GetWithdrawalHistoryAsync(new GetWithdrawalsRequest(), ct: TestContext.Current.CancellationToken);

        var query = handler.Requests.ShouldHaveSingleItem().RequestUri!.Query;
        query.ShouldContain("limit=500");
        query.ShouldNotContain("symbol=");
        query.ShouldNotContain("start=");
        query.ShouldNotContain("end=");
    }

    [Fact]
    public async Task A_full_withdrawal_page_returns_the_request_for_the_next_older_page_and_the_token_continues_from_there()
    {
        var handler = new StubHttpMessageHandler(Withdrawals);
        var api = handler.RestClient().SpotApi.SharedApi;
        var request = new GetWithdrawalsRequest(limit: 2);

        var first = await api.GetWithdrawalHistoryAsync(request, ct: TestContext.Current.CancellationToken);

        var next = first.NextPageRequest.ShouldNotBeNull();
        next.EndTime.ShouldBe(_older.AddMilliseconds(-1));

        await api.GetWithdrawalHistoryAsync(request, next, TestContext.Current.CancellationToken);

        handler.Requests[1].RequestUri!.Query.ShouldContain("end=" + new DateTimeOffset(_older.AddMilliseconds(-1)).ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task The_next_withdrawal_page_keeps_the_callers_start_time()
    {
        var handler = new StubHttpMessageHandler(Withdrawals);
        var api = handler.RestClient().SpotApi.SharedApi;
        var start = new DateTime(2024, 4, 26, 0, 0, 0, DateTimeKind.Utc);
        var request = new GetWithdrawalsRequest("BTC", start, limit: 2);

        var first = await api.GetWithdrawalHistoryAsync(request, ct: TestContext.Current.CancellationToken);

        var next = first.NextPageRequest.ShouldNotBeNull();
        next.StartTime.ShouldBe(start);

        await api.GetWithdrawalHistoryAsync(request, next, TestContext.Current.CancellationToken);

        handler.Requests[1].RequestUri!.Query.ShouldContain("start=" + new DateTimeOffset(start).ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task A_withdrawal_without_an_amount_counts_it_as_zero()
    {
        var api = new StubHttpMessageHandler("""[{"timestamp":1714132800000,"symbol":"BTC","status":"completed"}]""").RestClient().SpotApi.SharedApi;

        var result = await api.GetWithdrawalHistoryAsync(new GetWithdrawalsRequest(), ct: TestContext.Current.CancellationToken);

        result.Data.ShouldHaveSingleItem().Quantity.ShouldBe(0m);
    }

    [Fact]
    public async Task A_failed_withdrawal_history_call_passes_the_server_error_through()
    {
        var api = BadSignature().RestClient().SpotApi.SharedApi;

        var result = await api.GetWithdrawalHistoryAsync(new GetWithdrawalsRequest(), ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        ShouldBeTheBadSignatureError(result.Error);
    }

    [Fact]
    public async Task A_short_withdrawal_page_has_no_next_page()
    {
        var api = new StubHttpMessageHandler(Withdrawals).RestClient().SpotApi.SharedApi;

        var result = await api.GetWithdrawalHistoryAsync(new GetWithdrawalsRequest(limit: 100), ct: TestContext.Current.CancellationToken);

        result.NextPageRequest.ShouldBeNull();
    }

    [Fact]
    public async Task Withdrawal_requests_the_options_forbid_are_rejected_before_anything_is_sent()
    {
        var handler = new StubHttpMessageHandler(Withdrawals);
        var api = handler.RestClient().SpotApi.SharedApi;

        var ascending = await api.GetWithdrawalHistoryAsync(new GetWithdrawalsRequest(direction: DataDirection.Ascending), ct: TestContext.Current.CancellationToken);
        var tooMany = await api.GetWithdrawalHistoryAsync(new GetWithdrawalsRequest(limit: 1001), ct: TestContext.Current.CancellationToken);

        ascending.Error.ShouldBeOfType<ArgumentError>();
        tooMany.Error.ShouldBeOfType<ArgumentError>();
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_withdrawal_history_without_credentials_fails_with_NoApiCredentialsError_and_sends_nothing()
    {
        var handler = new StubHttpMessageHandler(Withdrawals);
        var api = handler.RestClient(withCredentials: false).SpotApi.SharedApi;

        var result = await api.GetWithdrawalHistoryAsync(new GetWithdrawalsRequest(), ct: TestContext.Current.CancellationToken);

        result.Error.ShouldBeOfType<NoApiCredentialsError>();
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public void The_withdrawal_history_options_describe_what_Bitvavo_supports()
    {
        var options = new StubHttpMessageHandler("[]").RestClient().SpotApi.SharedApi.GetWithdrawalHistoryOptions;

        options.SupportsAscending.ShouldBeFalse();
        options.SupportsDescending.ShouldBeTrue();
        options.TimePeriodFilterSupport.ShouldBeTrue();
        options.MaxLimit.ShouldBe(1000);
        options.NeedsAuthentication.ShouldBeTrue();
    }

    [Fact]
    public async Task The_legacy_interface_serves_withdrawals()
    {
        var legacy = new StubHttpMessageHandler("""[{"timestamp":1714132800000,"symbol":"BTC","amount":"0.25","address":"bc1qabc","txId":"tx-9","fee":"0.0002","status":"completed"}]""").RestClient().SpotApi.SharedClient;

        var result = await legacy.GetWithdrawalsAsync(new GetWithdrawalsRequest("BTC"), ct: TestContext.Current.CancellationToken);

        var withdrawal = result.Data.ShouldHaveSingleItem();
        withdrawal.Asset.ShouldBe("BTC");
        withdrawal.Quantity.ShouldBe(0.25m);
        withdrawal.Address.ShouldBe("bc1qabc");
        withdrawal.Fee.ShouldBe(0.0002m);
        withdrawal.Completed.ShouldBeTrue();
    }

    [Fact]
    public void The_legacy_withdrawal_options_are_the_V2_history_options_object_under_its_old_name()
    {
        var client = new StubHttpMessageHandler("[]").RestClient();

        client.SpotApi.SharedClient.GetWithdrawalsOptions.ShouldBeSameAs(client.SpotApi.SharedApi.GetWithdrawalHistoryOptions);
    }

    // ── withdraw ─────────────────────────────────────────────────────────────────────────

    /// <summary>The 0.4.0 oracle, flipped (W9): the crypto endpoint, the network in the body, and the id Bitvavo issues instead of the asset symbol.</summary>
    [Fact]
    public async Task Withdraw_posts_to_the_crypto_endpoint_and_returns_the_server_issued_id()
    {
        var handler = new StubHttpMessageHandler(WithdrawalAccepted, HttpStatusCode.Created);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.WithdrawAsync(new WithdrawRequest("BTC", 0.1m, "bc1qdest", network: "Bitcoin"), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Id.ShouldBe("wd-1");
        var request = handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Post);
        request.RequestUri!.AbsolutePath.ShouldBe("/v2/crypto/withdrawal");
        ShouldBeSigned(request);
    }

    [Fact]
    public async Task The_withdrawal_body_carries_the_asset_the_network_the_address_and_the_amount_and_nothing_else()
    {
        var handler = new StubHttpMessageHandler(WithdrawalAccepted, HttpStatusCode.Created);
        var api = handler.RestClient().SpotApi.SharedApi;

        await api.WithdrawAsync(new WithdrawRequest("BTC", 0.1m, "bc1qdest", network: "Bitcoin"), TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(await BodyOfAsync(handler.Requests.ShouldHaveSingleItem()));
        body.RootElement.GetProperty("asset").GetString().ShouldBe("BTC");
        body.RootElement.GetProperty("network").GetString().ShouldBe("Bitcoin");
        body.RootElement.GetProperty("address").GetString().ShouldBe("bc1qdest");
        body.RootElement.GetProperty("amount").GetString().ShouldBe("0.1");
        body.RootElement.EnumerateObject().Select(property => property.Name).ShouldBe(new[] { "address", "amount", "asset", "network" }, ignoreOrder: true);
    }

    [Fact]
    public async Task The_address_tag_travels_as_the_memo()
    {
        var handler = new StubHttpMessageHandler(WithdrawalAccepted, HttpStatusCode.Created);
        var api = handler.RestClient().SpotApi.SharedApi;

        await api.WithdrawAsync(new WithdrawRequest("XRP", 50m, "rDestination", network: "XRP", addressTag: "12345"), TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(await BodyOfAsync(handler.Requests.ShouldHaveSingleItem()));
        body.RootElement.GetProperty("memo").GetString().ShouldBe("12345");
        body.RootElement.GetProperty("asset").GetString().ShouldBe("XRP");
        body.RootElement.GetProperty("network").GetString().ShouldBe("XRP");
        body.RootElement.GetProperty("amount").GetString().ShouldBe("50");
    }

    [Fact]
    public async Task A_withdrawal_without_a_network_is_rejected_before_anything_is_sent()
    {
        var handler = new StubHttpMessageHandler(WithdrawalAccepted, HttpStatusCode.Created);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.WithdrawAsync(new WithdrawRequest("BTC", 0.1m, "bc1qdest"), TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.Error.ShouldBeOfType<ArgumentError>();
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_withdrawal_Bitvavo_refuses_passes_the_server_error_through()
    {
        var handler = new StubHttpMessageHandler("""{"errorCode":216,"error":"Your balance is insufficient."}""", HttpStatusCode.BadRequest);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.WithdrawAsync(new WithdrawRequest("BTC", 0.1m, "bc1qdest", network: "Bitcoin"), TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        var error = result.Error.ShouldBeOfType<ServerError>();
        error.ErrorType.ShouldBe(ErrorType.InsufficientBalance);
        error.Code.ShouldBe(216);
        handler.Requests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Withdraw_without_credentials_fails_with_NoApiCredentialsError_and_sends_nothing()
    {
        var handler = new StubHttpMessageHandler(WithdrawalAccepted, HttpStatusCode.Created);
        var api = handler.RestClient(withCredentials: false).SpotApi.SharedApi;

        var result = await api.WithdrawAsync(new WithdrawRequest("BTC", 0.1m, "bc1qdest", network: "Bitcoin"), TestContext.Current.CancellationToken);

        result.Error.ShouldBeOfType<NoApiCredentialsError>();
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public void The_withdraw_options_need_credentials_and_require_the_network()
    {
        var options = new StubHttpMessageHandler("{}").RestClient().SpotApi.SharedApi.WithdrawOptions;

        options.NeedsAuthentication.ShouldBeTrue();
        var network = options.RequestParameterRules.Single(rule => rule.Name == nameof(WithdrawRequest.Network));
        network.Support.ShouldBe(RequestParameterSupport.Required);
        network.DefaultSupport.ShouldBe(RequestParameterSupport.Optional);
        options.RequestParameterRules.Single(rule => rule.Name == nameof(WithdrawRequest.AddressTag)).Support.ShouldBe(RequestParameterSupport.Optional);
    }

    [Fact]
    public async Task The_legacy_interface_withdraws_over_the_crypto_endpoint_and_returns_the_server_issued_id()
    {
        var handler = new StubHttpMessageHandler(WithdrawalAccepted, HttpStatusCode.Created);
        var legacy = handler.RestClient().SpotApi.SharedClient;

        var result = await legacy.WithdrawAsync(new WithdrawRequest("BTC", 0.1m, "bc1qdest", network: "Bitcoin"), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue(result.Error?.ToString());
        result.Data.Id.ShouldBe("wd-1");
        handler.Requests.ShouldHaveSingleItem().RequestUri!.AbsolutePath.ShouldBe("/v2/crypto/withdrawal");
    }

    [Fact]
    public void The_legacy_withdraw_options_are_the_V2_options_object()
    {
        var client = new StubHttpMessageHandler("{}").RestClient();

        client.SpotApi.SharedClient.WithdrawOptions.ShouldBeSameAs(client.SpotApi.SharedApi.WithdrawOptions);
    }
}
