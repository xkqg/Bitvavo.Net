// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using CryptoExchange.Net.Interfaces;

namespace Bitvavo.Net.Objects.Models.Spot;

/// <summary>
/// Concrete <see cref="ISymbolOrderBookEntry"/> used to populate the CryptoExchange.Net
/// Shared-API <see cref="CryptoExchange.Net.SharedApis.SharedOrderBook"/>. CryptoExchange.Net
/// 11.x exposes the <c>ISymbolOrderBookEntry</c> contract but no public concrete record, so
/// each exchange library supplies its own — this mirrors the per-exchange pattern in
/// Kraken.Net / Binance.Net.
/// </summary>
public record BitvavoSharedOrderBookEntry : ISymbolOrderBookEntry
{
    /// <summary>Price level (quote-asset units).</summary>
    public decimal Price { get; set; }

    /// <summary>Aggregate quantity resting at the price level (base-asset units).</summary>
    public decimal Quantity { get; set; }
}
