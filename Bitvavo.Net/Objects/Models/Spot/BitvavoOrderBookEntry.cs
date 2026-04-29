// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Text.Json.Serialization;
using CryptoExchange.Net.Converters;
using CryptoExchange.Net.Converters.SystemTextJson;

namespace Bitvavo.Net.Objects.Models.Spot;

/// <summary>
/// Single bid or ask in the order book — Bitvavo emits these as positional 2-element
/// arrays <c>[price, size]</c>, so we decode via <see cref="ArrayConverter{T}"/>.
/// </summary>
/// <remarks>
/// Mutable setters are required by <see cref="ArrayConverter{T}"/>: the converter assigns
/// each <see cref="ArrayPropertyAttribute"/>-tagged property by index after constructing
/// the record. Init-only properties would break decoding.
/// </remarks>
[JsonConverter(typeof(ArrayConverter<BitvavoOrderBookEntry>))]
public record BitvavoOrderBookEntry
{
    /// <summary>Price level (quote-asset units).</summary>
    [ArrayProperty(0)]
    public decimal Price { get; set; }

    /// <summary>Aggregate size at the price level (base-asset units).</summary>
    [ArrayProperty(1)]
    public decimal Size { get; set; }
}
