// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bitvavo.Net.Objects.Models.Spot;
using CryptoExchange.Net.Objects;

namespace Bitvavo.Net.Interfaces.Clients.SpotApi;

/// <summary>
/// Bitvavo signed account-data REST endpoints — fee schedule, balances, capabilities.
/// Requires API credentials (HMAC-SHA256). Mirrors KrakenRestClientSpotApi.Account.
/// </summary>
public interface IBitvavoRestClientSpotApiAccount
{
    /// <summary>
    /// Get the active fee tier and the capabilities granted to the API key.
    /// <para><a href="https://docs.bitvavo.com/docs/rest-api/get-account-balance">Bitvavo API docs</a></para>
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task<WebCallResult<BitvavoAccountInfo>> GetAccountInfoAsync(CancellationToken ct = default);

    /// <summary>
    /// Get the balances of all assets, optionally filtered to a single symbol.
    /// <para><a href="https://docs.bitvavo.com/docs/rest-api/get-account-balance">Bitvavo API docs</a></para>
    /// </summary>
    /// <param name="symbol">Optional asset symbol (e.g. <c>"BTC"</c>) to filter the result. Null returns all assets.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<WebCallResult<IEnumerable<BitvavoBalance>>> GetBalancesAsync(string? symbol = null, CancellationToken ct = default);

    /// <summary>
    /// Get the market-specific fee schedule for the active 30-day-volume tier.
    /// <para><a href="https://docs.bitvavo.com/docs/rest-api/get-account-fees">Bitvavo API docs</a></para>
    /// </summary>
    /// <param name="market">Optional market identifier (e.g. <c>"ETH-EUR"</c>) to scope the fee response.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<WebCallResult<BitvavoMarketFee>> GetTradingFeesAsync(string? market = null, CancellationToken ct = default);

    /// <summary>
    /// Set or refresh the server-side cancel-on-disconnect deadline for orders tagged with
    /// <paramref name="codGroupId"/>. The Bitvavo broker cancels every open order in the group
    /// when no further <c>POST /v2/cancelOrdersAfter</c> lands before
    /// <paramref name="expiryAfterSeconds"/>. Fase 1 dead-man-switch primitive — replaces an
    /// ill-conceived per-order REST-polling bandaid that the entry-council M7 inspector verified
    /// is not a real Bitvavo endpoint.
    /// <para><a href="https://docs.bitvavo.com/docs/cancel-on-disconnect">Bitvavo API docs</a></para>
    /// </summary>
    /// <param name="codGroupId">Numeric cancel-on-disconnect group identifier.</param>
    /// <param name="expiryAfterSeconds">Seconds until the broker cancels the group (minimum 10; 0 removes the group).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<WebCallResult<BitvavoCancelOrdersAfter>> ResetCancelOnDisconnectAsync(
        int codGroupId, int expiryAfterSeconds, CancellationToken ct = default);

    /// <summary>
    /// Get the account transaction history (the account ledger) — trades, deposits,
    /// withdrawals, staking rewards, affiliate payouts, internal transfers, rebates, and
    /// more. Results are paginated by page number; walk
    /// <see cref="BitvavoTransactionHistory.CurrentPage"/> / <see cref="BitvavoTransactionHistory.TotalPages"/>.
    /// <para><a href="https://docs.bitvavo.com/docs/rest-api/get-transaction-history">Bitvavo API docs</a></para>
    /// </summary>
    /// <param name="fromDate">Inclusive UTC lower bound on transaction execution time.</param>
    /// <param name="toDate">Inclusive UTC upper bound on transaction execution time.</param>
    /// <param name="page">One-based page number to return (default 1).</param>
    /// <param name="maxItems">Maximum number of items per page (1–100, default 100).</param>
    /// <param name="type">
    /// Optional transaction-type filter — one of <c>sell</c>, <c>buy</c>, <c>staking</c>,
    /// <c>fixed_staking</c>, <c>deposit</c>, <c>withdrawal</c>, <c>affiliate</c>,
    /// <c>distribution</c>, <c>internal_transfer</c>, <c>withdrawal_cancelled</c>,
    /// <c>rebate</c>, <c>loan</c>, <c>external_transferred_funds</c>, <c>manually_assigned</c>.
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    Task<WebCallResult<BitvavoTransactionHistory>> GetTransactionHistoryAsync(
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int? page = null,
        int? maxItems = null,
        string? type = null,
        CancellationToken ct = default);
}
