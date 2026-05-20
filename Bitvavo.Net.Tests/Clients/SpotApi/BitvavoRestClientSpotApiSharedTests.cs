// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Bitvavo.Net.Clients;
using Bitvavo.Net.Interfaces.Clients.SpotApi;
using Bitvavo.Net.Objects.Options;
using CryptoExchange.Net.SharedApis;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests.Clients.SpotApi;

/// <summary>
/// Round-trip tests for the CryptoExchange.Net Shared-API surface implemented by the
/// facade-hosted partial <c>BitvavoRestClientSpotApi.Shared.cs</c>. One test per Shared
/// REST interface: feeds a canned Bitvavo JSON envelope through the real request pipeline
/// and asserts the typed DTO is mapped onto the exchange-agnostic Shared model.
/// </summary>
public class BitvavoRestClientSpotApiSharedTests
{
    private static IBitvavoRestClientSpotApiShared SharedReturning(string json, out StubHttpMessageHandler handler)
    {
        handler = new StubHttpMessageHandler(json);
        var http = new HttpClient(handler);
        var opts = new BitvavoRestOptions
        {
            ApiCredentials = new BitvavoCredentials("test-key", "test-secret"),
        };
        var client = new BitvavoRestClient(http, null, Options.Create(opts));
        return client.SpotApi.SharedClient;
    }

    private static SharedSymbol EthEur => new(TradingMode.Spot, "ETH", "EUR", "ETH-EUR");

    [Fact]
    public void SharedClient_reports_Bitvavo_exchange_and_spot_only_trading_mode()
    {
        var shared = SharedReturning("{}", out _);

        shared.Exchange.ShouldBe("Bitvavo");
        shared.SupportedTradingModes.ShouldBe(new[] { TradingMode.Spot });
    }

