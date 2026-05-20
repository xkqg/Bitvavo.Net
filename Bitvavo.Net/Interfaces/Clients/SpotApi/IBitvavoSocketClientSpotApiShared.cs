// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using CryptoExchange.Net.SharedApis;

namespace Bitvavo.Net.Interfaces.Clients.SpotApi;

/// <summary>
/// Aggregate CryptoExchange.Net Shared-API surface for the Bitvavo Spot WebSocket client.
/// <para>
/// Composes every Shared socket sub-interface Bitvavo implements into a single type so
/// consumers can program against the exchange-agnostic Shared layer with one reference
/// (mirroring <c>IKrakenSocketClientSpotApiShared</c>). Reachable via
/// <see cref="IBitvavoSocketClientSpotApi.SharedClient"/>.
/// </para>
/// <para>
/// <see cref="IBalanceSocketClient"/> is intentionally omitted: Bitvavo's private
/// <c>account</c> channel emits only <c>order</c> and <c>fill</c> events — no balance
/// snapshot or balance-delta stream exists to back a balance subscription. This is a
/// correct ISP-driven omission, not a gap.
/// </para>
/// </summary>
public interface IBitvavoSocketClientSpotApiShared :
    IKlineSocketClient,
    ITradeSocketClient,
    ISpotOrderSocketClient,
    IUserTradeSocketClient
{
}
