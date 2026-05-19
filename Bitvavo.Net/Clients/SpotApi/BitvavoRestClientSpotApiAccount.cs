// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Bitvavo.Net.Interfaces.Clients.SpotApi;
using Bitvavo.Net.Objects.Models.Spot;
using CryptoExchange.Net.Objects;

namespace Bitvavo.Net.Clients.SpotApi;

/// <inheritdoc cref="IBitvavoRestClientSpotApiAccount" />
internal sealed class BitvavoRestClientSpotApiAccount : IBitvavoRestClientSpotApiAccount
{
    private static readonly RequestDefinitionCache _definitions = new();
    private readonly BitvavoRestClientSpotApi _baseClient;

    internal BitvavoRestClientSpotApiAccount(BitvavoRestClientSpotApi baseClient)
    {
        _baseClient = baseClient;
    }

    /// <inheritdoc />
    public Task<WebCallResult<BitvavoAccountInfo>> GetAccountInfoAsync(CancellationToken ct = default)
    {
        var request = _definitions.GetOrCreate(HttpMethod.Get, "v2/account", true);
        return _baseClient.SendAsync<BitvavoAccountInfo>(request, parameters: null, ct);
    }

    /// <inheritdoc />
    public Task<WebCallResult<IEnumerable<BitvavoBalance>>> GetBalancesAsync(string? symbol = null, CancellationToken ct = default)
    {
        var parameters = new ParameterCollection();
        parameters.AddOptional("symbol", symbol);

        var request = _definitions.GetOrCreate(HttpMethod.Get, "v2/balance", true);
        return _baseClient.SendAsync<IEnumerable<BitvavoBalance>>(request, parameters, ct);
    }

    /// <inheritdoc />
    public Task<WebCallResult<BitvavoMarketFee>> GetTradingFeesAsync(string? market = null, CancellationToken ct = default)
    {
        var parameters = new ParameterCollection();
        parameters.AddOptional("market", market);

        var request = _definitions.GetOrCreate(HttpMethod.Get, "v2/account/fees", true);
        return _baseClient.SendAsync<BitvavoMarketFee>(request, parameters, ct);
    }

    /// <inheritdoc />
    public Task<WebCallResult<BitvavoCancelOrdersAfter>> ResetCancelOnDisconnectAsync(
        string codGroupId, int expiryAfterSeconds, CancellationToken ct = default)
    {
        var parameters = new ParameterCollection();
        parameters.Add("codGroupId", codGroupId);
        parameters.Add("expiryAfterSeconds", expiryAfterSeconds);
        var request = _definitions.GetOrCreate(HttpMethod.Post, "v2/cancelOrdersAfter", true);
        return _baseClient.SendAsync<BitvavoCancelOrdersAfter>(request, parameters, ct);
    }
}
