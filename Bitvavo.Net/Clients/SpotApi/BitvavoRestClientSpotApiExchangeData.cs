// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Bitvavo.Net.Enums;
using Bitvavo.Net.Interfaces.Clients.SpotApi;
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
    public Task<WebCallResult<IEnumerable<BitvavoMarket>>> GetMarketsAsync(CancellationToken ct = default)
    {
        var request = _definitions.GetOrCreate(HttpMethod.Get, "v2/markets", BitvavoRestClientSpotApi.RateLimitGate, weight: 1, authenticated: false);
        return _baseClient.SendAsync<IEnumerable<BitvavoMarket>>(request, parameters: null, ct);
    }

    /// <inheritdoc />
    public Task<WebCallResult<IEnumerable<BitvavoKline>>> GetKlinesAsync(
        string market,
        KlineInterval interval,
        int? limit = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        CancellationToken ct = default)
    {
        var parameters = new ParameterCollection();
        parameters.AddEnum("interval", interval);
        parameters.AddOptional("limit", limit);
        parameters.AddOptionalMilliseconds("start", startTime);
        parameters.AddOptionalMilliseconds("end", endTime);

        var request = _definitions.GetOrCreate(HttpMethod.Get, $"v2/{market}/candles", BitvavoRestClientSpotApi.RateLimitGate, weight: 1, authenticated: false);
        return _baseClient.SendAsync<IEnumerable<BitvavoKline>>(request, parameters, ct);
    }

    /// <inheritdoc />
    public Task<WebCallResult<BitvavoServerTime>> GetServerTimeAsync(CancellationToken ct = default)
    {
        var request = _definitions.GetOrCreate(HttpMethod.Get, "v2/time", false);
        return _baseClient.SendAsync<BitvavoServerTime>(request, parameters: null, ct);
    }

    /// <inheritdoc />
    public Task<WebCallResult<IEnumerable<BitvavoAsset>>> GetAssetsAsync(string? symbol = null, CancellationToken ct = default)
    {
        var parameters = new ParameterCollection();
        parameters.AddOptional("symbol", symbol);

        var request = _definitions.GetOrCreate(HttpMethod.Get, "v2/assets", false);
        return _baseClient.SendAsync<IEnumerable<BitvavoAsset>>(request, parameters, ct);
    }

    /// <inheritdoc />
    public Task<WebCallResult<IEnumerable<BitvavoTickerPrice>>> GetTickerPricesAsync(string? market = null, CancellationToken ct = default)
    {
        var parameters = new ParameterCollection();
        parameters.AddOptional("market", market);

        var request = _definitions.GetOrCreate(HttpMethod.Get, "v2/ticker/price", false);
        return _baseClient.SendAsync<IEnumerable<BitvavoTickerPrice>>(request, parameters, ct);
    }

    /// <inheritdoc />
    public Task<WebCallResult<IEnumerable<BitvavoTickerBook>>> GetTickerBookAsync(string? market = null, CancellationToken ct = default)
    {
        var parameters = new ParameterCollection();
        parameters.AddOptional("market", market);

        var request = _definitions.GetOrCreate(HttpMethod.Get, "v2/ticker/book", false);
        return _baseClient.SendAsync<IEnumerable<BitvavoTickerBook>>(request, parameters, ct);
    }

    /// <inheritdoc />
    public Task<WebCallResult<IEnumerable<BitvavoTicker24h>>> GetTicker24hAsync(string? market = null, CancellationToken ct = default)
    {
        var parameters = new ParameterCollection();
        parameters.AddOptional("market", market);

        // ticker/24h: weight=25 when no market filter (returns all markets), weight=1 otherwise.
        var request = _definitions.GetOrCreate(HttpMethod.Get, "v2/ticker/24h", BitvavoRestClientSpotApi.RateLimitGate, weight: market is null ? 25 : 1, authenticated: false);
        return _baseClient.SendAsync<IEnumerable<BitvavoTicker24h>>(request, parameters, ct);
    }

    /// <inheritdoc />
    public Task<WebCallResult<BitvavoOrderBook>> GetOrderBookAsync(string market, int? depth = null, CancellationToken ct = default)
    {
        var parameters = new ParameterCollection();
        parameters.AddOptional("depth", depth);

        var request = _definitions.GetOrCreate(HttpMethod.Get, $"v2/{market}/book", false);
        return _baseClient.SendAsync<BitvavoOrderBook>(request, parameters, ct);
    }

    /// <inheritdoc />
    public Task<WebCallResult<IEnumerable<BitvavoPublicTrade>>> GetPublicTradesAsync(
        string market,
        int? limit = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        string? tradeIdFrom = null,
        string? tradeIdTo = null,
        CancellationToken ct = default)
    {
        var parameters = new ParameterCollection();
        parameters.AddOptional("limit", limit);
        parameters.AddOptionalMilliseconds("start", startTime);
        parameters.AddOptionalMilliseconds("end", endTime);
        parameters.AddOptional("tradeIdFrom", tradeIdFrom);
        parameters.AddOptional("tradeIdTo", tradeIdTo);

        var request = _definitions.GetOrCreate(HttpMethod.Get, $"v2/{market}/trades", false);
        return _baseClient.SendAsync<IEnumerable<BitvavoPublicTrade>>(request, parameters, ct);
    }
}
