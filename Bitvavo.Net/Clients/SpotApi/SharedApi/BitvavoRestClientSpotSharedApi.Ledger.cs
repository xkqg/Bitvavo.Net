// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.SharedApis;

namespace Bitvavo.Net.Clients.SpotApi;

/// <summary>Ledger capability: [V2] <see cref="IGetLedgerRest"/>. The legacy [V1] contract has no ledger.</summary>
internal sealed partial class BitvavoRestClientSpotSharedApi : IGetLedgerRest
{
    /// <summary>Bitvavo's transaction history serves 1 to 100 transactions per page.</summary>
    private const int _ledgerPageSize = 100;

    async Task<IExchangeCallResult<SharedLedgerEntry[]>> IGetLedger.GetLedgerAsync(GetLedgerRequest request, PageRequest? nextPageToken, CancellationToken ct)
        => await GetLedgerAsync(request, nextPageToken, ct).ConfigureAwait(false);

    /// <summary>
    /// Bitvavo's transaction history is paged by page number (1 to 100 transactions per page) within an optional
    /// <c>fromDate</c>/<c>toDate</c> window. The options declare it newest first, as every other Bitvavo history is; the documentation does
    /// not state the order of this one. The limit counts Bitvavo transactions, not Shared entries.
    /// </summary>
    public GetLedgerOptions GetLedgerOptions { get; } = new GetLedgerOptions(
        _exchangeName,
        supportsAscending: false,
        supportsDescending: true,
        timeFilterSupported: true,
        maxLimit: _ledgerPageSize)
    {
        RequestNotes = "Limit counts Bitvavo transactions per page (1 to 100), not entries: a trade moved two balances and is returned as two entries "
            + "(one per asset, sharing the RelationId), so a page can hold more entries than the limit. The fee is not an entry of its own, because "
            + "Bitvavo does not say whether the sent and received amounts already include it. Bitvavo cannot filter by asset: the Asset filter is "
            + "applied to each fetched page, so a page can hold fewer entries, or none, while a next page remains.",
    };

    /// <inheritdoc />
    public async Task<HttpResult<SharedLedgerEntry[]>> GetLedgerAsync(GetLedgerRequest request, PageRequest? nextPageToken = null, CancellationToken ct = default)
    {
        var validationError = GetLedgerOptions.ValidateRequest(request, this);
        if (validationError != null)
        {
            return HttpResult.Fail<SharedLedgerEntry[]>(Exchange, validationError);
        }

        // The options do not check the limit; Bitvavo would refuse it after the call has been counted against the rate limit.
        if (request.Limit > GetLedgerOptions.MaxLimit)
        {
            return HttpResult.Fail<SharedLedgerEntry[]>(
                Exchange,
                ArgumentError.Invalid(nameof(GetLedgerRequest.Limit), $"Only {GetLedgerOptions.MaxLimit} transactions can be retrieved per request"));
        }

        var limit = request.Limit ?? GetLedgerOptions.MaxLimit;
        var pageParams = Pagination.GetPaginationParameters(DataDirection.Descending, limit, request.StartTime, request.EndTime ?? DateTime.UtcNow, nextPageToken);

        // The window is the caller's own (or the one a page token carries): a bound the caller never set is not invented.
        var result = await _api.Account.GetTransactionHistoryAsync(
            fromDate: pageParams.StartTime,
            toDate: nextPageToken?.EndTime ?? request.EndTime,
            page: pageParams.Page,
            maxItems: pageParams.Limit,
            ct: ct).ConfigureAwait(false);
        if (!result.Success)
        {
            return HttpResult.Fail<SharedLedgerEntry[]>(result);
        }

        var history = result.Data;
        var entries = history.Items.SelectMany(x => x.ToSharedLedgerEntries());
        if (request.Asset != null)
        {
            entries = entries.Where(x => string.Equals(x.Asset, request.Asset, StringComparison.OrdinalIgnoreCase));
        }

        // Bitvavo says how many pages there are, so the next page is known exactly instead of guessed from the size of this one.
        var nextPageRequest = history.CurrentPage < history.TotalPages ? Pagination.NextPageFromPage(pageParams) : null;

        return HttpResult.Ok(result, entries.ToArray(), nextPageRequest);
    }
}
