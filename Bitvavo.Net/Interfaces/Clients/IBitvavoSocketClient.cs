// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using Bitvavo.Net.Interfaces.Clients.SpotApi;
using CryptoExchange.Net.Interfaces.Clients;

namespace Bitvavo.Net.Interfaces.Clients;

/// <summary>
/// Top-level Bitvavo WebSocket client. Bitvavo only offers spot trading, so there's only
/// <see cref="SpotApi"/>. Mirrors <c>IKrakenSocketClient</c> / <c>IBinanceSocketClient</c>: the credentials can be changed
/// through the interface (<c>SetApiCredentials</c>), and a private subscription made afterwards authenticates with the new key.
/// </summary>
public interface IBitvavoSocketClient : ISocketClient<BitvavoCredentials>
{
    /// <summary>Spot WebSocket API endpoints.</summary>
    IBitvavoSocketClientSpotApi SpotApi { get; }
}
