// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using Bitvavo.Net.Enums;

namespace Bitvavo.Net.Objects.Models.Spot;

/// <summary>
/// Strongly-typed request payload for <c>PUT /v2/order</c> (limit + trigger orders only).
/// Either <paramref name="OrderId"/> or <paramref name="ClientOrderId"/> must be set —
/// Bitvavo accepts either as the order locator.
/// </summary>
/// <param name="Market">Market the order belongs to (e.g. <c>"ETH-EUR"</c>). Required.</param>
/// <param name="OperatorId">Account-scoped integer identifying the request originator. Required.</param>
/// <param name="OrderId">Server-assigned order id. Set this OR <paramref name="ClientOrderId"/>.</param>
/// <param name="ClientOrderId">Client-assigned order id. Set this OR <paramref name="OrderId"/>.</param>
/// <param name="Amount">New base-asset quantity (replaces the original).</param>
/// <param name="AmountQuote">New quote-asset budget (replaces the original).</param>
/// <param name="Price">New limit price.</param>
/// <param name="TriggerAmount">New trigger price for stop / take-profit orders.</param>
/// <param name="TimeInForce">New time-in-force policy.</param>
/// <param name="SelfTradePrevention">New self-trade-prevention rule.</param>
/// <param name="PostOnly">New post-only flag.</param>
/// <param name="ResponseRequired">If false, Bitvavo returns a slimmer ack-only response.</param>
public record BitvavoUpdateOrderRequest(
    string Market,
    long OperatorId,
    string? OrderId = null,
    string? ClientOrderId = null,
    decimal? Amount = null,
    decimal? AmountQuote = null,
    decimal? Price = null,
    decimal? TriggerAmount = null,
    TimeInForce? TimeInForce = null,
    SelfTradePrevention? SelfTradePrevention = null,
    bool? PostOnly = null,
    bool? ResponseRequired = null);
