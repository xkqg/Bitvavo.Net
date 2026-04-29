// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using CryptoExchange.Net.Interfaces.Clients;

namespace Bitvavo.Net.Interfaces.Clients.SpotApi;

/// <summary>
/// Bitvavo Spot REST API client surface. Public-data endpoints sit on
/// <see cref="ExchangeData"/>; signed endpoints sit on the peer accessors
/// <see cref="Account"/>, <see cref="Trading"/>, and <see cref="Funding"/>
/// (mirroring KrakenRestClientSpotApi.Trading/Account).
/// </summary>
public interface IBitvavoRestClientSpotApi : IRestApiClient
{
    /// <summary>Public-data endpoints (markets, candles, ticker, orderbook).</summary>
    IBitvavoRestClientSpotApiExchangeData ExchangeData { get; }

    /// <summary>Signed account-data endpoints (account info, balances, fees).</summary>
    IBitvavoRestClientSpotApiAccount Account { get; }

    /// <summary>Signed trading endpoints (place / update / cancel orders + own-trade history).</summary>
    IBitvavoRestClientSpotApiTrading Trading { get; }

    /// <summary>Signed funding endpoints (deposit address, deposit / withdrawal history).</summary>
    IBitvavoRestClientSpotApiFunding Funding { get; }
}
