// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bitvavo.Net.Objects.Models.Spot;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.SharedApis;

namespace Bitvavo.Net.Clients.SpotApi;

/// <summary>
/// Withdrawal capabilities: [V2] <see cref="IGetWithdrawalHistoryRest"/> and <see cref="IWithdrawRest"/>, plus the legacy [V1]
/// <see cref="IWithdrawalRestClient"/> (whose <c>GetWithdrawalsAsync</c> is the V2 <c>GetWithdrawalHistoryAsync</c> under its old name) and
/// <see cref="IWithdrawRestClient"/>.
/// </summary>
internal sealed partial class BitvavoRestClientSpotSharedApi : IGetWithdrawalHistoryRest, IWithdrawRest, IWithdrawalRestClient, IWithdrawRestClient
{
    /// <summary>Bitvavo serves 1 to 1000 withdrawals per request and 500 when the request names no limit.</summary>
    private const int _withdrawalHistoryMaxLimit = 1000;

    private const int _withdrawalHistoryDefaultLimit = 500;

    // ── history ──────────────────────────────────────────────────────────────────────────

    async Task<IExchangeCallResult<SharedWithdrawal[]>> IGetWithdrawalHistory.GetWithdrawalHistoryAsync(GetWithdrawalsRequest request, PageRequest? nextPageToken, CancellationToken ct)
        => await GetWithdrawalHistoryAsync(request, nextPageToken, ct).ConfigureAwait(false);

    Task<HttpResult<SharedWithdrawal[]>> IWithdrawalRestClient.GetWithdrawalsAsync(GetWithdrawalsRequest request, PageRequest? nextPageToken, CancellationToken ct)
        => GetWithdrawalHistoryAsync(request, nextPageToken, ct);

    GetWithdrawalHistoryOptions IWithdrawalRestClient.GetWithdrawalsOptions => GetWithdrawalHistoryOptions;

    /// <summary>
    /// Up to 1000 withdrawals per request within an optional <c>start</c>/<c>end</c> window; paging goes backwards by moving <c>end</c>.
    /// Bitvavo does not document the order of this list: it is treated as newest first, like Bitvavo's other histories (unverified).
    /// The history carries no network and no withdrawal id: <c>TransactionId</c> is the on-chain transaction id.
    /// A status between the request and the finished transfer (approved, sending, in the mempool, processed) is in progress.
    /// </summary>
    public GetWithdrawalHistoryOptions GetWithdrawalHistoryOptions { get; } = new GetWithdrawalHistoryOptions(
        _exchangeName,
        supportsAscending: false,
        supportsDescending: true,
        timeFilterSupported: true,
        maxLimit: _withdrawalHistoryMaxLimit);

    /// <inheritdoc />
    public async Task<HttpResult<SharedWithdrawal[]>> GetWithdrawalHistoryAsync(GetWithdrawalsRequest request, PageRequest? nextPageToken = null, CancellationToken ct = default)
    {
        var validationError = GetWithdrawalHistoryOptions.ValidateRequest(request, this);
        if (validationError != null)
        {
            return HttpResult.Fail<SharedWithdrawal[]>(Exchange, validationError);
        }

        // The options do not check the limit; Bitvavo would refuse it after the call has been counted against the rate limit.
        if (request.Limit > GetWithdrawalHistoryOptions.MaxLimit)
        {
            return HttpResult.Fail<SharedWithdrawal[]>(
                Exchange,
                ArgumentError.Invalid(nameof(GetWithdrawalsRequest.Limit), $"Only {GetWithdrawalHistoryOptions.MaxLimit} withdrawals can be retrieved per request"));
        }

        var limit = request.Limit ?? _withdrawalHistoryDefaultLimit;
        var pageParams = Pagination.GetPaginationParameters(DataDirection.Descending, limit, request.StartTime, request.EndTime ?? DateTime.UtcNow, nextPageToken);

        // An end time the caller never set is not invented: the newest withdrawals are what a request without one asks for.
        var result = await _api.Funding.GetWithdrawalHistoryAsync(
            request.Asset,
            pageParams.Limit,
            pageParams.StartTime,
            nextPageToken?.EndTime ?? request.EndTime,
            ct).ConfigureAwait(false);
        if (!result.Success)
        {
            return HttpResult.Fail<SharedWithdrawal[]>(result);
        }

        var withdrawals = result.Data.ToArray();
        var nextPageRequest = Pagination.GetNextPageRequest(
            () => Pagination.NextPageFromTime(pageParams, withdrawals.Min(x => x.Timestamp)),
            withdrawals.Length,
            withdrawals.Select(x => x.Timestamp),
            request.StartTime,
            request.EndTime ?? DateTime.UtcNow,
            pageParams);

        return HttpResult.Ok(
            result,
            withdrawals
                .Select(x =>
                {
                    var status = x.Status.ToSharedTransferStatus();
                    return new SharedWithdrawal(x.Symbol, x.Address ?? string.Empty, x.Amount ?? 0m, status == SharedTransferStatus.Completed, x.Timestamp, status)
                    {
                        TransactionId = x.TxId,
                        Tag = x.PaymentReference,
                        Fee = x.Fee,
                    };
                })
                .ToArray(),
            nextPageRequest);
    }

    // ── withdraw ─────────────────────────────────────────────────────────────────────────

    async Task<IExchangeCallResult<SharedId>> IWithdraw.WithdrawAsync(WithdrawRequest request, CancellationToken ct)
        => await WithdrawAsync(request, ct).ConfigureAwait(false);

    /// <summary>
    /// Withdraws over Bitvavo's crypto endpoint (<c>POST /v2/crypto/withdrawal</c>), which names the blockchain network, so the network is
    /// required. The address tag travels as the memo. The network fee is charged on top of the quantity.
    /// </summary>
    public WithdrawOptions WithdrawOptions { get; } = new WithdrawOptions(_exchangeName)
    {
        ParameterRuleOverrides =
        [
            RequestParameterRuleOverride<WithdrawRequest>.Required(
                x => x.Network,
                "The blockchain network to withdraw over (a network name of the asset, as the asset data lists them); Bitvavo requires it"),
        ],
        RequestNotes = "The address must be in the account's address book. The network fee is charged on top of Quantity, so the recipient receives the full "
            + "quantity. The returned id is the one Bitvavo issues for the withdrawal. 2FA and the e-mail confirmation of the address are off for API "
            + "withdrawals: the API key's withdraw permission is the only gate.",
    };

    /// <inheritdoc />
    public async Task<HttpResult<SharedId>> WithdrawAsync(WithdrawRequest request, CancellationToken ct = default)
    {
        var validationError = WithdrawOptions.ValidateRequest(request, this);
        if (validationError != null)
        {
            return HttpResult.Fail<SharedId>(Exchange, validationError);
        }

        var result = await _api.Funding.WithdrawCryptoAsync(
            new BitvavoCryptoWithdrawRequest(request.Asset, request.Network!, request.Address, request.Quantity, Memo: request.AddressTag),
            ct).ConfigureAwait(false);
        if (!result.Success)
        {
            return HttpResult.Fail<SharedId>(result);
        }

        return HttpResult.Ok(result, new SharedId(result.Data.Id));
    }
}
