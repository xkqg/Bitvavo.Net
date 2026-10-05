// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Globalization;
using Bitvavo.Net.Enums;
using Bitvavo.Net.Objects.Models.Spot;
using CryptoExchange.Net;
using CryptoExchange.Net.SharedApis;

namespace Bitvavo.Net.Clients.SpotApi;

internal static partial class BitvavoSharedMappingExtensions
{
    /// <summary>
    /// A Bitvavo market as a Shared spot symbol.
    /// <list type="bullet">
    ///   <item><description>The symbol is trading while the market status is <see cref="BitvavoMarketStatus.Trading"/>; any other or unknown status is not.</description></item>
    ///   <item><description>The price grid is the <c>tickSize</c>: <c>PriceStep</c> is the tick size and <c>PriceDecimals</c> its decimal places. A tick size that is not positive is no grid and leaves both unset.</description></item>
    ///   <item><description><c>PriceSignificantFigures</c> is never set: its only source, <c>pricePrecision</c>, is deprecated and null on every market the live API lists (0.4.0 filled it from that field).</description></item>
    ///   <item><description>The quantity step is the smallest increment <c>quantityDecimals</c> allows (see <see cref="ToQuantityStep"/>); a minimum or maximum that is empty or unreadable is left unset.</description></item>
    ///   <item><description>Base assets are crypto. A quote asset is fiat for <c>EUR</c>, the one fiat Bitvavo quotes in, and crypto otherwise; a known stable coin on either side is crypto of the stable coin sub type.</description></item>
    /// </list>
    /// </summary>
    public static SharedSpotSymbol ToSharedSpotSymbol(this BitvavoMarket market)
    {
        var tickSize = market.TickSize is > 0m ? market.TickSize : null;
        var quoteIsFiat = string.Equals(market.QuoteAsset, "EUR", StringComparison.OrdinalIgnoreCase);

        var symbol = new SharedSpotSymbol(market.BaseAsset, market.QuoteAsset, market.Market, market.Status == BitvavoMarketStatus.Trading)
        {
            MinTradeQuantity = Parse(market.MinOrderInBaseAsset),
            MaxTradeQuantity = Parse(market.MaxOrderInBaseAsset),
            MinNotionalValue = Parse(market.MinOrderInQuoteAsset),
            QuantityDecimals = market.QuantityDecimals,
            QuantityStep = market.QuantityDecimals.ToQuantityStep(),
            PriceStep = tickSize,
            PriceDecimals = tickSize?.DecimalPlaces(),
            BaseAssetType = SharedAssetType.Crypto,
            QuoteAssetType = quoteIsFiat ? SharedAssetType.Fiat : SharedAssetType.Crypto,
        };

        if (LibraryHelpers.IsStableCoin(market.BaseAsset))
        {
            symbol.BaseAssetSubType = SharedAssetSubType.StableCoin;
        }

        if (LibraryHelpers.IsStableCoin(market.QuoteAsset))
        {
            symbol.QuoteAssetSubType = SharedAssetSubType.StableCoin;
        }

        return symbol;

        static decimal? Parse(string? value)
            => decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) ? parsed : (decimal?)null;
    }

    /// <summary>
    /// The smallest quantity increment that a number of quantity decimals allows: 8 decimals is <c>0.00000001</c>, 0 decimals is 1.
    /// Null when the decimals are unknown or outside what a decimal can hold (0 to 28).
    /// </summary>
    public static decimal? ToQuantityStep(this int? quantityDecimals)
        => quantityDecimals is >= 0 and <= 28
            ? new decimal(1, 0, 0, false, (byte)quantityDecimals.Value)
            : (decimal?)null;
}
