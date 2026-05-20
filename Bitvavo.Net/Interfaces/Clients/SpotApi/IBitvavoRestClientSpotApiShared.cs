// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using CryptoExchange.Net.SharedApis;

namespace Bitvavo.Net.Interfaces.Clients.SpotApi;

/// <summary>
/// Aggregate CryptoExchange.Net Shared-API surface for the Bitvavo Spot REST client.
/// <para>
/// This interface composes every Shared REST sub-interface Bitvavo implements into a
/// single type so consumers can program against the exchange-agnostic Shared layer with
/// one reference (mirroring <c>IKrakenRestClientSpotApiShared</c> /
/// <c>IBinanceRestClientSpotApiShared</c>). It is reachable via
/// <see cref="IBitvavoRestClientSpotApi.SharedClient"/>.
/// </para>
/// <para>
/// Bitvavo is spot-only and its account channel exposes no isolated-margin or futures
/// surface, so this composite intentionally omits the futures / margin / leverage Shared
/// interfaces — that is a correct ISP-driven omission, not a gap.
/// </para>
/// </summary>
public interface IBitvavoRestClientSpotApiShared :
    IAssetsRestClient,
    IKlineRestClient,
    IRecentTradeRestClient,
    IOrderBookRestClient,
    ISpotSymbolRestClient,
    ISpotTickerRestClient,
    IBookTickerRestClient,
    IBalanceRestClient,
    ISpotOrderRestClient,
    ISpotOrderClientIdRestClient,
    IFeeRestClient,
    IDepositRestClient,
    IWithdrawalRestClient,
    IWithdrawRestClient
{
}
