// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Bitvavo.Net.Clients.SpotApi;
using Bitvavo.Net.Objects.Internal;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Errors;
using CryptoExchange.Net.SharedApis;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests.Clients.SpotApi.SharedApi;

/// <summary>
/// The Shared order-management capabilities on the REST API, [V2] only (the legacy interfaces have no equivalent):
/// <c>ICancelAllSpotOrdersRest</c>, <c>ICancelAllSpotSymbolOrdersRest</c>, <c>IEditSpotOrderRest</c> and
/// <c>IEditSpotOrderByClientOrderIdRest</c>, through the real request pipeline. Every call is signed and carries the
/// <c>OperatorId</c> Bitvavo wants on every order operation; <c>DELETE /v2/orders</c> takes its parameters in the query,
/// <c>PUT /v2/order</c> in the JSON body (keys sorted case-insensitively).
/// </summary>
public class BitvavoRestSharedOrderManagementTests
{
    private const string LimitOrderId = "b8f5f5a4-6d1e-4a8c-9a3b-2f2f9d9a1c11";

    private const string LimitClientOrderId = "2be7d0df-d8dc-7b93-a550-8876f3b393e9";

    /// <summary>PUT /order answers with the updated order (every order field of the specification).</summary>
    private const string UpdatedOrder = """{"orderId":"b8f5f5a4-6d1e-4a8c-9a3b-2f2f9d9a1c11","clientOrderId":"2be7d0df-d8dc-7b93-a550-8876f3b393e9","market":"ETH-EUR","created":1714132800000,"updated":1714132900000,"status":"new","side":"buy","orderType":"limit","amount":"0.75","amountRemaining":"0.75","price":"1600","filledAmount":"0","filledAmountQuote":"0","feePaid":"0","feeCurrency":"EUR","fills":[],"selfTradePrevention":"decrementAndCancel","visible":true,"timeInForce":"GTC","postOnly":false,"operatorId":7,"createdNs":1714132800000000000,"updatedNs":1714132900000000000}""";

    /// <summary>DELETE /orders answers with the id and the operator of every canceled order.</summary>
    private const string CanceledOrders = """[{"orderId":"a8d4a0f2-5a7e-4c0b-8d58-1d3c5e4b2f10","operatorId":7},{"orderId":"c1f2e3d4-b5a6-4789-8a9b-0c1d2e3f4a5b","operatorId":7}]""";

    private static SharedSymbol EthEur => new(TradingMode.Spot, "ETH", "EUR");

    private static ExchangeParameters Operator(long id = 7)
        => new(new ExchangeParameter(BitvavoExchange.ExchangeName, BitvavoSharedParameters.OperatorId, id));

    private static async Task<string> BodyOf(HttpRequestMessage request)
        => await request.Content!.ReadAsStringAsync(TestContext.Current.CancellationToken);

    private static void ShouldBeSigned(HttpRequestMessage request)
    {
        request.Headers.GetValues("Bitvavo-Access-Key").ShouldHaveSingleItem().ShouldBe("test-key");
        request.Headers.GetValues("Bitvavo-Access-Signature").ShouldHaveSingleItem().Length.ShouldBe(64);
    }

    // ── cancel all orders ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Cancel_all_sends_one_signed_DELETE_to_orders_with_only_the_operator_id_in_the_query()
    {
        var handler = new StubHttpMessageHandler(CanceledOrders);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.CancelAllSpotOrdersAsync(new CancelAllOrdersRequest(Operator()), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.ResponseStatusCode.ShouldBe(HttpStatusCode.OK);
        result.RequestMethod.ShouldBe(HttpMethod.Delete);
        var request = handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Delete);
        request.RequestUri!.PathAndQuery.ShouldBe("/v2/orders?operatorId=7");
        request.Content.ShouldBeNull();
        ShouldBeSigned(request);
    }

