// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using Bitvavo.Net.Interfaces.Clients;
using Bitvavo.Net.Interfaces.Clients.SpotApi;
using Bitvavo.Net.Objects.Options;
using CryptoExchange.Net.SharedApis;
using Microsoft.Extensions.Options;

namespace Bitvavo.Net.Clients;

/// <inheritdoc cref="IBitvavoSharedApiClient" />
public sealed class BitvavoSharedApiClient : SharedApiClientBase, IBitvavoSharedApiClient
{
    /// <inheritdoc />
    public IBitvavoRestClientSpotSharedApi SpotRest { get; }

    /// <inheritdoc />
    public IBitvavoSocketClientSpotSharedApi SpotSocket { get; }

    /// <summary>Construct from the two clients; the transport preference comes from <see cref="BitvavoOptions.SharedApi"/>.</summary>
    /// <param name="restClient">The REST client whose spot Shared API is exposed.</param>
    /// <param name="socketClient">The WebSocket client whose spot Shared API is exposed.</param>
    /// <param name="options">The library options carrying the Shared API options.</param>
    public BitvavoSharedApiClient(IBitvavoRestClient restClient, IBitvavoSocketClient socketClient, IOptions<BitvavoOptions> options)
        : base(options.Value.SharedApi.PreferredTransport, restClient.SpotApi.SharedApi, socketClient.SpotApi.SharedApi)
    {
        SpotRest = restClient.SpotApi.SharedApi;
        SpotSocket = socketClient.SpotApi.SharedApi;
    }
}
