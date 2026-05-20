// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using CryptoExchange.Net.Interfaces.Clients;

namespace Bitvavo.Net.Interfaces.Clients.SpotApi;

/// <summary>
/// Bitvavo Spot WebSocket API client surface. Public-stream endpoints sit on
/// <see cref="ExchangeData"/>; signed private-stream endpoints sit on
/// <see cref="Account"/> (mirroring KrakenSocketClientSpotApi.Account).
/// </summary>
public interface IBitvavoSocketClientSpotApi : ISocketApiClient
{
    /// <summary>Public-stream subscriptions (candles, trades, ticker, book).</summary>
    IBitvavoSocketClientSpotApiExchangeData ExchangeData { get; }

    /// <summary>Signed private-stream subscriptions on the <c>account</c> channel — order-state + fill events.</summary>
    IBitvavoSocketClientSpotApiAccount Account { get; }

    /// <summary>
    /// The exchange-agnostic CryptoExchange.Net Shared-API surface for Bitvavo Spot
    /// WebSocket. Use this to program against the Shared abstractions common to every
    /// CryptoExchange.Net client library (mirrors <c>IKrakenSocketClientSpotApi.SharedClient</c>).
    /// </summary>
    IBitvavoSocketClientSpotApiShared SharedClient { get; }
}
