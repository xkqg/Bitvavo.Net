// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Text.Json.Serialization;
using CryptoExchange.Net.Attributes;
using CryptoExchange.Net.Converters.SystemTextJson;

namespace Bitvavo.Net.Enums;

/// <summary>Lifecycle state of an order on the Bitvavo exchange.</summary>
[JsonConverter(typeof(EnumConverter<OrderStatus>))]
public enum OrderStatus
{
    /// <summary>Order accepted, resting on the book.</summary>
    [Map("new")] New = 0,
    /// <summary>Trigger order awaiting its trigger price.</summary>
    [Map("awaitingTrigger")] AwaitingTrigger = 1,
    /// <summary>Order canceled by the user.</summary>
    [Map("canceled")] Canceled = 2,
    /// <summary>Order canceled because it was placed during an auction.</summary>
    [Map("canceledAuction")] CanceledAuction = 3,
    /// <summary>Order canceled by the self-trade-prevention rule.</summary>
    [Map("canceledSelfTradePrevention")] CanceledSelfTradePrevention = 4,
    /// <summary>IOC order canceled because it could not fill immediately.</summary>
    [Map("canceledIOC")] CanceledIoc = 5,
    /// <summary>FOK order canceled because it could not fill in full immediately.</summary>
    [Map("canceledFOK")] CanceledFok = 6,
    /// <summary>Order canceled because of Bitvavo's market-protection rule.</summary>
    [Map("canceledMarketProtection")] CanceledMarketProtection = 7,
    /// <summary>Post-only order canceled because it would have crossed the spread.</summary>
    [Map("canceledPostOnly")] CanceledPostOnly = 8,
    /// <summary>Order fully filled.</summary>
    [Map("filled")] Filled = 9,
    /// <summary>Order partially filled (still resting for the remaining amount).</summary>
    [Map("partiallyFilled")] PartiallyFilled = 10,
    /// <summary>Order expired (TimeInForce honored).</summary>
    [Map("expired")] Expired = 11,
    /// <summary>Order rejected by the exchange.</summary>
    [Map("rejected")] Rejected = 12,
}
