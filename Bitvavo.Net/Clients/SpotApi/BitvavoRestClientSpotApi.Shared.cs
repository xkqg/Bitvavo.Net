// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bitvavo.Net.Enums;
using Bitvavo.Net.Interfaces.Clients.SpotApi;
using Bitvavo.Net.Objects.Internal;
using Bitvavo.Net.Objects.Models.Spot;
using CryptoExchange.Net.Interfaces;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Errors;
using CryptoExchange.Net.SharedApis;

namespace Bitvavo.Net.Clients.SpotApi;

/// <summary>
/// CryptoExchange.Net Shared-API implementation for the Bitvavo Spot REST client.
/// <para>
/// This is a facade-hosted partial on <see cref="BitvavoRestClientSpotApi"/> itself
/// (mirroring <c>KrakenRestClientSpotApi.Shared.cs</c>): the Shared interfaces are
/// implemented here and delegate to the typed <see cref="BitvavoRestClientSpotApi.ExchangeData"/>,
/// <see cref="BitvavoRestClientSpotApi.Account"/>, <see cref="BitvavoRestClientSpotApi.Trading"/>,
/// and <see cref="BitvavoRestClientSpotApi.Funding"/> sub-clients. The Shared layer is the
/// exchange-agnostic surface other CryptoExchange.Net consumers program against.
/// </para>
/// <para>
/// Bitvavo is spot-only, so every endpoint reports <see cref="TradingMode.Spot"/> as the
/// single supported trading mode. Symbol translation goes through
/// <see cref="BitvavoExchange.FormatSymbol(string, string, TradingMode, DateTime?)"/>
/// (uppercase <c>BASE-QUOTE</c>).
/// </para>
/// </summary>
internal partial class BitvavoRestClientSpotApi :
    IAssetsRestClient,
    IKlineRestClient,
    IRecentTradeRestClient,
    IOrderBookRestClient,
    ISpotSymbolRestClient,
    ISpotTickerRestClient,
    IBookTickerRestClient,
    IBalanceRestClient,
    ISpotOrderRestClient,
    ISpotOrderClientIdRestClient,
    IFeeRestClient,
    IDepositRestClient,
    IWithdrawalRestClient,
    IWithdrawRestClient,
    IBitvavoRestClientSpotApiShared
{
    private static readonly TradingMode[] _spotOnly = [TradingMode.Spot];

    /// <inheritdoc />
    public string Exchange => BitvavoExchange.ExchangeName;

    /// <inheritdoc />
    public TradingMode[] SupportedTradingModes => _spotOnly;

    // ISharedClient.Authenticated is satisfied by the inherited RestApiClient.Authenticated.

    /// <inheritdoc />
    public void SetDefaultExchangeParameter(string name, object value)
        => ExchangeParameters.SetStaticParameter(Exchange, name, value);

    /// <inheritdoc />
    public void ResetDefaultExchangeParameters()
        => ExchangeParameters.ResetStaticParameters();

    // ── IAssetsRestClient ─────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public EndpointOptions<GetAssetRequest> GetAssetOptions { get; } = new(false) { EndpointName = "GetAssetRequest" };

    /// <inheritdoc />
    public async Task<ExchangeWebResult<SharedAsset>> GetAssetAsync(GetAssetRequest request, CancellationToken ct = default)
    {
        var validationError = GetAssetOptions.ValidateRequest(Exchange, request.ExchangeParameters, TradingMode.Spot, SupportedTradingModes);
        if (validationError != null)
            return new ExchangeWebResult<SharedAsset>(Exchange, validationError);

        var result = await ExchangeData.GetAssetsAsync(request.Asset, ct).ConfigureAwait(false);
        if (!result)
            return result.AsExchangeError<SharedAsset>(Exchange, result.Error!);

        var asset = result.Data!.SingleOrDefault();
        if (asset == null)
            return result.AsExchangeError<SharedAsset>(Exchange, new ServerError(new ErrorInfo(ErrorType.UnknownSymbol, "Asset not found")));

        return result.AsExchangeResult(Exchange, TradingMode.Spot, ToSharedAsset(asset), null);
    }

    /// <inheritdoc />
    public EndpointOptions<GetAssetsRequest> GetAssetsOptions { get; } = new(false) { EndpointName = "GetAssetsRequest" };

    /// <inheritdoc />
    public async Task<ExchangeWebResult<SharedAsset[]>> GetAssetsAsync(GetAssetsRequest request, CancellationToken ct = default)
    {
        var validationError = GetAssetsOptions.ValidateRequest(Exchange, request.ExchangeParameters, TradingMode.Spot, SupportedTradingModes);
        if (validationError != null)
            return new ExchangeWebResult<SharedAsset[]>(Exchange, validationError);

        var result = await ExchangeData.GetAssetsAsync(symbol: null, ct).ConfigureAwait(false);
        if (!result)
            return result.AsExchangeError<SharedAsset[]>(Exchange, result.Error!);

        return result.AsExchangeResult(Exchange, TradingMode.Spot, result.Data!.Select(ToSharedAsset).ToArray(), null);
    }

    private static SharedAsset ToSharedAsset(BitvavoAsset asset) => new(asset.Symbol)
    {
        FullName = asset.Name,
        Networks = asset.Networks
            .Select(network => new SharedAssetNetwork(network)
            {
                WithdrawFee = asset.WithdrawalFee,
                MinWithdrawQuantity = asset.WithdrawalMinAmount,
                WithdrawEnabled = string.Equals(asset.WithdrawalStatus, "OK", StringComparison.OrdinalIgnoreCase),
                DepositEnabled = string.Equals(asset.DepositStatus, "OK", StringComparison.OrdinalIgnoreCase),
                MinConfirmations = asset.DepositConfirmations,
            })
            .ToArray(),
    };

    // ── IKlineRestClient ──────────────────────────────────────────────────────────────────

    private static readonly SharedKlineInterval[] _supportedKlineIntervals =
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
    public GetKlinesOptions GetKlinesOptions { get; } = new(
        supportsAscending: true,
        supportsDescending: true,
        timeFilterSupported: true,
        maxLimit: 1440,
        needsAuthentication: false,
        intervals: _supportedKlineIntervals)
    {
        MaxTotalDataPoints = 1440,
    };

    /// <inheritdoc />
    public async Task<ExchangeWebResult<SharedKline[]>> GetKlinesAsync(GetKlinesRequest request, PageRequest? nextPageToken = null, CancellationToken ct = default)
    {
        // ValidateRequest rejects intervals outside _supportedKlineIntervals (e.g.
        // SharedKlineInterval.ThreeMinutes — Bitvavo has no 3m bucket) before the
        // ToBitvavoKlineInterval mapper is reached, so the mapper only sees supported values.
        var validationError = GetKlinesOptions.ValidateRequest(Exchange, request, TradingMode.Spot, SupportedTradingModes);
        if (validationError != null)
            return new ExchangeWebResult<SharedKline[]>(Exchange, validationError);

        var interval = ToBitvavoKlineInterval(request.Interval);
        var symbol = request.Symbol!.GetSymbol(FormatSymbol);
        var result = await ExchangeData.GetKlinesAsync(
            symbol,
            interval,
            request.Limit,
            request.StartTime,
            request.EndTime,
            ct).ConfigureAwait(false);
        if (!result)
            return result.AsExchangeError<SharedKline[]>(Exchange, result.Error!);

        return result.AsExchangeResult(Exchange, TradingMode.Spot, result.Data!
            .Select(kline => new SharedKline(
                request.Symbol,
                symbol,
                kline.OpenTime,
                kline.ClosePrice,
                kline.HighPrice,
                kline.LowPrice,
                kline.OpenPrice,
                kline.Volume))
            .ToArray(), null);
    }

    // ── IRecentTradeRestClient ────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public GetRecentTradesOptions GetRecentTradesOptions { get; } = new(limit: 1000, authenticated: false);

    /// <inheritdoc />
    public async Task<ExchangeWebResult<SharedTrade[]>> GetRecentTradesAsync(GetRecentTradesRequest request, CancellationToken ct = default)
    {
        var validationError = GetRecentTradesOptions.ValidateRequest(Exchange, request, TradingMode.Spot, SupportedTradingModes);
        if (validationError != null)
            return new ExchangeWebResult<SharedTrade[]>(Exchange, validationError);

        var symbol = request.Symbol!.GetSymbol(FormatSymbol);
        var result = await ExchangeData.GetPublicTradesAsync(symbol, request.Limit, ct: ct).ConfigureAwait(false);
        if (!result)
            return result.AsExchangeError<SharedTrade[]>(Exchange, result.Error!);

        return result.AsExchangeResult(Exchange, TradingMode.Spot, result.Data!
            .Select(trade => new SharedTrade(
                request.Symbol,
                symbol,
                trade.Amount ?? 0m,
                trade.Price ?? 0m,
                trade.Timestamp)
            {
                Side = ToSharedOrderSide(trade.Side),
            })
            .ToArray(), null);
    }

    // ── IOrderBookRestClient ──────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public GetOrderBookOptions GetOrderBookOptions { get; } = new(minLimit: 1, maxLimit: 1000, authenticated: false);

    /// <inheritdoc />
    public async Task<ExchangeWebResult<SharedOrderBook>> GetOrderBookAsync(GetOrderBookRequest request, CancellationToken ct = default)
    {
        var validationError = GetOrderBookOptions.ValidateRequest(Exchange, request, TradingMode.Spot, SupportedTradingModes);
        if (validationError != null)
            return new ExchangeWebResult<SharedOrderBook>(Exchange, validationError);

        var symbol = request.Symbol!.GetSymbol(FormatSymbol);
        var result = await ExchangeData.GetOrderBookAsync(symbol, request.Limit, ct).ConfigureAwait(false);
        if (!result)
            return result.AsExchangeError<SharedOrderBook>(Exchange, result.Error!);

        return result.AsExchangeResult(Exchange, TradingMode.Spot, new SharedOrderBook(
            result.Data!.Asks.Select(entry => (ISymbolOrderBookEntry)new BitvavoSharedOrderBookEntry { Price = entry.Price, Quantity = entry.Size }).ToArray(),
            result.Data!.Bids.Select(entry => (ISymbolOrderBookEntry)new BitvavoSharedOrderBookEntry { Price = entry.Price, Quantity = entry.Size }).ToArray()), null);
    }

    // ── ISpotSymbolRestClient ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public EndpointOptions<GetSymbolsRequest> GetSpotSymbolsOptions { get; } = new(false) { EndpointName = "GetSymbolsRequest" };

    /// <inheritdoc />
    public async Task<ExchangeWebResult<SharedSpotSymbol[]>> GetSpotSymbolsAsync(GetSymbolsRequest request, CancellationToken ct = default)
    {
        var validationError = GetSpotSymbolsOptions.ValidateRequest(Exchange, request.ExchangeParameters, TradingMode.Spot, SupportedTradingModes);
        if (validationError != null)
            return new ExchangeWebResult<SharedSpotSymbol[]>(Exchange, validationError);

        var result = await ExchangeData.GetMarketsAsync(ct).ConfigureAwait(false);
        if (!result)
            return result.AsExchangeError<SharedSpotSymbol[]>(Exchange, result.Error!);

        return result.AsExchangeResult(Exchange, TradingMode.Spot, result.Data!
            .Select(market => new SharedSpotSymbol(
                market.BaseAsset,
                market.QuoteAsset,
                market.Market,
                string.Equals(market.Status, "trading", StringComparison.OrdinalIgnoreCase))
            {
                MinTradeQuantity = ParseDecimal(market.MinOrderInBaseAsset),
                MaxTradeQuantity = ParseDecimal(market.MaxOrderInBaseAsset),
                MinNotionalValue = ParseDecimal(market.MinOrderInQuoteAsset),
                PriceSignificantFigures = ParseInt(market.PricePrecision),
            })
            .ToArray(), null);
    }

    /// <inheritdoc />
    public async Task<ExchangeResult<SharedSymbol[]>> GetSpotSymbolsForBaseAssetAsync(string baseAsset)
    {
        var result = await GetSpotSymbolsAsync(new GetSymbolsRequest(TradingMode.Spot)).ConfigureAwait(false);
        if (!result)
            return new ExchangeResult<SharedSymbol[]>(Exchange, result.Error!);

        var matches = result.Data!
            .Where(symbol => string.Equals(symbol.BaseAsset, baseAsset, StringComparison.OrdinalIgnoreCase))
            .Select(symbol => symbol.SharedSymbol)
            .ToArray();
        return new ExchangeResult<SharedSymbol[]>(Exchange, matches);
    }

    /// <inheritdoc />
    public Task<ExchangeResult<bool>> SupportsSpotSymbolAsync(SharedSymbol symbol)
        => SupportsSpotSymbolAsync(symbol.GetSymbol(FormatSymbol));

    /// <inheritdoc />
    public async Task<ExchangeResult<bool>> SupportsSpotSymbolAsync(string symbolName)
    {
        var result = await GetSpotSymbolsAsync(new GetSymbolsRequest(TradingMode.Spot)).ConfigureAwait(false);
        if (!result)
            return new ExchangeResult<bool>(Exchange, result.Error!);

        var supported = result.Data!.Any(symbol => string.Equals(symbol.Name, symbolName, StringComparison.OrdinalIgnoreCase));
        return new ExchangeResult<bool>(Exchange, supported);
    }

    // ── ISpotTickerRestClient ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public GetTickerOptions GetSpotTickerOptions { get; } = new(SharedTickerType.Day24H);

    /// <inheritdoc />
    public async Task<ExchangeWebResult<SharedSpotTicker>> GetSpotTickerAsync(GetTickerRequest request, CancellationToken ct = default)
    {
        var validationError = GetSpotTickerOptions.ValidateRequest(Exchange, request, TradingMode.Spot, SupportedTradingModes);
        if (validationError != null)
            return new ExchangeWebResult<SharedSpotTicker>(Exchange, validationError);

        var symbol = request.Symbol!.GetSymbol(FormatSymbol);
        var result = await ExchangeData.GetTicker24hAsync(symbol, ct).ConfigureAwait(false);
        if (!result)
            return result.AsExchangeError<SharedSpotTicker>(Exchange, result.Error!);

        var ticker = result.Data!.SingleOrDefault();
        if (ticker == null)
            return result.AsExchangeError<SharedSpotTicker>(Exchange, new ServerError(new ErrorInfo(ErrorType.UnknownSymbol, "Ticker not found")));

        return result.AsExchangeResult(Exchange, TradingMode.Spot, ToSharedSpotTicker(request.Symbol, ticker), null);
    }

    /// <inheritdoc />
    public GetTickersOptions GetSpotTickersOptions { get; } = new(SharedTickerType.Day24H);

    /// <inheritdoc />
    public async Task<ExchangeWebResult<SharedSpotTicker[]>> GetSpotTickersAsync(GetTickersRequest request, CancellationToken ct = default)
    {
        var validationError = GetSpotTickersOptions.ValidateRequest(Exchange, request.ExchangeParameters, TradingMode.Spot, SupportedTradingModes);
        if (validationError != null)
            return new ExchangeWebResult<SharedSpotTicker[]>(Exchange, validationError);

        var result = await ExchangeData.GetTicker24hAsync(market: null, ct).ConfigureAwait(false);
        if (!result)
            return result.AsExchangeError<SharedSpotTicker[]>(Exchange, result.Error!);

        return result.AsExchangeResult(Exchange, TradingMode.Spot, result.Data!
            .Select(ticker => ToSharedSpotTicker(ParseSharedSymbol(ticker.Market), ticker))
            .ToArray(), null);
    }

    private static SharedSpotTicker ToSharedSpotTicker(SharedSymbol sharedSymbol, BitvavoTicker24h ticker)
    {
        var changePercentage = ticker.Open is > 0m && ticker.Last != null
            ? Math.Round((ticker.Last.Value - ticker.Open.Value) / ticker.Open.Value * 100m, 6)
            : (decimal?)null;

        return new SharedSpotTicker(
            sharedSymbol,
            ticker.Market,
            ticker.Last,
            ticker.High,
            ticker.Low,
            ticker.Volume ?? 0m,
            changePercentage)
        {
            QuoteVolume = ticker.VolumeQuote,
        };
    }

    // ── IBookTickerRestClient ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public EndpointOptions<GetBookTickerRequest> GetBookTickerOptions { get; } = new(false) { EndpointName = "GetBookTickerRequest" };

    /// <inheritdoc />
    public async Task<ExchangeWebResult<SharedBookTicker>> GetBookTickerAsync(GetBookTickerRequest request, CancellationToken ct = default)
    {
        var validationError = GetBookTickerOptions.ValidateRequest(Exchange, request, TradingMode.Spot, SupportedTradingModes);
        if (validationError != null)
            return new ExchangeWebResult<SharedBookTicker>(Exchange, validationError);

        var symbol = request.Symbol!.GetSymbol(FormatSymbol);
        var result = await ExchangeData.GetTickerBookAsync(symbol, ct).ConfigureAwait(false);
        if (!result)
            return result.AsExchangeError<SharedBookTicker>(Exchange, result.Error!);

        var book = result.Data!.SingleOrDefault();
        if (book == null)
            return result.AsExchangeError<SharedBookTicker>(Exchange, new ServerError(new ErrorInfo(ErrorType.UnknownSymbol, "Book ticker not found")));

        return result.AsExchangeResult(Exchange, TradingMode.Spot, new SharedBookTicker(
            request.Symbol,
            book.Market,
            book.Ask ?? 0m,
            book.AskSize ?? 0m,
            book.Bid ?? 0m,
            book.BidSize ?? 0m), null);
    }

    // ── IBalanceRestClient ────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public GetBalancesOptions GetBalancesOptions { get; } = new([AccountTypeFilter.Spot]);

    /// <inheritdoc />
    public async Task<ExchangeWebResult<SharedBalance[]>> GetBalancesAsync(GetBalancesRequest request, CancellationToken ct = default)
    {
        var validationError = GetBalancesOptions.ValidateRequest(Exchange, request, SupportedTradingModes);
        if (validationError != null)
            return new ExchangeWebResult<SharedBalance[]>(Exchange, validationError);

        var result = await Account.GetBalancesAsync(symbol: null, ct).ConfigureAwait(false);
        if (!result)
            return result.AsExchangeError<SharedBalance[]>(Exchange, result.Error!);

        return result.AsExchangeResult(Exchange, TradingMode.Spot, result.Data!
            .Select(balance => new SharedBalance(
                balance.Symbol,
                balance.Available ?? 0m,
                (balance.Available ?? 0m) + (balance.InOrder ?? 0m)))
            .ToArray(), null);
    }

    // ── ISpotOrderRestClient ──────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public SharedFeeDeductionType SpotFeeDeductionType => SharedFeeDeductionType.DeductFromOutput;

    /// <inheritdoc />
    public SharedFeeAssetType SpotFeeAssetType => SharedFeeAssetType.QuoteAsset;

    /// <inheritdoc />
    public SharedOrderType[] SpotSupportedOrderTypes { get; } = [SharedOrderType.Limit, SharedOrderType.Market, SharedOrderType.LimitMaker];

    /// <inheritdoc />
    public SharedTimeInForce[] SpotSupportedTimeInForce { get; } =
        [SharedTimeInForce.GoodTillCanceled, SharedTimeInForce.ImmediateOrCancel, SharedTimeInForce.FillOrKill];

    /// <inheritdoc />
    public SharedQuantitySupport SpotSupportedOrderQuantity { get; } = new(
        SharedQuantityType.BaseAsset,
        SharedQuantityType.BaseAsset,
        SharedQuantityType.BaseAndQuoteAsset,
        SharedQuantityType.BaseAndQuoteAsset);

    /// <inheritdoc />
    public string GenerateClientOrderId() => Guid.NewGuid().ToString();

    /// <inheritdoc />
    public PlaceSpotOrderOptions PlaceSpotOrderOptions { get; } = new()
    {
        RequiredExchangeParameters =
        [
            new ParameterDescription(
                nameof(BitvavoPlaceOrderRequest.OperatorId),
                typeof(long),
                "Account-scoped integer identifying the originator of the order request",
                1L),
        ],
    };

    /// <inheritdoc />
    public async Task<ExchangeWebResult<SharedId>> PlaceSpotOrderAsync(PlaceSpotOrderRequest request, CancellationToken ct = default)
    {
        var validationError = PlaceSpotOrderOptions.ValidateRequest(
            Exchange,
            request,
            TradingMode.Spot,
            SupportedTradingModes,
            SpotSupportedOrderTypes,
            SpotSupportedTimeInForce,
            SpotSupportedOrderQuantity);
        if (validationError != null)
            return new ExchangeWebResult<SharedId>(Exchange, validationError);

        var operatorId = ExchangeParameters.GetValue<long?>(request.ExchangeParameters, Exchange, nameof(BitvavoPlaceOrderRequest.OperatorId))
            ?? throw new ArgumentException($"{nameof(BitvavoPlaceOrderRequest.OperatorId)} exchange parameter is required for Bitvavo orders", nameof(request));

        var placeRequest = new BitvavoPlaceOrderRequest(
            request.Symbol!.GetSymbol(FormatSymbol),
            ToBitvavoOrderSide(request.Side),
            ToBitvavoOrderType(request.OrderType),
            operatorId,
            Amount: request.Quantity?.QuantityInBaseAsset,
            AmountQuote: request.Quantity?.QuantityInQuoteAsset,
            Price: request.Price,
            TimeInForce: request.TimeInForce == null ? null : ToBitvavoTimeInForce(request.TimeInForce.Value),
            PostOnly: request.OrderType == SharedOrderType.LimitMaker ? true : null,
            ClientOrderId: request.ClientOrderId);

        var result = await Trading.PlaceOrderAsync(placeRequest, ct).ConfigureAwait(false);
        if (!result)
            return result.AsExchangeError<SharedId>(Exchange, result.Error!);

        return result.AsExchangeResult(Exchange, TradingMode.Spot, new SharedId(result.Data!.OrderId), null);
    }

    /// <inheritdoc />
    public EndpointOptions<GetOrderRequest> GetSpotOrderOptions { get; } = new(true) { EndpointName = "GetOrderRequest" };

    /// <inheritdoc />
    public async Task<ExchangeWebResult<SharedSpotOrder>> GetSpotOrderAsync(GetOrderRequest request, CancellationToken ct = default)
    {
        var validationError = GetSpotOrderOptions.ValidateRequest(Exchange, request, TradingMode.Spot, SupportedTradingModes);
        if (validationError != null)
            return new ExchangeWebResult<SharedSpotOrder>(Exchange, validationError);

        var symbol = request.Symbol!.GetSymbol(FormatSymbol);
        var result = await Trading.GetOrderAsync(symbol, orderId: request.OrderId, ct: ct).ConfigureAwait(false);
        if (!result)
            return result.AsExchangeError<SharedSpotOrder>(Exchange, result.Error!);

        return result.AsExchangeResult(Exchange, TradingMode.Spot, ToSharedSpotOrder(request.Symbol, result.Data!), null);
    }

    /// <inheritdoc />
    public EndpointOptions<GetOpenOrdersRequest> GetOpenSpotOrdersOptions { get; } = new(true) { EndpointName = "GetOpenOrdersRequest" };

    /// <inheritdoc />
    public async Task<ExchangeWebResult<SharedSpotOrder[]>> GetOpenSpotOrdersAsync(GetOpenOrdersRequest request, CancellationToken ct = default)
    {
        var validationError = GetOpenSpotOrdersOptions.ValidateRequest(Exchange, request.ExchangeParameters, request.Symbol?.TradingMode, SupportedTradingModes);
        if (validationError != null)
            return new ExchangeWebResult<SharedSpotOrder[]>(Exchange, validationError);

        var symbol = request.Symbol?.GetSymbol(FormatSymbol);
        var result = await Trading.GetOpenOrdersAsync(market: symbol, ct: ct).ConfigureAwait(false);
        if (!result)
            return result.AsExchangeError<SharedSpotOrder[]>(Exchange, result.Error!);

        return result.AsExchangeResult(Exchange, TradingMode.Spot, result.Data!
            .Select(order => ToSharedSpotOrder(ParseSharedSymbol(order.Market), order))
            .ToArray(), null);
    }

    /// <inheritdoc />
    public GetClosedOrdersOptions GetClosedSpotOrdersOptions { get; } = new(
        supportsAscending: true,
        supportsDescending: true,
        timeFilterSupported: true,
        maxLimit: 1000);

    /// <inheritdoc />
    public async Task<ExchangeWebResult<SharedSpotOrder[]>> GetClosedSpotOrdersAsync(GetClosedOrdersRequest request, PageRequest? nextPageToken = null, CancellationToken ct = default)
    {
        var validationError = GetClosedSpotOrdersOptions.ValidateRequest(Exchange, request, TradingMode.Spot, SupportedTradingModes);
        if (validationError != null)
            return new ExchangeWebResult<SharedSpotOrder[]>(Exchange, validationError);

        var symbol = request.Symbol!.GetSymbol(FormatSymbol);
        var result = await Trading.GetOrderHistoryAsync(
            symbol,
            request.Limit,
            request.StartTime,
            request.EndTime,
            ct: ct).ConfigureAwait(false);
        if (!result)
            return result.AsExchangeError<SharedSpotOrder[]>(Exchange, result.Error!);

        // Filter to terminal states — Bitvavo's /v2/orders returns the full history,
        // the Shared "closed orders" contract is for non-open orders only.
        var closed = result.Data!
            .Where(order => ToSharedOrderStatus(order.Status) != SharedOrderStatus.Open)
            .Select(order => ToSharedSpotOrder(request.Symbol, order))
            .ToArray();
        return result.AsExchangeResult(Exchange, TradingMode.Spot, closed, null);
    }

    /// <inheritdoc />
    public EndpointOptions<GetOrderTradesRequest> GetSpotOrderTradesOptions { get; } = new(true) { EndpointName = "GetOrderTradesRequest" };

    /// <inheritdoc />
    public async Task<ExchangeWebResult<SharedUserTrade[]>> GetSpotOrderTradesAsync(GetOrderTradesRequest request, CancellationToken ct = default)
    {
        var validationError = GetSpotOrderTradesOptions.ValidateRequest(Exchange, request, TradingMode.Spot, SupportedTradingModes);
        if (validationError != null)
            return new ExchangeWebResult<SharedUserTrade[]>(Exchange, validationError);

        var symbol = request.Symbol!.GetSymbol(FormatSymbol);
        var result = await Trading.GetUserTradesAsync(symbol, ct: ct).ConfigureAwait(false);
        if (!result)
            return result.AsExchangeError<SharedUserTrade[]>(Exchange, result.Error!);

        var trades = result.Data!
            .Where(fill => fill.OrderId == request.OrderId)
            .Select(fill => ToSharedUserTrade(request.Symbol, symbol, fill))
            .ToArray();
        return result.AsExchangeResult(Exchange, TradingMode.Spot, trades, null);
    }

    /// <inheritdoc />
    public GetUserTradesOptions GetSpotUserTradesOptions { get; } = new(
        supportsAscending: true,
        supportsDescending: true,
        timeFilterSupported: true,
        maxLimit: 1000);

    /// <inheritdoc />
    public async Task<ExchangeWebResult<SharedUserTrade[]>> GetSpotUserTradesAsync(GetUserTradesRequest request, PageRequest? nextPageToken = null, CancellationToken ct = default)
    {
        var validationError = GetSpotUserTradesOptions.ValidateRequest(Exchange, request, TradingMode.Spot, SupportedTradingModes);
        if (validationError != null)
            return new ExchangeWebResult<SharedUserTrade[]>(Exchange, validationError);

        var symbol = request.Symbol!.GetSymbol(FormatSymbol);
        var result = await Trading.GetUserTradesAsync(
            symbol,
            request.Limit,
            request.StartTime,
            request.EndTime,
            ct: ct).ConfigureAwait(false);
        if (!result)
            return result.AsExchangeError<SharedUserTrade[]>(Exchange, result.Error!);

        return result.AsExchangeResult(Exchange, TradingMode.Spot, result.Data!
            .Select(fill => ToSharedUserTrade(request.Symbol, symbol, fill))
            .ToArray(), null);
    }

    /// <inheritdoc />
    public EndpointOptions<CancelOrderRequest> CancelSpotOrderOptions { get; } = new(true)
    {
        EndpointName = "CancelOrderRequest",
        RequiredExchangeParameters =
        [
            new ParameterDescription(
                nameof(BitvavoPlaceOrderRequest.OperatorId),
                typeof(long),
                "Account-scoped integer identifying the originator of the cancel request",
                1L),
        ],
    };

    /// <inheritdoc />
    public async Task<ExchangeWebResult<SharedId>> CancelSpotOrderAsync(CancelOrderRequest request, CancellationToken ct = default)
    {
        var validationError = CancelSpotOrderOptions.ValidateRequest(Exchange, request, TradingMode.Spot, SupportedTradingModes);
        if (validationError != null)
            return new ExchangeWebResult<SharedId>(Exchange, validationError);

        var operatorId = ExchangeParameters.GetValue<long?>(request.ExchangeParameters, Exchange, nameof(BitvavoPlaceOrderRequest.OperatorId))
            ?? throw new ArgumentException($"{nameof(BitvavoPlaceOrderRequest.OperatorId)} exchange parameter is required for Bitvavo order cancellation", nameof(request));

        var symbol = request.Symbol!.GetSymbol(FormatSymbol);
        var result = await Trading.CancelOrderAsync(symbol, operatorId, orderId: request.OrderId, ct: ct).ConfigureAwait(false);
        if (!result)
            return result.AsExchangeError<SharedId>(Exchange, result.Error!);

        return result.AsExchangeResult(Exchange, TradingMode.Spot, new SharedId(result.Data!.OrderId), null);
    }

    // ── ISpotOrderClientIdRestClient ──────────────────────────────────────────────────────

    /// <inheritdoc />
    public EndpointOptions<GetOrderRequest> GetSpotOrderByClientOrderIdOptions { get; } = new(true) { EndpointName = "GetOrderByClientOrderIdRequest" };

    /// <inheritdoc />
    public async Task<ExchangeWebResult<SharedSpotOrder>> GetSpotOrderByClientOrderIdAsync(GetOrderRequest request, CancellationToken ct = default)
    {
        var validationError = GetSpotOrderByClientOrderIdOptions.ValidateRequest(Exchange, request, TradingMode.Spot, SupportedTradingModes);
        if (validationError != null)
            return new ExchangeWebResult<SharedSpotOrder>(Exchange, validationError);

        var symbol = request.Symbol!.GetSymbol(FormatSymbol);
        var result = await Trading.GetOrderAsync(symbol, clientOrderId: request.OrderId, ct: ct).ConfigureAwait(false);
        if (!result)
            return result.AsExchangeError<SharedSpotOrder>(Exchange, result.Error!);

        return result.AsExchangeResult(Exchange, TradingMode.Spot, ToSharedSpotOrder(request.Symbol, result.Data!), null);
    }

    /// <inheritdoc />
    public EndpointOptions<CancelOrderRequest> CancelSpotOrderByClientOrderIdOptions { get; } = new(true)
    {
        EndpointName = "CancelOrderByClientOrderIdRequest",
        RequiredExchangeParameters =
        [
            new ParameterDescription(
                nameof(BitvavoPlaceOrderRequest.OperatorId),
                typeof(long),
                "Account-scoped integer identifying the originator of the cancel request",
                1L),
        ],
    };

    /// <inheritdoc />
    public async Task<ExchangeWebResult<SharedId>> CancelSpotOrderByClientOrderIdAsync(CancelOrderRequest request, CancellationToken ct = default)
    {
        var validationError = CancelSpotOrderByClientOrderIdOptions.ValidateRequest(Exchange, request, TradingMode.Spot, SupportedTradingModes);
        if (validationError != null)
            return new ExchangeWebResult<SharedId>(Exchange, validationError);

        var operatorId = ExchangeParameters.GetValue<long?>(request.ExchangeParameters, Exchange, nameof(BitvavoPlaceOrderRequest.OperatorId))
            ?? throw new ArgumentException($"{nameof(BitvavoPlaceOrderRequest.OperatorId)} exchange parameter is required for Bitvavo order cancellation", nameof(request));

        var symbol = request.Symbol!.GetSymbol(FormatSymbol);
        var result = await Trading.CancelOrderAsync(symbol, operatorId, clientOrderId: request.OrderId, ct: ct).ConfigureAwait(false);
        if (!result)
            return result.AsExchangeError<SharedId>(Exchange, result.Error!);

        return result.AsExchangeResult(Exchange, TradingMode.Spot, new SharedId(result.Data!.OrderId), null);
    }

    private SharedSpotOrder ToSharedSpotOrder(SharedSymbol sharedSymbol, BitvavoOrder order)
    {
        return new SharedSpotOrder(
            sharedSymbol,
            order.Market,
            order.OrderId,
            ToSharedOrderType(order.OrderType),
            ToSharedOrderSide(order.Side),
            ToSharedOrderStatus(order.Status),
            order.Created)
        {
            ClientOrderId = order.ClientOrderId,
            OrderPrice = order.Price,
            OrderQuantity = new SharedOrderQuantity(order.Amount, order.AmountQuote),
            QuantityFilled = new SharedOrderQuantity(order.FilledAmount, order.FilledAmountQuote),
            AveragePrice = order.FilledAmount is > 0m && order.FilledAmountQuote != null
                ? order.FilledAmountQuote / order.FilledAmount
                : null,
            TimeInForce = order.TimeInForce == null ? null : ToSharedTimeInForce(order.TimeInForce.Value),
            UpdateTime = order.Updated,
            Fee = order.FeePaid,
            FeeAsset = order.FeeCurrency,
            TriggerPrice = order.TriggerAmount,
            IsTriggerOrder = order.TriggerAmount != null,
        };
    }

    private static SharedUserTrade ToSharedUserTrade(SharedSymbol sharedSymbol, string symbol, BitvavoFill fill)
    {
        return new SharedUserTrade(
            sharedSymbol,
            symbol,
            fill.OrderId ?? string.Empty,
            fill.Id,
            ToSharedOrderSide(fill.Side),
            fill.Amount ?? 0m,
            fill.Price ?? 0m,
            fill.Timestamp)
        {
            ClientOrderId = fill.ClientOrderId,
            Fee = fill.Fee,
            FeeAsset = fill.FeeCurrency,
            Role = fill.Taker switch
            {
                true => SharedRole.Taker,
                false => SharedRole.Maker,
                null => null,
            },
        };
    }

    // ── IFeeRestClient ────────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public EndpointOptions<GetFeeRequest> GetFeeOptions { get; } = new(true) { EndpointName = "GetFeeRequest" };

    /// <inheritdoc />
    public async Task<ExchangeWebResult<SharedFee>> GetFeesAsync(GetFeeRequest request, CancellationToken ct = default)
    {
        var validationError = GetFeeOptions.ValidateRequest(Exchange, request, TradingMode.Spot, SupportedTradingModes);
        if (validationError != null)
            return new ExchangeWebResult<SharedFee>(Exchange, validationError);

        var symbol = request.Symbol!.GetSymbol(FormatSymbol);
        var result = await Account.GetTradingFeesAsync(symbol, ct).ConfigureAwait(false);
        if (!result)
            return result.AsExchangeError<SharedFee>(Exchange, result.Error!);

        return result.AsExchangeResult(Exchange, TradingMode.Spot, new SharedFee(
            (result.Data!.Maker ?? 0m) * 100m,
            (result.Data!.Taker ?? 0m) * 100m), null);
    }

    // ── IDepositRestClient ────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public EndpointOptions<GetDepositAddressesRequest> GetDepositAddressesOptions { get; } = new(true) { EndpointName = "GetDepositAddressesRequest" };

    /// <inheritdoc />
    public async Task<ExchangeWebResult<SharedDepositAddress[]>> GetDepositAddressesAsync(GetDepositAddressesRequest request, CancellationToken ct = default)
    {
        var validationError = GetDepositAddressesOptions.ValidateRequest(Exchange, request.ExchangeParameters, TradingMode.Spot, SupportedTradingModes);
        if (validationError != null)
            return new ExchangeWebResult<SharedDepositAddress[]>(Exchange, validationError);

        var result = await Funding.GetDepositAddressAsync(request.Asset, ct).ConfigureAwait(false);
        if (!result)
            return result.AsExchangeError<SharedDepositAddress[]>(Exchange, result.Error!);

        if (result.Data!.Address == null)
            return result.AsExchangeResult(Exchange, TradingMode.Spot, Array.Empty<SharedDepositAddress>(), null);

        SharedDepositAddress[] addresses =
        [
            new SharedDepositAddress(request.Asset, result.Data!.Address)
            {
                Network = request.Network,
                TagOrMemo = result.Data!.PaymentReference,
            },
        ];
        return result.AsExchangeResult(Exchange, TradingMode.Spot, addresses, null);
    }

    /// <inheritdoc />
    public GetDepositsOptions GetDepositsOptions { get; } = new(
        supportsAscending: true,
        supportsDescending: true,
        timeFilterSupported: true,
        maxLimit: 1000);

    /// <inheritdoc />
    public async Task<ExchangeWebResult<SharedDeposit[]>> GetDepositsAsync(GetDepositsRequest request, PageRequest? nextPageToken = null, CancellationToken ct = default)
    {
        var validationError = GetDepositsOptions.ValidateRequest(Exchange, request.ExchangeParameters, TradingMode.Spot, SupportedTradingModes);
        if (validationError != null)
            return new ExchangeWebResult<SharedDeposit[]>(Exchange, validationError);

        var result = await Funding.GetDepositHistoryAsync(
            request.Asset,
            request.Limit,
            request.StartTime,
            request.EndTime,
            ct).ConfigureAwait(false);
        if (!result)
            return result.AsExchangeError<SharedDeposit[]>(Exchange, result.Error!);

        return result.AsExchangeResult(Exchange, TradingMode.Spot, result.Data!
            .Select(deposit =>
            {
                var completed = string.Equals(deposit.Status, "completed", StringComparison.OrdinalIgnoreCase);
                return new SharedDeposit(
                    deposit.Symbol,
                    deposit.Amount ?? 0m,
                    completed,
                    deposit.Timestamp,
                    ToSharedTransferStatus(deposit.Status))
                {
                    TransactionId = deposit.TxId,
                    Tag = deposit.PaymentReference,
                };
            })
            .ToArray(), null);
    }

    // ── IWithdrawalRestClient ─────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public GetWithdrawalsOptions GetWithdrawalsOptions { get; } = new(
        supportsAscending: true,
        supportsDescending: true,
        timeFilterSupported: true,
        maxLimit: 1000);

    /// <inheritdoc />
    public async Task<ExchangeWebResult<SharedWithdrawal[]>> GetWithdrawalsAsync(GetWithdrawalsRequest request, PageRequest? nextPageToken = null, CancellationToken ct = default)
    {
        var validationError = GetWithdrawalsOptions.ValidateRequest(Exchange, request.ExchangeParameters, TradingMode.Spot, SupportedTradingModes);
        if (validationError != null)
            return new ExchangeWebResult<SharedWithdrawal[]>(Exchange, validationError);

        var result = await Funding.GetWithdrawalHistoryAsync(
            request.Asset,
            request.Limit,
            request.StartTime,
            request.EndTime,
            ct).ConfigureAwait(false);
        if (!result)
            return result.AsExchangeError<SharedWithdrawal[]>(Exchange, result.Error!);

        return result.AsExchangeResult(Exchange, TradingMode.Spot, result.Data!
            .Select(withdrawal => new SharedWithdrawal(
                withdrawal.Symbol,
                withdrawal.Address ?? string.Empty,
                withdrawal.Amount ?? 0m,
                string.Equals(withdrawal.Status, "completed", StringComparison.OrdinalIgnoreCase),
                withdrawal.Timestamp)
            {
                TransactionId = withdrawal.TxId,
                Tag = withdrawal.PaymentReference,
                Fee = withdrawal.Fee,
            })
            .ToArray(), null);
    }

    // ── IWithdrawRestClient ───────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public WithdrawOptions WithdrawOptions { get; } = new();

    /// <inheritdoc />
    public async Task<ExchangeWebResult<SharedId>> WithdrawAsync(WithdrawRequest request, CancellationToken ct = default)
    {
        var validationError = WithdrawOptions.ValidateRequest(Exchange, request.ExchangeParameters, TradingMode.Spot, SupportedTradingModes);
        if (validationError != null)
            return new ExchangeWebResult<SharedId>(Exchange, validationError);

        var withdrawRequest = new BitvavoWithdrawRequest(
            request.Asset,
            request.Quantity,
            request.Address,
            PaymentId: request.AddressTag);

        var result = await Funding.WithdrawAsync(withdrawRequest, ct).ConfigureAwait(false);
        if (!result)
            return result.AsExchangeError<SharedId>(Exchange, result.Error!);

        // Bitvavo's withdrawal endpoint returns no id — echo the requested asset symbol
        // so the Shared contract's non-null SharedId is satisfied.
        return result.AsExchangeResult(Exchange, TradingMode.Spot, new SharedId(result.Data!.Symbol), null);
    }

    // ── Shared helpers ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Parse a Bitvavo wire symbol (<c>BASE-QUOTE</c>) back into a <see cref="SharedSymbol"/>.
    /// Used when the typed DTO carries only the joined market string (ticker / order list
    /// responses) and the Shared model needs the split base / quote.
    /// </summary>
    private static SharedSymbol ParseSharedSymbol(string market)
    {
        var dash = market.IndexOf('-');
        return dash > 0
            ? new SharedSymbol(TradingMode.Spot, market[..dash], market[(dash + 1)..], market)
            : new SharedSymbol(TradingMode.Spot, market, string.Empty, market);
    }

    /// <summary>
    /// Map a CryptoExchange.Net <see cref="SharedKlineInterval"/> onto Bitvavo's
    /// <see cref="KlineInterval"/>. The two enums do not share underlying integer values
    /// (<see cref="SharedKlineInterval"/> is interval-seconds, <see cref="KlineInterval"/>
    /// is ordinal) so an explicit map is mandatory — a direct cast would silently corrupt.
    /// <see cref="SharedKlineInterval.ThreeMinutes"/> has no Bitvavo equivalent; callers are
    /// guarded by <see cref="GetKlinesOptions"/> validation before this mapper runs.
    /// </summary>
    private static KlineInterval ToBitvavoKlineInterval(SharedKlineInterval interval) => interval switch
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

    private static decimal? ParseDecimal(string value)
        => decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static int? ParseInt(string value)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;

    private static SharedOrderSide ToSharedOrderSide(OrderSide side) => side switch
    {
        OrderSide.Buy => SharedOrderSide.Buy,
        OrderSide.Sell => SharedOrderSide.Sell,
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Unmapped Bitvavo OrderSide"),
    };

    private static SharedOrderSide? ToSharedOrderSide(OrderSide? side) => side == null ? null : ToSharedOrderSide(side.Value);

    private static OrderSide ToBitvavoOrderSide(SharedOrderSide side) => side switch
    {
        SharedOrderSide.Buy => OrderSide.Buy,
        SharedOrderSide.Sell => OrderSide.Sell,
        _ => throw new ArgumentOutOfRangeException(nameof(side), side, "Unmapped SharedOrderSide"),
    };

    private static SharedOrderType ToSharedOrderType(OrderType type) => type switch
    {
        OrderType.Market => SharedOrderType.Market,
        OrderType.Limit => SharedOrderType.Limit,
        OrderType.StopLoss => SharedOrderType.Other,
        OrderType.StopLossLimit => SharedOrderType.Other,
        OrderType.TakeProfit => SharedOrderType.Other,
        OrderType.TakeProfitLimit => SharedOrderType.Other,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unmapped Bitvavo OrderType"),
    };

    private static OrderType ToBitvavoOrderType(SharedOrderType type) => type switch
    {
        SharedOrderType.Market => OrderType.Market,
        SharedOrderType.Limit => OrderType.Limit,
        SharedOrderType.LimitMaker => OrderType.Limit,
        SharedOrderType.Other => throw new ArgumentException("SharedOrderType.Other cannot be placed on Bitvavo — use a concrete order type", nameof(type)),
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unmapped SharedOrderType"),
    };

    private static SharedOrderStatus ToSharedOrderStatus(OrderStatus status) => status switch
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

    private static SharedTimeInForce ToSharedTimeInForce(TimeInForce timeInForce) => timeInForce switch
    {
        TimeInForce.GoodTillCanceled => SharedTimeInForce.GoodTillCanceled,
        TimeInForce.ImmediateOrCancel => SharedTimeInForce.ImmediateOrCancel,
        TimeInForce.FillOrKill => SharedTimeInForce.FillOrKill,
        _ => throw new ArgumentOutOfRangeException(nameof(timeInForce), timeInForce, "Unmapped Bitvavo TimeInForce"),
    };

    private static TimeInForce ToBitvavoTimeInForce(SharedTimeInForce timeInForce) => timeInForce switch
    {
        SharedTimeInForce.GoodTillCanceled => TimeInForce.GoodTillCanceled,
        SharedTimeInForce.ImmediateOrCancel => TimeInForce.ImmediateOrCancel,
        SharedTimeInForce.FillOrKill => TimeInForce.FillOrKill,
        _ => throw new ArgumentOutOfRangeException(nameof(timeInForce), timeInForce, "Unmapped SharedTimeInForce"),
    };

    private static SharedTransferStatus ToSharedTransferStatus(string status) => status.ToLowerInvariant() switch
    {
        "completed" => SharedTransferStatus.Completed,
        "awaiting_processing" => SharedTransferStatus.InProgress,
        "awaiting_email_confirmation" => SharedTransferStatus.InProgress,
        "awaiting_bitvavo_inspection" => SharedTransferStatus.InProgress,
        "in_mempool" => SharedTransferStatus.InProgress,
        "processing" => SharedTransferStatus.InProgress,
        "canceled" => SharedTransferStatus.Failed,
        "rejected" => SharedTransferStatus.Failed,
        _ => SharedTransferStatus.Unknown,
    };
}
