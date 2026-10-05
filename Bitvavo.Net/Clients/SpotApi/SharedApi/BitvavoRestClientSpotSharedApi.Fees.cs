// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.SharedApis;

namespace Bitvavo.Net.Clients.SpotApi;

/// <summary>Fee capability: [V2] <see cref="IGetFeesRest"/> and the legacy [V1] <see cref="IFeeRestClient"/> on one implementation.</summary>
internal sealed partial class BitvavoRestClientSpotSharedApi : IGetFeesRest, IFeeRestClient
{
    async Task<IExchangeCallResult<SharedFee>> IGetFees.GetFeesAsync(GetFeeRequest request, CancellationToken ct)
        => await GetFeesAsync(request, ct).ConfigureAwait(false);

    /// <summary>The fees are those of the account's current 30-day volume tier for the fee category of the requested market.</summary>
    public GetFeeOptions GetFeeOptions { get; } = new GetFeeOptions(_exchangeName, true)
    {
        RequestNotes = "Fees are percentages: Bitvavo reports the maker and taker rate as a fraction (0.0015) and it is returned as 0.15.",
    };

    /// <inheritdoc />
    public async Task<HttpResult<SharedFee>> GetFeesAsync(GetFeeRequest request, CancellationToken ct = default)
    {
        var validationError = GetFeeOptions.ValidateRequest(request, this);
        if (validationError != null)
        {
            return HttpResult.Fail<SharedFee>(Exchange, validationError);
        }

        var result = await _api.Account.GetTradingFeesAsync(request.Symbol!.GetSymbol(FormatSymbol), ct: ct).ConfigureAwait(false);
        if (!result.Success)
        {
            return HttpResult.Fail<SharedFee>(result);
        }

        // Bitvavo's rates are fractions (0.0015 is 0.15 %); the Shared fee is a percentage. A rate the response leaves out counts as zero.
        return HttpResult.Ok(result, new SharedFee((result.Data.Maker ?? 0m) * 100m, (result.Data.Taker ?? 0m) * 100m));
    }
}
