// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Bitvavo.Net.Clients;
using Bitvavo.Net.Enums;
using Bitvavo.Net.Objects.Options;
using CryptoExchange.Net.Objects.Sockets;
using CryptoExchange.Net.Testing;
using CryptoExchange.Net.Testing.Implementations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests;

/// <summary>
/// Subscriptions that overlap on one connection. What Bitvavo's server holds is the replay of the frames the client sent
/// (subscribe adds a market of a channel, unsubscribe removes it, a closed connection ends everything), so the invariant pinned
/// here is: a channel stays subscribed server-side until the last live subscription that needs it closes.
/// CryptoExchange.Net does not send a subscribe frame for a subscription an earlier one covers and decides on its own whether a
/// close needs an unsubscribe frame; it gets partial overlap wrong in both directions (13.1.0 and 13.2.x differ), so the
/// unsubscribe frame is built from what no other live subscription still needs. Measured live on the public WebSocket
/// (2026-10-19): without that, a subscriber whose frame was skipped received 0 trades in 120 s after the covering subscription closed.
/// </summary>
public class BitvavoOverlappingSubscriptionTests
{
    private const string EthTrade = """{"event":"trade","timestamp":1548685870299,"market":"ETH-EUR","id":"a1","amount":"1","price":"3000","side":"buy"}""";

    private const string BtcOrder = """{"event":"order","orderId":"11111111-1111-1111-1111-111111111111","market":"BTC-EUR","created":1700000000000,"updated":1700000000001,"status":"new","side":"buy","orderType":"limit","amount":"0.001","amountRemaining":"0.001","price":"50000","onHold":"50","onHoldCurrency":"EUR","timeInForce":"GTC","postOnly":false,"selfTradePrevention":"decrementAndCancel","visible":true,"filledAmount":"0","filledAmountQuote":"0","executionType":"new"}""";

    private const string EthCandleHour = """{"event":"candle","market":"ETH-EUR","interval":"1h","candle":[[1714132800000,"2003.35","2004.46","2001.95","2004.20","28.85"]]}""";

    /// <summary>The client, its test socket and every frame the client sent, in order.</summary>
    private sealed class Rig
    {
        public Rig(bool withCredentials = false)
        {
            var options = new BitvavoSocketOptions();
            if (withCredentials)
            {
                options.ApiCredentials = new BitvavoCredentials("test-key", "test-secret");
            }

            Client = new BitvavoSocketClient(new LoggerFactory(), Options.Create(options));
            Socket = TestHelpers.ConfigureSocketClient(Client, "wss://ws.bitvavo.com/v2/");
            Socket.OnMessageSend += frame =>
            {
                Frames.Add(frame);
                if (frame.Contains("\"action\":\"authenticate\""))
                {
                    Socket.InvokeMessage("{\"event\":\"authenticate\",\"authenticated\":true}");
                }
            };
        }

        public BitvavoSocketClient Client { get; }

        public TestSocket Socket { get; }

        public List<string> Frames { get; } = new();

        /// <summary>True when the feed is gone: no channel is held any more, or the connection itself was closed (closing the last subscription closes the socket).</summary>
        public bool FeedEnded => ServerSide().Count == 0 || Client.SpotApi.CurrentConnections == 0;

        /// <summary>The channels Bitvavo's server holds after the frames sent so far, as <c>name:market:interval</c>.</summary>
        public SortedSet<string> ServerSide()
        {
            var held = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var frame in Frames)
            {
                using var doc = JsonDocument.Parse(frame);
                var root = doc.RootElement;
                if (!root.TryGetProperty("action", out var action) || !root.TryGetProperty("channels", out var channels))
                {
                    continue;
                }

                foreach (var channel in channels.EnumerateArray())
                {
                    var name = channel.GetProperty("name").GetString();
                    var intervals = channel.TryGetProperty("interval", out var iv)
                        ? iv.EnumerateArray().Select(x => x.GetString()).ToArray()
                        : [string.Empty];
                    foreach (var market in channel.GetProperty("markets").EnumerateArray().Select(x => x.GetString()))
                    {
                        foreach (var interval in intervals)
                        {
                            var key = $"{name}:{market}:{interval}";
                            if (action.GetString() == "subscribe")
                            {
                                held.Add(key);
                            }
                            else
                            {
                                held.Remove(key);
                            }
                        }
                    }
                }
            }

