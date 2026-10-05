// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CryptoExchange.Net;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.SharedApis;

namespace Bitvavo.Net.Clients.SpotApi;

/// <summary>
/// Spot symbol capability: [V2] <see cref="IGetSpotSymbolsRest"/> and the legacy [V1] <see cref="ISpotSymbolRestClient"/> on one
/// implementation. Bitvavo's market list is the symbol catalog. Reading it also fills the process-wide
/// <see cref="ExchangeSymbolCache"/> of this API's environment, which backs <see cref="SpotSymbolCatalog"/> and the legacy
/// <c>GetSpotSymbolsForBaseAssetAsync</c> / <c>SupportsSpotSymbolAsync</c> lookups: they read the markets once and answer from the cache.
/// </summary>
internal sealed partial class BitvavoRestClientSpotSharedApi : IGetSpotSymbolsRest, ISpotSymbolRestClient
{
    /// <inheritdoc />
    public SharedSymbolCatalog? SpotSymbolCatalog
        => ExchangeSymbolCache.GetSymbolCatalog(_exchangeName, _topicId, _api.EnvironmentName, null);

    async Task<IExchangeCallResult<SharedSpotSymbol[]>> IGetSpotSymbols.GetSpotSymbolsAsync(GetSymbolsRequest request, CancellationToken ct)
        => await GetSpotSymbolsAsync(request, ct).ConfigureAwait(false);

    /// <inheritdoc />
    public GetSpotSymbolsOptions GetSpotSymbolsOptions { get; } = new GetSpotSymbolsOptions(_exchangeName, false)
    {
        RequestNotes = "Reads every Bitvavo market once; the answer also fills the symbol cache behind SpotSymbolCatalog and the legacy symbol lookups",
    };

    /// <inheritdoc />
    public async Task<HttpResult<SharedSpotSymbol[]>> GetSpotSymbolsAsync(GetSymbolsRequest request, CancellationToken ct = default)
    {
        var validationError = GetSpotSymbolsOptions.ValidateRequest(request, this);
        if (validationError != null)
        {
            return HttpResult.Fail<SharedSpotSymbol[]>(Exchange, validationError);
        }

        var result = await _api.ExchangeData.GetMarketsAsync(ct).ConfigureAwait(false);
        if (!result.Success)
        {
            return HttpResult.Fail<SharedSpotSymbol[]>(result);
        }

        // A market is its name, and the cache refuses a list that names one twice: each name counts once.
        var symbols = result.Data.Select(market => market.ToSharedSpotSymbol()).DistinctBy(symbol => symbol.Name).ToArray();
        ExchangeSymbolCache.UpdateSymbolInfo(_topicId, _api.EnvironmentName, null, symbols);
        return HttpResult.Ok(result, SharedUtils.ApplySymbolFilter(symbols, request));
    }

    /// <inheritdoc />
    public async Task<ExchangeCallResult<SharedSymbol[]>> GetSpotSymbolsForBaseAssetAsync(string baseAsset)
    {
        var error = await SpotSymbolsEnsureCachedAsync().ConfigureAwait(false);
        return error != null
            ? ExchangeCallResult<SharedSymbol[]>.Fail(Exchange, error)
            : ExchangeCallResult<SharedSymbol[]>.Ok(Exchange, ExchangeSymbolCache.GetSymbolsForBaseAsset(_topicId, _api.EnvironmentName, null, baseAsset));
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">The symbol is not a spot symbol: Bitvavo has no other market type.</exception>
    public async Task<ExchangeCallResult<bool>> SupportsSpotSymbolAsync(SharedSymbol symbol)
    {
        if (symbol.TradingMode != TradingMode.Spot)
        {
            throw new ArgumentException("Only spot symbols can be checked: Bitvavo has no other market type", nameof(symbol));
        }

        var error = await SpotSymbolsEnsureCachedAsync().ConfigureAwait(false);
        return error != null
            ? ExchangeCallResult<bool>.Fail(Exchange, error)
            : ExchangeCallResult<bool>.Ok(Exchange, ExchangeSymbolCache.SupportsSymbol(_topicId, _api.EnvironmentName, null, symbol));
    }

    /// <inheritdoc />
    public async Task<ExchangeCallResult<bool>> SupportsSpotSymbolAsync(string symbolName)
    {
        var error = await SpotSymbolsEnsureCachedAsync().ConfigureAwait(false);
        return error != null
            ? ExchangeCallResult<bool>.Fail(Exchange, error)
            : ExchangeCallResult<bool>.Ok(Exchange, ExchangeSymbolCache.SupportsSymbol(_topicId, _api.EnvironmentName, null, symbolName));
    }

    /// <summary>
    /// Reads the markets into the symbol cache when this API's environment has none yet; a second call, or a second client of the
    /// same environment, finds the cache filled.
    /// </summary>
    /// <returns>The error when the markets could not be read; null when the cache is filled.</returns>
    private async Task<Error?> SpotSymbolsEnsureCachedAsync()
    {
        if (ExchangeSymbolCache.HasCached(_topicId, _api.EnvironmentName, null))
        {
            return null;
        }

        var symbols = await GetSpotSymbolsAsync(new GetSymbolsRequest()).ConfigureAwait(false);
        return symbols.Error;
    }
}
