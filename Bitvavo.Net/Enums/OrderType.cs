// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Text.Json.Serialization;
using CryptoExchange.Net.Attributes;
using CryptoExchange.Net.Converters.SystemTextJson;

namespace Bitvavo.Net.Enums;

/// <summary>Order placement type — controls how the order matches against the book.</summary>
[JsonConverter(typeof(EnumConverter<OrderType>))]
public enum OrderType
{
    /// <summary>Market order — fills immediately at best available prices.</summary>
    [Map("market")] Market,
    /// <summary>Limit order — fills only at the specified price or better; rests on the book if not immediately marketable.</summary>
    [Map("limit")] Limit,
    /// <summary>Stop-loss market order — triggers a market order when the trigger price is hit.</summary>
    [Map("stopLoss")] StopLoss,
    /// <summary>Stop-loss limit order — triggers a limit order when the trigger price is hit.</summary>
    [Map("stopLossLimit")] StopLossLimit,
    /// <summary>Take-profit market order — triggers a market order when the trigger price is hit.</summary>
    [Map("takeProfit")] TakeProfit,
    /// <summary>Take-profit limit order — triggers a limit order when the trigger price is hit.</summary>
    [Map("takeProfitLimit")] TakeProfitLimit,
}
