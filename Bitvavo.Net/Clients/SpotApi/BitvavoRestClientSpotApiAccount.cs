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

/// <inheritdoc cref="IBitvavoRestClientSpotApiAccount" />
internal sealed class BitvavoRestClientSpotApiAccount : IBitvavoRestClientSpotApiAccount
{
    private static readonly RequestDefinitionCache _definitions = new();
    private readonly BitvavoRestClientSpotApi _baseClient;

    internal BitvavoRestClientSpotApiAccount(BitvavoRestClientSpotApi baseClient)
    {
        _baseClient = baseClient;
    }

    /// <inheritdoc />
    public Task<HttpResult<BitvavoAccountInfo>> GetAccountInfoAsync(CancellationToken ct = default)
    {
        // weight assumed — Bitvavo docs page did not state it; conservative default
        var request = _definitions.GetOrCreate(HttpMethod.Get, _baseClient.BaseAddress, "v2/account", BitvavoRestClientSpotApi.RateLimitGate, weight: 1, authenticated: true);
        return _baseClient.SendAsync<BitvavoAccountInfo>(request, parameters: null, ct);
    }

    /// <inheritdoc />
    public Task<HttpResult<IEnumerable<BitvavoBalance>>> GetBalancesAsync(string? symbol = null, CancellationToken ct = default)
    {
        var parameters = new Parameters(BitvavoExchange.ParameterSerializationSettings);
        parameters.Add("symbol", symbol);

        var request = _definitions.GetOrCreate(HttpMethod.Get, _baseClient.BaseAddress, "v2/balance", BitvavoRestClientSpotApi.RateLimitGate, weight: 5, authenticated: true);
        return _baseClient.SendAsync<IEnumerable<BitvavoBalance>>(request, parameters, ct);
    }

    /// <inheritdoc />
    public Task<HttpResult<BitvavoMarketFee>> GetTradingFeesAsync(string? market = null, string? quote = null, CancellationToken ct = default)
    {
        var parameters = new Parameters(BitvavoExchange.ParameterSerializationSettings);
        parameters.Add("market", market);
        parameters.Add("quote", quote);

        var request = _definitions.GetOrCreate(HttpMethod.Get, _baseClient.BaseAddress, "v2/account/fees", BitvavoRestClientSpotApi.RateLimitGate, weight: 1, authenticated: true);
        return _baseClient.SendAsync<BitvavoMarketFee>(request, parameters, ct);
    }

    /// <inheritdoc />
    public Task<HttpResult<IEnumerable<BitvavoStakingBalance>>> GetStakingBalanceAsync(string? symbol = null, CancellationToken ct = default)
    {
        var parameters = new Parameters(BitvavoExchange.ParameterSerializationSettings);
        parameters.Add("symbol", symbol);

        var request = _definitions.GetOrCreate(HttpMethod.Get, _baseClient.BaseAddress, "v2/stakingBalance", BitvavoRestClientSpotApi.RateLimitGate, weight: 5, authenticated: true);
        return _baseClient.SendAsync<IEnumerable<BitvavoStakingBalance>>(request, parameters, ct);
    }

    /// <inheritdoc />
    public Task<HttpResult<BitvavoCancelOrdersAfter>> ResetCancelOnDisconnectAsync(
        int codGroupId, int expiryAfterSeconds, CancellationToken ct = default)
    {
        var body = new Parameters(BitvavoExchange.ParameterSerializationSettings);
        body.Add("codGroupId", codGroupId);
        body.Add("expiryAfterSeconds", expiryAfterSeconds);
        // Documented weight 5, deliberately NOT counted — the CoD heartbeat must never be client-side rate-limited: a refused
        // heartbeat lets the timer expire and Bitvavo cancels the group's orders. At one call per ~30 s its weight (~10/min) fits in
        // the headroom the limiter leaves free (10 % of the budget).
        var request = _definitions.GetOrCreate(HttpMethod.Post, _baseClient.BaseAddress, "v2/cancelOrdersAfter", true);
        return _baseClient.SendAsync<BitvavoCancelOrdersAfter>(request, queryParameters: null, bodyParameters: body, ct);
    }

    /// <inheritdoc />
    public Task<HttpResult<BitvavoTransactionHistory>> GetTransactionHistoryAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int? page = null,
        int? maxItems = null,
        string? type = null,
        CancellationToken ct = default)
    {
        var parameters = new Parameters(BitvavoExchange.ParameterSerializationSettings);
        parameters.Add("fromDate", fromDate);
        parameters.Add("toDate", toDate);
        parameters.Add("page", page);
        parameters.Add("maxItems", maxItems);
        parameters.Add("type", type);

        // Bitvavo documents weight 1 for the transaction history ("Get transaction history").
        var request = _definitions.GetOrCreate(HttpMethod.Get, _baseClient.BaseAddress, "v2/account/history", BitvavoRestClientSpotApi.RateLimitGate, weight: 1, authenticated: true);
        return _baseClient.SendAsync<BitvavoTransactionHistory>(request, parameters, ct);
    }
}
