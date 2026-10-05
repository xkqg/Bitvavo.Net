// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Sockets;
using CryptoExchange.Net.SharedApis;

namespace Bitvavo.Net.Clients.SpotApi;

/// <summary>
/// Order subscription: [V2] <see cref="ISubscribeSpotOrdersSocket"/> and the legacy [V1] <see cref="ISpotOrderSocketClient"/> (which
/// hands out the same updates typed as plain <see cref="SharedSpotOrder"/>) on one implementation.
/// </summary>
internal sealed partial class BitvavoSocketClientSpotSharedApi : ISubscribeSpotOrdersSocket, ISpotOrderSocketClient
{
    Task<WebSocketResult<UpdateSubscription>> ISpotOrderSocketClient.SubscribeToSpotOrderUpdatesAsync(SubscribeSpotOrderRequest request, Action<DataEvent<SharedSpotOrder[]>> handler, CancellationToken ct)
        => SubscribeToSpotOrderUpdatesAsync(request, update => handler(update.ToType<SharedSpotOrder[]>(update.Data)), ct);

    /// <summary>
    /// Private (the <c>account</c> channel, authenticated per connection), subscribed per market: the markets travel in the
    /// <c>Markets</c> exchange parameter because the request has no symbol set.
    /// </summary>
    public SubscribeSpotOrderOptions SubscribeSpotOrderOptions { get; } = new SubscribeSpotOrderOptions(_exchangeName, true)
    {
        ExchangeParameterRules = [BitvavoSharedParameters.MarketsRule],
    };

    /// <inheritdoc />
    public async Task<WebSocketResult<UpdateSubscription>> SubscribeToSpotOrderUpdatesAsync(SubscribeSpotOrderRequest request, Action<DataEvent<SharedSpotOrderUpdate[]>> handler, CancellationToken ct = default)
    {
        var validationError = SubscribeSpotOrderOptions.ValidateRequest(request, this);
        if (validationError != null)
        {
            return WebSocketResult.Fail<UpdateSubscription>(Exchange, validationError);
        }

        return await _api.Account.SubscribeToOrderUpdatesAsync(
            request.GetMarkets(),
            update => handler(update.ToType<SharedSpotOrderUpdate[]>([update.Data.ToSharedSpotOrderUpdate()])),
            ct).ConfigureAwait(false);
    }
}
