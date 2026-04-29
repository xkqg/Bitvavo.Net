// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using Bitvavo.Net.Interfaces.Clients.SpotApi;
using CryptoExchange.Net.Interfaces.Clients;

namespace Bitvavo.Net.Interfaces.Clients;

/// <summary>
/// Top-level Bitvavo WebSocket client. Bitvavo only offers spot trading, so there's only
/// <see cref="SpotApi"/>. Mirrors <c>IKrakenSocketClient</c> / <c>IBinanceSocketClient</c>.
/// </summary>
public interface IBitvavoSocketClient : ISocketClient
{
    /// <summary>Spot WebSocket API endpoints.</summary>
    IBitvavoSocketClientSpotApi SpotApi { get; }
}
