// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Bitvavo.Net.Interfaces.Clients.SpotApi;
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
    public Task<WebCallResult<BitvavoSubaccount>> CreateSubaccountAsync(string? label = null, CancellationToken ct = default)
    {
        var body = new ParameterCollection();
        body.AddOptional("label", label);

        var def = _definitions.GetOrCreate(HttpMethod.Post, "v2/subaccounts", BitvavoRestClientSpotApi.RateLimitGate, weight: 5, authenticated: true);
        return _baseClient.SendAsync<BitvavoSubaccount>(def, queryParameters: null, bodyParameters: body, ct);
    }

    /// <inheritdoc />
    public Task<WebCallResult<BitvavoSubaccountList>> GetSubaccountsAsync(int? page = null, int? maxItems = null, CancellationToken ct = default)
    {
        var parameters = new ParameterCollection();
        parameters.AddOptional("page", page);
        parameters.AddOptional("maxItems", maxItems);

        var def = _definitions.GetOrCreate(HttpMethod.Get, "v2/subaccounts", BitvavoRestClientSpotApi.RateLimitGate, weight: 5, authenticated: true);
        return _baseClient.SendAsync<BitvavoSubaccountList>(def, parameters, ct);
    }

    /// <inheritdoc />
    public Task<WebCallResult<BitvavoSubaccountTransfer>> CreateTransferAsync(BitvavoCreateTransferRequest request, CancellationToken ct = default)
    {
        var body = new ParameterCollection();
        body.Add("subaccountId", request.SubaccountId);
        body.AddEnum("direction", request.Direction);
        body.Add("symbol", request.Symbol);
        body.AddString("amount", request.Amount);
        body.AddOptional("clientRequestId", request.ClientRequestId);

        var def = _definitions.GetOrCreate(HttpMethod.Post, "v2/subaccounts/transfers", BitvavoRestClientSpotApi.RateLimitGate, weight: 5, authenticated: true);
        return _baseClient.SendAsync<BitvavoSubaccountTransfer>(def, queryParameters: null, bodyParameters: body, ct);
    }

    /// <inheritdoc />
    public Task<WebCallResult<BitvavoSubaccountTransfer>> GetTransferAsync(string transferId, CancellationToken ct = default)
    {
        var def = _definitions.GetOrCreate(HttpMethod.Get, $"v2/subaccounts/transfers/{transferId}", BitvavoRestClientSpotApi.RateLimitGate, weight: 5, authenticated: true);
        return _baseClient.SendAsync<BitvavoSubaccountTransfer>(def, parameters: null, ct);
    }

    /// <inheritdoc />
    public Task<WebCallResult<BitvavoSubaccountTransferList>> GetTransfersAsync(
        string subaccountId,
        string? clientRequestId = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int? limit = null,
        CancellationToken ct = default)
    {
        var parameters = new ParameterCollection { { "subaccountId", subaccountId } };
        parameters.AddOptional("clientRequestId", clientRequestId);
        parameters.AddOptionalMilliseconds("start", startTime);
        parameters.AddOptionalMilliseconds("end", endTime);
        parameters.AddOptional("limit", limit);

        var def = _definitions.GetOrCreate(HttpMethod.Get, "v2/subaccounts/transfers", BitvavoRestClientSpotApi.RateLimitGate, weight: 5, authenticated: true);
        return _baseClient.SendAsync<BitvavoSubaccountTransferList>(def, parameters, ct);
    }

    /// <inheritdoc />
    public Task<WebCallResult<BitvavoSubaccountBalances>> GetSubaccountBalancesAsync(
        string? subaccountId = null,
        string? symbol = null,
        CancellationToken ct = default)
    {
        var parameters = new ParameterCollection();
        parameters.AddOptional("subaccountId", subaccountId);
        parameters.AddOptional("symbol", symbol);

        var def = _definitions.GetOrCreate(HttpMethod.Get, "v2/institutional/subaccounts/balance", BitvavoRestClientSpotApi.RateLimitGate, weight: 5, authenticated: true);
        return _baseClient.SendAsync<BitvavoSubaccountBalances>(def, parameters, ct);
    }

    /// <inheritdoc />
    public Task<WebCallResult<BitvavoTransactionHistory>> GetSubaccountTransactionHistoryAsync(
        string? subaccountId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int? page = null,
        int? maxItems = null,
        string? type = null,
        CancellationToken ct = default)
    {
        var parameters = new ParameterCollection();
        parameters.AddOptional("subaccountId", subaccountId);
        parameters.AddOptionalMilliseconds("fromDate", fromDate);
        parameters.AddOptionalMilliseconds("toDate", toDate);
        parameters.AddOptional("page", page);
        parameters.AddOptional("maxItems", maxItems);
        parameters.AddOptional("type", type);

        // weight assumed — Bitvavo docs page did not state it; conservative default
        var def = _definitions.GetOrCreate(HttpMethod.Get, "v2/institutional/subaccounts/history", BitvavoRestClientSpotApi.RateLimitGate, weight: 5, authenticated: true);
        return _baseClient.SendAsync<BitvavoTransactionHistory>(def, parameters, ct);
    }

    /// <inheritdoc />
    public Task<WebCallResult<IEnumerable<BitvavoOrder>>> GetSubaccountOpenOrdersAsync(
        string? subaccountId = null,
        string? market = null,
        string? baseAsset = null,
        CancellationToken ct = default)
    {
        var parameters = new ParameterCollection();
        parameters.AddOptional("subaccountId", subaccountId);
        parameters.AddOptional("market", market);
        parameters.AddOptional("base", baseAsset);

        // institutional/subaccounts/orders/open: weight=100 when no market filter, weight=5 otherwise.
        var def = _definitions.GetOrCreate(HttpMethod.Get, "v2/institutional/subaccounts/orders/open", BitvavoRestClientSpotApi.RateLimitGate, weight: market is null ? 100 : 5, authenticated: true);
        return _baseClient.SendAsync<IEnumerable<BitvavoOrder>>(def, parameters, ct);
    }

    /// <inheritdoc />
    public Task<WebCallResult<BitvavoOrderId>> CancelSubaccountOrderAsync(BitvavoSubaccountCancelOrderRequest request, CancellationToken ct = default)
    {
        var body = new ParameterCollection();
        body.AddOptional("subaccountId", request.SubaccountId);
        body.Add("market", request.Market);
        body.Add("orderId", request.OrderId);
        body.AddOptional("clientOrderId", request.ClientOrderId);
        body.Add("operatorId", request.OperatorId);

        var def = _definitions.GetOrCreate(HttpMethod.Delete, "v2/institutional/subaccounts/order", BitvavoRestClientSpotApi.RateLimitGate, weight: 1, authenticated: true);
        return _baseClient.SendAsync<BitvavoOrderId>(def, queryParameters: null, bodyParameters: body, ct);
    }

    /// <inheritdoc />
    public Task<WebCallResult<IEnumerable<BitvavoOrderId>>> CancelSubaccountOrdersAsync(
        long operatorId,
        string? subaccountId = null,
        string? market = null,
        CancellationToken ct = default)
    {
        var body = new ParameterCollection();
        body.AddOptional("subaccountId", subaccountId);
        body.AddOptional("market", market);
        body.Add("operatorId", operatorId);

        // institutional/subaccounts/orders DELETE: weight=100 when no market filter, weight=25 otherwise.
        var def = _definitions.GetOrCreate(HttpMethod.Delete, "v2/institutional/subaccounts/orders", BitvavoRestClientSpotApi.RateLimitGate, weight: market is null ? 100 : 25, authenticated: true);
        return _baseClient.SendAsync<IEnumerable<BitvavoOrderId>>(def, queryParameters: null, bodyParameters: body, ct);
    }
}
