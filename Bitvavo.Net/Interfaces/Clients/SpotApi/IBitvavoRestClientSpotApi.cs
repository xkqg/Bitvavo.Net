// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using CryptoExchange.Net.Interfaces.Clients;

namespace Bitvavo.Net.Interfaces.Clients.SpotApi;

/// <summary>
/// Bitvavo Spot REST API client surface. Public-data endpoints sit on
/// <see cref="ExchangeData"/>; signed endpoints sit on the peer accessors
/// <see cref="Account"/>, <see cref="Trading"/>, <see cref="Funding"/>,
/// <see cref="Report"/>, and <see cref="Institutional"/>
/// (mirroring KrakenRestClientSpotApi.Trading/Account).
/// </summary>
public interface IBitvavoRestClientSpotApi : IRestApiClient
{
    /// <summary>Public-data endpoints (markets, candles, ticker, orderbook).</summary>
    IBitvavoRestClientSpotApiExchangeData ExchangeData { get; }

    /// <summary>Signed account-data endpoints (account info, balances, fees, transaction history).</summary>
    IBitvavoRestClientSpotApiAccount Account { get; }

    /// <summary>Signed trading endpoints (place / update / cancel orders + own-trade history).</summary>
    IBitvavoRestClientSpotApiTrading Trading { get; }

    /// <summary>Signed funding endpoints (deposit address, deposit / withdrawal history).</summary>
    IBitvavoRestClientSpotApiFunding Funding { get; }

    /// <summary>MiCA regulatory-reporting endpoints (trade report, order-book report).</summary>
    IBitvavoRestClientSpotApiReport Report { get; }

    /// <summary>Institutional endpoints (subaccount management, asset transfers, per-subaccount queries).</summary>
    IBitvavoRestClientSpotApiInstitutional Institutional { get; }

    /// <summary>
    /// The exchange-agnostic CryptoExchange.Net Shared-API surface for Bitvavo Spot REST.
    /// Use this to program against the Shared abstractions common to every
    /// CryptoExchange.Net client library (mirrors <c>IKrakenRestClientSpotApi.SharedClient</c>).
    /// </summary>
    IBitvavoRestClientSpotApiShared SharedClient { get; }
}
