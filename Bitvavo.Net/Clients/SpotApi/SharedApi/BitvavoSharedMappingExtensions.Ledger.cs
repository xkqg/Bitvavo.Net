// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Collections.Generic;
using Bitvavo.Net.Objects.Models.Spot;
using CryptoExchange.Net.SharedApis;

namespace Bitvavo.Net.Clients.SpotApi;

internal static partial class BitvavoSharedMappingExtensions
{
    /// <summary>
    /// A Bitvavo transaction type (the fifteen of docs.bitvavo.com, <c>GET /account/history</c>) as a Shared ledger entry type. A type
    /// is mapped only where the Shared type is an unambiguous match; every other one is <see cref="SharedLedgerEntryType.Unknown"/>,
    /// and the entry's <c>TypeString</c> keeps the Bitvavo value for exact classification. A type Bitvavo adds later is Unknown as
    /// well, never an exception.
    /// </summary>
    public static SharedLedgerEntryType ToSharedLedgerEntryType(this string? type) => type?.ToLowerInvariant() switch
    {
        // A filled order. The balances it moved are separate entries (an asset leaves, another arrives), all of this type.
        "buy" => SharedLedgerEntryType.Trade,
        "sell" => SharedLedgerEntryType.Trade,

        // Assets that came in from, or went out to, another place over a blockchain or a bank.
        "deposit" => SharedLedgerEntryType.Deposit,
        "withdrawal" => SharedLedgerEntryType.Withdrawal,

        // The refund of a cancelled withdrawal belongs to the withdrawal flow: its positive quantity nets against the withdrawal's
        // negative one, so the Withdrawal entries still add up to what actually left the account.
        "withdrawal_cancelled" => SharedLedgerEntryType.Withdrawal,

        // A movement inside Bitvavo rather than over a blockchain: the closest Shared type is Transfer.
        "internal_transfer" => SharedLedgerEntryType.Transfer,

        // A fee rebate.
        "rebate" => SharedLedgerEntryType.Rebate,

        // No Shared counterpart for the rest: calling them Trade, Transfer or Rebate would put them in the wrong sum.
        "staking" => SharedLedgerEntryType.Unknown,
        "fixed_staking" => SharedLedgerEntryType.Unknown,

        // An affiliate payout is referral income, not a rebate of fees the account paid.
        "affiliate" => SharedLedgerEntryType.Unknown,
        "distribution" => SharedLedgerEntryType.Unknown,
        "loan" => SharedLedgerEntryType.Unknown,

        // Funds moved by a party outside the deposit and withdrawal flows: neither a Deposit nor a transfer between own accounts can be assumed.
        "external_transferred_funds" => SharedLedgerEntryType.Unknown,

        // A manual assignment, by the account holder or by Bitvavo.
        "manually_assigned" => SharedLedgerEntryType.Unknown,
        "manually_assigned_bitvavo" => SharedLedgerEntryType.Unknown,

        _ => SharedLedgerEntryType.Unknown,
    };

    /// <summary>
    /// A Bitvavo transaction as Shared ledger entries: one entry per balance that moved, because a Shared entry is the change of one
    /// asset. The sent leg (<c>sentCurrency</c>/<c>sentAmount</c>) is a negative quantity and the received leg a positive one, so a
    /// trade is two entries (sent first) and a deposit or a withdrawal one. The entries share the transaction id as
    /// <c>RelationId</c>; the <c>Id</c> of each is that transaction id plus <c>:sent</c> or <c>:received</c>, which makes it unique.
    /// A leg needs both its currency and its amount; a transaction without a leg moved no balance and yields no entry.
    /// <para>
    /// The fee is not an entry of its own. Bitvavo reports <c>feesAmount</c> as a separate field and does not say whether the sent and
    /// received amounts already include it, so an entry derived from it could count the fee twice.
    /// </para>
    /// </summary>
    public static SharedLedgerEntry[] ToSharedLedgerEntries(this BitvavoTransactionHistoryEntry transaction)
    {
        var type = transaction.Type.ToSharedLedgerEntryType();
        var entries = new List<SharedLedgerEntry>(2);

        if (transaction.SentCurrency is { Length: > 0 } sentCurrency && transaction.SentAmount is { } sentAmount)
        {
            entries.Add(LedgerLeg(transaction, sentCurrency, -sentAmount, type, "sent"));
        }

        if (transaction.ReceivedCurrency is { Length: > 0 } receivedCurrency && transaction.ReceivedAmount is { } receivedAmount)
        {
            entries.Add(LedgerLeg(transaction, receivedCurrency, receivedAmount, type, "received"));
        }

        return entries.ToArray();
    }

    private static SharedLedgerEntry LedgerLeg(BitvavoTransactionHistoryEntry transaction, string asset, decimal delta, SharedLedgerEntryType type, string leg)
        => new(asset, delta, type, transaction.Type, transaction.ExecutedAt)
        {
            Id = transaction.TransactionId + ":" + leg,
            RelationId = transaction.TransactionId,
        };
}
