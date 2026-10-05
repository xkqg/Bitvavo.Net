// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using Bitvavo.Net.Interfaces.Clients.SpotApi;
using Bitvavo.Net.Objects.Internal;
using CryptoExchange.Net.SharedApis;

namespace Bitvavo.Net.Clients.SpotApi;

/// <summary>
/// The CryptoExchange.Net Shared API of the Bitvavo spot REST client: one class holding the legacy [V1] interfaces and the [V2]
/// capabilities on the same instance (<see cref="BitvavoRestClientSpotApi.SharedClient"/> and
/// <see cref="BitvavoRestClientSpotApi.SharedApi"/> return it), as <c>KrakenRestClientSpotSharedApi</c> does.
/// </summary>
/// <remarks>
/// <para>Open for extension, closed for modification: a capability is one partial file
/// (<c>BitvavoRestClientSpotSharedApi.&lt;Capability&gt;.cs</c>) that declares the capability interface it implements and holds its
/// options and its call. Adding one means that file, its interface in the two aggregate interfaces, and its options in
/// <see cref="BitvavoRestClientSpotSharedApi(BitvavoRestClientSpotApi)"/>'s <c>SetCapabilities</c> list; a test fails when the
/// three disagree, so a capability can never be implemented but silently unregistered.</para>
/// <para>Every call validates its request against its options first, calls the typed sub-client, and maps the answer through
/// <see cref="BitvavoSharedMappingExtensions"/>.</para>
/// </remarks>
internal sealed partial class BitvavoRestClientSpotSharedApi :
    SharedApiBase,
    IBitvavoRestClientSpotApiShared,
    IBitvavoRestClientSpotSharedApi
{
    private const string _exchangeName = BitvavoExchange.ExchangeName;
    private const string _topicId = "BitvavoSpot";

    private readonly BitvavoRestClientSpotApi _api;

    public BitvavoRestClientSpotSharedApi(BitvavoRestClientSpotApi api)
        : base(
            SharedTransport.Rest,
            api,
            [TradingMode.Spot],
            () => api.Authenticated,
            api.FormatSymbol)
    {
        _api = api;

        SetCapabilities(
            GetAssetOptions,
            GetAllAssetsOptions,
            GetKlinesOptions,
            GetBalancesOptions,
            GetFeeOptions,
            GetLedgerOptions,
            GetDepositAddressesOptions,
            GetDepositHistoryOptions,
            GetWithdrawalHistoryOptions,
            WithdrawOptions,
            PlaceSpotOrderOptions,
            GetSpotOrderOptions,
            GetSpotOrderByClientOrderIdOptions,
            GetOpenSpotOrdersOptions,
            GetClosedSpotOrdersOptions,
            GetSpotOrderTradesOptions,
            GetSpotUserTradeHistoryOptions,
            CancelSpotOrderOptions,
            CancelSpotOrderByClientOrderIdOptions,
            GetSpotSymbolsOptions,
            GetTickerOptions,
            GetAllTickersOptions,
            GetOrderBookOptions,
            GetBookTickerOptions,
            GetRecentTradesOptions,
            CancelAllSpotOrdersOptions,
            CancelAllSpotSymbolOrdersOptions,
            EditSpotOrderOptions,
            EditSpotOrderByClientOrderIdOptions,
            PlaceSpotTriggerOrderOptions,
            GetSpotTriggerOrderOptions,
            CancelSpotTriggerOrderOptions);
    }

    /// <inheritdoc />
    public override SharedClientInfo Discover() => SharedUtils.GetClientInfo(BitvavoExchange.Metadata, this);
}
