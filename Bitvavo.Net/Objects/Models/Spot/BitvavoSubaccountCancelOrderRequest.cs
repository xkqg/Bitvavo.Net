// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

namespace Bitvavo.Net.Objects.Models.Spot;

/// <summary>
/// Strongly-typed request payload for <c>DELETE /v2/institutional/subaccounts/order</c> —
/// cancels a single order on a subaccount (or the main account when
/// <paramref name="SubaccountId"/> is null).
/// </summary>
/// <param name="Market">Market the order belongs to (e.g. <c>"BTC-EUR"</c>). Required.</param>
/// <param name="OrderId">Bitvavo's order identifier (UUID). Required.</param>
/// <param name="OperatorId">Account-scoped integer identifying the request originator. Required.</param>
/// <param name="SubaccountId">Identifier (UUID) of the subaccount. Null cancels on the main account.</param>
/// <param name="ClientOrderId">Caller-assigned order identifier (UUID). Takes precedence over <paramref name="OrderId"/> if both are supplied.</param>
public record BitvavoSubaccountCancelOrderRequest(
    string Market,
    string OrderId,
    long OperatorId,
    string? SubaccountId = null,
    string? ClientOrderId = null);
