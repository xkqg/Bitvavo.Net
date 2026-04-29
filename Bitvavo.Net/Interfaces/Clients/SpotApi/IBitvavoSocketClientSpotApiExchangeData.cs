// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Bitvavo.Net.Enums;
using Bitvavo.Net.Objects.Models.Spot.Streams;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Sockets;

namespace Bitvavo.Net.Interfaces.Clients.SpotApi;

/// <summary>
/// Bitvavo public-stream WebSocket subscriptions. No authentication required.
/// </summary>
public interface IBitvavoSocketClientSpotApiExchangeData
{
    /// <summary>
    /// Subscribe to candle (kline) updates for a market and interval. Bitvavo emits a
    /// new candle event after each trade in the bucket; the <c>candle</c> array contains
    /// the latest snapshot of the in-progress (or closed) candle.
    /// <para>
    /// <a href="https://docs.bitvavo.com/docs/websocket-overview/">Bitvavo WS docs</a>
    /// </para>
    /// </summary>
    /// <param name="market">Market identifier, e.g. <c>"ETH-EUR"</c>.</param>
    /// <param name="interval">Candle interval (1m, 5m, 1h, 1d, etc.).</param>
    /// <param name="onMessage">Handler invoked for each candle event.</param>
    /// <param name="ct">Cancellation token used to close this subscription.</param>
    Task<CallResult<UpdateSubscription>> SubscribeToKlineUpdatesAsync(
        string market,
        KlineInterval interval,
        Action<DataEvent<BitvavoStreamCandleEvent>> onMessage,
        CancellationToken ct = default);

    /// <summary>
    /// Subscribe to public trade updates for a market. Each event is one trade that
    /// occurred on Bitvavo's order book.
    /// <para>
    /// <a href="https://docs.bitvavo.com/docs/websocket-overview/">Bitvavo WS docs</a>
    /// </para>
    /// </summary>
    /// <param name="market">Market identifier, e.g. <c>"BTC-EUR"</c>.</param>
    /// <param name="onMessage">Handler invoked for each public trade event.</param>
    /// <param name="ct">Cancellation token used to close this subscription.</param>
    Task<CallResult<UpdateSubscription>> SubscribeToTradeUpdatesAsync(
        string market,
        Action<DataEvent<BitvavoStreamTrade>> onMessage,
        CancellationToken ct = default);
}
