// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using Bitvavo.Net.Objects.Models.Spot;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.SharedApis;

namespace Bitvavo.Net.Clients.SpotApi;

/// <summary>
/// Order-management capabilities, [V2] only (the legacy interfaces have no equivalent), all signed and all carrying the
/// <c>OperatorId</c> exchange parameter Bitvavo requires on every order operation: <see cref="ICancelAllSpotOrdersRest"/>,
/// <see cref="ICancelAllSpotSymbolOrdersRest"/>, <see cref="IEditSpotOrderRest"/> and <see cref="IEditSpotOrderByClientOrderIdRest"/>.
/// </summary>
internal sealed partial class BitvavoRestClientSpotSharedApi :
    ICancelAllSpotOrdersRest,
    ICancelAllSpotSymbolOrdersRest,
    IEditSpotOrderRest,
    IEditSpotOrderByClientOrderIdRest
{
    /// <summary>What Bitvavo can change on an open order, shared by both edit capabilities.</summary>
    private const string _spotOrderManagementEditNotes =
        "PUT /v2/order (rate-limit weight 1) updates a limit order or a trigger order that has not been triggered yet; a market order cannot be updated (error 234) and an order that is filled, canceled or unknown answers error 240. "
        + "Quantity is the new total amount of the order (Bitvavo adjusts the remaining amount itself): in the base asset (amount) or, for an untriggered stop-loss or take-profit order without a limit price, in the quote asset (amountQuote); Bitvavo cannot switch an order between the two (error 239), so give it in the notation the order was placed with. "
        + "Price is the new limit price. Give at least one of them (error 232). The trigger price, the time in force, post-only and self-trade prevention can change on Bitvavo too but have no field in the Shared request. "
        + "Needs the OperatorId exchange parameter.";

    private static readonly RequestParameterRuleOverride[] _spotOrderManagementEditRules =
    [
        RequestParameterRuleOverride<EditOrderRequest>.Optional(
            x => x.Quantity,
            "The new total quantity of the order (Bitvavo's amount): in the base asset, or, for an untriggered stop-loss or take-profit order without a limit price, in the quote asset (amountQuote). Bitvavo cannot switch an order between the two (error 239): give it in the notation the order was placed with, never both"),
        RequestParameterRuleOverride<EditOrderRequest>.Optional(
            x => x.Price,
            "The new limit price of a limit order or of an untriggered trigger order with a limit price; a multiple of the market's tick size"),
    ];

    // ── cancel all orders ────────────────────────────────────────────────────────────────

    async Task<IExchangeCallResult> ICancelAllSpotOrders.CancelAllSpotOrdersAsync(CancelAllOrdersRequest request, CancellationToken ct)
        => await CancelAllSpotOrdersAsync(request, ct).ConfigureAwait(false);

    /// <summary>
    /// Cancels every open order of the account, in every market. The Shared contract of this capability returns no data, so the ids
    /// of the canceled orders are not surfaced.
    /// </summary>
    public CancelAllSpotOrdersOptions CancelAllSpotOrdersOptions { get; } = new CancelAllSpotOrdersOptions(_exchangeName)
    {
        ExchangeParameterRules = [BitvavoSharedParameters.OperatorIdRule],
        RequestNotes = "Cancels every open order of the account in all markets, trigger orders that wait for their trigger included (DELETE /v2/orders without a market, rate-limit weight 100). The Shared contract returns no order ids: read the open orders first when the ids matter. Needs the OperatorId exchange parameter.",
    };

    /// <inheritdoc />
    public async Task<HttpResult> CancelAllSpotOrdersAsync(CancelAllOrdersRequest request, CancellationToken ct = default)
    {
        var validationError = CancelAllSpotOrdersOptions.ValidateRequest(request, this);
        if (validationError != null)
        {
            return HttpResult.Fail(Exchange, validationError);
        }

        // Without a market Bitvavo cancels every open order of the account.
        var result = await _api.Trading.CancelOrdersAsync(request.GetOperatorId(), market: null, ct).ConfigureAwait(false);
        if (!result.Success)
        {
            return HttpResult.Fail(result);
        }

        return HttpResult.Ok(result);
    }

    // ── cancel all orders of one symbol ──────────────────────────────────────────────────

    async Task<IExchangeCallResult> ICancelAllSpotSymbolOrders.CancelAllSpotSymbolOrdersAsync(CancelAllSymbolOrdersRequest request, CancellationToken ct)
        => await CancelAllSpotSymbolOrdersAsync(request, ct).ConfigureAwait(false);

    /// <summary>
    /// Cancels every open order of one market. The Shared contract of this capability returns no data, so the ids of the canceled
    /// orders are not surfaced.
    /// </summary>
    public CancelAllSpotSymbolOrdersOptions CancelAllSpotSymbolOrdersOptions { get; } = new CancelAllSpotSymbolOrdersOptions(_exchangeName, true)
    {
        ExchangeParameterRules = [BitvavoSharedParameters.OperatorIdRule],
        RequestNotes = "Cancels every open order of one market, trigger orders that wait for their trigger included (DELETE /v2/orders with a market, rate-limit weight 25). The Shared contract returns no order ids. Needs the OperatorId exchange parameter.",
    };

    /// <inheritdoc />
    public async Task<HttpResult> CancelAllSpotSymbolOrdersAsync(CancelAllSymbolOrdersRequest request, CancellationToken ct = default)
    {
        var validationError = CancelAllSpotSymbolOrdersOptions.ValidateRequest(request, this);
        if (validationError != null)
        {
            return HttpResult.Fail(Exchange, validationError);
        }

        var result = await _api.Trading.CancelOrdersAsync(request.GetOperatorId(), request.Symbol!.GetSymbol(FormatSymbol), ct).ConfigureAwait(false);
        if (!result.Success)
        {
            return HttpResult.Fail(result);
        }

        return HttpResult.Ok(result);
    }

    // ── edit an order by its order id ────────────────────────────────────────────────────

    async Task<IExchangeCallResult<SharedId>> IEditSpotOrder.EditSpotOrderAsync(EditOrderRequest request, CancellationToken ct)
        => await EditSpotOrderAsync(request, ct).ConfigureAwait(false);

    /// <summary>Edits the quantity and the price of an open limit order or of an untriggered trigger order, found by its order id.</summary>
    public EditSpotOrderOptions EditSpotOrderOptions { get; } = new EditSpotOrderOptions(_exchangeName)
    {
        ExchangeParameterRules = [BitvavoSharedParameters.OperatorIdRule],
        ParameterRuleOverrides = _spotOrderManagementEditRules,
        RequestNotes = _spotOrderManagementEditNotes,
    };

    /// <inheritdoc />
    public async Task<HttpResult<SharedId>> EditSpotOrderAsync(EditOrderRequest request, CancellationToken ct = default)
        => await SpotOrderManagementEditAsync(request, EditSpotOrderOptions.ValidateRequest(request, this), byClientOrderId: false, ct).ConfigureAwait(false);

    // ── edit an order by its client order id ─────────────────────────────────────────────

    async Task<IExchangeCallResult<SharedId>> IEditSpotOrderByClientOrderId.EditSpotOrderByClientOrderIdAsync(EditOrderRequest request, CancellationToken ct)
        => await EditSpotOrderByClientOrderIdAsync(request, ct).ConfigureAwait(false);

    /// <summary>Edits the quantity and the price of an open limit order or of an untriggered trigger order, found by its client order id.</summary>
    public EditSpotOrderByClientOrderIdOptions EditSpotOrderByClientOrderIdOptions { get; } = new EditSpotOrderByClientOrderIdOptions(_exchangeName, true)
    {
        ExchangeParameterRules = [BitvavoSharedParameters.OperatorIdRule],
        ParameterRuleOverrides = _spotOrderManagementEditRules,
        RequestNotes = _spotOrderManagementEditNotes + " The order is found by its client order id (a UUID), which the request carries in OrderId.",
    };

    /// <inheritdoc />
    public async Task<HttpResult<SharedId>> EditSpotOrderByClientOrderIdAsync(EditOrderRequest request, CancellationToken ct = default)
        => await SpotOrderManagementEditAsync(request, EditSpotOrderByClientOrderIdOptions.ValidateRequest(request, this), byClientOrderId: true, ct).ConfigureAwait(false);

    // ── the one edit path ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Both edit capabilities send the same <c>PUT /v2/order</c>; they differ in the options that validate the request and in whether
    /// <see cref="EditOrderRequest.OrderId"/> is the order id or the client order id.
    /// </summary>
    private async Task<HttpResult<SharedId>> SpotOrderManagementEditAsync(EditOrderRequest request, Error? optionsError, bool byClientOrderId, CancellationToken ct)
    {
        var validationError = optionsError ?? SpotOrderManagementGetUpdateError(request);
        if (validationError != null)
        {
            return HttpResult.Fail<SharedId>(Exchange, validationError);
        }

        var result = await _api.Trading.UpdateOrderAsync(
            new BitvavoUpdateOrderRequest(
                request.Symbol!.GetSymbol(FormatSymbol),
                request.GetOperatorId(),
                OrderId: byClientOrderId ? null : request.OrderId,
                ClientOrderId: byClientOrderId ? request.OrderId : null,
                Amount: request.Quantity?.QuantityInBaseAsset,
                AmountQuote: request.Quantity?.QuantityInQuoteAsset,
                Price: request.Price),
            ct).ConfigureAwait(false);
        if (!result.Success)
        {
            return HttpResult.Fail<SharedId>(result);
        }

        return HttpResult.Ok(result, new SharedId(result.Data.OrderId));
    }

    /// <summary>
    /// The reason an edit cannot be expressed on Bitvavo, or null when it can: Bitvavo rejects an update that changes nothing
    /// (error 232) and one that names more than one of <c>amount</c> and <c>amountQuote</c> (error 236).
    /// </summary>
    private static Error? SpotOrderManagementGetUpdateError(EditOrderRequest request)
    {
        var quantity = request.Quantity;
        if (quantity == null && request.Price == null)
        {
            return ArgumentError.Missing(nameof(request.Quantity), "Bitvavo rejects an update that changes nothing: give a new Quantity, a new Price or both");
        }

        if (quantity != null)
        {
            var inBase = quantity.QuantityInBaseAsset != null;
            var inQuote = quantity.QuantityInQuoteAsset != null;
            if (inBase && inQuote)
            {
                return ArgumentError.Invalid(nameof(request.Quantity), "Bitvavo takes the quantity in the base asset (amount) or in the quote asset (amountQuote), not in both");
            }

            if (!inBase && !inQuote)
            {
                return ArgumentError.Missing(nameof(request.Quantity), "give the new quantity in the base asset or in the quote asset");
            }
        }

        return null;
    }
}
