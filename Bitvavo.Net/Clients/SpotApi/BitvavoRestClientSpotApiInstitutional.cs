// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Bitvavo.Net.Interfaces.Clients.SpotApi;
using Bitvavo.Net.Objects.Internal;
using Bitvavo.Net.Objects.Models.Spot;
using CryptoExchange.Net.Objects;

namespace Bitvavo.Net.Clients.SpotApi;

/// <inheritdoc cref="IBitvavoRestClientSpotApiInstitutional" />
internal sealed class BitvavoRestClientSpotApiInstitutional : IBitvavoRestClientSpotApiInstitutional
{
    private static readonly RequestDefinitionCache _definitions = new();
    private readonly BitvavoRestClientSpotApi _baseClient;

    internal BitvavoRestClientSpotApiInstitutional(BitvavoRestClientSpotApi baseClient)
    {
        _baseClient = baseClient;
    }

    /// <inheritdoc />
    public Task<HttpResult<BitvavoSubaccount>> CreateSubaccountAsync(string? label = null, CancellationToken ct = default)
    {
        var body = new Parameters(BitvavoExchange.ParameterSerializationSettings);
        body.Add("label", label);

        var def = _definitions.GetOrCreate(HttpMethod.Post, _baseClient.BaseAddress, "v2/subaccounts", BitvavoRestClientSpotApi.RateLimitGate, weight: 5, authenticated: true);
        return _baseClient.SendAsync<BitvavoSubaccount>(def, queryParameters: null, bodyParameters: body, ct);
    }

    /// <inheritdoc />
    public Task<HttpResult<BitvavoSubaccountList>> GetSubaccountsAsync(int? page = null, int? maxItems = null, CancellationToken ct = default)
    {
        var parameters = new Parameters(BitvavoExchange.ParameterSerializationSettings);
        parameters.Add("page", page);
        parameters.Add("maxItems", maxItems);

        var def = _definitions.GetOrCreate(HttpMethod.Get, _baseClient.BaseAddress, "v2/subaccounts", BitvavoRestClientSpotApi.RateLimitGate, weight: 5, authenticated: true);
        return _baseClient.SendAsync<BitvavoSubaccountList>(def, parameters, ct);
    }

    /// <inheritdoc />
    public Task<HttpResult<BitvavoSubaccountTransfer>> CreateTransferAsync(BitvavoCreateTransferRequest request, CancellationToken ct = default)
    {
        var body = new Parameters(BitvavoExchange.ParameterSerializationSettings);
        body.Add("subaccountId", request.SubaccountId);
        body.Add("direction", request.Direction);
        body.Add("symbol", request.Symbol);
        body.Add("amount", request.Amount);
        body.Add("clientRequestId", request.ClientRequestId);

        var def = _definitions.GetOrCreate(HttpMethod.Post, _baseClient.BaseAddress, "v2/subaccounts/transfers", BitvavoRestClientSpotApi.RateLimitGate, weight: 5, authenticated: true);
        return _baseClient.SendAsync<BitvavoSubaccountTransfer>(def, queryParameters: null, bodyParameters: body, ct);
    }

    /// <inheritdoc />
    public Task<HttpResult<BitvavoSubaccountTransfer>> GetTransferAsync(string transferId, CancellationToken ct = default)
    {
        var def = _definitions.GetOrCreate(HttpMethod.Get, _baseClient.BaseAddress, $"v2/subaccounts/transfers/{transferId}", BitvavoRestClientSpotApi.RateLimitGate, weight: 5, authenticated: true);
        return _baseClient.SendAsync<BitvavoSubaccountTransfer>(def, parameters: null, ct);
    }

    /// <inheritdoc />
    public Task<HttpResult<BitvavoSubaccountTransferList>> GetTransfersAsync(
        string subaccountId,
        string? clientRequestId = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int? limit = null,
        CancellationToken ct = default)
    {
        var parameters = new Parameters(BitvavoExchange.ParameterSerializationSettings) { { "subaccountId", subaccountId } };
        parameters.Add("clientRequestId", clientRequestId);
        parameters.Add("start", startTime);
        parameters.Add("end", endTime);
        parameters.Add("limit", limit);

        var def = _definitions.GetOrCreate(HttpMethod.Get, _baseClient.BaseAddress, "v2/subaccounts/transfers", BitvavoRestClientSpotApi.RateLimitGate, weight: 5, authenticated: true);
        return _baseClient.SendAsync<BitvavoSubaccountTransferList>(def, parameters, ct);
    }

