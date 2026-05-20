// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Threading.Tasks;
using Bitvavo.Net.Clients;
using Bitvavo.Net.Interfaces.Clients.SpotApi;
using Bitvavo.Net.Objects.Options;
using CryptoExchange.Net.SharedApis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests.Clients.SpotApi;

/// <summary>
/// Tests for the CryptoExchange.Net Shared-API surface implemented by the facade-hosted
/// partial <c>BitvavoSocketClientSpotApi.Shared.cs</c>.
/// <para>
/// Live-WebSocket round-trips for the subscribe methods are covered by the typed
/// <c>BitvavoSocketSubscriptionTests</c> (the Shared methods delegate straight to those
/// same typed sub-client subscriptions). The tests here exercise the Shared-specific code
/// paths that do not require a socket connection: the <c>SharedClient</c> accessor wiring,
/// the <see cref="ISharedClient"/> identity properties, the per-interface options surface,
/// and the request-validation rejection path that runs before any socket I/O.
/// </para>
/// </summary>
public class BitvavoSocketClientSpotApiSharedTests
{
    private static IBitvavoSocketClientSpotApiShared Shared()
    {
        var client = new BitvavoSocketClient(new LoggerFactory(), Options.Create(new BitvavoSocketOptions()));
        return client.SpotApi.SharedClient;
    }

    [Fact]
    public void SharedClient_accessor_returns_the_socket_spot_api_itself()
    {
        var client = new BitvavoSocketClient(new LoggerFactory(), Options.Create(new BitvavoSocketOptions()));

        client.SpotApi.SharedClient.ShouldBeSameAs(client.SpotApi);
    }

    [Fact]
    public void SharedClient_reports_Bitvavo_exchange_and_spot_only_trading_mode()
    {
        var shared = Shared();

        shared.Exchange.ShouldBe("Bitvavo");
        shared.SupportedTradingModes.ShouldBe(new[] { TradingMode.Spot });
    }

    [Fact]
    public void SharedClient_exposes_kline_trade_order_and_userTrade_socket_options()
    {
        var shared = Shared();

        shared.SubscribeKlineOptions.ShouldNotBeNull();
        shared.SubscribeTradeOptions.ShouldNotBeNull();
        shared.SubscribeSpotOrderOptions.ShouldNotBeNull();
        shared.SubscribeUserTradeOptions.ShouldNotBeNull();
    }

    [Fact]
    public void SharedClient_kline_options_advertise_every_Bitvavo_supported_interval()
    {
        var shared = Shared();

        // Bitvavo has no 3-minute bucket — the Shared options must not advertise it.
        shared.SubscribeKlineOptions.IsSupported(SharedKlineInterval.OneHour).ShouldBeTrue();
        shared.SubscribeKlineOptions.IsSupported(SharedKlineInterval.OneWeek).ShouldBeTrue();
        shared.SubscribeKlineOptions.IsSupported(SharedKlineInterval.ThreeMinutes).ShouldBeFalse();
    }

    /// <summary>
    /// ISP-driven omission guard: Bitvavo's account channel emits no balance snapshot, so
    /// the socket Shared facade must NOT implement <see cref="IBalanceSocketClient"/>.
    /// </summary>
    [Fact]
    public void SharedClient_does_not_implement_IBalanceSocketClient()
    {
        var shared = Shared();

        shared.ShouldNotBeAssignableTo<IBalanceSocketClient>();
    }

    [Fact]
    public async Task SubscribeToSpotOrderUpdatesAsync_without_Markets_exchange_parameter_fails_validation()
    {
        var shared = Shared();

        // No 'Markets' exchange parameter supplied — the RequiredExchangeParameters
        // validation must reject the request before any socket connection is attempted.
        var result = await shared.SubscribeToSpotOrderUpdatesAsync(
            new SubscribeSpotOrderRequest(),
            _ => { },
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
    }

    [Fact]
    public async Task SubscribeToUserTradeUpdatesAsync_without_Markets_exchange_parameter_fails_validation()
    {
        var shared = Shared();

        var result = await shared.SubscribeToUserTradeUpdatesAsync(
            new SubscribeUserTradeRequest(),
            _ => { },
            TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
    }
}
