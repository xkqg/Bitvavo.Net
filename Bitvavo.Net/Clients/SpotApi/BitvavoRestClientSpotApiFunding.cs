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

/// <inheritdoc cref="IBitvavoRestClientSpotApiFunding" />
internal sealed class BitvavoRestClientSpotApiFunding : IBitvavoRestClientSpotApiFunding
{
    private static readonly RequestDefinitionCache _definitions = new();
    private readonly BitvavoRestClientSpotApi _baseClient;

    internal BitvavoRestClientSpotApiFunding(BitvavoRestClientSpotApi baseClient)
    {
        _baseClient = baseClient;
    }

    /// <inheritdoc />
    public Task<HttpResult<BitvavoDepositAddress>> GetDepositAddressAsync(string symbol, CancellationToken ct = default)
    {
        var parameters = new Parameters(BitvavoExchange.ParameterSerializationSettings) { { "symbol", symbol } };
        var def = _definitions.GetOrCreate(HttpMethod.Get, _baseClient.BaseAddress, "v2/deposit", BitvavoRestClientSpotApi.RateLimitGate, weight: 1, authenticated: true);
        return _baseClient.SendAsync<BitvavoDepositAddress>(def, parameters, ct);
    }

    /// <inheritdoc />
    public Task<HttpResult<IEnumerable<BitvavoDepositHistoryEntry>>> GetDepositHistoryAsync(
        string? symbol = null,
        int? limit = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        CancellationToken ct = default)
    {
        var parameters = new Parameters(BitvavoExchange.ParameterSerializationSettings);
        parameters.Add("symbol", symbol);
        parameters.Add("limit", limit);
        parameters.Add("start", startTime);
        parameters.Add("end", endTime);

        var def = _definitions.GetOrCreate(HttpMethod.Get, _baseClient.BaseAddress, "v2/depositHistory", BitvavoRestClientSpotApi.RateLimitGate, weight: 5, authenticated: true);
        return _baseClient.SendAsync<IEnumerable<BitvavoDepositHistoryEntry>>(def, parameters, ct);
    }

    /// <inheritdoc />
    public Task<HttpResult<IEnumerable<BitvavoWithdrawalHistoryEntry>>> GetWithdrawalHistoryAsync(
        string? symbol = null,
        int? limit = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        CancellationToken ct = default)
    {
        var parameters = new Parameters(BitvavoExchange.ParameterSerializationSettings);
        parameters.Add("symbol", symbol);
        parameters.Add("limit", limit);
        parameters.Add("start", startTime);
        parameters.Add("end", endTime);

        var def = _definitions.GetOrCreate(HttpMethod.Get, _baseClient.BaseAddress, "v2/withdrawalHistory", BitvavoRestClientSpotApi.RateLimitGate, weight: 5, authenticated: true);
        return _baseClient.SendAsync<IEnumerable<BitvavoWithdrawalHistoryEntry>>(def, parameters, ct);
    }

    /// <inheritdoc />
    public Task<HttpResult<BitvavoWithdrawalResult>> WithdrawAsync(BitvavoWithdrawRequest request, CancellationToken ct = default)
    {
        var body = new Parameters(BitvavoExchange.ParameterSerializationSettings);
        body.Add("symbol", request.Symbol);
        body.Add("amount", request.Amount);
        body.Add("address", request.Address);
        body.Add("paymentId", request.PaymentId);
        body.Add("addWithdrawalFee", request.AddWithdrawalFee);
        body.Add("internal", request.Internal);

        var def = _definitions.GetOrCreate(HttpMethod.Post, _baseClient.BaseAddress, "v2/withdrawal", BitvavoRestClientSpotApi.RateLimitGate, weight: 1, authenticated: true);
        return _baseClient.SendAsync<BitvavoWithdrawalResult>(def, queryParameters: null, bodyParameters: body, ct);
    }

    /// <inheritdoc />
    public Task<HttpResult<BitvavoCryptoWithdrawal>> WithdrawCryptoAsync(BitvavoCryptoWithdrawRequest request, CancellationToken ct = default)
    {
        var body = new Parameters(BitvavoExchange.ParameterSerializationSettings);
        body.Add("asset", request.Asset);
        body.Add("network", request.Network);
        body.Add("address", request.Address);
        body.Add("amount", request.Amount);
        body.Add("deductFeeFromAmount", request.DeductFeeFromAmount);
        body.Add("idempotencyKey", request.IdempotencyKey);
        body.Add("memo", request.Memo);

        var def = _definitions.GetOrCreate(HttpMethod.Post, _baseClient.BaseAddress, "v2/crypto/withdrawal", BitvavoRestClientSpotApi.RateLimitGate, weight: 25, authenticated: true);
        return _baseClient.SendAsync<BitvavoCryptoWithdrawal>(def, queryParameters: null, bodyParameters: body, ct);
    }
}