            return held;
        }
    }

    /// <summary>Two identical subscriptions both get every event; closing one leaves the feed running, closing the last ends it.</summary>
    [Fact]
    public async Task Two_identical_trade_subscriptions_share_the_feed_until_the_last_one_closes()
    {
        var rig = new Rig();
        var a = 0;
        var b = 0;
        var subA = await rig.Client.SpotApi.ExchangeData.SubscribeToTradeUpdatesAsync("ETH-EUR", _ => a++, TestContext.Current.CancellationToken);
        var subB = await rig.Client.SpotApi.ExchangeData.SubscribeToTradeUpdatesAsync("ETH-EUR", _ => b++, TestContext.Current.CancellationToken);
        subA.Success.ShouldBeTrue();
        subB.Success.ShouldBeTrue();

        rig.Socket.InvokeMessage(EthTrade);
        a.ShouldBe(1);
        b.ShouldBe(1);

        await subA.Data.CloseAsync();
        rig.ServerSide().ShouldContain("trades:ETH-EUR:", "B is still subscribed");
        rig.Socket.InvokeMessage(EthTrade);
        b.ShouldBe(2);

        await subB.Data.CloseAsync();
        rig.FeedEnded.ShouldBeTrue("the last subscription closed: unsubscribe frame or closed connection");
    }

    /// <summary>
    /// The superset came first and covered the subset, whose subscribe frame was therefore never sent. Closing the superset must
    /// unsubscribe only the market nobody else needs.
    /// </summary>
    [Fact]
    public async Task A_subset_subscription_keeps_its_feed_when_the_superset_that_covered_it_closes()
    {
        var rig = new Rig();
        var b = 0;
        var subA = await rig.Client.SpotApi.ExchangeData.SubscribeToTradeUpdatesAsync(["BTC-EUR", "ETH-EUR"], _ => { }, TestContext.Current.CancellationToken);
        var subB = await rig.Client.SpotApi.ExchangeData.SubscribeToTradeUpdatesAsync("ETH-EUR", _ => b++, TestContext.Current.CancellationToken);
        subA.Success.ShouldBeTrue();
        subB.Success.ShouldBeTrue();

        await subA.Data.CloseAsync();

        rig.ServerSide().ShouldContain("trades:ETH-EUR:", "B (ETH-EUR) is still subscribed, so the server must still send it");
        rig.ServerSide().ShouldNotContain("trades:BTC-EUR:", "nobody needs BTC-EUR any more");
        rig.Socket.InvokeMessage(EthTrade);
        b.ShouldBe(1);
    }

    /// <summary>The reverse order: the subset first, then the superset. Closing the subset must not take the superset's market down.</summary>
    [Fact]
    public async Task A_superset_subscription_keeps_its_feed_when_the_subset_that_came_first_closes()
    {
        var rig = new Rig();
        var a = 0;
        var subB = await rig.Client.SpotApi.ExchangeData.SubscribeToTradeUpdatesAsync("ETH-EUR", _ => { }, TestContext.Current.CancellationToken);
        var subA = await rig.Client.SpotApi.ExchangeData.SubscribeToTradeUpdatesAsync(["BTC-EUR", "ETH-EUR"], _ => a++, TestContext.Current.CancellationToken);
        subA.Success.ShouldBeTrue();
        subB.Success.ShouldBeTrue();

        await subB.Data.CloseAsync();

        rig.ServerSide().ShouldContain("trades:ETH-EUR:");
        rig.ServerSide().ShouldContain("trades:BTC-EUR:");
        rig.Socket.InvokeMessage(EthTrade);
        a.ShouldBe(1);
    }

    /// <summary>The order and fill subscriptions are two subscriptions on ONE Bitvavo channel (<c>account</c>): closing the fill subscription must not stop the order events.</summary>
    [Fact]
    public async Task Closing_the_fill_subscription_keeps_the_order_events_of_the_shared_account_channel()
    {
        var rig = new Rig(withCredentials: true);
        var orders = 0;
        var orderSub = await rig.Client.SpotApi.Account.SubscribeToOrderUpdatesAsync(["BTC-EUR"], _ => orders++, TestContext.Current.CancellationToken);
        var fillSub = await rig.Client.SpotApi.Account.SubscribeToFillUpdatesAsync(["BTC-EUR"], _ => { }, TestContext.Current.CancellationToken);
        orderSub.Success.ShouldBeTrue();
        fillSub.Success.ShouldBeTrue();

        await fillSub.Data.CloseAsync();

        rig.ServerSide().ShouldContain("account:BTC-EUR:", "the order subscription still needs the account channel");
        rig.Socket.InvokeMessage(BtcOrder);
        orders.ShouldBe(1);
    }

    /// <summary>The other way round: closing the order subscription keeps the fill events.</summary>
    [Fact]
    public async Task Closing_the_order_subscription_keeps_the_fill_events_of_the_shared_account_channel()
    {
        var rig = new Rig(withCredentials: true);
        var orderSub = await rig.Client.SpotApi.Account.SubscribeToOrderUpdatesAsync(["BTC-EUR"], _ => { }, TestContext.Current.CancellationToken);
        var fillSub = await rig.Client.SpotApi.Account.SubscribeToFillUpdatesAsync(["BTC-EUR"], _ => { }, TestContext.Current.CancellationToken);
        orderSub.Success.ShouldBeTrue();
        fillSub.Success.ShouldBeTrue();

        await orderSub.Data.CloseAsync();

        rig.ServerSide().ShouldContain("account:BTC-EUR:", "the fill subscription still needs the account channel");
    }

    /// <summary>Two candle subscriptions of one market with different intervals are different channels: closing one ends only its interval.</summary>
    [Fact]
    public async Task Closing_one_candle_interval_keeps_the_other_interval_of_the_same_market()
    {
        var rig = new Rig();
        var hours = 0;
        var subHour = await rig.Client.SpotApi.ExchangeData.SubscribeToKlineUpdatesAsync("ETH-EUR", KlineInterval.OneHour, _ => hours++, TestContext.Current.CancellationToken);
        var subDay = await rig.Client.SpotApi.ExchangeData.SubscribeToKlineUpdatesAsync("ETH-EUR", KlineInterval.OneDay, _ => { }, TestContext.Current.CancellationToken);
        subHour.Success.ShouldBeTrue();
        subDay.Success.ShouldBeTrue();

        await subDay.Data.CloseAsync();

        rig.ServerSide().ShouldContain("candles:ETH-EUR:1h");
        rig.ServerSide().ShouldNotContain("candles:ETH-EUR:1d");
        rig.Socket.InvokeMessage(EthCandleHour);
        hours.ShouldBe(1);
    }
}
