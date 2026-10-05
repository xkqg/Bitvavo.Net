// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using Bitvavo.Net.Interfaces.Clients.SpotApi;
using CryptoExchange.Net.SharedApis;

namespace Bitvavo.Net.Interfaces.Clients;

/// <summary>
/// The Shared API of Bitvavo as one client: the REST and the WebSocket Shared API next to each other, so a caller asks for a
/// capability (<c>GetCapability&lt;IGetKlines&gt;()</c>) and the transport preference decides which of them answers when both can.
/// Resolve it from the container after <c>AddBitvavo()</c>.
/// </summary>
public interface IBitvavoSharedApiClient : ISharedApiClientBase
{
    /// <summary>The Shared API of the spot REST client.</summary>
    IBitvavoRestClientSpotSharedApi SpotRest { get; }

    /// <summary>The Shared API of the spot WebSocket client.</summary>
    IBitvavoSocketClientSpotSharedApi SpotSocket { get; }
}
