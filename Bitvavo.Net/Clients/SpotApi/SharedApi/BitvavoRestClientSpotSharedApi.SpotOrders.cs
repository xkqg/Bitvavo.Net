// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bitvavo.Net.Objects.Models.Spot;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.SharedApis;

namespace Bitvavo.Net.Clients.SpotApi;

/// <summary>
/// Spot order capabilities, all signed. [V2]: <see cref="IPlaceSpotOrderRest"/>, <see cref="IGetSpotOrderRest"/>,
/// <see cref="IGetSpotOrderByClientOrderIdRest"/>, <see cref="IGetOpenSpotOrdersRest"/>, <see cref="IGetClosedSpotOrdersRest"/>,
/// <see cref="IGetSpotOrderTradesRest"/>, <see cref="IGetSpotUserTradeHistoryRest"/>, <see cref="ICancelSpotOrderRest"/> and
/// <see cref="ICancelSpotOrderByClientOrderIdRest"/>. [V1]: <see cref="ISpotOrderRestClient"/>, which needs all of them at once, and
/// <see cref="ISpotOrderClientIdRestClient"/>. Only the legacy <c>GetSpotUserTradesAsync</c> differs by name: it is the V2
/// <c>GetSpotUserTradeHistoryAsync</c>. Every order operation carries the <c>OperatorId</c> exchange parameter Bitvavo requires.
/// </summary>
internal sealed partial class BitvavoRestClientSpotSharedApi :
    IPlaceSpotOrderRest,
    IGetSpotOrderRest,
    IGetSpotOrderByClientOrderIdRest,
    IGetOpenSpotOrdersRest,
    IGetClosedSpotOrdersRest,
    IGetSpotOrderTradesRest,
    IGetSpotUserTradeHistoryRest,
    ICancelSpotOrderRest,
    ICancelSpotOrderByClientOrderIdRest,
    ISpotOrderRestClient,
    ISpotOrderClientIdRestClient
{
    /// <summary>The page size Bitvavo applies to the order history and the trade history when a request names none.</summary>
    private const int _spotOrdersDefaultLimit = 500;

    /// <summary>The most orders or trades one request can return.</summary>
    private const int _spotOrdersMaxLimit = 1000;

    // ── place ────────────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public SharedFeeDeductionType SpotFeeDeductionType => SharedFeeDeductionType.DeductFromOutput;

    /// <inheritdoc />
    public SharedFeeAssetType SpotFeeAssetType => SharedFeeAssetType.QuoteAsset;

    /// <inheritdoc />
    public SharedOrderType[] SpotSupportedOrderTypes { get; } = [SharedOrderType.Limit, SharedOrderType.Market, SharedOrderType.LimitMaker];

    /// <inheritdoc />
    public SharedTimeInForce[] SpotSupportedTimeInForce { get; } = [SharedTimeInForce.GoodTillCanceled, SharedTimeInForce.ImmediateOrCancel, SharedTimeInForce.FillOrKill];

    /// <inheritdoc />
    public SharedQuantitySupport SpotSupportedOrderQuantity { get; } = new(
        SharedQuantityType.BaseAsset,
        SharedQuantityType.BaseAsset,
        SharedQuantityType.BaseAndQuoteAsset,
        SharedQuantityType.BaseAndQuoteAsset);

    /// <summary>Bitvavo only accepts a client order id in UUID format.</summary>
    public string GenerateClientOrderId() => Guid.NewGuid().ToString();

    async Task<IExchangeCallResult<SharedId>> IPlaceSpotOrder.PlaceSpotOrderAsync(PlaceSpotOrderRequest request, CancellationToken ct)
        => await PlaceSpotOrderAsync(request, ct).ConfigureAwait(false);

    /// <summary>
    /// A limit order, a post-only limit order (<see cref="SharedOrderType.LimitMaker"/>) or a market order; a market order may be
    /// sized in the base or the quote asset. The stop-loss and take-profit family goes through the trigger-order capabilities.
    /// </summary>
    public PlaceSpotOrderOptions PlaceSpotOrderOptions { get; } = new PlaceSpotOrderOptions(_exchangeName)
    {
        ExchangeParameterRules = [BitvavoSharedParameters.OperatorIdRule],
        RequestNotes = "Bitvavo requires an operator id on every order operation (the OperatorId exchange parameter) and a client order id in UUID format (GenerateClientOrderId makes one). Stop-loss and take-profit orders are placed through the trigger-order capabilities, not here.",
    };

    /// <inheritdoc />
    public async Task<HttpResult<SharedId>> PlaceSpotOrderAsync(PlaceSpotOrderRequest request, CancellationToken ct = default)
    {
        var validationError = PlaceSpotOrderOptions.ValidateRequest(request, this);
        if (validationError != null)
        {
            return HttpResult.Fail<SharedId>(Exchange, validationError);
        }

        var result = await _api.Trading.PlaceOrderAsync(
            new BitvavoPlaceOrderRequest(
                request.Symbol!.GetSymbol(FormatSymbol),
                request.Side.ToBitvavoSide(),
                request.OrderType.ToBitvavoOrderType(),
                request.GetOperatorId(),
                Amount: request.Quantity?.QuantityInBaseAsset,
                AmountQuote: request.Quantity?.QuantityInQuoteAsset,
                Price: request.Price,
                TimeInForce: request.TimeInForce?.ToBitvavoTimeInForce(),
                // A limit-maker order is Bitvavo's limit order flagged post-only; no other order type sends the flag.
                PostOnly: request.OrderType == SharedOrderType.LimitMaker ? true : null,
                ClientOrderId: request.ClientOrderId),
            ct).ConfigureAwait(false);
        if (!result.Success)
        {
            return HttpResult.Fail<SharedId>(result);
        }

        return HttpResult.Ok(result, new SharedId(result.Data.OrderId));
    }

    // ── get one order ────────────────────────────────────────────────────────────────────

    async Task<IExchangeCallResult<SharedSpotOrder>> IGetSpotOrder.GetSpotOrderAsync(GetOrderRequest request, CancellationToken ct)
        => await GetSpotOrderAsync(request, ct).ConfigureAwait(false);

    /// <inheritdoc />
    public GetSpotOrderOptions GetSpotOrderOptions { get; } = new GetSpotOrderOptions(_exchangeName, true);

    /// <inheritdoc />
    public async Task<HttpResult<SharedSpotOrder>> GetSpotOrderAsync(GetOrderRequest request, CancellationToken ct = default)
    {
        var validationError = GetSpotOrderOptions.ValidateRequest(request, this);
        if (validationError != null)
        {
            return HttpResult.Fail<SharedSpotOrder>(Exchange, validationError);
        }

        // An unknown order is the server's error 240, which the error mapping already types as UnknownOrder.
        var result = await _api.Trading.GetOrderAsync(request.Symbol!.GetSymbol(FormatSymbol), orderId: request.OrderId, ct: ct).ConfigureAwait(false);
        if (!result.Success)
        {
            return HttpResult.Fail<SharedSpotOrder>(result);
        }

        return HttpResult.Ok(result, result.Data.ToSharedSpotOrder(request.Symbol));
    }

    // ── get one order by client order id ─────────────────────────────────────────────────

    async Task<IExchangeCallResult<SharedSpotOrder>> IGetSpotOrderByClientOrderId.GetSpotOrderByClientOrderIdAsync(GetOrderRequest request, CancellationToken ct)
        => await GetSpotOrderByClientOrderIdAsync(request, ct).ConfigureAwait(false);

    /// <inheritdoc />
    public GetSpotOrderByClientOrderIdOptions GetSpotOrderByClientOrderIdOptions { get; } = new GetSpotOrderByClientOrderIdOptions(_exchangeName, true);

    /// <inheritdoc />
    public async Task<HttpResult<SharedSpotOrder>> GetSpotOrderByClientOrderIdAsync(GetOrderRequest request, CancellationToken ct = default)
    {
        var validationError = GetSpotOrderByClientOrderIdOptions.ValidateRequest(request, this);
        if (validationError != null)
        {
            return HttpResult.Fail<SharedSpotOrder>(Exchange, validationError);
        }

        var result = await _api.Trading.GetOrderAsync(request.Symbol!.GetSymbol(FormatSymbol), clientOrderId: request.OrderId, ct: ct).ConfigureAwait(false);
        if (!result.Success)
        {
            return HttpResult.Fail<SharedSpotOrder>(result);
        }

        return HttpResult.Ok(result, result.Data.ToSharedSpotOrder(request.Symbol));
    }

    // ── open orders ──────────────────────────────────────────────────────────────────────

    async Task<IExchangeCallResult<SharedSpotOrder[]>> IGetOpenSpotOrders.GetOpenSpotOrdersAsync(GetOpenOrdersRequest request, CancellationToken ct)
        => await GetOpenSpotOrdersAsync(request, ct).ConfigureAwait(false);

    /// <summary>The symbol is optional: Bitvavo lists the open orders of every market when none is named.</summary>
    public GetOpenSpotOrdersOptions GetOpenSpotOrdersOptions { get; } = new GetOpenSpotOrdersOptions(_exchangeName, true)
    {
        RequestNotes = "Without a symbol Bitvavo lists the open orders of every market, which costs 100 rate-limit weight points instead of 5.",
    };

    /// <inheritdoc />
    public async Task<HttpResult<SharedSpotOrder[]>> GetOpenSpotOrdersAsync(GetOpenOrdersRequest request, CancellationToken ct = default)
    {
        var validationError = GetOpenSpotOrdersOptions.ValidateRequest(request, this);
        if (validationError != null)
        {
            return HttpResult.Fail<SharedSpotOrder[]>(Exchange, validationError);
        }

        var result = await _api.Trading.GetOpenOrdersAsync(request.Symbol?.GetSymbol(FormatSymbol), ct: ct).ConfigureAwait(false);
        if (!result.Success)
        {
            return HttpResult.Fail<SharedSpotOrder[]>(result);
        }

        // Every order names its own market, and its Shared symbol is derived from that: the request may name no symbol at all.
        return HttpResult.Ok(result, result.Data.Select(x => x.ToSharedSpotOrder()).ToArray());
    }

    // ── closed orders ────────────────────────────────────────────────────────────────────

    async Task<IExchangeCallResult<SharedSpotOrder[]>> IGetClosedSpotOrders.GetClosedSpotOrdersAsync(GetClosedOrdersRequest request, PageRequest? nextPageToken, CancellationToken ct)
        => await GetClosedSpotOrdersAsync(request, nextPageToken, ct).ConfigureAwait(false);

    /// <summary>
    /// Bitvavo's order history of one market within an optional <c>start</c>/<c>end</c> window of up to 1000 orders; it has no
    /// cursor, so paging goes backwards by moving <c>end</c> to just before the oldest creation time. Bitvavo does not document the
    /// order of this list: it is treated as newest first, like its trade history (unverified).
    /// </summary>
    public GetSpotClosedOrdersOptions GetClosedSpotOrdersOptions { get; } = new GetSpotClosedOrdersOptions(
        _exchangeName,
        supportsAscending: false,
        supportsDescending: true,
        timeFilterSupported: true,
        maxLimit: _spotOrdersMaxLimit)
    {
        RequestNotes = "Bitvavo's order history of a market also lists orders that are still open. Those are dropped here, so a page can hold fewer orders than the limit while a next page still exists. Orders are paged by their creation time.",
    };

    /// <inheritdoc />
    public async Task<HttpResult<SharedSpotOrder[]>> GetClosedSpotOrdersAsync(GetClosedOrdersRequest request, PageRequest? nextPageToken = null, CancellationToken ct = default)
    {
        var validationError = GetClosedSpotOrdersOptions.ValidateRequest(request, this);
        if (validationError != null)
        {
            return HttpResult.Fail<SharedSpotOrder[]>(Exchange, validationError);
        }

        var limit = request.Limit ?? _spotOrdersDefaultLimit;
        var pageParams = Pagination.GetPaginationParameters(DataDirection.Descending, limit, request.StartTime, request.EndTime ?? DateTime.UtcNow, nextPageToken);

        // An end time the caller never set is not invented: the newest orders are what a request without one asks for.
        var endTime = nextPageToken?.EndTime ?? request.EndTime;
        var result = await _api.Trading.GetOrderHistoryAsync(
            request.Symbol!.GetSymbol(FormatSymbol),
            pageParams.Limit,
            pageParams.StartTime,
            endTime,
            ct: ct).ConfigureAwait(false);
        if (!result.Success)
        {
            return HttpResult.Fail<SharedSpotOrder[]>(result);
        }

        // The page is counted before the open orders are dropped: it is the server's page that was full, not its closed part.
        var orders = result.Data.ToArray();
        var nextPageRequest = orders.Length == 0
            ? null
            : Pagination.GetNextPageRequest(
                () => Pagination.NextPageFromTime(pageParams, orders.Min(x => x.Created)),
                orders.Length,
                orders.Select(x => x.Created),
                request.StartTime,
                request.EndTime ?? DateTime.UtcNow,
                pageParams);

        return HttpResult.Ok(
            result,
            orders.Where(x => x.Status.ToSharedStatus() != SharedOrderStatus.Open).Select(x => x.ToSharedSpotOrder(request.Symbol)).ToArray(),
            nextPageRequest);
    }

    // ── the trades of one order ──────────────────────────────────────────────────────────

    async Task<IExchangeCallResult<SharedUserTrade[]>> IGetSpotOrderTrades.GetSpotOrderTradesAsync(GetOrderTradesRequest request, CancellationToken ct)
        => await GetSpotOrderTradesAsync(request, ct).ConfigureAwait(false);

    /// <inheritdoc />
    public GetSpotOrderTradesOptions GetSpotOrderTradesOptions { get; } = new GetSpotOrderTradesOptions(_exchangeName, true)
    {
        RequestNotes = "Bitvavo has no endpoint for the trades of one order: the trades are the fills embedded in the order, so they are available for an order of any age.",
    };

    /// <inheritdoc />
    public async Task<HttpResult<SharedUserTrade[]>> GetSpotOrderTradesAsync(GetOrderTradesRequest request, CancellationToken ct = default)
    {
        var validationError = GetSpotOrderTradesOptions.ValidateRequest(request, this);
        if (validationError != null)
        {
            return HttpResult.Fail<SharedUserTrade[]>(Exchange, validationError);
        }

        var result = await _api.Trading.GetOrderAsync(request.Symbol!.GetSymbol(FormatSymbol), orderId: request.OrderId, ct: ct).ConfigureAwait(false);
        if (!result.Success)
        {
            return HttpResult.Fail<SharedUserTrade[]>(result);
        }

        var order = result.Data;
        return HttpResult.Ok(result, order.Fills.Select(x => x.ToSharedUserTrade(order, request.Symbol)).ToArray());
    }

    // ── user trade history ───────────────────────────────────────────────────────────────

    async Task<IExchangeCallResult<SharedUserTrade[]>> IGetSpotUserTradeHistory.GetSpotUserTradeHistoryAsync(GetUserTradesRequest request, PageRequest? nextPageToken, CancellationToken ct)
        => await GetSpotUserTradeHistoryAsync(request, nextPageToken, ct).ConfigureAwait(false);

    Task<HttpResult<SharedUserTrade[]>> ISpotOrderRestClient.GetSpotUserTradesAsync(GetUserTradesRequest request, PageRequest? nextPageToken, CancellationToken ct)
        => GetSpotUserTradeHistoryAsync(request, nextPageToken, ct);

    GetSpotUserTradeHistoryOptions ISpotOrderRestClient.GetSpotUserTradesOptions => GetSpotUserTradeHistoryOptions;

    /// <summary>
    /// The account's own trades in one market, newest first and within an optional <c>start</c>/<c>end</c> window of up to 1000
    /// trades; paging goes backwards by moving <c>end</c> to just before the oldest trade.
    /// </summary>
    public GetSpotUserTradeHistoryOptions GetSpotUserTradeHistoryOptions { get; } = new GetSpotUserTradeHistoryOptions(
        _exchangeName,
        supportsAscending: false,
        supportsDescending: true,
        timeFilterSupported: true,
        maxLimit: _spotOrdersMaxLimit)
    {
        RequestNotes = "Bitvavo documents that the start and end time of this query must lie at most 24 hours apart. That is not checked here: the server answers a longer period with its own error.",
    };

    /// <inheritdoc />
    public async Task<HttpResult<SharedUserTrade[]>> GetSpotUserTradeHistoryAsync(GetUserTradesRequest request, PageRequest? nextPageToken = null, CancellationToken ct = default)
    {
        var validationError = GetSpotUserTradeHistoryOptions.ValidateRequest(request, this);
        if (validationError != null)
        {
            return HttpResult.Fail<SharedUserTrade[]>(Exchange, validationError);
        }

        var limit = request.Limit ?? _spotOrdersDefaultLimit;
        var pageParams = Pagination.GetPaginationParameters(DataDirection.Descending, limit, request.StartTime, request.EndTime ?? DateTime.UtcNow, nextPageToken);

        // An end time the caller never set is not invented: the newest trades are what a request without one asks for.
        var endTime = nextPageToken?.EndTime ?? request.EndTime;
        var market = request.Symbol!.GetSymbol(FormatSymbol);
        var result = await _api.Trading.GetUserTradesAsync(
            market,
            pageParams.Limit,
            pageParams.StartTime,
            endTime,
            ct: ct).ConfigureAwait(false);
        if (!result.Success)
        {
            return HttpResult.Fail<SharedUserTrade[]>(result);
        }

        var fills = result.Data.ToArray();
        var nextPageRequest = fills.Length == 0
            ? null
            : Pagination.GetNextPageRequest(
                () => Pagination.NextPageFromTime(pageParams, fills.Min(x => x.Timestamp)),
                fills.Length,
                fills.Select(x => x.Timestamp),
                request.StartTime,
                request.EndTime ?? DateTime.UtcNow,
                pageParams);

        return HttpResult.Ok(
            result,
            fills.Select(x => x.ToSharedUserTrade(market, request.Symbol)).ToArray(),
            nextPageRequest);
    }

    // ── cancel ───────────────────────────────────────────────────────────────────────────

    async Task<IExchangeCallResult<SharedId>> ICancelSpotOrder.CancelSpotOrderAsync(CancelOrderRequest request, CancellationToken ct)
        => await CancelSpotOrderAsync(request, ct).ConfigureAwait(false);

    /// <inheritdoc />
    public CancelSpotOrderOptions CancelSpotOrderOptions { get; } = new CancelSpotOrderOptions(_exchangeName, true)
    {
        ExchangeParameterRules = [BitvavoSharedParameters.OperatorIdRule],
    };

    /// <inheritdoc />
    public async Task<HttpResult<SharedId>> CancelSpotOrderAsync(CancelOrderRequest request, CancellationToken ct = default)
    {
        var validationError = CancelSpotOrderOptions.ValidateRequest(request, this);
        if (validationError != null)
        {
            return HttpResult.Fail<SharedId>(Exchange, validationError);
        }

        var result = await _api.Trading.CancelOrderAsync(request.Symbol!.GetSymbol(FormatSymbol), request.GetOperatorId(), orderId: request.OrderId, ct: ct).ConfigureAwait(false);
        if (!result.Success)
        {
            return HttpResult.Fail<SharedId>(result);
        }

        return HttpResult.Ok(result, new SharedId(result.Data.OrderId));
    }

    // ── cancel by client order id ────────────────────────────────────────────────────────

    async Task<IExchangeCallResult<SharedId>> ICancelSpotOrderByClientOrderId.CancelSpotOrderByClientOrderIdAsync(CancelOrderRequest request, CancellationToken ct)
        => await CancelSpotOrderByClientOrderIdAsync(request, ct).ConfigureAwait(false);

    /// <inheritdoc />
    public CancelSpotOrderByClientOrderIdOptions CancelSpotOrderByClientOrderIdOptions { get; } = new CancelSpotOrderByClientOrderIdOptions(_exchangeName, true)
    {
        ExchangeParameterRules = [BitvavoSharedParameters.OperatorIdRule],
    };

    /// <inheritdoc />
    public async Task<HttpResult<SharedId>> CancelSpotOrderByClientOrderIdAsync(CancelOrderRequest request, CancellationToken ct = default)
    {
        var validationError = CancelSpotOrderByClientOrderIdOptions.ValidateRequest(request, this);
        if (validationError != null)
        {
            return HttpResult.Fail<SharedId>(Exchange, validationError);
        }

        var result = await _api.Trading.CancelOrderAsync(request.Symbol!.GetSymbol(FormatSymbol), request.GetOperatorId(), clientOrderId: request.OrderId, ct: ct).ConfigureAwait(false);
        if (!result.Success)
        {
            return HttpResult.Fail<SharedId>(result);
        }

        return HttpResult.Ok(result, new SharedId(result.Data.OrderId));
    }
}
