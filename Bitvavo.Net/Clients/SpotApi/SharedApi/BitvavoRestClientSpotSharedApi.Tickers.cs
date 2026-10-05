// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Errors;
using CryptoExchange.Net.SharedApis;

namespace Bitvavo.Net.Clients.SpotApi;

/// <summary>
/// Ticker capabilities: [V2] <see cref="IGetTickerRest"/> and <see cref="IGetAllTickersRest"/>, plus the legacy [V1]
/// <see cref="ISpotTickerRestClient"/>. Both read Bitvavo's 24-hour ticker (<c>GET /v2/ticker/24h</c>). V2 answers with
/// <see cref="SharedTicker"/>, V1 with its subtype <see cref="SharedSpotTicker"/>; one mapping produces the spot ticker and V2 widens
/// it, so the two can never disagree. The V1 members <c>GetSpotTickerOptions</c> and <c>GetSpotTickersOptions</c> are the V2 options
/// under their old names.
/// </summary>
internal sealed partial class BitvavoRestClientSpotSharedApi : IGetTickerRest, IGetAllTickersRest, ISpotTickerRestClient
{
    // ── one ticker ───────────────────────────────────────────────────────────────────────

    async Task<IExchangeCallResult<SharedTicker>> IGetTicker.GetTickerAsync(GetTickerRequest request, CancellationToken ct)
        => await ((IGetTickerRest)this).GetTickerAsync(request, ct).ConfigureAwait(false);

    async Task<HttpResult<SharedTicker>> IGetTickerRest.GetTickerAsync(GetTickerRequest request, CancellationToken ct)
    {
        var result = await GetSpotTickerAsync(request, ct).ConfigureAwait(false);
        if (!result.Success)
        {
            return HttpResult.Fail<SharedTicker>(result);
        }

        return HttpResult.Ok<SharedTicker>(result, result.Data);
    }

    GetTickerOptions ISpotTickerRestClient.GetSpotTickerOptions => GetTickerOptions;

    /// <inheritdoc />
    public GetTickerOptions GetTickerOptions { get; } = new GetTickerOptions(_exchangeName, SharedTickerType.Day24H);

    /// <inheritdoc />
    public async Task<HttpResult<SharedSpotTicker>> GetSpotTickerAsync(GetTickerRequest request, CancellationToken ct = default)
    {
        var validationError = GetTickerOptions.ValidateRequest(request, this);
        if (validationError != null)
        {
            return HttpResult.Fail<SharedSpotTicker>(Exchange, validationError);
        }

        var sharedSymbol = request.Symbol!;
        var market = sharedSymbol.GetSymbol(FormatSymbol);
        var result = await _api.ExchangeData.GetTicker24hAsync(market, ct).ConfigureAwait(false);
        if (!result.Success)
        {
            return HttpResult.Fail<SharedSpotTicker>(result);
        }

        // The market filter is the server's job; matching here as well means a longer list can never pick the wrong market.
        var ticker = result.Data.FirstOrDefault(x => string.Equals(x.Market, market, StringComparison.OrdinalIgnoreCase));
        if (ticker == null)
        {
            return HttpResult.Fail<SharedSpotTicker>(result, new ServerError(new ErrorInfo(ErrorType.UnknownSymbol, "Ticker not found")));
        }

        return HttpResult.Ok(result, ticker.ToSharedSpotTicker(sharedSymbol));
    }

    // ── all tickers ──────────────────────────────────────────────────────────────────────

    async Task<IExchangeCallResult<SharedTicker[]>> IGetAllTickers.GetAllTickersAsync(GetTickersRequest request, CancellationToken ct)
        => await ((IGetAllTickersRest)this).GetAllTickersAsync(request, ct).ConfigureAwait(false);

    async Task<HttpResult<SharedTicker[]>> IGetAllTickersRest.GetAllTickersAsync(GetTickersRequest request, CancellationToken ct)
    {
        var result = await GetAllSpotTickersAsync(request, ct).ConfigureAwait(false);
        if (!result.Success)
        {
            return HttpResult.Fail<SharedTicker[]>(result);
        }

        // A copy typed as the V2 ticker: a SharedSpotTicker[] handed out as a SharedTicker[] would reject other tickers stored into it.
        return HttpResult.Ok(result, result.Data.Cast<SharedTicker>().ToArray());
    }

    Task<HttpResult<SharedSpotTicker[]>> ISpotTickerRestClient.GetSpotTickersAsync(GetTickersRequest request, CancellationToken ct)
        => GetAllSpotTickersAsync(request, ct);

    GetAllTickersOptions ISpotTickerRestClient.GetSpotTickersOptions => GetAllTickersOptions;

    /// <summary>The ticker of every market costs 25 rate-limit weight points; the ticker of one market costs 1.</summary>
    public GetAllTickersOptions GetAllTickersOptions { get; } = new GetAllTickersOptions(_exchangeName, SharedTickerType.Day24H)
    {
        RequestNotes = "Reading every market's ticker costs 25 rate-limit weight points, a single market's ticker 1",
    };

    /// <summary>All tickers as spot tickers: the V2 <c>GetAllTickersAsync</c> and, under its old name, the V1 <c>GetSpotTickersAsync</c>.</summary>
    public async Task<HttpResult<SharedSpotTicker[]>> GetAllSpotTickersAsync(GetTickersRequest request, CancellationToken ct = default)
    {
        var validationError = GetAllTickersOptions.ValidateRequest(request, this);
        if (validationError != null)
        {
            return HttpResult.Fail<SharedSpotTicker[]>(Exchange, validationError);
        }

        var result = await _api.ExchangeData.GetTicker24hAsync(market: null, ct).ConfigureAwait(false);
        if (!result.Success)
        {
            return HttpResult.Fail<SharedSpotTicker[]>(result);
        }

        return HttpResult.Ok(result, result.Data.Select(x => x.ToSharedSpotTicker(x.Market.ToSharedSymbol())).ToArray());
    }
}
