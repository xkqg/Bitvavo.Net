// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Text.Json.Serialization;
using CryptoExchange.Net.Attributes;
using CryptoExchange.Net.Converters.SystemTextJson;

namespace Bitvavo.Net.Enums;

/// <summary>Which price reference Bitvavo evaluates a trigger order against.</summary>
[JsonConverter(typeof(EnumConverter<TriggerReference>))]
public enum TriggerReference
{
    /// <summary>The most recent trade price.</summary>
    [Map("lastTrade")] LastTrade,
    /// <summary>The current best bid on the order book.</summary>
    [Map("bestBid")] BestBid,
    /// <summary>The current best ask on the order book.</summary>
    [Map("bestAsk")] BestAsk,
    /// <summary>The midpoint between best bid and best ask.</summary>
    [Map("midPrice")] MidPrice,
}
