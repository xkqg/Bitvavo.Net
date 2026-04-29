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

/// <inheritdoc cref="IBitvavoRestClientSpotApiTrading" />
internal sealed class BitvavoRestClientSpotApiTrading : IBitvavoRestClientSpotApiTrading
{
    private static readonly RequestDefinitionCache _definitions = new();
    private readonly BitvavoRestClientSpotApi _baseClient;

    internal BitvavoRestClientSpotApiTrading(BitvavoRestClientSpotApi baseClient)
    {
        _baseClient = baseClient;
    }

    /// <inheritdoc />
    public Task<WebCallResult<BitvavoOrder>> PlaceOrderAsync(BitvavoPlaceOrderRequest request, CancellationToken ct = default)
    {
        var body = new ParameterCollection();
        body.Add("market", request.Market);
        body.AddEnum("side", request.Side);
        body.AddEnum("orderType", request.OrderType);
        body.Add("operatorId", request.OperatorId);
        body.AddOptionalString("amount", request.Amount);
        body.AddOptionalString("amountQuote", request.AmountQuote);
        body.AddOptionalString("price", request.Price);
        body.AddOptionalString("triggerAmount", request.TriggerAmount);
        body.AddOptionalEnum("triggerType", request.TriggerType);
        body.AddOptionalEnum("triggerReference", request.TriggerReference);
        body.AddOptionalEnum("timeInForce", request.TimeInForce);
        body.AddOptional("postOnly", request.PostOnly);
        body.AddOptionalEnum("selfTradePrevention", request.SelfTradePrevention);
        body.AddOptional("responseRequired", request.ResponseRequired);
        body.AddOptional("clientOrderId", request.ClientOrderId);
        body.AddOptional("codGroupId", request.CodGroupId);

        var def = _definitions.GetOrCreate(HttpMethod.Post, "v2/order", BitvavoRestClientSpotApi.RateLimitGate, weight: 1, authenticated: true);
        return _baseClient.SendAsync<BitvavoOrder>(def, queryParameters: null, bodyParameters: body, ct);
    }

    /// <inheritdoc />
    public Task<WebCallResult<BitvavoOrder>> UpdateOrderAsync(BitvavoUpdateOrderRequest request, CancellationToken ct = default)
    {
        var body = new ParameterCollection();
        body.Add("market", request.Market);
        body.Add("operatorId", request.OperatorId);
        body.AddOptional("orderId", request.OrderId);
        body.AddOptional("clientOrderId", request.ClientOrderId);
        body.AddOptionalString("amount", request.Amount);
        body.AddOptionalString("amountQuote", request.AmountQuote);
        body.AddOptionalString("price", request.Price);
        body.AddOptionalString("triggerAmount", request.TriggerAmount);
        body.AddOptionalEnum("timeInForce", request.TimeInForce);
        body.AddOptionalEnum("selfTradePrevention", request.SelfTradePrevention);
        body.AddOptional("postOnly", request.PostOnly);
        body.AddOptional("responseRequired", request.ResponseRequired);

        var def = _definitions.GetOrCreate(HttpMethod.Put, "v2/order", true);
        return _baseClient.SendAsync<BitvavoOrder>(def, queryParameters: null, bodyParameters: body, ct);
    }

    /// <inheritdoc />
    public Task<WebCallResult<BitvavoOrder>> GetOrderAsync(string market, string? orderId = null, string? clientOrderId = null, CancellationToken ct = default)
    {
        var parameters = new ParameterCollection();
        parameters.Add("market", market);
        parameters.AddOptional("orderId", orderId);
        parameters.AddOptional("clientOrderId", clientOrderId);

        var def = _definitions.GetOrCreate(HttpMethod.Get, "v2/order", true);
        return _baseClient.SendAsync<BitvavoOrder>(def, parameters, ct);
    }

    /// <inheritdoc />
    public Task<WebCallResult<BitvavoOrderId>> CancelOrderAsync(string market, long operatorId, string? orderId = null, string? clientOrderId = null, CancellationToken ct = default)
    {
        var parameters = new ParameterCollection();
        parameters.Add("market", market);
        parameters.Add("operatorId", operatorId);
        parameters.AddOptional("orderId", orderId);
        parameters.AddOptional("clientOrderId", clientOrderId);

        // Bitvavo signs path + query for DELETE (never body for non-POST/PUT), so parameters
        // must land in the URI — see RequestDefinitionCacheExtensions.GetOrCreateInUri.
        var def = _definitions.GetOrCreateInUri(HttpMethod.Delete, "v2/order", true);
        return _baseClient.SendAsync<BitvavoOrderId>(def, parameters, ct);
    }

    /// <inheritdoc />
    public Task<WebCallResult<IEnumerable<BitvavoOrderId>>> CancelOrdersAsync(string? market = null, CancellationToken ct = default)
    {
        var parameters = new ParameterCollection();
        parameters.AddOptional("market", market);

        var def = _definitions.GetOrCreateInUri(HttpMethod.Delete, "v2/orders", true);
        return _baseClient.SendAsync<IEnumerable<BitvavoOrderId>>(def, parameters, ct);
    }

    /// <inheritdoc />
    public Task<WebCallResult<IEnumerable<BitvavoOrder>>> GetOpenOrdersAsync(string? market = null, string? baseAsset = null, CancellationToken ct = default)
    {
        var parameters = new ParameterCollection();
        parameters.AddOptional("market", market);
        parameters.AddOptional("base", baseAsset);

        var def = _definitions.GetOrCreate(HttpMethod.Get, "v2/ordersOpen", true);
        return _baseClient.SendAsync<IEnumerable<BitvavoOrder>>(def, parameters, ct);
    }

    /// <inheritdoc />
    public Task<WebCallResult<IEnumerable<BitvavoOrder>>> GetOrderHistoryAsync(
        string market,
        int? limit = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        string? orderIdFrom = null,
        string? orderIdTo = null,
        CancellationToken ct = default)
    {
        var parameters = new ParameterCollection();
        parameters.Add("market", market);
        parameters.AddOptional("limit", limit);
        parameters.AddOptionalMilliseconds("start", startTime);
        parameters.AddOptionalMilliseconds("end", endTime);
        parameters.AddOptional("orderIdFrom", orderIdFrom);
        parameters.AddOptional("orderIdTo", orderIdTo);

        var def = _definitions.GetOrCreate(HttpMethod.Get, "v2/orders", true);
        return _baseClient.SendAsync<IEnumerable<BitvavoOrder>>(def, parameters, ct);
    }

    /// <inheritdoc />
    public Task<WebCallResult<IEnumerable<BitvavoFill>>> GetUserTradesAsync(
        string market,
        int? limit = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        string? tradeIdFrom = null,
        string? tradeIdTo = null,
        CancellationToken ct = default)
    {
        var parameters = new ParameterCollection();
        parameters.Add("market", market);
        parameters.AddOptional("limit", limit);
        parameters.AddOptionalMilliseconds("start", startTime);
        parameters.AddOptionalMilliseconds("end", endTime);
        parameters.AddOptional("tradeIdFrom", tradeIdFrom);
        parameters.AddOptional("tradeIdTo", tradeIdTo);

        var def = _definitions.GetOrCreate(HttpMethod.Get, "v2/trades", true);
        return _baseClient.SendAsync<IEnumerable<BitvavoFill>>(def, parameters, ct);
    }
}
