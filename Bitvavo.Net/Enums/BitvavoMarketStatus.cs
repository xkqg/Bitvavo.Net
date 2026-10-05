// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Text.Json.Serialization;
using CryptoExchange.Net.Attributes;
using CryptoExchange.Net.Converters.SystemTextJson;

namespace Bitvavo.Net.Enums;

/// <summary>
/// Bitvavo market trading status as returned by <c>GET /v2/markets</c> in the
/// <c>status</c> field. Wire format matches Bitvavo's documented strings
/// (<c>"trading"</c>, <c>"halted"</c>, <c>"auction"</c>, <c>"auctionMatching"</c>, <c>"cancelOnly"</c>).
/// Only <see cref="Trading"/> matches orders; during <see cref="Auction"/> every order type except a market order can still be placed.
/// </summary>
[JsonConverter(typeof(EnumConverter<BitvavoMarketStatus>))]
public enum BitvavoMarketStatus
{
    /// <summary>Market is fully operational.</summary>
    [Map("trading")] Trading = 0,
    /// <summary>Market is halted (maintenance, technical or other operational reasons) — orders cannot be placed.</summary>
    [Map("halted")] Halted = 1,
    /// <summary>Market is in the auction phase (in transition from closed to open): market orders are refused, other order types can be created but nothing matches yet.</summary>
    [Map("auction")] Auction = 2,
    /// <summary>Market is matching the auction's orders; wait for it to complete.</summary>
    [Map("auctionMatching")] AuctionMatching = 3,
    /// <summary>Market only accepts cancellations.</summary>
    [Map("cancelOnly")] CancelOnly = 4,
}
