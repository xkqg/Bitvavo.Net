// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

namespace Bitvavo.Net.Objects.Models.Spot;

/// <summary>
/// Strongly-typed request payload for <c>POST /v2/withdrawal</c>. Bitvavo's API key
/// must have the <c>withdraw</c> capability — note the docs warning that "2FA and
/// address confirmation by email are disabled for withdrawals using the API."
/// </summary>
/// <param name="Symbol">Asset to withdraw (e.g. <c>"BTC"</c>, <c>"EUR"</c>). Required.</param>
/// <param name="Amount">Quantity of the asset to withdraw, in base-asset units. Required.</param>
/// <param name="Address">Destination wallet address (crypto) or IBAN (fiat). Required.</param>
/// <param name="PaymentId">Memo / payment reference for assets that require it (e.g. XRP destination tag).</param>
/// <param name="AddWithdrawalFee">If true, fee is added on top of <paramref name="Amount"/> (caller is debited <c>Amount + fee</c>); if false or omitted, fee is deducted from <paramref name="Amount"/>.</param>
/// <param name="Internal">If true, transfer between Bitvavo accounts (no on-chain / fiat-rail movement, no fee). Used for inter-account moves.</param>
public record BitvavoWithdrawRequest(
    string Symbol,
    decimal Amount,
    string Address,
    string? PaymentId = null,
    bool? AddWithdrawalFee = null,
    bool? Internal = null);
