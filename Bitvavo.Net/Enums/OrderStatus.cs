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
    [Map("new")] New,
    /// <summary>Trigger order awaiting its trigger price.</summary>
    [Map("awaitingTrigger")] AwaitingTrigger,
    /// <summary>Order canceled by the user.</summary>
    [Map("canceled")] Canceled,
    /// <summary>Order canceled because it was placed during an auction.</summary>
    [Map("canceledAuction")] CanceledAuction,
    /// <summary>Order canceled by the self-trade-prevention rule.</summary>
    [Map("canceledSelfTradePrevention")] CanceledSelfTradePrevention,
    /// <summary>IOC order canceled because it could not fill immediately.</summary>
    [Map("canceledIOC")] CanceledIoc,
    /// <summary>FOK order canceled because it could not fill in full immediately.</summary>
    [Map("canceledFOK")] CanceledFok,
    /// <summary>Order canceled because of Bitvavo's market-protection rule.</summary>
    [Map("canceledMarketProtection")] CanceledMarketProtection,
    /// <summary>Post-only order canceled because it would have crossed the spread.</summary>
    [Map("canceledPostOnly")] CanceledPostOnly,
    /// <summary>Order fully filled.</summary>
    [Map("filled")] Filled,
    /// <summary>Order partially filled (still resting for the remaining amount).</summary>
    [Map("partiallyFilled")] PartiallyFilled,
    /// <summary>Order expired (TimeInForce honored).</summary>
    [Map("expired")] Expired,
    /// <summary>Order rejected by the exchange.</summary>
    [Map("rejected")] Rejected,
}
