// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Sockets;
using CryptoExchange.Net.SharedApis;

namespace Bitvavo.Net.Clients.SpotApi;

/// <summary>Own-trade subscription: [V2] <see cref="ISubscribeUserTradesSocket"/> and the legacy [V1] <see cref="IUserTradeSocketClient"/> on one implementation.</summary>
internal sealed partial class BitvavoSocketClientSpotSharedApi : ISubscribeUserTradesSocket, IUserTradeSocketClient
{
    /// <summary>Private, the <c>fill</c> events of the <c>account</c> channel; subscribed per market, so the markets travel in the <c>Markets</c> exchange parameter.</summary>
    public SubscribeUserTradeOptions SubscribeUserTradeOptions { get; } = new SubscribeUserTradeOptions(_exchangeName, true)
    {
        ExchangeParameterRules = [BitvavoSharedParameters.MarketsRule],
    };

    /// <inheritdoc />
    public async Task<WebSocketResult<UpdateSubscription>> SubscribeToUserTradeUpdatesAsync(SubscribeUserTradeRequest request, Action<DataEvent<SharedUserTrade[]>> handler, CancellationToken ct = default)
    {
        var validationError = SubscribeUserTradeOptions.ValidateRequest(request, this);
        if (validationError != null)
        {
            return WebSocketResult.Fail<UpdateSubscription>(Exchange, validationError);
        }

        return await _api.Account.SubscribeToFillUpdatesAsync(
            request.GetMarkets(),
            update => handler(update.ToType<SharedUserTrade[]>([update.Data.ToSharedUserTrade()])),
            ct).ConfigureAwait(false);
    }
}
