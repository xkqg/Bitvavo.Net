// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading.Tasks;
using Bitvavo.Net.Clients;
using Bitvavo.Net.Enums;
using Bitvavo.Net.Objects.Models.Spot.Streams;
using Bitvavo.Net.Objects.Options;
using CryptoExchange.Net.Objects.Sockets;
using CryptoExchange.Net.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests;

/// <summary>
/// The private <c>account</c> channel — the live fill path of the order flow — end to end through the
/// framework: the client authenticates the connection (<c>authenticate</c> frame, answered like Bitvavo's
/// <c>{"event":"authenticate","authenticated":true}</c>), subscribes, and an <c>order</c> and a <c>fill</c> event reach
/// their own subscription. Event field sets follow the Bitvavo AsyncAPI v2.10.0 (<c>accountOrderEvent</c>, <c>accountFillEvent</c>).
/// </summary>
public class BitvavoSocketAccountDispatchTests
{
    private const string OrderId = "11111111-1111-1111-1111-111111111111";
    private const string FillId = "22222222-2222-2222-2222-222222222222";

    private const string OrderEvent =
        "{\"event\":\"order\",\"orderId\":\"" + OrderId + "\",\"market\":\"BTC-EUR\",\"created\":1700000000000,\"updated\":1700000000001," +
        "\"status\":\"new\",\"side\":\"buy\",\"orderType\":\"limit\",\"amount\":\"0.001\",\"amountRemaining\":\"0.001\",\"price\":\"50000\"," +
        "\"onHold\":\"50\",\"onHoldCurrency\":\"EUR\",\"timeInForce\":\"GTC\",\"postOnly\":false,\"selfTradePrevention\":\"decrementAndCancel\"," +
        "\"visible\":true,\"filledAmount\":\"0\",\"filledAmountQuote\":\"0\",\"executionType\":\"new\"}";

    private const string FillEvent =
        "{\"event\":\"fill\",\"market\":\"BTC-EUR\",\"orderId\":\"" + OrderId + "\",\"fillId\":\"" + FillId + "\",\"timestamp\":1700000000002," +
        "\"amount\":\"0.001\",\"side\":\"buy\",\"price\":\"50000\",\"taker\":true,\"fee\":\"0.125\",\"feeCurrency\":\"EUR\"}";

    [Fact]
    public async Task Account_order_and_fill_events_dispatch_to_their_subscriptions()
    {
        var options = new BitvavoSocketOptions { ApiCredentials = new BitvavoCredentials("test-key", "test-secret") };
        var client = new BitvavoSocketClient(new LoggerFactory(), Options.Create(options));
        var socket = TestHelpers.ConfigureSocketClient(client, "wss://ws.bitvavo.com/v2/");

        var frames = new List<string>();
        socket.OnMessageSend += frame =>
        {
            frames.Add(frame);
            if (frame.Contains("\"action\":\"authenticate\""))
            {
                socket.InvokeMessage("{\"event\":\"authenticate\",\"authenticated\":true}");
            }
        };

        var orders = new List<DataEvent<BitvavoStreamOrderUpdate>>();
        var fills = new List<DataEvent<BitvavoStreamFillEvent>>();

        var orderSubscription = await client.SpotApi.Account.SubscribeToOrderUpdatesAsync(["BTC-EUR"], orders.Add, TestContext.Current.CancellationToken);
        var fillSubscription = await client.SpotApi.Account.SubscribeToFillUpdatesAsync(["BTC-EUR"], fills.Add, TestContext.Current.CancellationToken);

        socket.InvokeMessage(OrderEvent);
        socket.InvokeMessage(FillEvent);

        orderSubscription.Success.ShouldBeTrue();
        fillSubscription.Success.ShouldBeTrue();
        frames.Count.ShouldBe(3, string.Join(" | ", frames));
        frames[0].ShouldContain("\"action\":\"authenticate\"");
        frames[0].ShouldContain("\"key\":\"test-key\"");
        frames[1].ShouldContain("\"action\":\"subscribe\"");
        frames[1].ShouldContain("\"name\":\"account\"");
        frames[2].ShouldBe(frames[1]);

        orders.Count.ShouldBe(1);
        orders[0].Data.OrderId.ShouldBe(OrderId);
        orders[0].Data.Status.ShouldBe(OrderStatus.New);
        orders[0].Data.Price.ShouldBe(50000m);
        fills.Count.ShouldBe(1);
        fills[0].Data.FillId.ShouldBe(FillId);
        fills[0].Data.Taker.ShouldBeTrue();
        fills[0].Data.Fee.ShouldBe(0.125m);
    }
}
