// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bitvavo.Net.Enums;
using Bitvavo.Net.Interfaces.Clients.SpotApi;
using Bitvavo.Net.Objects.Internal;
using CryptoExchange.Net.Objects.Sockets;
using CryptoExchange.Net.SharedApis;

namespace Bitvavo.Net.Clients.SpotApi;

/// <summary>
/// CryptoExchange.Net Shared-API implementation for the Bitvavo Spot WebSocket client.
/// <para>
/// Facade-hosted partial on <see cref="BitvavoSocketClientSpotApi"/> (mirroring
/// <c>KrakenSocketClientSpotApi.Shared.cs</c>): the Shared socket interfaces are
/// implemented here and delegate to the typed <see cref="BitvavoSocketClientSpotApi.ExchangeData"/>
/// and <see cref="BitvavoSocketClientSpotApi.Account"/> sub-clients.
/// </para>
/// <para>
/// <see cref="IBalanceSocketClient"/> is intentionally NOT implemented: Bitvavo's private
/// <c>account</c> channel emits only <c>order</c> and <c>fill</c> events — it carries no
/// balance snapshot or balance-delta stream — so there is no data source to back a balance
/// subscription. That is a correct ISP-driven omission, not a stub.
/// </para>
/// <para>
/// The Shared <see cref="ISpotOrderSocketClient"/> / <see cref="IUserTradeSocketClient"/>
/// request types (<see cref="SubscribeSpotOrderRequest"/> / <see cref="SubscribeUserTradeRequest"/>)
/// carry no symbol set, but Bitvavo's <c>account</c> channel is subscribed per-market. The
/// market list is therefore supplied through the <see cref="ExchangeParameters"/> escape
/// hatch under the <c>Markets</c> key (a <c>string[]</c>) — the canonical CryptoExchange.Net
/// mechanism for exchange-specific subscription parameters.
/// </para>
/// </summary>
internal sealed partial class BitvavoSocketClientSpotApi :
    IKlineSocketClient,
    ITradeSocketClient,
    ISpotOrderSocketClient,
    IUserTradeSocketClient,
    IBitvavoSocketClientSpotApiShared
{
    /// <summary>
    /// <see cref="ExchangeParameters"/> key carrying the <c>string[]</c> of markets the
    /// account-channel subscription should cover (e.g. <c>["ETH-EUR", "BTC-EUR"]</c>).
    /// </summary>
    public const string MarketsExchangeParameter = "Markets";

    private static readonly TradingMode[] _spotOnlySocket = [TradingMode.Spot];

    /// <inheritdoc />
    public string Exchange => BitvavoExchange.ExchangeName;

    /// <inheritdoc />
    public TradingMode[] SupportedTradingModes => _spotOnlySocket;

    // ISharedClient.Authenticated is satisfied by the inherited SocketApiClient.Authenticated.

    /// <inheritdoc />
    public void SetDefaultExchangeParameter(string name, object value)
        => ExchangeParameters.SetStaticParameter(Exchange, name, value);

    /// <inheritdoc />
    public void ResetDefaultExchangeParameters()
        => ExchangeParameters.ResetStaticParameters();

    // ── IKlineSocketClient ────────────────────────────────────────────────────────────────

    private static readonly SharedKlineInterval[] _supportedSocketKlineIntervals =
    [
        SharedKlineInterval.OneMinute,
        SharedKlineInterval.FiveMinutes,
        SharedKlineInterval.FifteenMinutes,
        SharedKlineInterval.ThirtyMinutes,
        SharedKlineInterval.OneHour,
        SharedKlineInterval.TwoHours,
        SharedKlineInterval.FourHours,
        SharedKlineInterval.SixHours,
        SharedKlineInterval.EightHours,
        SharedKlineInterval.TwelveHours,
        SharedKlineInterval.OneDay,
        SharedKlineInterval.OneWeek,
        SharedKlineInterval.OneMonth,
    ];

    /// <inheritdoc />
    public SubscribeKlineOptions SubscribeKlineOptions { get; } = new(false, _supportedSocketKlineIntervals);

    /// <inheritdoc />
    public async Task<ExchangeResult<UpdateSubscription>> SubscribeToKlineUpdatesAsync(
        SubscribeKlineRequest request,
        Action<DataEvent<SharedKline>> handler,
        CancellationToken ct = default)
    {
        var validationError = SubscribeKlineOptions.ValidateRequest(Exchange, request, TradingMode.Spot, SupportedTradingModes);
        if (validationError != null)
            return new ExchangeResult<UpdateSubscription>(Exchange, validationError);

        var interval = ToBitvavoSocketKlineInterval(request.Interval);
        var sharedSymbol = request.Symbol!;
        var symbol = sharedSymbol.GetSymbol(FormatSymbol);

        var result = await ExchangeData.SubscribeToKlineUpdatesAsync(
            symbol,
            interval,
            update =>
            {
                // Bitvavo's candle event carries an array of snapshots — surface each as
                // a separate SharedKline update so the consumer sees one event per candle.
                foreach (var candle in update.Data.Candle)
                {
                    handler(update.ToType(new SharedKline(
                        sharedSymbol,
                        symbol,
                        candle.OpenTime,
                        candle.ClosePrice,
                        candle.HighPrice,
                        candle.LowPrice,
                        candle.OpenPrice,
                        candle.Volume)));
                }
            },
            ct).ConfigureAwait(false);

        return new ExchangeResult<UpdateSubscription>(Exchange, result);
    }

    // ── ITradeSocketClient ────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public EndpointOptions<SubscribeTradeRequest> SubscribeTradeOptions { get; } = new(false) { EndpointName = "SubscribeTradeRequest" };

    /// <inheritdoc />
    public async Task<ExchangeResult<UpdateSubscription>> SubscribeToTradeUpdatesAsync(
        SubscribeTradeRequest request,
        Action<DataEvent<SharedTrade[]>> handler,
        CancellationToken ct = default)
    {
        var validationError = SubscribeTradeOptions.ValidateRequest(Exchange, request, TradingMode.Spot, SupportedTradingModes);
        if (validationError != null)
            return new ExchangeResult<UpdateSubscription>(Exchange, validationError);

        var sharedSymbol = request.Symbol!;
        var symbol = sharedSymbol.GetSymbol(FormatSymbol);

        var result = await ExchangeData.SubscribeToTradeUpdatesAsync(
            symbol,
            update => handler(update.ToType<SharedTrade[]>(
            [
                new SharedTrade(
                    sharedSymbol,
                    symbol,
                    update.Data.Amount,
                    update.Data.Price,
                    update.Data.Timestamp)
                {
                    Side = ToSharedSocketOrderSide(update.Data.Side),
                },
            ])),
            ct).ConfigureAwait(false);

        return new ExchangeResult<UpdateSubscription>(Exchange, result);
    }

    // ── ISpotOrderSocketClient ────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public EndpointOptions<SubscribeSpotOrderRequest> SubscribeSpotOrderOptions { get; } = new(true)
    {
        EndpointName = "SubscribeSpotOrderRequest",
        RequiredExchangeParameters =
        [
            new ParameterDescription(
                MarketsExchangeParameter,
                typeof(string[]),
                "Markets the Bitvavo account channel subscription should cover",
                new[] { "ETH-EUR" }),
        ],
    };

    /// <inheritdoc />
    public async Task<ExchangeResult<UpdateSubscription>> SubscribeToSpotOrderUpdatesAsync(
        SubscribeSpotOrderRequest request,
        Action<DataEvent<SharedSpotOrder[]>> handler,
        CancellationToken ct = default)
    {
        var validationError = SubscribeSpotOrderOptions.ValidateRequest(Exchange, request.ExchangeParameters, TradingMode.Spot, SupportedTradingModes);
        if (validationError != null)
            return new ExchangeResult<UpdateSubscription>(Exchange, validationError);

        var markets = ExchangeParameters.GetValue<string[]>(request.ExchangeParameters, Exchange, MarketsExchangeParameter)
            ?? throw new ArgumentException($"The '{MarketsExchangeParameter}' exchange parameter (string[]) is required — Bitvavo's account channel is subscribed per market", nameof(request));

        var result = await Account.SubscribeToOrderUpdatesAsync(
            markets,
            update =>
            {
                var order = update.Data;
                var sharedSymbol = ParseSocketSharedSymbol(order.Market);
                handler(update.ToType<SharedSpotOrder[]>(
                [
                    new SharedSpotOrder(
                        sharedSymbol,
                        order.Market,
                        order.OrderId,
                        ToSharedSocketOrderType(order.OrderType),
                        ToSharedSocketOrderSide(order.Side),
                        ToSharedSocketOrderStatus(order.Status),
                        order.Created)
                    {
                        ClientOrderId = order.ClientOrderId,
                        OrderPrice = order.Price,
                        OrderQuantity = new SharedOrderQuantity(order.Amount, null),
                        QuantityFilled = new SharedOrderQuantity(order.FilledAmount, order.FilledAmountQuote),
                        TimeInForce = order.TimeInForce == null ? null : ToSharedSocketTimeInForce(order.TimeInForce.Value),
                        UpdateTime = order.Updated,
                        TriggerPrice = order.TriggerAmount,
                        IsTriggerOrder = order.TriggerAmount != null,
                    },
                ]));
            },
            ct).ConfigureAwait(false);

        return new ExchangeResult<UpdateSubscription>(Exchange, result);
    }

    // ── IUserTradeSocketClient ────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public EndpointOptions<SubscribeUserTradeRequest> SubscribeUserTradeOptions { get; } = new(true)
    {
        EndpointName = "SubscribeUserTradeRequest",
        RequiredExchangeParameters =
        [
            new ParameterDescription(
                MarketsExchangeParameter,
                typeof(string[]),
                "Markets the Bitvavo account channel subscription should cover",
                new[] { "ETH-EUR" }),
        ],
    };

    /// <inheritdoc />
    public async Task<ExchangeResult<UpdateSubscription>> SubscribeToUserTradeUpdatesAsync(
        SubscribeUserTradeRequest request,
        Action<DataEvent<SharedUserTrade[]>> handler,
        CancellationToken ct = default)
    {
        var validationError = SubscribeUserTradeOptions.ValidateRequest(Exchange, request.ExchangeParameters, TradingMode.Spot, SupportedTradingModes);
        if (validationError != null)
            return new ExchangeResult<UpdateSubscription>(Exchange, validationError);

        var markets = ExchangeParameters.GetValue<string[]>(request.ExchangeParameters, Exchange, MarketsExchangeParameter)
            ?? throw new ArgumentException($"The '{MarketsExchangeParameter}' exchange parameter (string[]) is required — Bitvavo's account channel is subscribed per market", nameof(request));

        // User trades are surfaced from Bitvavo's account-channel `fill` event.
        var result = await Account.SubscribeToFillUpdatesAsync(
            markets,
            update =>
            {
                var fill = update.Data;
                var sharedSymbol = ParseSocketSharedSymbol(fill.Market);
                handler(update.ToType<SharedUserTrade[]>(
                [
                    new SharedUserTrade(
                        sharedSymbol,
                        fill.Market,
                        fill.OrderId,
                        fill.FillId,
                        ToSharedSocketOrderSide(fill.Side),
                        fill.Amount,
                        fill.Price,
                        fill.Timestamp)
                    {
                        ClientOrderId = fill.ClientOrderId,
                        Fee = fill.Fee,
                        FeeAsset = fill.FeeCurrency,
                        Role = fill.Taker ? SharedRole.Taker : SharedRole.Maker,
                    },
                ]));
            },
            ct).ConfigureAwait(false);

        return new ExchangeResult<UpdateSubscription>(Exchange, result);
    }

    // ── Shared socket helpers ─────────────────────────────────────────────────────────────

    private static SharedSymbol ParseSocketSharedSymbol(string market)
    {
        var dash = market.IndexOf('-');
        return dash > 0
            ? new SharedSymbol(TradingMode.Spot, market[..dash], market[(dash + 1)..], market)
            : new SharedSymbol(TradingMode.Spot, market, string.Empty, market);
    }

    private static KlineInterval ToBitvavoSocketKlineInterval(SharedKlineInterval interval) => interval switch
    {
        SharedKlineInterval.OneMinute => KlineInterval.OneMinute,
        SharedKlineInterval.FiveMinutes => KlineInterval.FiveMinutes,
        SharedKlineInterval.FifteenMinutes => KlineInterval.FifteenMinutes,
        SharedKlineInterval.ThirtyMinutes => KlineInterval.ThirtyMinutes,
        SharedKlineInterval.OneHour => KlineInterval.OneHour,
        SharedKlineInterval.TwoHours => KlineInterval.TwoHours,
        SharedKlineInterval.FourHours => KlineInterval.FourHours,
        SharedKlineInterval.SixHours => KlineInterval.SixHours,
        SharedKlineInterval.EightHours => KlineInterval.EightHours,
        SharedKlineInterval.TwelveHours => KlineInterval.TwelveHours,
        SharedKlineInterval.OneDay => KlineInterval.OneDay,
        SharedKlineInterval.OneWeek => KlineInterval.OneWeek,
        SharedKlineInterval.OneMonth => KlineInterval.OneMonth,
        _ => throw new ArgumentOutOfRangeException(nameof(interval), interval, "Interval not supported by Bitvavo"),
    };

    private static SharedOrderSide ToSharedSocketOrderSide(OrderSide side) => side switch
    {
        OrderSide.Buy => SharedOrderSide.Buy,
        OrderSide.Sell => SharedOrderSide.Sell,
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Unmapped Bitvavo OrderSide"),
    };

    private static SharedOrderType ToSharedSocketOrderType(OrderType type) => type switch
    {
        OrderType.Market => SharedOrderType.Market,
        OrderType.Limit => SharedOrderType.Limit,
        OrderType.StopLoss => SharedOrderType.Other,
        OrderType.StopLossLimit => SharedOrderType.Other,
        OrderType.TakeProfit => SharedOrderType.Other,
        OrderType.TakeProfitLimit => SharedOrderType.Other,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unmapped Bitvavo OrderType"),
    };

    private static SharedOrderStatus ToSharedSocketOrderStatus(OrderStatus status) => status switch
    {
        OrderStatus.New => SharedOrderStatus.Open,
        OrderStatus.AwaitingTrigger => SharedOrderStatus.Open,
        OrderStatus.PartiallyFilled => SharedOrderStatus.Open,
        OrderStatus.Filled => SharedOrderStatus.Filled,
        OrderStatus.Canceled => SharedOrderStatus.Canceled,
        OrderStatus.CanceledAuction => SharedOrderStatus.Canceled,
        OrderStatus.CanceledSelfTradePrevention => SharedOrderStatus.Canceled,
        OrderStatus.CanceledIoc => SharedOrderStatus.Canceled,
        OrderStatus.CanceledFok => SharedOrderStatus.Canceled,
        OrderStatus.CanceledMarketProtection => SharedOrderStatus.Canceled,
        OrderStatus.CanceledPostOnly => SharedOrderStatus.Canceled,
        OrderStatus.Expired => SharedOrderStatus.Canceled,
        OrderStatus.Rejected => SharedOrderStatus.Canceled,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unmapped Bitvavo OrderStatus"),
    };

    private static SharedTimeInForce ToSharedSocketTimeInForce(TimeInForce timeInForce) => timeInForce switch
    {
        TimeInForce.GoodTillCanceled => SharedTimeInForce.GoodTillCanceled,
        TimeInForce.ImmediateOrCancel => SharedTimeInForce.ImmediateOrCancel,
        TimeInForce.FillOrKill => SharedTimeInForce.FillOrKill,
        _ => throw new ArgumentOutOfRangeException(nameof(timeInForce), timeInForce, "Unmapped Bitvavo TimeInForce"),
    };
}