    /// <inheritdoc />
    public Task<HttpResult<BitvavoSubaccountBalances>> GetSubaccountBalancesAsync(
        string? subaccountId = null,
        string? symbol = null,
        CancellationToken ct = default)
    {
        var parameters = new Parameters(BitvavoExchange.ParameterSerializationSettings);
        parameters.Add("subaccountId", subaccountId);
        parameters.Add("symbol", symbol);

        var def = _definitions.GetOrCreate(HttpMethod.Get, _baseClient.BaseAddress, "v2/institutional/subaccounts/balance", BitvavoRestClientSpotApi.RateLimitGate, weight: 5, authenticated: true);
        return _baseClient.SendAsync<BitvavoSubaccountBalances>(def, parameters, ct);
    }

    /// <inheritdoc />
    public Task<HttpResult<BitvavoTransactionHistory>> GetSubaccountTransactionHistoryAsync(
        string? subaccountId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int? page = null,
        int? maxItems = null,
        string? type = null,
        CancellationToken ct = default)
    {
        var parameters = new Parameters(BitvavoExchange.ParameterSerializationSettings);
        parameters.Add("subaccountId", subaccountId);
        parameters.Add("fromDate", fromDate);
        parameters.Add("toDate", toDate);
        parameters.Add("page", page);
        parameters.Add("maxItems", maxItems);
        parameters.Add("type", type);

        // weight assumed — Bitvavo docs page did not state it; conservative default
        var def = _definitions.GetOrCreate(HttpMethod.Get, _baseClient.BaseAddress, "v2/institutional/subaccounts/history", BitvavoRestClientSpotApi.RateLimitGate, weight: 5, authenticated: true);
        return _baseClient.SendAsync<BitvavoTransactionHistory>(def, parameters, ct);
    }

    /// <inheritdoc />
    public Task<HttpResult<IEnumerable<BitvavoOrder>>> GetSubaccountOpenOrdersAsync(
        string? subaccountId = null,
        string? market = null,
        string? baseAsset = null,
        CancellationToken ct = default)
    {
        var parameters = new Parameters(BitvavoExchange.ParameterSerializationSettings);
        parameters.Add("subaccountId", subaccountId);
        parameters.Add("market", market);
        parameters.Add("base", baseAsset);

        // institutional/subaccounts/orders/open: weight=100 when no market filter, weight=5 otherwise — per call, not per cached definition.
        var weight = market is null ? 100 : 5;
        var def = _definitions.GetOrCreate(HttpMethod.Get, _baseClient.BaseAddress, "v2/institutional/subaccounts/orders/open", BitvavoRestClientSpotApi.RateLimitGate, weight: weight, authenticated: true);
        return _baseClient.SendAsync<IEnumerable<BitvavoOrder>>(def, parameters, ct, weight);
    }

    /// <inheritdoc />
    public Task<HttpResult<BitvavoOrderId>> CancelSubaccountOrderAsync(BitvavoSubaccountCancelOrderRequest request, CancellationToken ct = default)
    {
        var body = new Parameters(BitvavoExchange.ParameterSerializationSettings);
        body.Add("subaccountId", request.SubaccountId);
        body.Add("market", request.Market);
        body.Add("orderId", request.OrderId);
        body.Add("clientOrderId", request.ClientOrderId);
        body.Add("operatorId", request.OperatorId);

        var def = _definitions.GetOrCreate(HttpMethod.Delete, _baseClient.BaseAddress, "v2/institutional/subaccounts/order", BitvavoRestClientSpotApi.RateLimitGate, weight: 1, authenticated: true);
        return _baseClient.SendAsync<BitvavoOrderId>(def, queryParameters: null, bodyParameters: body, ct);
    }

    /// <inheritdoc />
    public Task<HttpResult<IEnumerable<BitvavoOrderId>>> CancelSubaccountOrdersAsync(
        long operatorId,
        string? subaccountId = null,
        string? market = null,
        CancellationToken ct = default)
    {
        var body = new Parameters(BitvavoExchange.ParameterSerializationSettings);
        body.Add("subaccountId", subaccountId);
        body.Add("market", market);
        body.Add("operatorId", operatorId);

        // institutional/subaccounts/orders DELETE: weight=100 when no market filter, weight=25 otherwise — per call, not per cached definition.
        var weight = market is null ? 100 : 25;
        var def = _definitions.GetOrCreate(HttpMethod.Delete, _baseClient.BaseAddress, "v2/institutional/subaccounts/orders", BitvavoRestClientSpotApi.RateLimitGate, weight: weight, authenticated: true);
        return _baseClient.SendAsync<IEnumerable<BitvavoOrderId>>(def, queryParameters: null, bodyParameters: body, ct, weight);
    }
}
