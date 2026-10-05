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
    /// <summary>Public-stream subscriptions (candles, trades). The ticker and book channels are not implemented.</summary>
    IBitvavoSocketClientSpotApiExchangeData ExchangeData { get; }

    /// <summary>Signed private-stream subscriptions on the <c>account</c> channel — order-state + fill events.</summary>
    IBitvavoSocketClientSpotApiAccount Account { get; }

    /// <summary>
    /// [V1] The legacy shared socket client: the exchange-independent CryptoExchange.Net subscription interfaces as they were before
    /// the Shared API. Kept for compatibility; for new implementations use <see cref="SharedApi"/>.
    /// </summary>
    IBitvavoSocketClientSpotApiShared SharedClient { get; }

    /// <summary>
    /// [V2] The aggregate Shared API for subscriptions. The same instance as <see cref="SharedClient"/>.
    /// </summary>
    IBitvavoSocketClientSpotSharedApi SharedApi { get; }
}
