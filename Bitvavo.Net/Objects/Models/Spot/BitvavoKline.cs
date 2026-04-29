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
/// <para>
/// Each decimal field uses <see cref="DecimalConverter"/> so sub-cent coins (e.g. VTHO at
/// <c>"1e-8"</c>) deserialise correctly. The default System.Text.Json decimal parser uses
/// <c>NumberStyles.Number</c> which rejects exponent notation; <see cref="DecimalConverter"/>
/// uses <c>NumberStyles.Float</c>.
/// </para>
/// </remarks>
[JsonConverter(typeof(ArrayConverter<BitvavoKline>))]
public record BitvavoKline
{
    /// <summary>Open time of the candle (UTC). Bitvavo emits unix-millis at the array head.</summary>
    [ArrayProperty(0), JsonConverter(typeof(DateTimeConverter))]
    public DateTime OpenTime { get; set; }

    /// <summary>Open price.</summary>
    [ArrayProperty(1), JsonConverter(typeof(DecimalConverter))]
    public decimal OpenPrice { get; set; }

    /// <summary>High price.</summary>
    [ArrayProperty(2), JsonConverter(typeof(DecimalConverter))]
    public decimal HighPrice { get; set; }

    /// <summary>Low price.</summary>
    [ArrayProperty(3), JsonConverter(typeof(DecimalConverter))]
    public decimal LowPrice { get; set; }

    /// <summary>Close price.</summary>
    [ArrayProperty(4), JsonConverter(typeof(DecimalConverter))]
    public decimal ClosePrice { get; set; }

    /// <summary>Volume traded during the candle period (base-asset units).</summary>
    [ArrayProperty(5), JsonConverter(typeof(DecimalConverter))]
    public decimal Volume { get; set; }
}
