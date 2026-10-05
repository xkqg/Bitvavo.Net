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
/// Asset capabilities: [V2] <see cref="IGetAssetRest"/> and <see cref="IGetAllAssetsRest"/>, plus the legacy [V1]
/// <see cref="IAssetsRestClient"/>, whose <c>GetAssetsAsync</c> is the V2 <c>GetAllAssetsAsync</c> under its old name.
/// </summary>
internal sealed partial class BitvavoRestClientSpotSharedApi : IGetAssetRest, IGetAllAssetsRest, IAssetsRestClient
{
    // ── one asset ────────────────────────────────────────────────────────────────────────

    async Task<IExchangeCallResult<SharedAsset>> IGetAsset.GetAssetAsync(GetAssetRequest request, CancellationToken ct)
        => await GetAssetAsync(request, ct).ConfigureAwait(false);

    /// <inheritdoc />
    public GetAssetOptions GetAssetOptions { get; } = new GetAssetOptions(_exchangeName, false);

    /// <inheritdoc />
    public async Task<HttpResult<SharedAsset>> GetAssetAsync(GetAssetRequest request, CancellationToken ct = default)
    {
        var validationError = GetAssetOptions.ValidateRequest(request, this);
        if (validationError != null)
        {
            return HttpResult.Fail<SharedAsset>(Exchange, validationError);
        }

        var result = await _api.ExchangeData.GetAssetsAsync(request.Asset, ct).ConfigureAwait(false);
        if (!result.Success)
        {
            return HttpResult.Fail<SharedAsset>(result);
        }

        // The symbol filter is the server's job; matching here as well means a full list can never throw or pick the wrong asset.
        var asset = result.Data.FirstOrDefault(x => string.Equals(x.Symbol, request.Asset, StringComparison.OrdinalIgnoreCase));
        if (asset == null)
        {
            return HttpResult.Fail<SharedAsset>(result, new ServerError(new ErrorInfo(ErrorType.UnknownAsset, "Asset not found")));
        }

        return HttpResult.Ok(result, asset.ToSharedAsset());
    }

    // ── all assets ───────────────────────────────────────────────────────────────────────

    async Task<IExchangeCallResult<SharedAsset[]>> IGetAllAssets.GetAllAssetsAsync(GetAssetsRequest request, CancellationToken ct)
        => await GetAllAssetsAsync(request, ct).ConfigureAwait(false);

    Task<HttpResult<SharedAsset[]>> IAssetsRestClient.GetAssetsAsync(GetAssetsRequest request, CancellationToken ct)
        => GetAllAssetsAsync(request, ct);

    GetAllAssetsOptions IAssetsRestClient.GetAssetsOptions => GetAllAssetsOptions;

    /// <inheritdoc />
    public GetAllAssetsOptions GetAllAssetsOptions { get; } = new GetAllAssetsOptions(_exchangeName, false);

    /// <inheritdoc />
    public async Task<HttpResult<SharedAsset[]>> GetAllAssetsAsync(GetAssetsRequest request, CancellationToken ct = default)
    {
        var validationError = GetAllAssetsOptions.ValidateRequest(request, this);
        if (validationError != null)
        {
            return HttpResult.Fail<SharedAsset[]>(Exchange, validationError);
        }

        var result = await _api.ExchangeData.GetAssetsAsync(symbol: null, ct).ConfigureAwait(false);
        if (!result.Success)
        {
            return HttpResult.Fail<SharedAsset[]>(result);
        }

        return HttpResult.Ok(result, result.Data.Select(x => x.ToSharedAsset()).ToArray());
    }
}
