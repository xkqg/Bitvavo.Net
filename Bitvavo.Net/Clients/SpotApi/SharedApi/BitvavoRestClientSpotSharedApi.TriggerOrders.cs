// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Errors;
using CryptoExchange.Net.SharedApis;

namespace Bitvavo.Net.Clients.SpotApi;

/// <summary>
/// Trigger-order capabilities, [V2] only (nothing in the legacy interfaces replaces them), all signed: <see cref="IPlaceSpotTriggerOrderRest"/>,
/// <see cref="IGetSpotTriggerOrderRest"/> and <see cref="ICancelSpotTriggerOrderRest"/>. Bitvavo has no trigger-order endpoint of its
/// own: a trigger order is an ordinary order of type <c>stopLoss</c>, <c>stopLossLimit</c>, <c>takeProfit</c> or <c>takeProfitLimit</c>
/// (placed with <c>POST /v2/order</c>, read with <c>GET /v2/order</c>, canceled with <c>DELETE /v2/order</c>) that waits in status
/// <c>awaitingTrigger</c>. The mappings are in <see cref="BitvavoSharedMappingExtensions"/>.
/// </summary>
internal sealed partial class BitvavoRestClientSpotSharedApi :
    IPlaceSpotTriggerOrderRest,
    IGetSpotTriggerOrderRest,
    ICancelSpotTriggerOrderRest
{
    // ── place ────────────────────────────────────────────────────────────────────────────

    async Task<IExchangeCallResult<SharedId>> IPlaceSpotTriggerOrder.PlaceSpotTriggerOrderAsync(PlaceSpotTriggerOrderRequest request, CancellationToken ct)
        => await PlaceSpotTriggerOrderAsync(request, ct).ConfigureAwait(false);

    /// <summary>
    /// A trigger order that waits for its trigger price and then places a market order or, when the request has a limit price, a limit
    /// order. <c>HoldsFunds</c> is true on the strength of Bitvavo's FIX documentation, which requires the reserved amount (<c>OnHold</c>)
    /// on the execution report of every active order, <c>awaitingTrigger</c> included; no recorded REST answer confirms it.
    /// </summary>
    public PlaceSpotTriggerOrderOptions PlaceSpotTriggerOrderOptions { get; } = new PlaceSpotTriggerOrderOptions(_exchangeName, holdsFunds: true)
    {
        ExchangeParameterRules = [BitvavoSharedParameters.OperatorIdRule, BitvavoSharedMappingExtensions.TriggerReferenceRule],
        ParameterRuleOverrides =
        [
            RequestParameterRuleOverride<PlaceSpotTriggerOrderRequest>.Required(
                x => x.PriceDirection,
                "The price movement that fires the trigger, relative to the trigger price. With the order side it decides the Bitvavo order type: sell + PriceBelow = stopLoss, sell + PriceAbove = takeProfit, buy + PriceAbove = stopLoss, buy + PriceBelow = takeProfit (a stop loss fires on the worse price, a take profit on the better one)"),
            RequestParameterRuleOverride<PlaceSpotTriggerOrderRequest>.Required(
                x => x.Quantity,
                "The quantity in the base asset (amount); a trigger order without a limit price (stopLoss, takeProfit) may give it in the quote asset (amountQuote) instead, never both"),
            RequestParameterRuleOverride<PlaceSpotTriggerOrderRequest>.Optional(
                x => x.OrderPrice,
                "The limit price: with it the trigger places a limit order (stopLossLimit, takeProfitLimit) when it fires, without it a market order (stopLoss, takeProfit)"),
            RequestParameterRuleOverride<PlaceSpotTriggerOrderRequest>.Optional(
                x => x.TimeInForce,
                "Applies to a trigger order with a limit price; a trigger order without one places a market order, which executes at once, so none is sent"),
            RequestParameterRuleOverride<PlaceSpotTriggerOrderRequest>.Optional(
                x => x.ClientOrderId,
                "Your identifier of the order; Bitvavo takes it in UUID format and wants it unique across open orders"),
        ],
        RequestNotes = "Bitvavo has no separate trigger-order endpoint: a trigger order is an ordinary order (POST /v2/order, rate-limit weight 1) of type stopLoss, stopLossLimit, takeProfit or takeProfitLimit with triggerType price. "
            + "The type follows from the order side, the price direction and whether a limit price is given: sell + PriceBelow = stopLoss, sell + PriceAbove = takeProfit, buy + PriceAbove = stopLoss, buy + PriceBelow = takeProfit; with a limit price the order is a stopLossLimit or takeProfitLimit, without one a stopLoss or takeProfit that places a market order. "
            + "The trigger fires when the trigger reference price is at or beyond the trigger price in the chosen direction: the last trade by default, the best bid, the best ask or the mid price through the optional TriggerReference exchange parameter (a TriggerReference value). "
            + "The quantity is in the base asset (amount); only a trigger order without a limit price can take it in the quote asset (amountQuote). The time in force is sent only for a trigger order with a limit price. "
            + "The order waits in status awaitingTrigger and keeps its order id once it fired. Needs the OperatorId exchange parameter.",
    };

    /// <inheritdoc />
    public async Task<HttpResult<SharedId>> PlaceSpotTriggerOrderAsync(PlaceSpotTriggerOrderRequest request, CancellationToken ct = default)
    {
        var validationError = PlaceSpotTriggerOrderOptions.ValidateRequest(request, this)
            ?? request.GetTriggerReferenceError()
            ?? request.GetBitvavoTriggerOrderError();
        if (validationError != null)
        {
            return HttpResult.Fail<SharedId>(Exchange, validationError);
        }

        var result = await _api.Trading.PlaceOrderAsync(
            request.ToBitvavoTriggerOrderRequest(request.Symbol!.GetSymbol(FormatSymbol), request.GetOperatorId(), request.GetTriggerReference()),
            ct).ConfigureAwait(false);
        if (!result.Success)
        {
            return HttpResult.Fail<SharedId>(result);
        }

        return HttpResult.Ok(result, new SharedId(result.Data.OrderId));
    }

    // ── get ──────────────────────────────────────────────────────────────────────────────

    async Task<IExchangeCallResult<SharedSpotTriggerOrder>> IGetSpotTriggerOrder.GetSpotTriggerOrderAsync(GetOrderRequest request, CancellationToken ct)
        => await GetSpotTriggerOrderAsync(request, ct).ConfigureAwait(false);

    /// <summary>Reads a trigger order, which needs no operator id: Bitvavo identifies an order by its market and its order id.</summary>
    public GetSpotTriggerOrderOptions GetSpotTriggerOrderOptions { get; } = new GetSpotTriggerOrderOptions(_exchangeName, true)
    {
        RequestNotes = "Reads the order with GET /v2/order (rate-limit weight 1). A trigger order is an ordinary order that waits in status awaitingTrigger until it fires: awaitingTrigger is Active, new and partiallyFilled are Triggered (the order keeps its id once it fired), filled is Filled, and every canceled status, expired and rejected are CanceledOrRejected. An order that is not a trigger order (no stop-loss or take-profit type and no trigger price) is reported as UnknownOrder.",
    };

    /// <inheritdoc />
    public async Task<HttpResult<SharedSpotTriggerOrder>> GetSpotTriggerOrderAsync(GetOrderRequest request, CancellationToken ct = default)
    {
        var validationError = GetSpotTriggerOrderOptions.ValidateRequest(request, this);
        if (validationError != null)
        {
            return HttpResult.Fail<SharedSpotTriggerOrder>(Exchange, validationError);
        }

        // An unknown order is the server's error 240, which the error mapping already types as UnknownOrder.
        var result = await _api.Trading.GetOrderAsync(request.Symbol!.GetSymbol(FormatSymbol), orderId: request.OrderId, ct: ct).ConfigureAwait(false);
        if (!result.Success)
        {
            return HttpResult.Fail<SharedSpotTriggerOrder>(result);
        }

        // Bitvavo's order endpoint answers for any order, so an ordinary one has to be refused here.
        if (!result.Data.WasPlacedAsTriggerOrder())
        {
            return HttpResult.Fail<SharedSpotTriggerOrder>(result, new ServerError(new ErrorInfo(ErrorType.UnknownOrder, $"Order {request.OrderId} is not a trigger order")));
        }

        return HttpResult.Ok(result, result.Data.ToSharedSpotTriggerOrder(request.Symbol));
    }

    // ── cancel ───────────────────────────────────────────────────────────────────────────

    async Task<IExchangeCallResult<SharedId>> ICancelSpotTriggerOrder.CancelSpotTriggerOrderAsync(CancelOrderRequest request, CancellationToken ct)
        => await CancelSpotTriggerOrderAsync(request, ct).ConfigureAwait(false);

    /// <summary>Cancels a trigger order the way Bitvavo cancels any open order.</summary>
    public CancelSpotTriggerOrderOptions CancelSpotTriggerOrderOptions { get; } = new CancelSpotTriggerOrderOptions(_exchangeName, true)
    {
        ExchangeParameterRules = [BitvavoSharedParameters.OperatorIdRule],
        RequestNotes = "Bitvavo has no separate trigger-order cancel: the order is canceled with DELETE /v2/order (rate-limit weight 1), whether it still waits for its trigger or already fired; an order that is filled, canceled or unknown answers error 240 (UnknownOrder). Needs the OperatorId exchange parameter.",
    };

    /// <inheritdoc />
    public async Task<HttpResult<SharedId>> CancelSpotTriggerOrderAsync(CancelOrderRequest request, CancellationToken ct = default)
    {
        var validationError = CancelSpotTriggerOrderOptions.ValidateRequest(request, this);
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
}
