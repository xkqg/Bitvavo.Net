// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using CryptoExchange.Net.SharedApis;

namespace Bitvavo.Net.Interfaces.Clients.SpotApi;

/// <summary>
/// [V1] Aggregate of the legacy CryptoExchange.Net Shared WebSocket interfaces Bitvavo implements, reachable via
/// <see cref="IBitvavoSocketClientSpotApi.SharedClient"/>; new code uses <see cref="IBitvavoSocketClientSpotSharedApi"/>.
/// <para>
/// There is no balance subscription: Bitvavo's private <c>account</c> channel emits only <c>order</c> and <c>fill</c> events,
/// no balance snapshot or delta, so nothing could back one. An interface-segregation omission, not a gap.
/// </para>
/// </summary>
public interface IBitvavoSocketClientSpotApiShared :
    IKlineSocketClient,
    ITradeSocketClient,
    ISpotOrderSocketClient,
    IUserTradeSocketClient
{
}

/// <summary>
/// [V2] Aggregate of the Shared WebSocket capabilities Bitvavo implements, reachable via
/// <see cref="IBitvavoSocketClientSpotApi.SharedApi"/>. The private subscriptions (spot orders, user trades) are subscribed per
/// market: the markets travel in the request's exchange parameters under the <c>Markets</c> key.
/// </summary>
public interface IBitvavoSocketClientSpotSharedApi :
    ISubscribeKlinesSocket,
    ISubscribeTradesSocket,
    ISubscribeSpotOrdersSocket,
    ISubscribeUserTradesSocket
{
}
