// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Text.Json.Serialization;
using CryptoExchange.Net.Attributes;
using CryptoExchange.Net.Converters.SystemTextJson;

namespace Bitvavo.Net.Enums;

/// <summary>
/// Kline (candlestick) interval as supported by Bitvavo's <c>/v2/{market}/candles</c>
/// REST endpoint and the <c>candles</c> WebSocket channel. Wire format matches Bitvavo's
/// documented strings (e.g. <c>"1m"</c>, <c>"1h"</c>, <c>"1d"</c>).
/// </summary>
[JsonConverter(typeof(EnumConverter<KlineInterval>))]
public enum KlineInterval
{
    /// <summary>1 minute.</summary>
    [Map("1m")] OneMinute = 0,
    /// <summary>5 minutes.</summary>
    [Map("5m")] FiveMinutes = 1,
    /// <summary>15 minutes.</summary>
    [Map("15m")] FifteenMinutes = 2,
    /// <summary>30 minutes.</summary>
    [Map("30m")] ThirtyMinutes = 3,
    /// <summary>1 hour.</summary>
    [Map("1h")] OneHour = 4,
    /// <summary>2 hours.</summary>
    [Map("2h")] TwoHours = 5,
    /// <summary>4 hours.</summary>
    [Map("4h")] FourHours = 6,
    /// <summary>6 hours.</summary>
    [Map("6h")] SixHours = 7,
    /// <summary>8 hours.</summary>
    [Map("8h")] EightHours = 8,
    /// <summary>12 hours.</summary>
    [Map("12h")] TwelveHours = 9,
    /// <summary>1 day.</summary>
    [Map("1d")] OneDay = 10,
    /// <summary>1 week. Wire token is capital <c>"1W"</c> per Bitvavo's documented set.</summary>
    [Map("1W")] OneWeek = 11,
    /// <summary>1 month. Wire token is capital <c>"1M"</c> (lowercase <c>"1m"</c> means one minute).</summary>
    [Map("1M")] OneMonth = 12,
}
