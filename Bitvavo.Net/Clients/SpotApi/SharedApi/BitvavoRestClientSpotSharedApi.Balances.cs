// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.SharedApis;

namespace Bitvavo.Net.Clients.SpotApi;

/// <summary>Balance capability: [V2] <see cref="IGetBalancesRest"/> and the legacy [V1] <see cref="IBalanceRestClient"/> on one implementation.</summary>
internal sealed partial class BitvavoRestClientSpotSharedApi : IGetBalancesRest, IBalanceRestClient
{
    async Task<IExchangeCallResult<SharedBalance[]>> IGetBalances.GetBalancesAsync(GetBalancesRequest request, CancellationToken ct)
        => await GetBalancesAsync(request, ct).ConfigureAwait(false);

    /// <summary>
    /// Bitvavo has one spot account, so the spot account type is the only one offered; a request for any other account type is
    /// rejected before anything is sent.
    /// </summary>
    public GetBalancesOptions GetBalancesOptions { get; } = new GetBalancesOptions(_exchangeName, AccountTypeFilter.Spot)
    {
        RequestNotes = "Bitvavo has one spot account. Total is the available amount plus the amount reserved for open orders (inOrder).",
    };

    /// <inheritdoc />
    public async Task<HttpResult<SharedBalance[]>> GetBalancesAsync(GetBalancesRequest request, CancellationToken ct = default)
    {
        var validationError = GetBalancesOptions.ValidateRequest(request, this);
        if (validationError != null)
        {
            return HttpResult.Fail<SharedBalance[]>(Exchange, validationError);
        }

        var result = await _api.Account.GetBalancesAsync(symbol: null, ct).ConfigureAwait(false);
        if (!result.Success)
        {
            return HttpResult.Fail<SharedBalance[]>(result);
        }

        // A figure the response leaves out counts as zero: the Shared balance has no "unknown" amount.
        return HttpResult.Ok(
            result,
            result.Data
                .Select(x => new SharedBalance(TradingMode.Spot, x.Symbol, x.Available ?? 0m, (x.Available ?? 0m) + (x.InOrder ?? 0m)))
                .ToArray());
    }
}
