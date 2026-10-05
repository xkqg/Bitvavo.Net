// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.SharedApis;

namespace Bitvavo.Net.Clients.SpotApi;

/// <summary>
/// Recent trades capability: [V2] <see cref="IGetRecentTradesRest"/> and the legacy [V1] <see cref="IRecentTradeRestClient"/>, whose
/// members carry the same names as the V2 ones and are implemented by the very members V2 uses.
/// </summary>
internal sealed partial class BitvavoRestClientSpotSharedApi : IGetRecentTradesRest, IRecentTradeRestClient
{
    private const int _recentTradesMaxLimit = 1000;

    async Task<IExchangeCallResult<SharedTrade[]>> IGetRecentTrades.GetRecentTradesAsync(GetRecentTradesRequest request, CancellationToken ct)
        => await GetRecentTradesAsync(request, ct).ConfigureAwait(false);

    /// <summary>Bitvavo serves up to 1000 public trades per request, newest first; the order is kept as Bitvavo sends it.</summary>
    public GetRecentTradesOptions GetRecentTradesOptions { get; } = new GetRecentTradesOptions(_exchangeName, _recentTradesMaxLimit, false);

    /// <inheritdoc />
    public async Task<HttpResult<SharedTrade[]>> GetRecentTradesAsync(GetRecentTradesRequest request, CancellationToken ct = default)
    {
        var validationError = GetRecentTradesOptions.ValidateRequest(request, this);
        if (validationError != null)
        {
            return HttpResult.Fail<SharedTrade[]>(Exchange, validationError);
        }

        var symbol = request.Symbol!.GetSymbol(FormatSymbol);
        var result = await _api.ExchangeData.GetPublicTradesAsync(symbol, request.Limit, ct: ct).ConfigureAwait(false);
        if (!result.Success)
        {
            return HttpResult.Fail<SharedTrade[]>(result);
        }

        return HttpResult.Ok(
            result,
            result.Data.Select(trade => new SharedTrade(request.Symbol, symbol, new SharedOrderQuantity(trade.Amount), trade.Price ?? 0m, trade.Timestamp)
            {
                Side = trade.Side.ToSharedSide(),
            }).ToArray());
    }
}