    [Fact]
    public async Task Cancel_all_with_nothing_open_is_a_success()
    {
        var handler = new StubHttpMessageHandler("[]");
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.CancelAllSpotOrdersAsync(new CancelAllOrdersRequest(Operator()), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
    }

    [Fact]
    public async Task Cancel_all_without_an_operator_id_is_rejected_before_anything_is_sent()
    {
        var handler = new StubHttpMessageHandler(CanceledOrders);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.CancelAllSpotOrdersAsync(new CancelAllOrdersRequest(), TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        var error = result.Error.ShouldBeOfType<ArgumentError>();
        error.ErrorType.ShouldBe(ErrorType.InvalidParameter);
        error.Message.ShouldNotBeNull().ShouldContain("OperatorId");
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Cancel_all_without_credentials_is_a_NoApiCredentialsError_and_sends_nothing()
    {
        var handler = new StubHttpMessageHandler(CanceledOrders);
        var api = handler.RestClient(withCredentials: false).SpotApi.SharedApi;

        var result = await api.CancelAllSpotOrdersAsync(new CancelAllOrdersRequest(Operator()), TestContext.Current.CancellationToken);

        result.Error.ShouldBeOfType<NoApiCredentialsError>();
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Cancel_all_hands_the_servers_refusal_to_the_caller_with_its_mapped_type()
    {
        var handler = new StubHttpMessageHandler("""{"errorCode":310,"error":"Your API key does not have the Trade digital assets permission."}""", HttpStatusCode.Forbidden);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.CancelAllSpotOrdersAsync(new CancelAllOrdersRequest(Operator()), TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        var error = result.Error.ShouldBeOfType<ServerError>();
        error.ErrorType.ShouldBe(ErrorType.Unauthorized);
        error.Code.ShouldBe(310);
    }

    // ── cancel all orders of one symbol ──────────────────────────────────────────────────

    [Fact]
    public async Task Cancel_all_of_a_symbol_adds_the_market_to_the_signed_DELETE_query()
    {
        var handler = new StubHttpMessageHandler(CanceledOrders);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.CancelAllSpotSymbolOrdersAsync(new CancelAllSymbolOrdersRequest(EthEur, Operator()), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.RequestMethod.ShouldBe(HttpMethod.Delete);
        var request = handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Delete);
        request.RequestUri!.PathAndQuery.ShouldBe("/v2/orders?market=ETH-EUR&operatorId=7");
        request.Content.ShouldBeNull();
        ShouldBeSigned(request);
    }

    [Fact]
    public async Task Cancel_all_of_a_symbol_hands_the_servers_refusal_to_the_caller_with_its_mapped_type()
    {
        var handler = new StubHttpMessageHandler("""{"errorCode":424,"error":"The market is in cancelOnly status."}""", HttpStatusCode.BadRequest);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.CancelAllSpotSymbolOrdersAsync(new CancelAllSymbolOrdersRequest(EthEur, Operator()), TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        var error = result.Error.ShouldBeOfType<ServerError>();
        error.ErrorType.ShouldBe(ErrorType.UnavailableSymbol);
        error.Code.ShouldBe(424);
    }

    [Fact]
    public async Task Cancel_all_of_a_symbol_without_an_operator_id_is_rejected_before_anything_is_sent()
    {
        var handler = new StubHttpMessageHandler(CanceledOrders);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.CancelAllSpotSymbolOrdersAsync(new CancelAllSymbolOrdersRequest(EthEur), TestContext.Current.CancellationToken);

        result.Error.ShouldBeOfType<ArgumentError>().Message.ShouldNotBeNull().ShouldContain("OperatorId");
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Cancel_all_of_a_symbol_without_a_symbol_is_rejected_before_anything_is_sent()
    {
        var handler = new StubHttpMessageHandler(CanceledOrders);
        var api = handler.RestClient().SpotApi.SharedApi;
        var request = new CancelAllSymbolOrdersRequest(EthEur, Operator());
        request.Symbol = null;

        var result = await api.CancelAllSpotSymbolOrdersAsync(request, TestContext.Current.CancellationToken);

        result.Error.ShouldBeOfType<ArgumentError>().Message.ShouldNotBeNull().ShouldContain("Symbol");
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Cancel_all_of_a_symbol_rejects_a_symbol_that_is_not_spot_before_anything_is_sent()
    {
        var handler = new StubHttpMessageHandler(CanceledOrders);
        var api = handler.RestClient().SpotApi.SharedApi;
        var perpetual = new SharedSymbol(TradingMode.PerpetualLinear, "ETH", "EUR");

        var result = await api.CancelAllSpotSymbolOrdersAsync(new CancelAllSymbolOrdersRequest(perpetual, Operator()), TestContext.Current.CancellationToken);

        result.Error.ShouldBeOfType<ArgumentError>();
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Cancel_all_of_a_symbol_without_credentials_is_a_NoApiCredentialsError_and_sends_nothing()
    {
        var handler = new StubHttpMessageHandler(CanceledOrders);
        var api = handler.RestClient(withCredentials: false).SpotApi.SharedApi;

        var result = await api.CancelAllSpotSymbolOrdersAsync(new CancelAllSymbolOrdersRequest(EthEur, Operator()), TestContext.Current.CancellationToken);

        result.Error.ShouldBeOfType<NoApiCredentialsError>();
        handler.Requests.ShouldBeEmpty();
    }

    // ── edit an order by its order id ────────────────────────────────────────────────────

    [Fact]
    public async Task Editing_quantity_and_price_sends_one_signed_PUT_with_the_order_id_in_the_json_body()
    {
        var handler = new StubHttpMessageHandler(UpdatedOrder);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.EditSpotOrderAsync(
            new EditOrderRequest(EthEur, LimitOrderId, SharedQuantity.Base(0.75m), 1600m, Operator()),
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Id.ShouldBe(LimitOrderId);
        var request = handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Put);
        request.RequestUri!.PathAndQuery.ShouldBe("/v2/order");
        (await BodyOf(request)).ShouldBe("""{"amount":"0.75","market":"ETH-EUR","operatorId":7,"orderId":"b8f5f5a4-6d1e-4a8c-9a3b-2f2f9d9a1c11","price":"1600"}""");
        ShouldBeSigned(request);
    }

    [Fact]
    public async Task Editing_only_the_price_sends_no_amount()
    {
        var handler = new StubHttpMessageHandler(UpdatedOrder);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.EditSpotOrderAsync(new EditOrderRequest(EthEur, LimitOrderId, null, 1600m, Operator()), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        var request = handler.Requests.ShouldHaveSingleItem();
        (await BodyOf(request)).ShouldBe("""{"market":"ETH-EUR","operatorId":7,"orderId":"b8f5f5a4-6d1e-4a8c-9a3b-2f2f9d9a1c11","price":"1600"}""");
    }

    [Fact]
    public async Task Editing_only_the_quantity_in_the_base_asset_sends_amount_and_no_price()
    {
        var handler = new StubHttpMessageHandler(UpdatedOrder);
        var api = handler.RestClient().SpotApi.SharedApi;

        await api.EditSpotOrderAsync(new EditOrderRequest(EthEur, LimitOrderId, SharedQuantity.Base(0.5m), null, Operator()), TestContext.Current.CancellationToken);

        var request = handler.Requests.ShouldHaveSingleItem();
        (await BodyOf(request)).ShouldBe("""{"amount":"0.5","market":"ETH-EUR","operatorId":7,"orderId":"b8f5f5a4-6d1e-4a8c-9a3b-2f2f9d9a1c11"}""");
    }

    [Fact]
    public async Task Editing_the_quantity_in_the_quote_asset_sends_amountQuote()
    {
        var handler = new StubHttpMessageHandler(UpdatedOrder);
        var api = handler.RestClient().SpotApi.SharedApi;

        await api.EditSpotOrderAsync(new EditOrderRequest(EthEur, LimitOrderId, SharedQuantity.Quote(250m), null, Operator()), TestContext.Current.CancellationToken);

        var request = handler.Requests.ShouldHaveSingleItem();
        (await BodyOf(request)).ShouldBe("""{"amountQuote":"250","market":"ETH-EUR","operatorId":7,"orderId":"b8f5f5a4-6d1e-4a8c-9a3b-2f2f9d9a1c11"}""");
    }

    [Fact]
    public async Task An_edit_with_quantity_in_both_assets_is_rejected_before_anything_is_sent()
    {
        var handler = new StubHttpMessageHandler(UpdatedOrder);
        var api = handler.RestClient().SpotApi.SharedApi;
        var both = new SharedQuantity { QuantityInBaseAsset = 1m, QuantityInQuoteAsset = 100m };

        var result = await api.EditSpotOrderAsync(new EditOrderRequest(EthEur, LimitOrderId, both, null, Operator()), TestContext.Current.CancellationToken);

        var error = result.Error.ShouldBeOfType<ArgumentError>();
        error.ErrorType.ShouldBe(ErrorType.InvalidParameter);
        error.Message.ShouldNotBeNull().ShouldContain("Quantity");
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_edit_with_a_quantity_in_contracts_only_is_rejected_before_anything_is_sent()
    {
        var handler = new StubHttpMessageHandler(UpdatedOrder);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.EditSpotOrderAsync(new EditOrderRequest(EthEur, LimitOrderId, SharedQuantity.Contracts(1m), null, Operator()), TestContext.Current.CancellationToken);

        var error = result.Error.ShouldBeOfType<ArgumentError>();
        error.ErrorType.ShouldBe(ErrorType.MissingParameter);
        error.Message.ShouldNotBeNull().ShouldContain("Quantity");
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_edit_that_changes_nothing_is_rejected_before_anything_is_sent()
    {
        var handler = new StubHttpMessageHandler(UpdatedOrder);
        var api = handler.RestClient().SpotApi.SharedApi;

        var byId = await api.EditSpotOrderAsync(new EditOrderRequest(EthEur, LimitOrderId, exchangeParameters: Operator()), TestContext.Current.CancellationToken);
        var byClientOrderId = await api.EditSpotOrderByClientOrderIdAsync(new EditOrderRequest(EthEur, LimitClientOrderId, exchangeParameters: Operator()), TestContext.Current.CancellationToken);

        byId.Error.ShouldBeOfType<ArgumentError>().ErrorType.ShouldBe(ErrorType.MissingParameter);
        byClientOrderId.Error.ShouldBeOfType<ArgumentError>().ErrorType.ShouldBe(ErrorType.MissingParameter);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_edit_without_an_operator_id_is_rejected_before_anything_is_sent()
    {
        var handler = new StubHttpMessageHandler(UpdatedOrder);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.EditSpotOrderAsync(new EditOrderRequest(EthEur, LimitOrderId, SharedQuantity.Base(0.75m), 1600m), TestContext.Current.CancellationToken);

        result.Error.ShouldBeOfType<ArgumentError>().Message.ShouldNotBeNull().ShouldContain("OperatorId");
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_edit_without_credentials_is_a_NoApiCredentialsError_and_sends_nothing()
    {
        var handler = new StubHttpMessageHandler(UpdatedOrder);
        var api = handler.RestClient(withCredentials: false).SpotApi.SharedApi;

        var result = await api.EditSpotOrderAsync(new EditOrderRequest(EthEur, LimitOrderId, SharedQuantity.Base(0.75m), 1600m, Operator()), TestContext.Current.CancellationToken);

        result.Error.ShouldBeOfType<NoApiCredentialsError>();
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_edit_of_a_symbol_that_is_not_spot_is_rejected_before_anything_is_sent()
    {
        var handler = new StubHttpMessageHandler(UpdatedOrder);
        var api = handler.RestClient().SpotApi.SharedApi;
        var perpetual = new SharedSymbol(TradingMode.PerpetualLinear, "ETH", "EUR");

        var result = await api.EditSpotOrderAsync(new EditOrderRequest(perpetual, LimitOrderId, SharedQuantity.Base(0.75m), 1600m, Operator()), TestContext.Current.CancellationToken);

        result.Error.ShouldBeOfType<ArgumentError>();
        handler.Requests.ShouldBeEmpty();
    }

    /// <summary>239: Bitvavo cannot switch an order between amount and amountQuote; 234: a market order cannot be updated; 240: the order is unknown or no longer active.</summary>
    [Theory]
    [InlineData(239, HttpStatusCode.BadRequest, ErrorType.InvalidParameter)]
    [InlineData(234, HttpStatusCode.BadRequest, ErrorType.RejectedOrderConfiguration)]
    [InlineData(240, HttpStatusCode.NotFound, ErrorType.UnknownOrder)]
    public async Task The_server_refusing_an_edit_reaches_the_caller_with_its_code_and_mapped_type(int code, HttpStatusCode status, ErrorType expected)
    {
        var handler = new StubHttpMessageHandler($$"""{"errorCode":{{code}},"error":"server message"}""", status);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.EditSpotOrderAsync(new EditOrderRequest(EthEur, LimitOrderId, SharedQuantity.Base(0.75m), 1600m, Operator()), TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        var error = result.Error.ShouldBeOfType<ServerError>();
        error.ErrorType.ShouldBe(expected);
        error.Code.ShouldBe(code);
        error.Message.ShouldBe("server message");
    }

    // ── edit an order by its client order id ─────────────────────────────────────────────

    [Fact]
    public async Task Editing_by_client_order_id_sends_the_client_order_id_and_no_order_id_and_returns_the_server_order_id()
    {
        var handler = new StubHttpMessageHandler(UpdatedOrder);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.EditSpotOrderByClientOrderIdAsync(
            new EditOrderRequest(EthEur, LimitClientOrderId, null, 1600m, Operator()),
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Id.ShouldBe(LimitOrderId);
        var request = handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Put);
        request.RequestUri!.PathAndQuery.ShouldBe("/v2/order");
        (await BodyOf(request)).ShouldBe("""{"clientOrderId":"2be7d0df-d8dc-7b93-a550-8876f3b393e9","market":"ETH-EUR","operatorId":7,"price":"1600"}""");
        ShouldBeSigned(request);
    }

    [Fact]
    public async Task Editing_by_client_order_id_without_an_operator_id_is_rejected_before_anything_is_sent()
    {
        var handler = new StubHttpMessageHandler(UpdatedOrder);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.EditSpotOrderByClientOrderIdAsync(new EditOrderRequest(EthEur, LimitClientOrderId, null, 1600m), TestContext.Current.CancellationToken);

        result.Error.ShouldBeOfType<ArgumentError>().Message.ShouldNotBeNull().ShouldContain("OperatorId");
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Editing_by_client_order_id_without_credentials_is_a_NoApiCredentialsError_and_sends_nothing()
    {
        var handler = new StubHttpMessageHandler(UpdatedOrder);
        var api = handler.RestClient(withCredentials: false).SpotApi.SharedApi;

        var result = await api.EditSpotOrderByClientOrderIdAsync(new EditOrderRequest(EthEur, LimitClientOrderId, null, 1600m, Operator()), TestContext.Current.CancellationToken);

        result.Error.ShouldBeOfType<NoApiCredentialsError>();
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_unknown_client_order_id_is_an_UnknownOrder_error()
    {
        var handler = new StubHttpMessageHandler("""{"errorCode":240,"error":"The order does not exist or is no longer active."}""", HttpStatusCode.NotFound);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.EditSpotOrderByClientOrderIdAsync(new EditOrderRequest(EthEur, LimitClientOrderId, null, 1600m, Operator()), TestContext.Current.CancellationToken);

        result.Error.ShouldBeOfType<ServerError>().ErrorType.ShouldBe(ErrorType.UnknownOrder);
    }

    // ── the transport-agnostic interfaces ────────────────────────────────────────────────

    [Fact]
    public async Task The_transport_agnostic_interfaces_reach_the_same_calls()
    {
        var handler = new StubHttpMessageHandler(request => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(request.Method == HttpMethod.Put ? UpdatedOrder : CanceledOrders, System.Text.Encoding.UTF8, "application/json"),
        });
        var api = handler.RestClient().SpotApi.SharedApi;
        ICancelAllSpotOrders cancelAll = api;
        ICancelAllSpotSymbolOrders cancelAllSymbol = api;
        IEditSpotOrder edit = api;
        IEditSpotOrderByClientOrderId editByClientOrderId = api;
        var ct = TestContext.Current.CancellationToken;

        var all = await cancelAll.CancelAllSpotOrdersAsync(new CancelAllOrdersRequest(Operator()), ct);
        var symbol = await cancelAllSymbol.CancelAllSpotSymbolOrdersAsync(new CancelAllSymbolOrdersRequest(EthEur, Operator()), ct);
        var byId = await edit.EditSpotOrderAsync(new EditOrderRequest(EthEur, LimitOrderId, SharedQuantity.Base(0.75m), 1600m, Operator()), ct);
        var byClientOrderId = await editByClientOrderId.EditSpotOrderByClientOrderIdAsync(new EditOrderRequest(EthEur, LimitClientOrderId, null, 1600m, Operator()), ct);

        all.Success.ShouldBeTrue();
        symbol.Success.ShouldBeTrue();
        byId.Success.ShouldBeTrue();
        byClientOrderId.Success.ShouldBeTrue();
        byId.Data.Id.ShouldBe(LimitOrderId);
        handler.Requests.Select(x => x.Method.Method + " " + x.RequestUri!.AbsolutePath).ToArray()
            .ShouldBe(new[] { "DELETE /v2/orders", "DELETE /v2/orders", "PUT /v2/order", "PUT /v2/order" });
    }

    // ── the options ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Every_order_management_capability_needs_credentials_and_the_operator_id()
    {
        var api = new StubHttpMessageHandler("[]").RestClient().SpotApi.SharedApi;
        CapabilityOptions[] all =
        [
            api.CancelAllSpotOrdersOptions,
            api.CancelAllSpotSymbolOrdersOptions,
            api.EditSpotOrderOptions,
            api.EditSpotOrderByClientOrderIdOptions,
        ];

        foreach (var options in all)
        {
            options.NeedsAuthentication.ShouldBeTrue(options.OperationName);
            var rule = options.ExchangeParameterRules.ShouldHaveSingleItem();
            rule.Name.ShouldBe("OperatorId");
            rule.Requirement.ShouldBe(ExchangeParameterRequirement.Required);
            rule.ValueType.ShouldBe(typeof(long));
            options.RequestNotes.ShouldNotBeNull().ShouldContain("OperatorId");
        }
    }

    [Fact]
    public void The_cancel_all_notes_state_the_rate_limit_weights_and_that_the_contract_returns_no_ids()
    {
        var api = new StubHttpMessageHandler("[]").RestClient().SpotApi.SharedApi;

        var allNotes = api.CancelAllSpotOrdersOptions.RequestNotes.ShouldNotBeNull();
        allNotes.ShouldContain("100");
        allNotes.ShouldContain("no order ids");
        api.CancelAllSpotSymbolOrdersOptions.RequestNotes.ShouldNotBeNull().ShouldContain("25");
        api.CancelAllSpotSymbolOrdersOptions.RequestParameterRules.Single(x => x.Name == "Symbol").Support.ShouldBe(RequestParameterSupport.Required);
    }

    [Fact]
    public void The_edit_options_say_what_Bitvavo_can_change_and_what_it_refuses()
    {
        var api = new StubHttpMessageHandler("[]").RestClient().SpotApi.SharedApi;

        foreach (var options in new CapabilityOptions[] { api.EditSpotOrderOptions, api.EditSpotOrderByClientOrderIdOptions })
        {
            var quantity = options.RequestParameterRules.Single(x => x.Name == "Quantity");
            var price = options.RequestParameterRules.Single(x => x.Name == "Price");
            quantity.Support.ShouldBe(RequestParameterSupport.Optional);
            price.Support.ShouldBe(RequestParameterSupport.Optional);
            quantity.Description.ShouldContain("239");
            var notes = options.RequestNotes.ShouldNotBeNull();
            notes.ShouldContain("234");
            notes.ShouldContain("239");
            notes.ShouldContain("232");
        }
    }
}
