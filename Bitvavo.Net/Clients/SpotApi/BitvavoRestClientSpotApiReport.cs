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

/// <inheritdoc cref="IBitvavoRestClientSpotApiReport" />
internal sealed class BitvavoRestClientSpotApiReport : IBitvavoRestClientSpotApiReport
{
    private static readonly RequestDefinitionCache _definitions = new();
    private readonly BitvavoRestClientSpotApi _baseClient;

    internal BitvavoRestClientSpotApiReport(BitvavoRestClientSpotApi baseClient)
    {
        _baseClient = baseClient;
    }

    /// <inheritdoc />
    public Task<HttpResult<IEnumerable<BitvavoTradesReport>>> GetTradesReportAsync(
        string market,
        int? limit = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        string? tradeIdFrom = null,
        string? tradeIdTo = null,
        CancellationToken ct = default)
    {
        var parameters = new Parameters(BitvavoExchange.ParameterSerializationSettings);
        parameters.Add("limit", limit);
        parameters.Add("start", startTime);
        parameters.Add("end", endTime);
        parameters.Add("tradeIdFrom", tradeIdFrom);
        parameters.Add("tradeIdTo", tradeIdTo);

        // A public endpoint (like every market-data endpoint): it needs no credentials and counts against the per-IP budget.
        var def = _definitions.GetOrCreate(HttpMethod.Get, _baseClient.BaseAddress, $"v2/report/{market}/trades", BitvavoRestClientSpotApi.RateLimitGate, weight: 5, authenticated: false);
        return _baseClient.SendAsync<IEnumerable<BitvavoTradesReport>>(def, parameters, ct);
    }

    /// <inheritdoc />
    public Task<HttpResult<BitvavoBookReport>> GetBookReportAsync(string market, int? depth = null, CancellationToken ct = default)
    {
        var parameters = new Parameters(BitvavoExchange.ParameterSerializationSettings);
        parameters.Add("depth", depth);

        var def = _definitions.GetOrCreate(HttpMethod.Get, _baseClient.BaseAddress, $"v2/report/{market}/book", BitvavoRestClientSpotApi.RateLimitGate, weight: 1, authenticated: false);
        return _baseClient.SendAsync<BitvavoBookReport>(def, parameters, ct);
    }
}
