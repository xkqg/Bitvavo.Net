// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bitvavo.Net.Enums;
using Bitvavo.Net.Objects.Models.Spot;
using CryptoExchange.Net.Objects;

namespace Bitvavo.Net.Interfaces.Clients.SpotApi;

/// <summary>
/// Bitvavo public-data REST endpoints — no authentication required. Includes market
/// discovery and historical kline (candle) retrieval.
/// </summary>
public interface IBitvavoRestClientSpotApiExchangeData
{
    /// <summary>
    /// Get all spot markets supported by Bitvavo.
    /// <para>
    /// <a href="https://docs.bitvavo.com/docs/rest-api/get-markets">Bitvavo API docs</a>
    /// </para>
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task<WebCallResult<IEnumerable<BitvavoMarket>>> GetMarketsAsync(CancellationToken ct = default);

    /// <summary>
    /// Get historical candles (klines) for a given market and interval.
    /// <para>
    /// <a href="https://docs.bitvavo.com/docs/rest-api/get-candles">Bitvavo API docs</a>
    /// </para>
    /// </summary>
    /// <param name="market">Market identifier, e.g. <c>"ETH-EUR"</c>.</param>
    /// <param name="interval">Candle interval (1m, 5m, 1h, 1d, etc.).</param>
    /// <param name="limit">Maximum candles to return (1-1440). Defaults to Bitvavo's server default if omitted.</param>
    /// <param name="startTime">Inclusive UTC lower bound — fetches candles starting from this time.</param>
    /// <param name="endTime">Inclusive UTC upper bound.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<WebCallResult<IEnumerable<BitvavoKline>>> GetKlinesAsync(
        string market,
        KlineInterval interval,
        int? limit = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        CancellationToken ct = default);

    /// <summary>
    /// Get the current server time. Useful for clock-drift detection before signed
    /// requests — <c>Bitvavo-Access-Window</c> rejects requests too far from the server's clock.
    /// <para><a href="https://docs.bitvavo.com/docs/rest-api/get-server-time">Bitvavo API docs</a></para>
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task<WebCallResult<BitvavoServerTime>> GetServerTimeAsync(CancellationToken ct = default);

    /// <summary>
    /// Get all Bitvavo-supported assets, optionally filtered to a single symbol.
    /// <para><a href="https://docs.bitvavo.com/docs/rest-api/get-asset-data">Bitvavo API docs</a></para>
    /// </summary>
    /// <param name="symbol">Optional symbol filter (e.g. <c>"BTC"</c>).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<WebCallResult<IEnumerable<BitvavoAsset>>> GetAssetsAsync(string? symbol = null, CancellationToken ct = default);

    /// <summary>
    /// Get the most-recent traded price for one or all markets.
    /// <para><a href="https://docs.bitvavo.com/docs/rest-api/get-ticker-prices">Bitvavo API docs</a></para>
    /// </summary>
    /// <param name="market">Optional market filter (e.g. <c>"ETH-EUR"</c>). Null returns every market.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<WebCallResult<IEnumerable<BitvavoTickerPrice>>> GetTickerPricesAsync(string? market = null, CancellationToken ct = default);

    /// <summary>
    /// Get the top-of-book quote (best bid + best ask) for one or all markets.
    /// <para><a href="https://docs.bitvavo.com/docs/rest-api/get-ticker-book">Bitvavo API docs</a></para>
    /// </summary>
    /// <param name="market">Optional market filter.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<WebCallResult<IEnumerable<BitvavoTickerBook>>> GetTickerBookAsync(string? market = null, CancellationToken ct = default);

    /// <summary>
    /// Get the 24-hour rolling OHLCV + best-quote stats for one or all markets.
    /// <para><a href="https://docs.bitvavo.com/docs/rest-api/get-candlestick-data-24-h">Bitvavo API docs</a></para>
    /// </summary>
    /// <param name="market">Optional market filter.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<WebCallResult<IEnumerable<BitvavoTicker24h>>> GetTicker24hAsync(string? market = null, CancellationToken ct = default);

    /// <summary>
    /// Get the order-book snapshot for a single market.
    /// <para><a href="https://docs.bitvavo.com/docs/rest-api/get-order-book">Bitvavo API docs</a></para>
    /// </summary>
    /// <param name="market">Market identifier. Required.</param>
    /// <param name="depth">Maximum entries per side (1–1000, default 1000).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<WebCallResult<BitvavoOrderBook>> GetOrderBookAsync(string market, int? depth = null, CancellationToken ct = default);

    /// <summary>
    /// Get the public trade tape for a single market — distinct from the user's own
    /// trade fills returned by <c>Trading.GetUserTradesAsync</c>.
    /// <para><a href="https://docs.bitvavo.com/docs/rest-api/get-trades">Bitvavo API docs</a></para>
    /// </summary>
    /// <param name="market">Market identifier. Required.</param>
    /// <param name="limit">Maximum entries (1–1000, default 500).</param>
    /// <param name="startTime">Inclusive UTC lower bound. Bitvavo limits the window to 24 hours.</param>
    /// <param name="endTime">Inclusive UTC upper bound.</param>
    /// <param name="tradeIdFrom">Pagination cursor — only return trades with id &gt;= this value.</param>
    /// <param name="tradeIdTo">Pagination cursor — only return trades with id &lt;= this value.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<WebCallResult<IEnumerable<BitvavoPublicTrade>>> GetPublicTradesAsync(
        string market,
        int? limit = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        string? tradeIdFrom = null,
        string? tradeIdTo = null,
        CancellationToken ct = default);
}
