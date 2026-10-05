// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Sockets;
using CryptoExchange.Net.SharedApis;

namespace Bitvavo.Net.Clients.SpotApi;

/// <summary>Public trade subscription: [V2] <see cref="ISubscribeTradesSocket"/> and the legacy [V1] <see cref="ITradeSocketClient"/> on one implementation.</summary>
internal sealed partial class BitvavoSocketClientSpotSharedApi : ISubscribeTradesSocket, ITradeSocketClient
{
    /// <summary>Public, so no credentials; one subscription carries any number of markets.</summary>
    public SubscribeTradeOptions SubscribeTradeOptions { get; } = new SubscribeTradeOptions(_exchangeName, false)
    {
        SupportsMultipleSymbols = true,
    };

    /// <inheritdoc />
    public async Task<WebSocketResult<UpdateSubscription>> SubscribeToTradeUpdatesAsync(SubscribeTradeRequest request, Action<DataEvent<SharedTrade[]>> handler, CancellationToken ct = default)
    {
        var validationError = SubscribeTradeOptions.ValidateRequest(request, this);
        if (validationError != null)
        {
            return WebSocketResult.Fail<UpdateSubscription>(Exchange, validationError);
        }

        // A request is built with one symbol or a non-empty list (its constructors refuse anything else), so the names always exist.
        return await _api.ExchangeData.SubscribeToTradeUpdatesAsync(
            request.SymbolNames(FormatSymbol),
            update => handler(update.ToType<SharedTrade[]>([update.Data.ToSharedTrade()])),
            ct).ConfigureAwait(false);
    }
}
