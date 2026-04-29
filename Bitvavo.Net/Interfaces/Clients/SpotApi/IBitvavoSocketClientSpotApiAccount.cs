// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Bitvavo.Net.Objects.Models.Spot.Streams;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Sockets;

namespace Bitvavo.Net.Interfaces.Clients.SpotApi;

/// <summary>
/// Bitvavo signed WebSocket subscriptions on the private <c>account</c> channel —
/// requires authentication. Bitvavo emits two distinct event types on this channel:
/// <c>order</c> (whenever any of the user's orders changes state) and <c>fill</c>
/// (whenever any of those orders trades).
/// </summary>
public interface IBitvavoSocketClientSpotApiAccount
{
    /// <summary>
    /// Subscribe to order-state updates for one or more markets. The connection must be
    /// authenticated before the framework dispatches the underlying subscribe payload —
    /// configure credentials on <see cref="Bitvavo.Net.Objects.Options.BitvavoSocketOptions"/>.
    /// <para><a href="https://docs.bitvavo.com/docs/websocket-api/track-your-orders">Bitvavo API docs</a></para>
    /// </summary>
    /// <param name="markets">Markets to subscribe to (e.g. <c>["ETH-EUR"]</c>).</param>
    /// <param name="onMessage">Per-event callback.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<CallResult<UpdateSubscription>> SubscribeToOrderUpdatesAsync(
        string[] markets,
        Action<DataEvent<BitvavoStreamOrderUpdate>> onMessage,
        CancellationToken ct = default);

    /// <summary>
    /// Subscribe to own-trade fills for one or more markets. Same channel as
    /// <see cref="SubscribeToOrderUpdatesAsync"/> — Bitvavo multiplexes order + fill
    /// events on the single private <c>account</c> stream.
    /// <para><a href="https://docs.bitvavo.com/docs/websocket-api/track-your-orders">Bitvavo API docs</a></para>
    /// </summary>
    /// <param name="markets">Markets to subscribe to.</param>
    /// <param name="onMessage">Per-fill callback.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<CallResult<UpdateSubscription>> SubscribeToFillUpdatesAsync(
        string[] markets,
        Action<DataEvent<BitvavoStreamFillEvent>> onMessage,
        CancellationToken ct = default);
}
