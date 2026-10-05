// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using Bitvavo.Net.Objects.Models.Spot;
using CryptoExchange.Net.SharedApis;

namespace Bitvavo.Net.Clients.SpotApi;

internal static partial class BitvavoSharedMappingExtensions
{
    /// <summary>
    /// A Bitvavo 24-hour ticker as a Shared spot ticker. The change percentage is the last price against the open price in
    /// percent, rounded to six decimals; it is unset when there is no last price or the open price is not positive. The volume
    /// carries both quantities Bitvavo reports (<c>volume</c> in the base asset, <c>volumeQuote</c> in the quote asset).
    /// </summary>
    /// <param name="ticker">The 24-hour ticker.</param>
    /// <param name="symbol">The symbol the ticker belongs to: the one the caller asked for, or the one parsed from the market name.</param>
    public static SharedSpotTicker ToSharedSpotTicker(this BitvavoTicker24h ticker, SharedSymbol symbol)
    {
        var changePercentage = ticker.Open is > 0m && ticker.Last != null
            ? Math.Round((ticker.Last.Value - ticker.Open.Value) / ticker.Open.Value * 100m, 6)
            : (decimal?)null;

        return new SharedSpotTicker(
            symbol,
            ticker.Market,
            ticker.Last,
            ticker.High,
            ticker.Low,
            new SharedOrderQuantity(ticker.Volume, ticker.VolumeQuote),
            changePercentage);
    }
}
