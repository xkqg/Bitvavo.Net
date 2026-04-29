// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using Bitvavo.Net.Interfaces.Clients.SpotApi;
using CryptoExchange.Net.Interfaces.Clients;

namespace Bitvavo.Net.Interfaces.Clients;

/// <summary>
/// Top-level Bitvavo REST client. Mirrors <c>IKrakenRestClient</c> / <c>IBinanceRestClient</c>
/// — single composition root with one accessor per market segment. Bitvavo only offers
/// spot trading, so there's only <see cref="SpotApi"/>.
/// </summary>
public interface IBitvavoRestClient : IRestClient
{
    /// <summary>Spot REST API endpoints.</summary>
    IBitvavoRestClientSpotApi SpotApi { get; }
}
