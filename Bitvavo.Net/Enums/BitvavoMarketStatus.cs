// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Text.Json.Serialization;
using CryptoExchange.Net.Attributes;
using CryptoExchange.Net.Converters.SystemTextJson;

namespace Bitvavo.Net.Enums;

/// <summary>
/// Bitvavo market trading status as returned by <c>GET /v2/markets</c> in the
/// <c>status</c> field. Wire format matches Bitvavo's documented strings
/// (<c>"trading"</c>, <c>"halted"</c>, <c>"auction"</c>).
/// </summary>
[JsonConverter(typeof(EnumConverter<BitvavoMarketStatus>))]
public enum BitvavoMarketStatus
{
    /// <summary>Market is fully operational.</summary>
    [Map("trading")] Trading = 0,
    /// <summary>Market is paused — orders cannot be placed.</summary>
    [Map("halted")] Halted = 1,
    /// <summary>Market is in auction phase.</summary>
    [Map("auction")] Auction = 2,
}
