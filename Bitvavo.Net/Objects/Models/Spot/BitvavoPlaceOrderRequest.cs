// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using Bitvavo.Net.Enums;

namespace Bitvavo.Net.Objects.Models.Spot;

/// <summary>
/// Strongly-typed request payload for <c>POST /v2/order</c>. Fields are surfaced as a
/// record (rather than a wide method signature) because Bitvavo exposes 15+ optional
/// parameters per order — call-site readability wins.
/// </summary>
/// <param name="Market">Market identifier (e.g. <c>"ETH-EUR"</c>). Required.</param>
/// <param name="Side">Buy or sell. Required.</param>
/// <param name="OrderType">Market / limit / stop-loss / take-profit family. Required.</param>
/// <param name="OperatorId">
/// Account-scoped integer identifying the originator of the order request. Bitvavo
/// requires this on every order op; pick any value that's stable for your tooling.
/// </param>
/// <param name="Amount">Quantity in base-asset units. Mutually exclusive with <paramref name="AmountQuote"/>.</param>
/// <param name="AmountQuote">Quote-asset budget for market buys. Mutually exclusive with <paramref name="Amount"/>.</param>
/// <param name="Price">Limit price. Required for limit / stop-limit / take-profit-limit orders.</param>
/// <param name="TriggerAmount">Trigger price for stop-loss / take-profit orders.</param>
/// <param name="TriggerType">Type of trigger event. Bitvavo currently only supports <see cref="Enums.TriggerType.Price"/>.</param>
/// <param name="TriggerReference">Which price reference Bitvavo evaluates the trigger against.</param>
/// <param name="TimeInForce">Time-in-force policy. Default GTC if omitted.</param>
/// <param name="PostOnly">If true, the order is rejected if it would have crossed the spread.</param>
/// <param name="SelfTradePrevention">How to resolve fills against this account's other orders.</param>
/// <param name="ResponseRequired">If false, Bitvavo returns a slimmer ack-only response (lower latency).</param>
/// <param name="ClientOrderId">Caller-assigned UUID — useful for client-side correlation when Bitvavo's network ack is unreliable.</param>
/// <param name="CodGroupId">Cancel-on-disconnect group id (1–1000).</param>
public record BitvavoPlaceOrderRequest(
    string Market,
    OrderSide Side,
    OrderType OrderType,
    long OperatorId,
    decimal? Amount = null,
    decimal? AmountQuote = null,
    decimal? Price = null,
    decimal? TriggerAmount = null,
    TriggerType? TriggerType = null,
    TriggerReference? TriggerReference = null,
    TimeInForce? TimeInForce = null,
    bool? PostOnly = null,
    SelfTradePrevention? SelfTradePrevention = null,
    bool? ResponseRequired = null,
    string? ClientOrderId = null,
    int? CodGroupId = null);
