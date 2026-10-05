// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

namespace Bitvavo.Net.Objects.Models.Spot;

/// <summary>
/// Strongly-typed request payload for <c>POST /v2/crypto/withdrawal</c> — the withdrawal endpoint that names the blockchain
/// network and returns a server-issued withdrawal id. Same warning as <see cref="BitvavoWithdrawRequest"/>: the API key's
/// <c>withdraw</c> permission is the only safety gate.
/// </summary>
/// <param name="Asset">Asset to withdraw (e.g. <c>"BTC"</c>). Required.</param>
/// <param name="Network">Blockchain network to send on (e.g. <c>"Bitcoin"</c>); the supported networks per asset are listed by the assets endpoint. Required.</param>
/// <param name="Address">Destination address; it must be verified in the account's address book. Required.</param>
/// <param name="Amount">Quantity to withdraw, in asset units. Required.</param>
/// <param name="DeductFeeFromAmount">If true the network fee is deducted from <paramref name="Amount"/>; if false or omitted it is charged on top.</param>
/// <param name="IdempotencyKey">Makes a retried request safe: the same key with the same details never withdraws twice; the same key with different details is rejected (error 412).</param>
/// <param name="Memo">Memo / destination tag for assets that need one (e.g. XRP).</param>
public record BitvavoCryptoWithdrawRequest(
    string Asset,
    string Network,
    string Address,
    decimal Amount,
    bool? DeductFeeFromAmount = null,
    string? IdempotencyKey = null,
    string? Memo = null);
