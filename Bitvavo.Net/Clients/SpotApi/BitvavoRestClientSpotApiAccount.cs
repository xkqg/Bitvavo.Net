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
    public Task<WebCallResult<BitvavoAccountInfo>> GetAccountInfoAsync(CancellationToken ct = default)
    {
        // weight assumed — Bitvavo docs page did not state it; conservative default
        var request = _definitions.GetOrCreate(HttpMethod.Get, "v2/account", BitvavoRestClientSpotApi.RateLimitGate, weight: 1, authenticated: true);
        return _baseClient.SendAsync<BitvavoAccountInfo>(request, parameters: null, ct);
    }

    /// <inheritdoc />
    public Task<WebCallResult<IEnumerable<BitvavoBalance>>> GetBalancesAsync(string? symbol = null, CancellationToken ct = default)
    {
        var parameters = new ParameterCollection();
        parameters.AddOptional("symbol", symbol);

        var request = _definitions.GetOrCreate(HttpMethod.Get, "v2/balance", BitvavoRestClientSpotApi.RateLimitGate, weight: 5, authenticated: true);
        return _baseClient.SendAsync<IEnumerable<BitvavoBalance>>(request, parameters, ct);
    }

    /// <inheritdoc />
    public Task<WebCallResult<BitvavoMarketFee>> GetTradingFeesAsync(string? market = null, CancellationToken ct = default)
    {
        var parameters = new ParameterCollection();
        parameters.AddOptional("market", market);

        var request = _definitions.GetOrCreate(HttpMethod.Get, "v2/account/fees", BitvavoRestClientSpotApi.RateLimitGate, weight: 1, authenticated: true);
        return _baseClient.SendAsync<BitvavoMarketFee>(request, parameters, ct);
    }

    /// <inheritdoc />
    public Task<WebCallResult<BitvavoCancelOrdersAfter>> ResetCancelOnDisconnectAsync(
        int codGroupId, int expiryAfterSeconds, CancellationToken ct = default)
    {
        var body = new ParameterCollection();
        body.Add("codGroupId", codGroupId);
        body.Add("expiryAfterSeconds", expiryAfterSeconds);
        // Intentionally NOT gated — the CoD heartbeat must never be client-side rate-limited; its
        // ~30 weight/min is absorbed by RateLimitGate's ClientSafetyMargin. (P1-bis mini-council 2026-05-20.)
        var request = _definitions.GetOrCreate(HttpMethod.Post, "v2/cancelOrdersAfter", true);
        return _baseClient.SendAsync<BitvavoCancelOrdersAfter>(request, queryParameters: null, bodyParameters: body, ct);
    }

    /// <inheritdoc />
    public Task<WebCallResult<BitvavoTransactionHistory>> GetTransactionHistoryAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int? page = null,
        int? maxItems = null,
        string? type = null,
        CancellationToken ct = default)
    {
        var parameters = new ParameterCollection();
        parameters.AddOptionalMilliseconds("fromDate", fromDate);
        parameters.AddOptionalMilliseconds("toDate", toDate);
        parameters.AddOptional("page", page);
        parameters.AddOptional("maxItems", maxItems);
        parameters.AddOptional("type", type);

        // weight assumed — Bitvavo docs page did not state it; conservative default
        var request = _definitions.GetOrCreate(HttpMethod.Get, "v2/account/history", BitvavoRestClientSpotApi.RateLimitGate, weight: 5, authenticated: true);
        return _baseClient.SendAsync<BitvavoTransactionHistory>(request, parameters, ct);
    }
}
