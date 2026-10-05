// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Bitvavo.Net.Clients;
using Bitvavo.Net.Enums;
using Bitvavo.Net.Objects.Models.Spot.Streams;
using Bitvavo.Net.Objects.Options;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests;

/// <summary>
/// Public-stream subscriptions through the framework's own test transport: the canonical subscribe envelope, event dispatch
/// per topic, and the lifetime of a subscription (a cancellation token closes it).
/// </summary>
public class BitvavoSocketSubscriptionTests
{
    /// <summary>
    /// Bitvavo acknowledges a subscribe with no per-request reply, so a subscribe counts as done once it is sent. The
    /// subscription must then be <c>Subscribed</c> — only a subscribed subscription registers its cancellation token — and
    /// cancelling the token the caller passed in must close it again.
    /// </summary>
    [Fact]
    public async Task Subscribe_then_cancel_token_closes_the_subscription()
    {
        var client = new BitvavoSocketClient(new LoggerFactory(), Options.Create(new BitvavoSocketOptions()));
        TestHelpers.ConfigureSocketClient(client, "wss://ws.bitvavo.com/v2/");
        using var cts = new CancellationTokenSource();

        var result = await client.SpotApi.ExchangeData.SubscribeToTradeUpdatesAsync("ETH-EUR", _ => { }, cts.Token);

        result.Success.ShouldBeTrue();
        result.Data.SubscriptionStatus.ShouldBe(SubscriptionStatus.Subscribed);
        client.SpotApi.CurrentSubscriptions.ShouldBe(1);

        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        result.Data.SubscriptionStatusChanged += status =>
        {
            if (status == SubscriptionStatus.Closed)
            {
                closed.TrySetResult();
            }
        };

        cts.Cancel();

        await closed.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        client.SpotApi.CurrentSubscriptions.ShouldBe(0);
    }

    [Fact]
    public async Task SubscribeToKlineUpdatesAsync_sends_canonical_envelope_and_dispatches_candle_event()
    {
        var loggerFactory = new LoggerFactory();
        var client = new BitvavoSocketClient(loggerFactory, Options.Create(new BitvavoSocketOptions()));

        var validator = new SocketSubscriptionValidator<BitvavoSocketClient>(
            client,
            folder: "Subscriptions/Spot/ExchangeData",
            baseAddress: "wss://ws.bitvavo.com/v2/",
            nestedPropertyForCompare: null);

        await validator.ValidateAsync<BitvavoStreamCandleEvent>(
            (c, handler) => c.SpotApi.ExchangeData.SubscribeToKlineUpdatesAsync("ETH-EUR", KlineInterval.OneHour, handler),
            name: "SubscribeToKlineUpdates");
    }

    [Fact]
    public async Task SubscribeToTradeUpdatesAsync_sends_canonical_envelope_and_dispatches_trade_event()
    {
        var loggerFactory = new LoggerFactory();
        var client = new BitvavoSocketClient(loggerFactory, Options.Create(new BitvavoSocketOptions()));

        var validator = new SocketSubscriptionValidator<BitvavoSocketClient>(
            client,
            folder: "Subscriptions/Spot/ExchangeData",
            baseAddress: "wss://ws.bitvavo.com/v2/",
            nestedPropertyForCompare: null);

        await validator.ValidateAsync<BitvavoStreamTrade>(
            (c, handler) => c.SpotApi.ExchangeData.SubscribeToTradeUpdatesAsync("BTC-EUR", handler),
            name: "SubscribeToTradeUpdates");
    }

    [Fact]
    public async Task Two_concurrent_kline_subscriptions_dispatch_independently_by_interval_topic()
    {
        var loggerFactory = new LoggerFactory();
        var client = new BitvavoSocketClient(loggerFactory, Options.Create(new BitvavoSocketOptions()));

        var validator = new SocketSubscriptionValidator<BitvavoSocketClient>(
            client,
            folder: "Subscriptions/Spot/ExchangeData",
            baseAddress: "wss://ws.bitvavo.com/v2/",
            nestedPropertyForCompare: null);

        await validator.ValidateConcurrentAsync<BitvavoStreamCandleEvent>(
            (c, handler) => c.SpotApi.ExchangeData.SubscribeToKlineUpdatesAsync("ETH-EUR", KlineInterval.OneHour, handler),
            (c, handler) => c.SpotApi.ExchangeData.SubscribeToKlineUpdatesAsync("ETH-EUR", KlineInterval.OneDay, handler),
            name: "Concurrent");
    }

    [Fact]
    public async Task SubscribeToTradeUpdatesAsync_MultiMarket_sends_canonical_envelope_and_dispatches_trade_event()
    {
        var loggerFactory = new LoggerFactory();
        var client = new BitvavoSocketClient(loggerFactory, Options.Create(new BitvavoSocketOptions()));

        var validator = new SocketSubscriptionValidator<BitvavoSocketClient>(
            client,
            folder: "Subscriptions/Spot/ExchangeData",
            baseAddress: "wss://ws.bitvavo.com/v2/",
            nestedPropertyForCompare: null);

        await validator.ValidateAsync<BitvavoStreamTrade>(
            (c, handler) => c.SpotApi.ExchangeData.SubscribeToTradeUpdatesAsync(
                new[] { "BTC-EUR", "ETH-EUR" }, handler),
            name: "SubscribeToTradeUpdatesMultiMarket");
    }

    // The private account channel is not validator-tested: the SocketSubscriptionValidator matches outgoing JSON literally,
    // while the authenticate frame carries a real-time timestamp and a per-call HMAC signature. It is covered by:
    //   1. BitvavoAuthenticationProviderTests.BuildSocketAuth_* + Socket_auth_timestamp_uses_the_time_offset (the signer),
    //   2. BitvavoSocketAccountDispatchTests (authenticate -> subscribe -> order/fill events through the framework),
    //   3. the env-gated live smoke (INTEGRATION=1; needs real credentials).
    // Fixtures at Subscriptions/Spot/Account/SubscribeTo{Order,Fill}Updates.txt document the expected wire shape.
}
