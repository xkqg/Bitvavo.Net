// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bitvavo.Net.Clients;
using Bitvavo.Net.Objects.Options;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Sockets;
using CryptoExchange.Net.SharedApis;
using CryptoExchange.Net.Testing;
using CryptoExchange.Net.Testing.Implementations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests.Clients.SpotApi.SharedApi;

/// <summary>
/// The Shared subscription capabilities on the WebSocket API ([V2] <c>ISubscribe…Socket</c> and the legacy [V1] interfaces on one
/// instance): klines, public trades, and the two private account-channel subscriptions (orders and own trades). Driven through the
/// real subscription machinery; only the socket itself is the framework's test socket, fed Bitvavo-shaped frames.
/// </summary>
public class BitvavoSocketSharedApiTests
{
    private const string Address = "wss://ws.bitvavo.com/v2/";

    private sealed class Wire
    {
        public BitvavoSocketClient Client { get; }
        public TestSocket Socket { get; }
        public List<string> Frames { get; } = [];

        public Wire(bool withCredentials)
        {
            var options = new BitvavoSocketOptions();
            if (withCredentials)
            {
                options.ApiCredentials = new BitvavoCredentials("test-key", "test-secret");
            }

            Client = new BitvavoSocketClient(new LoggerFactory(), Options.Create(options));
            Socket = TestHelpers.ConfigureSocketClient(Client, Address);
            Socket.OnMessageSend += frame =>
            {
                Frames.Add(frame);
                if (frame.Contains("\"action\":\"authenticate\""))
                {
                    Socket.InvokeMessage("{\"event\":\"authenticate\",\"authenticated\":true}");
                }
            };
        }
    }

    private static SharedSymbol EthEur => new(TradingMode.Spot, "ETH", "EUR");

    private static ExchangeParameters Markets(params string[] markets) => new(new ExchangeParameter("Bitvavo", "Markets", markets));

    // ── klines ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Kline_subscription_sends_the_subscribe_frame_and_each_candle_arrives_as_a_shared_kline()
    {
        var wire = new Wire(withCredentials: false);
        var klines = new List<DataEvent<SharedKline>>();

        var result = await wire.Client.SpotApi.SharedApi.SubscribeToKlineUpdatesAsync(
            new SubscribeKlineRequest(EthEur, SharedKlineInterval.OneHour), klines.Add, TestContext.Current.CancellationToken);
        wire.Socket.InvokeMessage("""{"event":"candle","market":"ETH-EUR","interval":"1h","candle":[[1714132800000,"3000","3100","2950","3050","12.5"],[1714129200000,"2990","3010","2980","3000","4"]]}""");

        result.Success.ShouldBeTrue();
        var frame = wire.Frames.ShouldHaveSingleItem();
        frame.ShouldContain("\"action\":\"subscribe\"");
        frame.ShouldContain("\"name\":\"candles\"");
        frame.ShouldContain("\"interval\":[\"1h\"]");
        frame.ShouldContain("\"markets\":[\"ETH-EUR\"]");
        klines.Count.ShouldBe(2);
        var first = klines[0].Data;
        first.OpenTime.ShouldBe(new DateTime(2024, 4, 26, 12, 0, 0, DateTimeKind.Utc));
        first.OpenPrice.ShouldBe(3000m);
        first.HighPrice.ShouldBe(3100m);
        first.LowPrice.ShouldBe(2950m);
        first.ClosePrice.ShouldBe(3050m);
        first.Volumes.QuantityInBaseAsset.ShouldBe(12.5m);
        first.Symbol.ShouldBe("ETH-EUR");
        first.SharedSymbol!.BaseAsset.ShouldBe("ETH");
    }

