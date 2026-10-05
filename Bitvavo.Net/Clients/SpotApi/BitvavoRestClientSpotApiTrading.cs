// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Bitvavo.Net.Enums;
using Bitvavo.Net.Interfaces.Clients.SpotApi;
using Bitvavo.Net.Objects.Internal;
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
    public Task<HttpResult<BitvavoOrder>> PlaceOrderAsync(BitvavoPlaceOrderRequest request, CancellationToken ct = default)
    {
        var body = new Parameters(BitvavoExchange.ParameterSerializationSettings);
        body.Add("market", request.Market);
        body.Add("side", request.Side);
        body.Add("orderType", request.OrderType);
        body.Add("operatorId", request.OperatorId);
        body.Add("amount", request.Amount);
        body.Add("amountQuote", request.AmountQuote);
        body.Add("price", request.Price);
        body.Add("triggerAmount", request.TriggerAmount);
        body.Add("triggerType", request.TriggerType);
        body.Add("triggerReference", request.TriggerReference);
        body.Add("timeInForce", request.TimeInForce);
        body.Add("postOnly", request.PostOnly);
        body.Add("selfTradePrevention", request.SelfTradePrevention);
        body.Add("responseRequired", request.ResponseRequired);
        body.Add("clientOrderId", request.ClientOrderId);
        body.Add("codGroupId", request.CodGroupId);
        body.Add("disableMarketProtection", request.DisableMarketProtection);

        var def = _definitions.GetOrCreate(HttpMethod.Post, _baseClient.BaseAddress, "v2/order", BitvavoRestClientSpotApi.RateLimitGate, weight: 1, authenticated: true);
        return _baseClient.SendAsync<BitvavoOrder>(def, queryParameters: null, bodyParameters: body, ct);
    }

    /// <inheritdoc />
    public Task<HttpResult<BitvavoOrder>> UpdateOrderAsync(BitvavoUpdateOrderRequest request, CancellationToken ct = default)
    {
        var body = new Parameters(BitvavoExchange.ParameterSerializationSettings);
        body.Add("market", request.Market);
        body.Add("operatorId", request.OperatorId);
        body.Add("orderId", request.OrderId);
        body.Add("clientOrderId", request.ClientOrderId);
        body.Add("amount", request.Amount);
        body.Add("amountQuote", request.AmountQuote);
        body.Add("price", request.Price);
        body.Add("triggerAmount", request.TriggerAmount);
        body.Add("timeInForce", request.TimeInForce);
        body.Add("selfTradePrevention", request.SelfTradePrevention);
        body.Add("postOnly", request.PostOnly);
        body.Add("responseRequired", request.ResponseRequired);
        body.Add("amountRemaining", request.AmountRemaining);

        var def = _definitions.GetOrCreate(HttpMethod.Put, _baseClient.BaseAddress, "v2/order", BitvavoRestClientSpotApi.RateLimitGate, weight: 1, authenticated: true);
        return _baseClient.SendAsync<BitvavoOrder>(def, queryParameters: null, bodyParameters: body, ct);
    }

    /// <inheritdoc />
    public Task<HttpResult<BitvavoOrder>> GetOrderAsync(string market, string? orderId = null, string? clientOrderId = null, CancellationToken ct = default)
    {
        var parameters = new Parameters(BitvavoExchange.ParameterSerializationSettings);
        parameters.Add("market", market);
        parameters.Add("orderId", orderId);
        parameters.Add("clientOrderId", clientOrderId);

        var def = _definitions.GetOrCreate(HttpMethod.Get, _baseClient.BaseAddress, "v2/order", BitvavoRestClientSpotApi.RateLimitGate, weight: 1, authenticated: true);
        return _baseClient.SendAsync<BitvavoOrder>(def, parameters, ct);
    }

    /// <inheritdoc />
    public Task<HttpResult<BitvavoOrderId>> CancelOrderAsync(string market, long operatorId, string? orderId = null, string? clientOrderId = null, CancellationToken ct = default)
    {
        var parameters = new Parameters(BitvavoExchange.ParameterSerializationSettings);
        parameters.Add("market", market);
        parameters.Add("operatorId", operatorId);
        parameters.Add("orderId", orderId);
        parameters.Add("clientOrderId", clientOrderId);

        // DELETE /order is a QUERY endpoint in Bitvavo's spec (market, orderId, operatorId, clientOrderId): the parameters travel in the URI.
        var def = _definitions.GetOrCreate(HttpMethod.Delete, _baseClient.BaseAddress, "v2/order", BitvavoRestClientSpotApi.RateLimitGate, weight: 1, authenticated: true, parameterPosition: HttpMethodParameterPosition.InUri);
        return _baseClient.SendAsync<BitvavoOrderId>(def, parameters, ct);
    }

    /// <inheritdoc />
    public Task<HttpResult<IEnumerable<BitvavoOrderId>>> CancelOrdersAsync(long operatorId, string? market = null, CancellationToken ct = default)
    {
        var parameters = new Parameters(BitvavoExchange.ParameterSerializationSettings);
        parameters.Add("operatorId", operatorId);
        parameters.Add("market", market);

        // v2/orders DELETE: weight=100 when cancelling all (no market filter), weight=25 otherwise. The weight depends on the call, and a
        // definition is cached on first use — so it travels with every call rather than with the definition.
        var weight = market is null ? 100 : 25;
        var def = _definitions.GetOrCreate(HttpMethod.Delete, _baseClient.BaseAddress, "v2/orders", BitvavoRestClientSpotApi.RateLimitGate, weight: weight, authenticated: true, parameterPosition: HttpMethodParameterPosition.InUri);
        return _baseClient.SendAsync<IEnumerable<BitvavoOrderId>>(def, parameters, ct, weight);
    }

    /// <inheritdoc />
    public Task<HttpResult<IEnumerable<BitvavoOrderId>>> CancelOrdersAtomicAsync(string market, OrderSide side, long operatorId, CancellationToken ct = default)
    {
        // Unlike DELETE /order(s), the atomic cancel is a BODY endpoint: the parameters travel as JSON and the signature covers them.
        var body = new Parameters(BitvavoExchange.ParameterSerializationSettings);
        body.Add("market", market);
        body.Add("side", side);
        body.Add("operatorId", operatorId);

        var def = _definitions.GetOrCreate(HttpMethod.Delete, _baseClient.BaseAddress, "v2/atomic/orders", BitvavoRestClientSpotApi.RateLimitGate, weight: 100, authenticated: true);
        return _baseClient.SendAsync<IEnumerable<BitvavoOrderId>>(def, queryParameters: null, bodyParameters: body, ct);
    }

    /// <inheritdoc />
    public Task<HttpResult<IEnumerable<BitvavoOrder>>> GetOpenOrdersAsync(string? market = null, string? baseAsset = null, CancellationToken ct = default)
    {
        var parameters = new Parameters(BitvavoExchange.ParameterSerializationSettings);
        parameters.Add("market", market);
        parameters.Add("base", baseAsset);

        // v2/ordersOpen: weight=100 when no market filter (returns all), weight=5 otherwise — per call, not per cached definition.
        var weight = market is null ? 100 : 5;
        var def = _definitions.GetOrCreate(HttpMethod.Get, _baseClient.BaseAddress, "v2/ordersOpen", BitvavoRestClientSpotApi.RateLimitGate, weight: weight, authenticated: true);
        return _baseClient.SendAsync<IEnumerable<BitvavoOrder>>(def, parameters, ct, weight);
    }

    /// <inheritdoc />
    public Task<HttpResult<IEnumerable<BitvavoOrder>>> GetOrderHistoryAsync(
        string market,
        int? limit = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        string? orderIdFrom = null,
        string? orderIdTo = null,
        CancellationToken ct = default)
    {
        var parameters = new Parameters(BitvavoExchange.ParameterSerializationSettings);
        parameters.Add("market", market);
        parameters.Add("limit", limit);
        parameters.Add("start", startTime);
        parameters.Add("end", endTime);
        parameters.Add("orderIdFrom", orderIdFrom);
        parameters.Add("orderIdTo", orderIdTo);

        var def = _definitions.GetOrCreate(HttpMethod.Get, _baseClient.BaseAddress, "v2/orders", BitvavoRestClientSpotApi.RateLimitGate, weight: 5, authenticated: true);
        return _baseClient.SendAsync<IEnumerable<BitvavoOrder>>(def, parameters, ct);
    }

    /// <inheritdoc />
    public Task<HttpResult<IEnumerable<BitvavoFill>>> GetUserTradesAsync(
        string market,
        int? limit = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        string? tradeIdFrom = null,
        string? tradeIdTo = null,
        string? tradeId = null,
        CancellationToken ct = default)
    {
        var parameters = new Parameters(BitvavoExchange.ParameterSerializationSettings);
        parameters.Add("market", market);
        parameters.Add("limit", limit);
        parameters.Add("start", startTime);
        parameters.Add("end", endTime);
        parameters.Add("tradeIdFrom", tradeIdFrom);
        parameters.Add("tradeIdTo", tradeIdTo);
        parameters.Add("tradeId", tradeId);

        var def = _definitions.GetOrCreate(HttpMethod.Get, _baseClient.BaseAddress, "v2/trades", BitvavoRestClientSpotApi.RateLimitGate, weight: 5, authenticated: true);
        return _baseClient.SendAsync<IEnumerable<BitvavoFill>>(def, parameters, ct);
    }
}
