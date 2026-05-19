// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bitvavo.Net.Objects.Models.Spot;
using CryptoExchange.Net.Objects;

namespace Bitvavo.Net.Interfaces.Clients.SpotApi;

/// <summary>
/// Bitvavo signed account-data REST endpoints — fee schedule, balances, capabilities.
/// Requires API credentials (HMAC-SHA256). Mirrors KrakenRestClientSpotApi.Account.
/// </summary>
public interface IBitvavoRestClientSpotApiAccount
{
    /// <summary>
    /// Get the active fee tier and the capabilities granted to the API key.
    /// <para><a href="https://docs.bitvavo.com/docs/rest-api/get-account-balance">Bitvavo API docs</a></para>
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    Task<WebCallResult<BitvavoAccountInfo>> GetAccountInfoAsync(CancellationToken ct = default);

    /// <summary>
    /// Get the balances of all assets, optionally filtered to a single symbol.
    /// <para><a href="https://docs.bitvavo.com/docs/rest-api/get-account-balance">Bitvavo API docs</a></para>
    /// </summary>
    /// <param name="symbol">Optional asset symbol (e.g. <c>"BTC"</c>) to filter the result. Null returns all assets.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<WebCallResult<IEnumerable<BitvavoBalance>>> GetBalancesAsync(string? symbol = null, CancellationToken ct = default);

    /// <summary>
    /// Get the market-specific fee schedule for the active 30-day-volume tier.
    /// <para><a href="https://docs.bitvavo.com/docs/rest-api/get-account-fees">Bitvavo API docs</a></para>
    /// </summary>
    /// <param name="market">Optional market identifier (e.g. <c>"ETH-EUR"</c>) to scope the fee response.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<WebCallResult<BitvavoMarketFee>> GetTradingFeesAsync(string? market = null, CancellationToken ct = default);

    /// <summary>
    /// Set or refresh the server-side cancel-on-disconnect deadline for orders tagged with
    /// <paramref name="codGroupId"/>. The Bitvavo broker cancels every open order in the group
    /// when no further <c>POST /v2/cancelOrdersAfter</c> lands before
    /// <paramref name="expiryAfterSeconds"/>. Fase 1 dead-man-switch primitive — replaces an
    /// ill-conceived per-order REST-polling bandaid that the entry-council M7 inspector verified
    /// is not a real Bitvavo endpoint.
    /// </summary>
    Task<WebCallResult<BitvavoCancelOrdersAfter>> ResetCancelOnDisconnectAsync(
        string codGroupId, int expiryAfterSeconds, CancellationToken ct = default);
}