    // ── IAssetsRestClient ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task IAssetsRestClient_GetAssetAsync_maps_single_asset()
    {
        var shared = SharedReturning(
            """[{"symbol":"BTC","name":"Bitcoin","decimals":8,"depositConfirmations":2,"depositStatus":"OK","withdrawalFee":"0.0002","withdrawalMinAmount":"0.001","withdrawalStatus":"OK","networks":["Mainnet"]}]""",
            out _);

        var result = await shared.GetAssetAsync(new GetAssetRequest("BTC"), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Name.ShouldBe("BTC");
        result.Data.FullName.ShouldBe("Bitcoin");
        result.Data.Networks.ShouldHaveSingleItem().WithdrawFee.ShouldBe(0.0002m);
    }

    [Fact]
    public async Task IAssetsRestClient_GetAssetsAsync_maps_asset_array()
    {
        var shared = SharedReturning(
            """[{"symbol":"BTC","name":"Bitcoin","decimals":8,"depositConfirmations":2,"depositStatus":"OK","withdrawalStatus":"OK","networks":["Mainnet"]},{"symbol":"ETH","name":"Ethereum","decimals":18,"depositConfirmations":12,"depositStatus":"OK","withdrawalStatus":"OK","networks":["Mainnet"]}]""",
            out _);

        var result = await shared.GetAssetsAsync(new GetAssetsRequest(), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Length.ShouldBe(2);
        result.Data[0].Name.ShouldBe("BTC");
        result.Data[1].Name.ShouldBe("ETH");
    }

    // ── IKlineRestClient ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task IKlineRestClient_GetKlinesAsync_maps_candle_array()
    {
        var shared = SharedReturning(
            """[[1714132800000,"3000","3100","2950","3050","12.5"]]""",
            out var handler);

        var result = await shared.GetKlinesAsync(
            new GetKlinesRequest(EthEur, SharedKlineInterval.OneHour),
            ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        var kline = result.Data.ShouldHaveSingleItem();
        kline.OpenPrice.ShouldBe(3000m);
        kline.HighPrice.ShouldBe(3100m);
        kline.ClosePrice.ShouldBe(3050m);
        kline.Volume.ShouldBe(12.5m);
        handler.Requests[0].RequestUri!.Query.ShouldContain("interval=1h");
    }

    // ── IRecentTradeRestClient ────────────────────────────────────────────────────────────

    [Fact]
    public async Task IRecentTradeRestClient_GetRecentTradesAsync_maps_trade_array()
    {
        var shared = SharedReturning(
            """[{"id":"t1","timestamp":1714132800000,"amount":"0.5","price":"3000","side":"buy"}]""",
            out _);

        var result = await shared.GetRecentTradesAsync(
            new GetRecentTradesRequest(EthEur, 10),
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        var trade = result.Data.ShouldHaveSingleItem();
        trade.Price.ShouldBe(3000m);
        trade.Quantity.ShouldBe(0.5m);
        trade.Side.ShouldBe(SharedOrderSide.Buy);
    }

    // ── IOrderBookRestClient ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task IOrderBookRestClient_GetOrderBookAsync_maps_bids_and_asks()
    {
        var shared = SharedReturning(
            """{"market":"ETH-EUR","nonce":42,"bids":[["2990","1.0"]],"asks":[["3010","2.0"]]}""",
            out _);

        var result = await shared.GetOrderBookAsync(
            new GetOrderBookRequest(EthEur, 100),
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Bids.ShouldHaveSingleItem().Price.ShouldBe(2990m);
        result.Data.Asks.ShouldHaveSingleItem().Price.ShouldBe(3010m);
        result.Data.Asks[0].Quantity.ShouldBe(2.0m);
    }

    // ── ISpotSymbolRestClient ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ISpotSymbolRestClient_GetSpotSymbolsAsync_maps_market_array()
    {
        var shared = SharedReturning(
            """[{"market":"ETH-EUR","status":"trading","base":"ETH","quote":"EUR","pricePrecision":"5","minOrderInBaseAsset":"0.001","minOrderInQuoteAsset":"5","maxOrderInBaseAsset":"1000000","maxOrderInQuoteAsset":"9000000","orderTypes":["limit","market"]}]""",
            out _);

        var result = await shared.GetSpotSymbolsAsync(new GetSymbolsRequest(TradingMode.Spot), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        var symbol = result.Data.ShouldHaveSingleItem();
        symbol.BaseAsset.ShouldBe("ETH");
        symbol.QuoteAsset.ShouldBe("EUR");
        symbol.Name.ShouldBe("ETH-EUR");
        symbol.Trading.ShouldBeTrue();
        symbol.MinTradeQuantity.ShouldBe(0.001m);
    }

    // ── ISpotTickerRestClient ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task ISpotTickerRestClient_GetSpotTickerAsync_maps_24h_ticker()
    {
        var shared = SharedReturning(
            """[{"market":"ETH-EUR","open":"3000","high":"3100","low":"2900","last":"3060","volume":"500","volumeQuote":"1500000"}]""",
            out _);

        var result = await shared.GetSpotTickerAsync(new GetTickerRequest(EthEur), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.LastPrice.ShouldBe(3060m);
        result.Data.HighPrice.ShouldBe(3100m);
        result.Data.Volume.ShouldBe(500m);
        result.Data.ChangePercentage!.Value.ShouldBe(2m, 0.0001m);
    }

    [Fact]
    public async Task ISpotTickerRestClient_GetSpotTickersAsync_maps_ticker_array()
    {
        var shared = SharedReturning(
            """[{"market":"ETH-EUR","open":"3000","high":"3100","low":"2900","last":"3060","volume":"500"},{"market":"BTC-EUR","open":"60000","high":"61000","low":"59000","last":"60500","volume":"30"}]""",
            out _);

        var result = await shared.GetSpotTickersAsync(new GetTickersRequest(TradingMode.Spot), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Length.ShouldBe(2);
        result.Data[1].SharedSymbol!.BaseAsset.ShouldBe("BTC");
    }

    // ── IBookTickerRestClient ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task IBookTickerRestClient_GetBookTickerAsync_maps_top_of_book()
    {
        var shared = SharedReturning(
            """[{"market":"ETH-EUR","bid":"2999","bidSize":"1.5","ask":"3001","askSize":"2.5"}]""",
            out _);

        var result = await shared.GetBookTickerAsync(new GetBookTickerRequest(EthEur), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.BestBidPrice.ShouldBe(2999m);
        result.Data.BestAskPrice.ShouldBe(3001m);
        result.Data.BestAskQuantity.ShouldBe(2.5m);
    }

    // ── IBalanceRestClient ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task IBalanceRestClient_GetBalancesAsync_maps_balance_array()
    {
        var shared = SharedReturning(
            """[{"symbol":"EUR","available":"1000","inOrder":"250"},{"symbol":"ETH","available":"2","inOrder":"0"}]""",
            out _);

        var result = await shared.GetBalancesAsync(new GetBalancesRequest(SharedAccountType.Spot), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Length.ShouldBe(2);
        var eur = result.Data[0];
        eur.Asset.ShouldBe("EUR");
        eur.Available.ShouldBe(1000m);
        eur.Total.ShouldBe(1250m);
    }

    // ── ISpotOrderRestClient ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ISpotOrderRestClient_PlaceSpotOrderAsync_maps_order_id()
    {
        var shared = SharedReturning(
            """{"orderId":"abc-123","market":"ETH-EUR","status":"new","side":"buy","orderType":"limit","created":0,"updated":0}""",
            out _);

        var request = new PlaceSpotOrderRequest(
            EthEur,
            SharedOrderSide.Buy,
            SharedOrderType.Limit,
            quantity: new SharedQuantity { QuantityInBaseAsset = 0.5m },
            price: 3000m)
        {
            ExchangeParameters = new ExchangeParameters(
                new ExchangeParameter("Bitvavo", "OperatorId", 1L)),
        };

        var result = await shared.PlaceSpotOrderAsync(request, TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Id.ShouldBe("abc-123");
    }

    [Fact]
    public async Task ISpotOrderRestClient_GetSpotOrderAsync_maps_order()
    {
        var shared = SharedReturning(
            """{"orderId":"abc-123","market":"ETH-EUR","status":"filled","side":"buy","orderType":"limit","amount":"0.5","filledAmount":"0.5","price":"3000","created":1714132800000,"updated":1714132900000}""",
            out _);

        var result = await shared.GetSpotOrderAsync(new GetOrderRequest(EthEur, "abc-123"), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.OrderId.ShouldBe("abc-123");
        result.Data.Status.ShouldBe(SharedOrderStatus.Filled);
        result.Data.Side.ShouldBe(SharedOrderSide.Buy);
    }

    [Fact]
    public async Task ISpotOrderRestClient_CancelSpotOrderAsync_maps_cancelled_id()
    {
        var shared = SharedReturning("""{"orderId":"abc-123","market":"ETH-EUR"}""", out _);

        var request = new CancelOrderRequest(EthEur, "abc-123")
        {
            ExchangeParameters = new ExchangeParameters(new ExchangeParameter("Bitvavo", "OperatorId", 1L)),
        };

        var result = await shared.CancelSpotOrderAsync(request, TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Id.ShouldBe("abc-123");
    }

    // ── ISpotOrderClientIdRestClient ──────────────────────────────────────────────────────

    [Fact]
    public async Task ISpotOrderClientIdRestClient_GetSpotOrderByClientOrderIdAsync_passes_clientOrderId_query()
    {
        var shared = SharedReturning(
            """{"orderId":"abc-123","clientOrderId":"client-1","market":"ETH-EUR","status":"new","side":"buy","orderType":"limit","created":0,"updated":0}""",
            out var handler);

        var result = await shared.GetSpotOrderByClientOrderIdAsync(new GetOrderRequest(EthEur, "client-1"), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.ClientOrderId.ShouldBe("client-1");
        handler.Requests[0].RequestUri!.Query.ShouldContain("clientOrderId=client-1");
    }

    // ── IFeeRestClient ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task IFeeRestClient_GetFeesAsync_maps_fee_rates_to_percentage()
    {
        var shared = SharedReturning("""{"tier":"0","volume":"0","taker":"0.0025","maker":"0.0015"}""", out _);

        var result = await shared.GetFeesAsync(new GetFeeRequest(EthEur), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.MakerFee.ShouldBe(0.15m);
        result.Data.TakerFee.ShouldBe(0.25m);
    }

    // ── IDepositRestClient ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task IDepositRestClient_GetDepositAddressesAsync_maps_crypto_address()
    {
        var shared = SharedReturning("""{"address":"bc1qxyz","paymentId":"memo-1"}""", out _);

        var result = await shared.GetDepositAddressesAsync(new GetDepositAddressesRequest("BTC"), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.ShouldHaveSingleItem().Address.ShouldBe("bc1qxyz");
        result.Data[0].TagOrMemo.ShouldBe("memo-1");
    }

    [Fact]
    public async Task IDepositRestClient_GetDepositsAsync_maps_deposit_history()
    {
        var shared = SharedReturning(
            """[{"timestamp":1714132800000,"symbol":"BTC","amount":"0.5","txId":"tx-1","status":"completed"}]""",
            out _);

        var result = await shared.GetDepositsAsync(new GetDepositsRequest("BTC"), ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        var deposit = result.Data.ShouldHaveSingleItem();
        deposit.Asset.ShouldBe("BTC");
        deposit.Quantity.ShouldBe(0.5m);
        deposit.Completed.ShouldBeTrue();
        deposit.Status.ShouldBe(SharedTransferStatus.Completed);
    }

    // ── IWithdrawalRestClient ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task IWithdrawalRestClient_GetWithdrawalsAsync_maps_withdrawal_history()
    {
        var shared = SharedReturning(
            """[{"timestamp":1714132800000,"symbol":"BTC","amount":"0.25","address":"bc1qabc","txId":"tx-9","fee":"0.0002","status":"completed"}]""",
            out _);

        var result = await shared.GetWithdrawalsAsync(new GetWithdrawalsRequest("BTC"), ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        var withdrawal = result.Data.ShouldHaveSingleItem();
        withdrawal.Asset.ShouldBe("BTC");
        withdrawal.Quantity.ShouldBe(0.25m);
        withdrawal.Address.ShouldBe("bc1qabc");
        withdrawal.Fee.ShouldBe(0.0002m);
    }

    // ── IWithdrawRestClient ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task IWithdrawRestClient_WithdrawAsync_dispatches_and_returns_id()
    {
        var shared = SharedReturning("""{"success":true,"symbol":"BTC","amount":"0.1"}""", out var handler);

        var result = await shared.WithdrawAsync(new WithdrawRequest("BTC", 0.1m, "bc1qdest"), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Id.ShouldBe("BTC");
        handler.Requests[0].Method.ShouldBe(HttpMethod.Post);
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/withdrawal");
    }
}
