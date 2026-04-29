// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bitvavo.Net.Objects.Models.Spot;
using CryptoExchange.Net.Objects;

namespace Bitvavo.Net.Interfaces.Clients.SpotApi;

/// <summary>
/// Bitvavo signed funding REST endpoints — deposit address (or fiat banking
/// instructions), deposit / withdrawal histories, and the <c>WithdrawAsync</c> write.
/// </summary>
public interface IBitvavoRestClientSpotApiFunding
{
    /// <summary>
    /// Get the deposit address (or fiat banking instructions) for a given asset.
    /// <para><a href="https://docs.bitvavo.com/docs/rest-api/get-deposit-data">Bitvavo API docs</a></para>
    /// </summary>
    /// <param name="symbol">Asset symbol (e.g. <c>"BTC"</c>, <c>"EUR"</c>). Required.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<WebCallResult<BitvavoDepositAddress>> GetDepositAddressAsync(string symbol, CancellationToken ct = default);

    /// <summary>
    /// Get the deposit history for the account, optionally filtered by asset and time range.
    /// <para><a href="https://docs.bitvavo.com/docs/rest-api/get-deposit-history">Bitvavo API docs</a></para>
    /// </summary>
    /// <param name="symbol">Optional asset filter.</param>
    /// <param name="limit">Maximum entries (1–1000, default 500).</param>
    /// <param name="startTime">Inclusive UTC lower bound.</param>
    /// <param name="endTime">Inclusive UTC upper bound.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<WebCallResult<IEnumerable<BitvavoDepositHistoryEntry>>> GetDepositHistoryAsync(
        string? symbol = null,
        int? limit = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        CancellationToken ct = default);

    /// <summary>
    /// Get the withdrawal history for the account, optionally filtered by asset and time range.
    /// <para><a href="https://docs.bitvavo.com/docs/rest-api/get-withdrawal-history">Bitvavo API docs</a></para>
    /// </summary>
    /// <param name="symbol">Optional asset filter.</param>
    /// <param name="limit">Maximum entries (1–1000, default 500).</param>
    /// <param name="startTime">Inclusive UTC lower bound.</param>
    /// <param name="endTime">Inclusive UTC upper bound.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<WebCallResult<IEnumerable<BitvavoWithdrawalHistoryEntry>>> GetWithdrawalHistoryAsync(
        string? symbol = null,
        int? limit = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        CancellationToken ct = default);

    /// <summary>
    /// Submit a withdrawal request. Bitvavo accepts the request synchronously; final
    /// settlement progresses through the pending → completed lifecycle visible via
    /// <see cref="GetWithdrawalHistoryAsync"/>.
    /// <para>
    /// <strong>Caution:</strong> this endpoint moves funds. The Bitvavo docs warn that
    /// "2FA and address confirmation by email are disabled for withdrawals using the API"
    /// — the API key's <c>withdraw</c> capability is the only safety gate.
    /// </para>
    /// <para><a href="https://docs.bitvavo.com/docs/rest-api/withdraw-assets">Bitvavo API docs</a></para>
    /// </summary>
    /// <param name="request">Withdrawal parameters (symbol, amount, destination, optional fee handling).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<WebCallResult<BitvavoWithdrawalResult>> WithdrawAsync(BitvavoWithdrawRequest request, CancellationToken ct = default);
}
