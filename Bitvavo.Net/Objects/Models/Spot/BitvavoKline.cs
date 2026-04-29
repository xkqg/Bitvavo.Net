// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Text.Json.Serialization;
using CryptoExchange.Net.Converters;
using CryptoExchange.Net.Converters.SystemTextJson;

namespace Bitvavo.Net.Objects.Models.Spot;

/// <summary>
/// Bitvavo candlestick (kline) as returned by <c>GET /v2/{market}/candles</c>. Bitvavo
/// returns candles as positional arrays (<c>[ts_ms, open, high, low, close, volume]</c>),
/// hence the <see cref="ArrayConverter{T}"/> mapping.
/// </summary>
/// <remarks>
/// The mutable setters and parameterless ctor below are required by
/// <see cref="ArrayConverter{T}"/>: the converter constructs the record then assigns each
/// <see cref="ArrayPropertyAttribute"/>-tagged member by index. Init-only properties or a
/// positional-record syntax would not work here.
/// </remarks>
[JsonConverter(typeof(ArrayConverter<BitvavoKline>))]
public record BitvavoKline
{
    /// <summary>Open time of the candle (UTC). Bitvavo emits unix-millis at the array head.</summary>
    [ArrayProperty(0), JsonConverter(typeof(DateTimeConverter))]
    public DateTime OpenTime { get; set; }

    /// <summary>Open price.</summary>
    [ArrayProperty(1)]
    public decimal OpenPrice { get; set; }

    /// <summary>High price.</summary>
    [ArrayProperty(2)]
    public decimal HighPrice { get; set; }

    /// <summary>Low price.</summary>
    [ArrayProperty(3)]
    public decimal LowPrice { get; set; }

    /// <summary>Close price.</summary>
    [ArrayProperty(4)]
    public decimal ClosePrice { get; set; }

    /// <summary>Volume traded during the candle period (base-asset units).</summary>
    [ArrayProperty(5)]
    public decimal Volume { get; set; }
}
