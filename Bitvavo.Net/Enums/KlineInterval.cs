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
    [Map("1m")] OneMinute,
    /// <summary>5 minutes.</summary>
    [Map("5m")] FiveMinutes,
    /// <summary>15 minutes.</summary>
    [Map("15m")] FifteenMinutes,
    /// <summary>30 minutes.</summary>
    [Map("30m")] ThirtyMinutes,
    /// <summary>1 hour.</summary>
    [Map("1h")] OneHour,
    /// <summary>2 hours.</summary>
    [Map("2h")] TwoHours,
    /// <summary>4 hours.</summary>
    [Map("4h")] FourHours,
    /// <summary>6 hours.</summary>
    [Map("6h")] SixHours,
    /// <summary>8 hours.</summary>
    [Map("8h")] EightHours,
    /// <summary>12 hours.</summary>
    [Map("12h")] TwelveHours,
    /// <summary>1 day.</summary>
    [Map("1d")] OneDay,
    /// <summary>1 week.</summary>
    [Map("1w")] OneWeek,
    /// <summary>1 month. Wire token is capital <c>"1M"</c> (lowercase <c>"1m"</c> means one minute).</summary>
    [Map("1M")] OneMonth,
}
