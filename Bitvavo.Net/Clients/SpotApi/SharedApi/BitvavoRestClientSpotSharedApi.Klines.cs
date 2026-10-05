// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.SharedApis;

namespace Bitvavo.Net.Clients.SpotApi;

/// <summary>Kline capability: [V2] <see cref="IGetKlinesRest"/> and the legacy [V1] <see cref="IKlineRestClient"/> on one implementation.</summary>
internal sealed partial class BitvavoRestClientSpotSharedApi : IGetKlinesRest, IKlineRestClient
{
    private const int _klineLimit = 1440;

    async Task<IExchangeCallResult<SharedKline[]>> IGetKlines.GetKlinesAsync(GetKlinesRequest request, PageRequest? nextPageToken, CancellationToken ct)
        => await GetKlinesAsync(request, nextPageToken, ct).ConfigureAwait(false);

    /// <summary>
    /// Bitvavo serves up to 1440 candles per request, newest first, within an optional <c>start</c>/<c>end</c> window; paging goes
    /// backwards by moving <c>end</c>. <c>MaxTotalDataPoints</c> keeps the ceiling the legacy interface always advertised.
    /// </summary>
    public GetKlinesOptions GetKlinesOptions { get; } = new GetKlinesOptions(
        _exchangeName,
        supportsAscending: false,
        supportsDescending: true,
        timeFilterSupported: true,
        maxLimit: _klineLimit,
        needsAuthentication: false,
        BitvavoSharedMappingExtensions.SupportedKlineIntervals)
    {
        MaxTotalDataPoints = _klineLimit,
    };

    /// <inheritdoc />
    public async Task<HttpResult<SharedKline[]>> GetKlinesAsync(GetKlinesRequest request, PageRequest? nextPageToken = null, CancellationToken ct = default)
    {
        var validationError = GetKlinesOptions.ValidateRequest(request, this);
        if (validationError != null)
        {
            return HttpResult.Fail<SharedKline[]>(Exchange, validationError);
        }

        var limit = request.Limit ?? _klineLimit;
        var pageParams = Pagination.GetPaginationParameters(DataDirection.Descending, limit, request.StartTime, request.EndTime ?? DateTime.UtcNow, nextPageToken);

        // An end time the caller never set is not invented: the newest candles are what a request without one asks for.
        var endTime = nextPageToken?.EndTime ?? request.EndTime;
        var symbol = request.Symbol!.GetSymbol(FormatSymbol);
        var result = await _api.ExchangeData.GetKlinesAsync(
            symbol,
            request.Interval.ToBitvavoInterval(),
            pageParams.Limit,
            pageParams.StartTime,
            endTime,
            ct).ConfigureAwait(false);
        if (!result.Success)
        {
            return HttpResult.Fail<SharedKline[]>(result);
        }

        var klines = result.Data.ToArray();
        var nextPageRequest = klines.Length == 0
            ? null
            : Pagination.GetNextPageRequestKlines(
                () => Pagination.NextPageFromTimeKlines(DataDirection.Descending, request, klines.Min(x => x.OpenTime), limit),
                klines.Length,
                klines.Select(x => x.OpenTime),
                request.StartTime,
                request.EndTime ?? DateTime.UtcNow,
                pageParams,
                request.Interval);

        return HttpResult.Ok(
            result,
            klines.Select(x => x.ToSharedKline(request.Symbol, symbol)).ToArray(),
            nextPageRequest);
    }
}
