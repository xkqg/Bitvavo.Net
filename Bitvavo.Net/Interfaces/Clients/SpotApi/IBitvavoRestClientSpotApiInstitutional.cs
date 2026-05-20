// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bitvavo.Net.Objects.Models.Spot;
using CryptoExchange.Net.Objects;

namespace Bitvavo.Net.Interfaces.Clients.SpotApi;

/// <summary>
/// Bitvavo Institutional REST endpoints — subaccount management, asset transfers between
/// the main account and subaccounts, and per-subaccount balance / order / transaction
/// queries. Every endpoint is authenticated; the API key must be the main account's key
/// and carry the <c>Include all subaccounts</c>, <c>Internal Transfer</c>, and
/// <c>Administrative</c> permissions.
/// <para><a href="https://docs.bitvavo.com/docs/institutional-api/introduction">Bitvavo API docs</a></para>
/// </summary>
public interface IBitvavoRestClientSpotApiInstitutional
{
    /// <summary>
    /// Create a new subaccount under the main account.
    /// <para><a href="https://docs.bitvavo.com/docs/institutional-api/create-subaccount">Bitvavo API docs</a></para>
    /// </summary>
    /// <param name="label">Optional description of the subaccount (up to 100 characters).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<WebCallResult<BitvavoSubaccount>> CreateSubaccountAsync(string? label = null, CancellationToken ct = default);

    /// <summary>
    /// List the subaccounts under the main account, paginated by page number.
    /// <para><a href="https://docs.bitvavo.com/docs/institutional-api/get-subaccounts">Bitvavo API docs</a></para>
    /// </summary>
    /// <param name="page">One-based page number to return.</param>
    /// <param name="maxItems">Maximum number of subaccounts per page.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<WebCallResult<BitvavoSubaccountList>> GetSubaccountsAsync(int? page = null, int? maxItems = null, CancellationToken ct = default);

    /// <summary>
    /// Transfer an asset between the main account and a subaccount.
    /// <para><a href="https://docs.bitvavo.com/docs/institutional-api/create-transfer">Bitvavo API docs</a></para>
    /// </summary>
    /// <param name="request">Transfer parameters (subaccount, direction, asset, amount).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<WebCallResult<BitvavoSubaccountTransfer>> CreateTransferAsync(BitvavoCreateTransferRequest request, CancellationToken ct = default);

    /// <summary>
    /// Get a single subaccount transfer by its Bitvavo transfer identifier.
    /// <para><a href="https://docs.bitvavo.com/docs/institutional-api/get-transfer">Bitvavo API docs</a></para>
    /// </summary>
    /// <param name="transferId">Bitvavo-assigned transfer identifier (UUID). Required.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<WebCallResult<BitvavoSubaccountTransfer>> GetTransferAsync(string transferId, CancellationToken ct = default);

    /// <summary>
    /// List subaccount transfers for a single subaccount, optionally filtered by time window.
    /// <para><a href="https://docs.bitvavo.com/docs/institutional-api/get-transfers">Bitvavo API docs</a></para>
    /// </summary>
    /// <param name="subaccountId">Identifier (UUID) of the subaccount. Required.</param>
    /// <param name="clientRequestId">Optional caller-supplied transfer identifier filter.</param>
    /// <param name="startTime">Inclusive UTC lower bound.</param>
    /// <param name="endTime">Inclusive UTC upper bound.</param>
    /// <param name="limit">Maximum entries (1–1000, default 25).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<WebCallResult<BitvavoSubaccountTransferList>> GetTransfersAsync(
        string subaccountId,
        string? clientRequestId = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        int? limit = null,
        CancellationToken ct = default);

    /// <summary>
    /// Get the balances of a subaccount, or of the main account when
    /// <paramref name="subaccountId"/> is null.
    /// <para><a href="https://docs.bitvavo.com/docs/institutional-api/get-balance">Bitvavo API docs</a></para>
    /// </summary>
    /// <param name="subaccountId">Identifier (UUID) of the subaccount. Null returns the main account balance.</param>
    /// <param name="symbol">Optional asset filter. Null returns every asset with a balance &gt; 0.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<WebCallResult<BitvavoSubaccountBalances>> GetSubaccountBalancesAsync(
        string? subaccountId = null,
        string? symbol = null,
        CancellationToken ct = default);

    /// <summary>
    /// Get the transaction history of a subaccount, paginated by page number.
    /// <para><a href="https://docs.bitvavo.com/docs/institutional-api/get-transaction-history">Bitvavo API docs</a></para>
    /// </summary>
    /// <param name="subaccountId">Identifier (UUID) of the subaccount.</param>
    /// <param name="fromDate">Inclusive UTC lower bound.</param>
    /// <param name="toDate">Inclusive UTC upper bound.</param>
    /// <param name="page">One-based page number to return.</param>
    /// <param name="maxItems">Maximum number of items per page (1–100).</param>
    /// <param name="type">Optional transaction-type filter (e.g. <c>"buy"</c>, <c>"deposit"</c>).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<WebCallResult<BitvavoTransactionHistory>> GetSubaccountTransactionHistoryAsync(
        string? subaccountId = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int? page = null,
        int? maxItems = null,
        string? type = null,
        CancellationToken ct = default);

    /// <summary>
    /// List the open orders of a subaccount, or of the main account when
    /// <paramref name="subaccountId"/> is null.
    /// <para><a href="https://docs.bitvavo.com/docs/institutional-api/get-open-orders">Bitvavo API docs</a></para>
    /// </summary>
    /// <param name="subaccountId">Identifier (UUID) of the subaccount. Null returns main-account open orders.</param>
    /// <param name="market">Optional market filter (e.g. <c>"BTC-EUR"</c>).</param>
    /// <param name="baseAsset">Optional base-asset filter (e.g. <c>"BTC"</c>).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<WebCallResult<IEnumerable<BitvavoOrder>>> GetSubaccountOpenOrdersAsync(
        string? subaccountId = null,
        string? market = null,
        string? baseAsset = null,
        CancellationToken ct = default);

    /// <summary>
    /// Cancel a single order on a subaccount (or the main account).
    /// <para><a href="https://docs.bitvavo.com/docs/institutional-api/cancel-order">Bitvavo API docs</a></para>
    /// </summary>
    /// <param name="request">Cancel parameters (market, order id, operator id, optional subaccount).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<WebCallResult<BitvavoOrderId>> CancelSubaccountOrderAsync(BitvavoSubaccountCancelOrderRequest request, CancellationToken ct = default);

    /// <summary>
    /// Cancel all open orders on a subaccount (or the main account), optionally scoped to a
    /// single market.
    /// <para><a href="https://docs.bitvavo.com/docs/institutional-api/cancel-orders">Bitvavo API docs</a></para>
    /// </summary>
    /// <param name="operatorId">Account-scoped integer identifying the request originator. Required by Bitvavo.</param>
    /// <param name="subaccountId">Identifier (UUID) of the subaccount. Null cancels on the main account.</param>
    /// <param name="market">Optional market to scope the cancellation.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<WebCallResult<IEnumerable<BitvavoOrderId>>> CancelSubaccountOrdersAsync(
        long operatorId,
        string? subaccountId = null,
        string? market = null,
        CancellationToken ct = default);
}