    [Fact]
    public async Task A_kline_interval_Bitvavo_does_not_have_is_rejected_and_nothing_is_sent()
    {
        var wire = new Wire(withCredentials: false);

        var result = await wire.Client.SpotApi.SharedApi.SubscribeToKlineUpdatesAsync(
            new SubscribeKlineRequest(EthEur, SharedKlineInterval.ThreeMinutes), _ => { }, TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.Error.ShouldBeOfType<ArgumentError>();
        wire.Frames.ShouldBeEmpty();
    }

    [Fact]
    public void The_kline_options_advertise_the_bitvavo_intervals_and_need_no_credentials()
    {
        var options = new Wire(withCredentials: false).Client.SpotApi.SharedApi.SubscribeKlineOptions;

        options.NeedsAuthentication.ShouldBeFalse();
        options.SupportIntervals.Length.ShouldBe(13);
        options.IsSupported(SharedKlineInterval.OneHour).ShouldBeTrue();
        options.IsSupported(SharedKlineInterval.OneWeek).ShouldBeTrue();
        options.IsSupported(SharedKlineInterval.ThreeMinutes).ShouldBeFalse();
    }

    // ── public trades ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Trade_subscription_sends_the_subscribe_frame_and_trades_arrive_as_shared_trades()
    {
        var wire = new Wire(withCredentials: false);
        var trades = new List<DataEvent<SharedTrade[]>>();

        var result = await wire.Client.SpotApi.SharedApi.SubscribeToTradeUpdatesAsync(
            new SubscribeTradeRequest(EthEur), trades.Add, TestContext.Current.CancellationToken);
        wire.Socket.InvokeMessage("""{"event":"trade","timestamp":1548685870299,"market":"ETH-EUR","id":"616bfa4e-b3ff-4b3f-a394-1538a49eb9bc","amount":"1.5","price":"2996","side":"sell"}""");

        result.Success.ShouldBeTrue();
        var frame = wire.Frames.ShouldHaveSingleItem();
        frame.ShouldContain("\"name\":\"trades\"");
        frame.ShouldContain("\"markets\":[\"ETH-EUR\"]");
        var trade = trades.ShouldHaveSingleItem().Data.ShouldHaveSingleItem();
        trade.Price.ShouldBe(2996m);
        trade.Quantities.QuantityInBaseAsset.ShouldBe(1.5m);
        trade.Side.ShouldBe(SharedOrderSide.Sell);
        trade.Symbol.ShouldBe("ETH-EUR");
    }

    [Fact]
    public async Task A_trade_subscription_for_a_futures_symbol_is_rejected_and_nothing_is_sent()
    {
        var wire = new Wire(withCredentials: false);
        var request = new SubscribeTradeRequest(new SharedSymbol(TradingMode.PerpetualLinear, "ETH", "USDT"));

        var result = await wire.Client.SpotApi.SharedApi.SubscribeToTradeUpdatesAsync(request, _ => { }, TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        wire.Frames.ShouldBeEmpty();
    }

    [Fact]
    public async Task Several_symbols_share_one_trade_subscription()
    {
        var wire = new Wire(withCredentials: false);
        var request = new SubscribeTradeRequest(new List<SharedSymbol> { EthEur, new SharedSymbol(TradingMode.Spot, "BTC", "EUR") });

        var result = await wire.Client.SpotApi.SharedApi.SubscribeToTradeUpdatesAsync(request, _ => { }, TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        var frame = wire.Frames.ShouldHaveSingleItem();
        frame.ShouldContain("\"ETH-EUR\"");
        frame.ShouldContain("\"BTC-EUR\"");
    }

    // ── private: orders and own trades (the account channel, subscribed per market) ──────

    [Fact]
    public async Task Order_subscription_authenticates_subscribes_per_market_and_order_events_arrive_as_shared_order_updates()
    {
        var wire = new Wire(withCredentials: true);
        var updates = new List<DataEvent<SharedSpotOrderUpdate[]>>();

        var result = await wire.Client.SpotApi.SharedApi.SubscribeToSpotOrderUpdatesAsync(
            new SubscribeSpotOrderRequest(Markets("ETH-EUR")), updates.Add, TestContext.Current.CancellationToken);
        wire.Socket.InvokeMessage("""
            {"event":"order","orderId":"11111111-1111-1111-1111-111111111111","clientOrderId":"22222222-2222-2222-2222-222222222222","operatorId":7,"market":"ETH-EUR",
             "created":1700000000000,"updated":1700000000001,"status":"partiallyFilled","side":"buy","orderType":"limit","amount":"0.5","amountRemaining":"0.2",
             "price":"3000","timeInForce":"GTC","postOnly":true,"filledAmount":"0.3","filledAmountQuote":"900","executionType":"trade"}
            """);

        result.Success.ShouldBeTrue();
        wire.Frames.Count.ShouldBe(2);
        wire.Frames[0].ShouldContain("\"action\":\"authenticate\"");
        wire.Frames[1].ShouldContain("\"name\":\"account\"");
        wire.Frames[1].ShouldContain("\"markets\":[\"ETH-EUR\"]");
        var order = updates.ShouldHaveSingleItem().Data.ShouldHaveSingleItem();
        order.OrderId.ShouldBe("11111111-1111-1111-1111-111111111111");
        order.ClientOrderId.ShouldBe("22222222-2222-2222-2222-222222222222");
        order.Symbol.ShouldBe("ETH-EUR");
        order.OrderType.ShouldBe(SharedOrderType.LimitMaker);
        order.Side.ShouldBe(SharedOrderSide.Buy);
        order.Status.ShouldBe(SharedOrderStatus.Open);
        order.OrderPrice.ShouldBe(3000m);
        order.OrderQuantity!.QuantityInBaseAsset.ShouldBe(0.5m);
        order.QuantityFilled!.QuantityInBaseAsset.ShouldBe(0.3m);
        order.QuantityFilled.QuantityInQuoteAsset.ShouldBe(900m);
        order.TimeInForce.ShouldBe(SharedTimeInForce.GoodTillCanceled);
        order.UpdateTime.ShouldBe(new DateTime(2023, 11, 14, 22, 13, 20, 1, DateTimeKind.Utc));
    }

    [Fact]
    public async Task The_legacy_order_interface_receives_the_same_events_as_shared_orders()
    {
        var wire = new Wire(withCredentials: true);
        var orders = new List<DataEvent<SharedSpotOrder[]>>();

        var result = await wire.Client.SpotApi.SharedClient.SubscribeToSpotOrderUpdatesAsync(
            new SubscribeSpotOrderRequest(Markets("ETH-EUR")), orders.Add, TestContext.Current.CancellationToken);
        wire.Socket.InvokeMessage("""{"event":"order","orderId":"o-1","operatorId":7,"market":"ETH-EUR","created":1700000000000,"updated":1700000000001,"status":"filled","side":"sell","orderType":"market"}""");

        result.Success.ShouldBeTrue();
        var order = orders.ShouldHaveSingleItem().Data.ShouldHaveSingleItem();
        order.OrderType.ShouldBe(SharedOrderType.Market);
        order.Side.ShouldBe(SharedOrderSide.Sell);
        order.Status.ShouldBe(SharedOrderStatus.Filled);
    }

    [Fact]
    public async Task User_trade_subscription_maps_fill_events_to_shared_user_trades()
    {
        var wire = new Wire(withCredentials: true);
        var trades = new List<DataEvent<SharedUserTrade[]>>();

        var result = await wire.Client.SpotApi.SharedApi.SubscribeToUserTradeUpdatesAsync(
            new SubscribeUserTradeRequest(exchangeParameters: Markets("ETH-EUR")), trades.Add, TestContext.Current.CancellationToken);
        wire.Socket.InvokeMessage("""
            {"event":"fill","market":"ETH-EUR","orderId":"11111111-1111-1111-1111-111111111111","clientOrderId":"c-1","operatorId":7,"fillId":"f-1",
             "timestamp":1700000000002,"amount":"0.001","side":"buy","price":"50000","taker":true,"fee":"0.125","feeCurrency":"EUR"}
            """);

        result.Success.ShouldBeTrue();
        wire.Frames[1].ShouldContain("\"name\":\"account\"");
        var trade = trades.ShouldHaveSingleItem().Data.ShouldHaveSingleItem();
        trade.OrderId.ShouldBe("11111111-1111-1111-1111-111111111111");
        trade.Id.ShouldBe("f-1");
        trade.ClientOrderId.ShouldBe("c-1");
        trade.Side.ShouldBe(SharedOrderSide.Buy);
        trade.Quantities.QuantityInBaseAsset.ShouldBe(0.001m);
        trade.Price.ShouldBe(50000m);
        trade.Fee.ShouldBe(0.125m);
        trade.FeeAsset.ShouldBe("EUR");
        trade.Role.ShouldBe(SharedRole.Taker);
        trade.Symbol.ShouldBe("ETH-EUR");
    }

    [Fact]
    public async Task The_legacy_user_trade_interface_serves_the_same_fills()
    {
        var wire = new Wire(withCredentials: true);
        var trades = new List<DataEvent<SharedUserTrade[]>>();

        var result = await wire.Client.SpotApi.SharedClient.SubscribeToUserTradeUpdatesAsync(
            new SubscribeUserTradeRequest(exchangeParameters: Markets("ETH-EUR")), trades.Add, TestContext.Current.CancellationToken);
        wire.Socket.InvokeMessage("""{"event":"fill","market":"ETH-EUR","orderId":"o-1","operatorId":7,"fillId":"f-2","timestamp":1700000000002,"amount":"1","side":"sell","price":"3000","taker":false,"fee":"0.1","feeCurrency":"EUR"}""");

        result.Success.ShouldBeTrue();
        trades.ShouldHaveSingleItem().Data.ShouldHaveSingleItem().Role.ShouldBe(SharedRole.Maker);
    }

    [Fact]
    public async Task Private_subscriptions_need_the_markets_and_credentials_and_nothing_is_sent_without_them()
    {
        var withKey = new Wire(withCredentials: true);
        var withoutKey = new Wire(withCredentials: false);

        var noMarkets = await withKey.Client.SpotApi.SharedApi.SubscribeToSpotOrderUpdatesAsync(new SubscribeSpotOrderRequest(), _ => { }, TestContext.Current.CancellationToken);
        var noMarketsTrades = await withKey.Client.SpotApi.SharedApi.SubscribeToUserTradeUpdatesAsync(new SubscribeUserTradeRequest(), _ => { }, TestContext.Current.CancellationToken);
        var noKey = await withoutKey.Client.SpotApi.SharedApi.SubscribeToSpotOrderUpdatesAsync(new SubscribeSpotOrderRequest(Markets("ETH-EUR")), _ => { }, TestContext.Current.CancellationToken);

        noMarkets.Error.ShouldBeOfType<ArgumentError>();
        noMarketsTrades.Error.ShouldBeOfType<ArgumentError>();
        noKey.Error.ShouldBeOfType<NoApiCredentialsError>();
        withKey.Frames.ShouldBeEmpty();
        withoutKey.Frames.ShouldBeEmpty();
    }

    // ── the instance, V1 and V2 ──────────────────────────────────────────────────────────

    [Fact]
    public void SharedClient_and_SharedApi_are_one_instance_and_not_the_api_client_itself()
    {
        var api = new Wire(withCredentials: false).Client.SpotApi;

        api.SharedClient.ShouldBeSameAs(api.SharedApi);
        api.SharedApi.ShouldNotBeSameAs(api);
    }

    [Fact]
    public void The_shared_api_reports_bitvavo_spot_websocket_and_has_no_balance_subscription()
    {
        var api = new Wire(withCredentials: false).Client.SpotApi.SharedApi;

        api.Exchange.ShouldBe("Bitvavo");
        api.Transport.ShouldBe(SharedTransport.Socket);
        api.SupportedTradingModes.ShouldBe([TradingMode.Spot]);
        api.ShouldNotBeAssignableTo<IBalanceSocketClient>();
        api.ShouldNotBeAssignableTo<ISubscribeBalancesSocket>();
    }

    [Fact]
    public async Task UnsubscribeAll_closes_every_subscription_of_the_api_client()
    {
        var wire = new Wire(withCredentials: false);
        await wire.Client.SpotApi.SharedApi.SubscribeToTradeUpdatesAsync(new SubscribeTradeRequest(EthEur), _ => { }, TestContext.Current.CancellationToken);
        wire.Client.SpotApi.CurrentSubscriptions.ShouldBe(1);

        await wire.Client.SpotApi.SharedApi.UnsubscribeAllAsync();

        wire.Client.SpotApi.CurrentSubscriptions.ShouldBe(0);
    }
}
