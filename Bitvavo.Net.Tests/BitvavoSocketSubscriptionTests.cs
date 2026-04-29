// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Threading.Tasks;
using Bitvavo.Net.Clients;
using Bitvavo.Net.Enums;
using Bitvavo.Net.Objects.Models.Spot.Streams;
using Bitvavo.Net.Objects.Options;
using CryptoExchange.Net.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Bitvavo.Net.Tests;




public class BitvavoSocketSubscriptionTests
{
    [Fact]
    public async Task SubscribeToKlineUpdatesAsync_sends_canonical_envelope_and_dispatches_candle_event()
    {
        var loggerFactory = new LoggerFactory();
        var client = new BitvavoSocketClient(loggerFactory, Options.Create(new BitvavoSocketOptions()));

        var validator = new SocketSubscriptionValidator<BitvavoSocketClient>(
            client,
            folder: "Subscriptions/Spot/ExchangeData",
            baseAddress: "wss://ws.bitvavo.com",
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
            baseAddress: "wss://ws.bitvavo.com",
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
            baseAddress: "wss://ws.bitvavo.com",
            nestedPropertyForCompare: null);

        await validator.ValidateConcurrentAsync<BitvavoStreamCandleEvent>(
            (c, handler) => c.SpotApi.ExchangeData.SubscribeToKlineUpdatesAsync("ETH-EUR", KlineInterval.OneHour, handler),
            (c, handler) => c.SpotApi.ExchangeData.SubscribeToKlineUpdatesAsync("ETH-EUR", KlineInterval.OneDay, handler),
            name: "Concurrent");
    }

    // Auth-WebSocket fixture-based integration tests are deferred — the
    // SocketSubscriptionValidator matches outgoing JSON literally, but the auth message
    // contains a real-time timestamp + per-call HMAC signature that can't be pinned without
    // a clock-injection refactor of the framework's AuthenticationProvider. Auth-WS is
    // covered by:
    //   1. BitvavoAuthenticationProviderTests.BuildSocketAuth_* (3 unit tests, green).
    //   2. BitvavoSmoke --signed-ws live smoke (manual; requires real creds).
    // Fixtures at Subscriptions/Spot/Account/SubscribeTo{Order,Fill}Updates.txt document
    // the expected wire shape for future validator-based tests once placeholder support lands.
}
