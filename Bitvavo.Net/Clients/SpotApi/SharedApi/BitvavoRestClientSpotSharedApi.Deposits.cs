// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.SharedApis;

namespace Bitvavo.Net.Clients.SpotApi;

/// <summary>
/// Deposit capabilities: [V2] <see cref="IGetDepositAddressesRest"/> and <see cref="IGetDepositHistoryRest"/>, plus the legacy [V1]
/// <see cref="IDepositRestClient"/>, whose <c>GetDepositsAsync</c> is the V2 <c>GetDepositHistoryAsync</c> under its old name.
/// </summary>
internal sealed partial class BitvavoRestClientSpotSharedApi : IGetDepositAddressesRest, IGetDepositHistoryRest, IDepositRestClient
{
    /// <summary>Bitvavo serves 1 to 1000 deposits per request and 500 when the request names no limit.</summary>
    private const int _depositHistoryMaxLimit = 1000;

    private const int _depositHistoryDefaultLimit = 500;

    // ── addresses ────────────────────────────────────────────────────────────────────────

    async Task<IExchangeCallResult<SharedDepositAddress[]>> IGetDepositAddresses.GetDepositAddressesAsync(GetDepositAddressesRequest request, CancellationToken ct)
        => await GetDepositAddressesAsync(request, ct).ConfigureAwait(false);

    /// <summary>
    /// Bitvavo's deposit endpoint takes the asset only: it returns one address per asset and cannot choose a network, so a request that
    /// names a network is rejected instead of being answered with an address that may belong to another one.
    /// </summary>
    public GetDepositAddressesOptions GetDepositAddressesOptions { get; } = new GetDepositAddressesOptions(_exchangeName, true)
    {
        ParameterRuleOverrides =
        [
            RequestParameterRuleOverride<GetDepositAddressesRequest>.NotSupported(
                x => x.Network,
                "Bitvavo returns one deposit address per asset and cannot choose a network; a request that names one is rejected"),
        ],
        RequestNotes = "A fiat asset has bank details instead of an address, so its list is empty. A memo (TagOrMemo) is returned for assets that need one.",
    };

    /// <inheritdoc />
    public async Task<HttpResult<SharedDepositAddress[]>> GetDepositAddressesAsync(GetDepositAddressesRequest request, CancellationToken ct = default)
    {
        var validationError = GetDepositAddressesOptions.ValidateRequest(request, this);
        if (validationError != null)
        {
            return HttpResult.Fail<SharedDepositAddress[]>(Exchange, validationError);
        }

        // Answering a request that names a network with the asset's default address could send funds over a network the caller did not choose.
        if (request.Network != null)
        {
            return HttpResult.Fail<SharedDepositAddress[]>(
                Exchange,
                ArgumentError.Invalid(nameof(GetDepositAddressesRequest.Network), "Bitvavo returns one deposit address per asset and cannot choose a network"));
        }

        var result = await _api.Funding.GetDepositAddressAsync(request.Asset, ct).ConfigureAwait(false);
        if (!result.Success)
        {
            return HttpResult.Fail<SharedDepositAddress[]>(result);
        }

        var address = result.Data.Address;
        if (string.IsNullOrEmpty(address))
        {
            return HttpResult.Ok(result, Array.Empty<SharedDepositAddress>());
        }

        return HttpResult.Ok(result, new[] { new SharedDepositAddress(request.Asset, address) { TagOrMemo = result.Data.PaymentReference } });
    }

    // ── history ──────────────────────────────────────────────────────────────────────────

    async Task<IExchangeCallResult<SharedDeposit[]>> IGetDepositHistory.GetDepositHistoryAsync(GetDepositsRequest request, PageRequest? nextPageToken, CancellationToken ct)
        => await GetDepositHistoryAsync(request, nextPageToken, ct).ConfigureAwait(false);

    Task<HttpResult<SharedDeposit[]>> IDepositRestClient.GetDepositsAsync(GetDepositsRequest request, PageRequest? nextPageToken, CancellationToken ct)
        => GetDepositHistoryAsync(request, nextPageToken, ct);

    GetDepositHistoryOptions IDepositRestClient.GetDepositsOptions => GetDepositHistoryOptions;

    /// <summary>
    /// Up to 1000 deposits per request within an optional <c>start</c>/<c>end</c> window; paging goes backwards by moving <c>end</c>.
    /// Bitvavo does not document the order of this list: it is treated as newest first, like Bitvavo's other histories (unverified).
    /// The history carries no network and no deposit id: <c>TransactionId</c> is the on-chain transaction id.
    /// </summary>
    public GetDepositHistoryOptions GetDepositHistoryOptions { get; } = new GetDepositHistoryOptions(
        _exchangeName,
        supportsAscending: false,
        supportsDescending: true,
        timeFilterSupported: true,
        maxLimit: _depositHistoryMaxLimit);

    /// <inheritdoc />
    public async Task<HttpResult<SharedDeposit[]>> GetDepositHistoryAsync(GetDepositsRequest request, PageRequest? nextPageToken = null, CancellationToken ct = default)
    {
        var validationError = GetDepositHistoryOptions.ValidateRequest(request, this);
        if (validationError != null)
        {
            return HttpResult.Fail<SharedDeposit[]>(Exchange, validationError);
        }

        // The options do not check the limit; Bitvavo would refuse it after the call has been counted against the rate limit.
        if (request.Limit > GetDepositHistoryOptions.MaxLimit)
        {
            return HttpResult.Fail<SharedDeposit[]>(
                Exchange,
                ArgumentError.Invalid(nameof(GetDepositsRequest.Limit), $"Only {GetDepositHistoryOptions.MaxLimit} deposits can be retrieved per request"));
        }

        var limit = request.Limit ?? _depositHistoryDefaultLimit;
        var pageParams = Pagination.GetPaginationParameters(DataDirection.Descending, limit, request.StartTime, request.EndTime ?? DateTime.UtcNow, nextPageToken);

        // An end time the caller never set is not invented: the newest deposits are what a request without one asks for.
        var result = await _api.Funding.GetDepositHistoryAsync(
            request.Asset,
            pageParams.Limit,
            pageParams.StartTime,
            nextPageToken?.EndTime ?? request.EndTime,
            ct).ConfigureAwait(false);
        if (!result.Success)
        {
            return HttpResult.Fail<SharedDeposit[]>(result);
        }

        var deposits = result.Data.ToArray();
        var nextPageRequest = Pagination.GetNextPageRequest(
            () => Pagination.NextPageFromTime(pageParams, deposits.Min(x => x.Timestamp)),
            deposits.Length,
            deposits.Select(x => x.Timestamp),
            request.StartTime,
            request.EndTime ?? DateTime.UtcNow,
            pageParams);

        return HttpResult.Ok(
            result,
            deposits
                .Select(x =>
                {
                    var status = x.Status.ToSharedTransferStatus();
                    return new SharedDeposit(x.Symbol, x.Amount ?? 0m, status == SharedTransferStatus.Completed, x.Timestamp, status)
                    {
                        TransactionId = x.TxId,
                        Tag = x.PaymentReference,
                    };
                })
                .ToArray(),
            nextPageRequest);
    }
}
