// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Sockets;
using CryptoExchange.Net.SharedApis;

namespace Bitvavo.Net.Clients.SpotApi;

/// <summary>Kline subscription: [V2] <see cref="ISubscribeKlinesSocket"/> and the legacy [V1] <see cref="IKlineSocketClient"/> on one implementation.</summary>
internal sealed partial class BitvavoSocketClientSpotSharedApi : ISubscribeKlinesSocket, IKlineSocketClient
{
    /// <summary>One market per subscription (the candle channel takes a market and an interval); public, so no credentials.</summary>
    public SubscribeKlineOptions SubscribeKlineOptions { get; } = new SubscribeKlineOptions(
        _exchangeName,
        false,
        BitvavoSharedMappingExtensions.SupportedKlineIntervals)
    {
        ParameterRuleOverrides =
        [
            RequestParameterRuleOverride<SubscribeKlineRequest>.Required(x => x.Symbol),
            RequestParameterRuleOverride<SubscribeKlineRequest>.NotSupported(x => x.Symbols),
        ],
    };

    /// <inheritdoc />
    public async Task<WebSocketResult<UpdateSubscription>> SubscribeToKlineUpdatesAsync(SubscribeKlineRequest request, Action<DataEvent<SharedKline>> handler, CancellationToken ct = default)
    {
        var validationError = SubscribeKlineOptions.ValidateRequest(request, this);
        if (validationError != null)
        {
            return WebSocketResult.Fail<UpdateSubscription>(Exchange, validationError);
        }

        var sharedSymbol = request.Symbol!;
        var symbol = sharedSymbol.GetSymbol(FormatSymbol);

        // A candle event carries an array of candles: each one reaches the handler as its own kline.
        return await _api.ExchangeData.SubscribeToKlineUpdatesAsync(
            symbol,
            request.Interval.ToBitvavoInterval(),
            update =>
            {
                foreach (var candle in update.Data.Candle)
                {
                    handler(update.ToType(candle.ToSharedKline(sharedSymbol, symbol)));
                }
            },
            ct).ConfigureAwait(false);
    }
}
