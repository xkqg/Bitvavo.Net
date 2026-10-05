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
using CryptoExchange.Net.Converters;
using CryptoExchange.Net.Objects;

namespace Bitvavo.Net.Clients.SpotApi;

/// <inheritdoc />
internal sealed class BitvavoRestClientSpotApiExchangeData : IBitvavoRestClientSpotApiExchangeData
{
    private static readonly RequestDefinitionCache _definitions = new();
    private readonly BitvavoRestClientSpotApi _baseClient;

    internal BitvavoRestClientSpotApiExchangeData(BitvavoRestClientSpotApi baseClient)
    {
        _baseClient = baseClient;
    }

    /// <inheritdoc />
    public Task<HttpResult<IEnumerable<BitvavoMarket>>> GetMarketsAsync(CancellationToken ct = default)
    {
        var request = _definitions.GetOrCreate(HttpMethod.Get, _baseClient.BaseAddress, "v2/markets", BitvavoRestClientSpotApi.RateLimitGate, weight: 1, authenticated: false);
        return _baseClient.SendAsync<IEnumerable<BitvavoMarket>>(request, parameters: null, ct);
    }

    /// <inheritdoc />
    public Task<HttpResult<IEnumerable<BitvavoKline>>> GetKlinesAsync(
        string market,
        KlineInterval interval,
        int? limit = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        CancellationToken ct = default)
    {
        var parameters = new Parameters(BitvavoExchange.ParameterSerializationSettings);
        parameters.Add("interval", interval);
        parameters.Add("limit", limit);
        parameters.Add("start", startTime);
        parameters.Add("end", endTime);

        var request = _definitions.GetOrCreate(HttpMethod.Get, _baseClient.BaseAddress, $"v2/{market}/candles", BitvavoRestClientSpotApi.RateLimitGate, weight: 1, authenticated: false);
        return _baseClient.SendAsync<IEnumerable<BitvavoKline>>(request, parameters, ct);
    }

    /// <inheritdoc />
    public Task<HttpResult<BitvavoServerTime>> GetServerTimeAsync(CancellationToken ct = default)
    {
        var request = _definitions.GetOrCreate(HttpMethod.Get, _baseClient.BaseAddress, "v2/time", BitvavoRestClientSpotApi.RateLimitGate, weight: 1, authenticated: false);
        return _baseClient.SendAsync<BitvavoServerTime>(request, parameters: null, ct);
    }

    /// <inheritdoc />
    public Task<HttpResult<IEnumerable<BitvavoAsset>>> GetAssetsAsync(string? symbol = null, CancellationToken ct = default)
    {
        var parameters = new Parameters(BitvavoExchange.ParameterSerializationSettings);
        parameters.Add("symbol", symbol);

        // weight assumed — Bitvavo docs page did not state it; conservative default
        var request = _definitions.GetOrCreate(HttpMethod.Get, _baseClient.BaseAddress, "v2/assets", BitvavoRestClientSpotApi.RateLimitGate, weight: 1, authenticated: false);
        return _baseClient.SendAsync<IEnumerable<BitvavoAsset>>(request, parameters, ct);
    }

    /// <inheritdoc />
    public Task<HttpResult<IEnumerable<BitvavoTickerPrice>>> GetTickerPricesAsync(string? market = null, CancellationToken ct = default)
    {
        var parameters = new Parameters(BitvavoExchange.ParameterSerializationSettings);
        parameters.Add("market", market);

        var request = _definitions.GetOrCreate(HttpMethod.Get, _baseClient.BaseAddress, "v2/ticker/price", BitvavoRestClientSpotApi.RateLimitGate, weight: 1, authenticated: false);
        return _baseClient.SendAsync<IEnumerable<BitvavoTickerPrice>>(request, parameters, ct);
    }

    /// <inheritdoc />
    public Task<HttpResult<IEnumerable<BitvavoTickerBook>>> GetTickerBookAsync(string? market = null, CancellationToken ct = default)
    {
        var parameters = new Parameters(BitvavoExchange.ParameterSerializationSettings);
        parameters.Add("market", market);

        var request = _definitions.GetOrCreate(HttpMethod.Get, _baseClient.BaseAddress, "v2/ticker/book", BitvavoRestClientSpotApi.RateLimitGate, weight: 1, authenticated: false);
        return _baseClient.SendAsync<IEnumerable<BitvavoTickerBook>>(request, parameters, ct);
    }

    /// <inheritdoc />
    public Task<HttpResult<IEnumerable<BitvavoTicker24h>>> GetTicker24hAsync(string? market = null, CancellationToken ct = default)
    {
        var parameters = new Parameters(BitvavoExchange.ParameterSerializationSettings);
        parameters.Add("market", market);

        // ticker/24h: weight=25 when no market filter (returns all markets), weight=1 otherwise — per call, not per cached definition.
        var weight = market is null ? 25 : 1;
        var request = _definitions.GetOrCreate(HttpMethod.Get, _baseClient.BaseAddress, "v2/ticker/24h", BitvavoRestClientSpotApi.RateLimitGate, weight: weight, authenticated: false);
        return _baseClient.SendAsync<IEnumerable<BitvavoTicker24h>>(request, parameters, ct, weight);
    }

    /// <inheritdoc />
    public Task<HttpResult<BitvavoOrderBook>> GetOrderBookAsync(string market, int? depth = null, CancellationToken ct = default)
    {
        var parameters = new Parameters(BitvavoExchange.ParameterSerializationSettings);
        parameters.Add("depth", depth);

        var request = _definitions.GetOrCreate(HttpMethod.Get, _baseClient.BaseAddress, $"v2/{market}/book", BitvavoRestClientSpotApi.RateLimitGate, weight: 1, authenticated: false);
        return _baseClient.SendAsync<BitvavoOrderBook>(request, parameters, ct);
    }

    /// <inheritdoc />
    public Task<HttpResult<IEnumerable<BitvavoPublicTrade>>> GetPublicTradesAsync(
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

        var request = _definitions.GetOrCreate(HttpMethod.Get, _baseClient.BaseAddress, $"v2/{market}/trades", BitvavoRestClientSpotApi.RateLimitGate, weight: 5, authenticated: false);
        return _baseClient.SendAsync<IEnumerable<BitvavoPublicTrade>>(request, parameters, ct);
    }
}
