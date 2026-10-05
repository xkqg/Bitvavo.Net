// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Threading.Tasks;
using Bitvavo.Net.Interfaces.Clients.SpotApi;
using Bitvavo.Net.Objects.Internal;
using CryptoExchange.Net.SharedApis;

namespace Bitvavo.Net.Clients.SpotApi;

/// <summary>
/// The CryptoExchange.Net Shared API of the Bitvavo spot WebSocket client: one class holding the legacy [V1] interfaces and the
/// [V2] capabilities on the same instance (<see cref="BitvavoSocketClientSpotApi.SharedClient"/> and
/// <see cref="BitvavoSocketClientSpotApi.SharedApi"/> return it). Extended the same way as
/// <see cref="BitvavoRestClientSpotSharedApi"/>: one partial file per capability.
/// </summary>
internal sealed partial class BitvavoSocketClientSpotSharedApi :
    SharedApiBase,
    IBitvavoSocketClientSpotApiShared,
    IBitvavoSocketClientSpotSharedApi
{
    private const string _exchangeName = BitvavoExchange.ExchangeName;
    private const string _topicId = "BitvavoSpot";

    private readonly BitvavoSocketClientSpotApi _api;

    public BitvavoSocketClientSpotSharedApi(BitvavoSocketClientSpotApi api)
        : base(
            SharedTransport.Socket,
            api,
            [TradingMode.Spot],
            () => api.Authenticated,
            api.FormatSymbol)
    {
        _api = api;

        SetCapabilities(
            SubscribeKlineOptions,
            SubscribeTradeOptions,
            SubscribeSpotOrderOptions,
            SubscribeUserTradeOptions);
    }

    /// <inheritdoc />
    public override SharedClientInfo Discover() => SharedUtils.GetClientInfo(BitvavoExchange.Metadata, this);

    /// <inheritdoc />
    public Task UnsubscribeAllAsync() => _api.UnsubscribeAllAsync();
}
