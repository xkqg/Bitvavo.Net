// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Bitvavo.Net.Objects.Models.Spot;
using CryptoExchange.Net.Objects;

namespace Bitvavo.Net.Interfaces.Clients.SpotApi;

/// <summary>
/// Bitvavo signed trading REST endpoints — place / update / cancel / inspect orders +
/// retrieve own-trade history. Requires API credentials with the trading capability.
/// Mirrors KrakenRestClientSpotApi.Trading / BinanceRestClientSpotApiTrading.
/// </summary>
public interface IBitvavoRestClientSpotApiTrading
{
    /// <summary>
    /// Place a new order.
    /// <para><a href="https://docs.bitvavo.com/docs/rest-api/create-order">Bitvavo API docs</a></para>
    /// </summary>
    /// <param name="request">Strongly-typed order parameters (market, side, type, quantities, triggers, …).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<WebCallResult<BitvavoOrder>> PlaceOrderAsync(BitvavoPlaceOrderRequest request, CancellationToken ct = default);

    /// <summary>
    /// Update a resting limit / trigger order. Either <see cref="BitvavoUpdateOrderRequest.OrderId"/>
    /// or <see cref="BitvavoUpdateOrderRequest.ClientOrderId"/> must be set.
    /// <para><a href="https://docs.bitvavo.com/docs/rest-api/update-order">Bitvavo API docs</a></para>
    /// </summary>
    /// <param name="request">Strongly-typed update parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<WebCallResult<BitvavoOrder>> UpdateOrderAsync(BitvavoUpdateOrderRequest request, CancellationToken ct = default);

    /// <summary>
    /// Get the current state of a single order.
    /// <para><a href="https://docs.bitvavo.com/docs/rest-api/get-order">Bitvavo API docs</a></para>
    /// </summary>
    /// <param name="market">Market identifier (e.g. <c>"ETH-EUR"</c>). Required.</param>
    /// <param name="orderId">Server-assigned order id. Set this OR <paramref name="clientOrderId"/>.</param>
    /// <param name="clientOrderId">Client-assigned order id. Set this OR <paramref name="orderId"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<WebCallResult<BitvavoOrder>> GetOrderAsync(string market, string? orderId = null, string? clientOrderId = null, CancellationToken ct = default);

    /// <summary>
    /// Cancel a single order.
    /// <para><a href="https://docs.bitvavo.com/docs/rest-api/cancel-order">Bitvavo API docs</a></para>
    /// </summary>
    /// <param name="market">Market the order belongs to. Required.</param>
    /// <param name="operatorId">Account-scoped originator id. Required by Bitvavo on every order operation.</param>
    /// <param name="orderId">Server-assigned order id. Set this OR <paramref name="clientOrderId"/>.</param>
    /// <param name="clientOrderId">Client-assigned order id. Set this OR <paramref name="orderId"/>.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<WebCallResult<BitvavoOrderId>> CancelOrderAsync(string market, long operatorId, string? orderId = null, string? clientOrderId = null, CancellationToken ct = default);

    /// <summary>
    /// Cancel all open orders, optionally scoped to a single market.
    /// <para><a href="https://docs.bitvavo.com/docs/rest-api/cancel-orders">Bitvavo API docs</a></para>
    /// </summary>
    /// <param name="market">Optional market to scope the cancellation. Null cancels every open order on the account.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<WebCallResult<IEnumerable<BitvavoOrderId>>> CancelOrdersAsync(string? market = null, CancellationToken ct = default);

    /// <summary>
    /// List open orders.
    /// <para><a href="https://docs.bitvavo.com/docs/rest-api/get-open-orders">Bitvavo API docs</a></para>
    /// </summary>
    /// <param name="market">Optional market filter (e.g. <c>"ETH-EUR"</c>).</param>
    /// <param name="baseAsset">Optional base-asset filter (e.g. <c>"ETH"</c>) — returns open orders across every market for that base.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<WebCallResult<IEnumerable<BitvavoOrder>>> GetOpenOrdersAsync(string? market = null, string? baseAsset = null, CancellationToken ct = default);

    /// <summary>
    /// List historical orders (any state) for a single market.
    /// <para><a href="https://docs.bitvavo.com/docs/rest-api/get-orders">Bitvavo API docs</a></para>
    /// </summary>
    /// <param name="market">Market identifier. Required.</param>
    /// <param name="limit">Maximum entries (1–1000, default 500).</param>
    /// <param name="startTime">Inclusive UTC lower bound.</param>
    /// <param name="endTime">Inclusive UTC upper bound.</param>
    /// <param name="orderIdFrom">Pagination cursor — only return orders with id &gt;= this value.</param>
    /// <param name="orderIdTo">Pagination cursor — only return orders with id &lt;= this value.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<WebCallResult<IEnumerable<BitvavoOrder>>> GetOrderHistoryAsync(
        string market,
        int? limit = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        string? orderIdFrom = null,
        string? orderIdTo = null,
        CancellationToken ct = default);

    /// <summary>
    /// List the user's own trade fills for a single market.
    /// <para><a href="https://docs.bitvavo.com/docs/rest-api/get-trade-history">Bitvavo API docs</a></para>
    /// </summary>
    /// <param name="market">Market identifier. Required.</param>
    /// <param name="limit">Maximum entries (1–1000, default 500).</param>
    /// <param name="startTime">Inclusive UTC lower bound.</param>
    /// <param name="endTime">Inclusive UTC upper bound.</param>
    /// <param name="tradeIdFrom">Pagination cursor — only return fills with id &gt;= this value.</param>
    /// <param name="tradeIdTo">Pagination cursor — only return fills with id &lt;= this value.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<WebCallResult<IEnumerable<BitvavoFill>>> GetUserTradesAsync(
        string market,
        int? limit = null,
        DateTime? startTime = null,
        DateTime? endTime = null,
        string? tradeIdFrom = null,
        string? tradeIdTo = null,
        CancellationToken ct = default);
}
