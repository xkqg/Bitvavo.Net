// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using CryptoExchange.Net.SharedApis;

namespace Bitvavo.Net.Interfaces.Clients.SpotApi;

/// <summary>
/// [V1] Aggregate of the legacy CryptoExchange.Net Shared REST interfaces Bitvavo implements, reachable via
/// <see cref="IBitvavoRestClientSpotApi.SharedClient"/>. Kept for compatibility (for example the kline ceiling
/// <c>SharedClient.GetKlinesOptions.MaxTotalDataPoints</c>); new code uses <see cref="IBitvavoRestClientSpotSharedApi"/>.
/// <para>
/// Bitvavo is spot-only, so the futures, margin and leverage interfaces are absent: an interface-segregation omission, not a gap.
/// </para>
/// </summary>
public interface IBitvavoRestClientSpotApiShared :
    IAssetsRestClient,
    IKlineRestClient,
    IBalanceRestClient,
    IFeeRestClient,
    IDepositRestClient,
    IWithdrawalRestClient,
    IWithdrawRestClient,
    ISpotOrderRestClient,
    ISpotOrderClientIdRestClient,
    ISpotSymbolRestClient,
    ISpotTickerRestClient,
    IOrderBookRestClient,
    IBookTickerRestClient,
    IRecentTradeRestClient
{
}

/// <summary>
/// [V2] Aggregate of the Shared REST capabilities Bitvavo implements, reachable via <see cref="IBitvavoRestClientSpotApi.SharedApi"/>.
/// Shared capabilities give every CryptoExchange.Net exchange library one exchange-independent contract. Each capability is its own
/// interface; a capability Bitvavo does not support (for example batch orders, which Bitvavo has no endpoint for) is simply absent.
/// <see cref="ISharedApi.Capabilities"/> lists what is registered and <see cref="ISharedApi.Discover"/> describes it.
/// </summary>
public interface IBitvavoRestClientSpotSharedApi :
    IGetAssetRest,
    IGetAllAssetsRest,
    IGetKlinesRest,
    IGetBalancesRest,
    IGetFeesRest,
    IGetLedgerRest,
    IGetDepositAddressesRest,
    IGetDepositHistoryRest,
    IGetWithdrawalHistoryRest,
    IWithdrawRest,
    IPlaceSpotOrderRest,
    IGetSpotOrderRest,
    IGetSpotOrderByClientOrderIdRest,
    IGetOpenSpotOrdersRest,
    IGetClosedSpotOrdersRest,
    IGetSpotOrderTradesRest,
    IGetSpotUserTradeHistoryRest,
    ICancelSpotOrderRest,
    ICancelSpotOrderByClientOrderIdRest,
    IGetSpotSymbolsRest,
    IGetTickerRest,
    IGetAllTickersRest,
    IGetOrderBookRest,
    IGetBookTickerRest,
    IGetRecentTradesRest,
    ICancelAllSpotOrdersRest,
    ICancelAllSpotSymbolOrdersRest,
    IEditSpotOrderRest,
    IEditSpotOrderByClientOrderIdRest,
    IPlaceSpotTriggerOrderRest,
    IGetSpotTriggerOrderRest,
    ICancelSpotTriggerOrderRest
{
}
