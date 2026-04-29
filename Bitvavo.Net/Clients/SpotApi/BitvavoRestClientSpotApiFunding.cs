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
    public Task<WebCallResult<BitvavoDepositAddress>> GetDepositAddressAsync(string symbol, CancellationToken ct = default)
    {
        var parameters = new ParameterCollection();
        parameters.Add("symbol", symbol);

        var def = _definitions.GetOrCreate(HttpMethod.Get, "v2/deposit", true);
        return _baseClient.SendAsync<BitvavoDepositAddress>(def, parameters, ct);
    }

    /// <inheritdoc />
    public Task<WebCallResult<IEnumerable<BitvavoDepositHistoryEntry>>> GetDepositHistoryAsync(
        string? symbol = null,
        int? limit = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        CancellationToken ct = default)
    {
        var parameters = new ParameterCollection();
        parameters.AddOptional("symbol", symbol);
        parameters.AddOptional("limit", limit);
        parameters.AddOptionalMilliseconds("start", startTime);
        parameters.AddOptionalMilliseconds("end", endTime);

        var def = _definitions.GetOrCreate(HttpMethod.Get, "v2/depositHistory", true);
        return _baseClient.SendAsync<IEnumerable<BitvavoDepositHistoryEntry>>(def, parameters, ct);
    }

    /// <inheritdoc />
    public Task<WebCallResult<IEnumerable<BitvavoWithdrawalHistoryEntry>>> GetWithdrawalHistoryAsync(
        string? symbol = null,
        int? limit = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        CancellationToken ct = default)
    {
        var parameters = new ParameterCollection();
        parameters.AddOptional("symbol", symbol);
        parameters.AddOptional("limit", limit);
        parameters.AddOptionalMilliseconds("start", startTime);
        parameters.AddOptionalMilliseconds("end", endTime);

        var def = _definitions.GetOrCreate(HttpMethod.Get, "v2/withdrawalHistory", true);
        return _baseClient.SendAsync<IEnumerable<BitvavoWithdrawalHistoryEntry>>(def, parameters, ct);
    }

    /// <inheritdoc />
    public Task<WebCallResult<BitvavoWithdrawalResult>> WithdrawAsync(BitvavoWithdrawRequest request, CancellationToken ct = default)
    {
        var body = new ParameterCollection();
        body.Add("symbol", request.Symbol);
        body.AddString("amount", request.Amount);
        body.Add("address", request.Address);
        body.AddOptional("paymentId", request.PaymentId);
        body.AddOptional("addWithdrawalFee", request.AddWithdrawalFee);

        var def = _definitions.GetOrCreate(HttpMethod.Post, "v2/withdrawal", true);
        return _baseClient.SendAsync<BitvavoWithdrawalResult>(def, queryParameters: null, bodyParameters: body, ct);
    }
}
