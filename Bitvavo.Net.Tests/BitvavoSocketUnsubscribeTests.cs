// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests;

/// <summary>
/// Closing one subscription of a connection that carries several sends the <c>unsubscribe</c> action for that subscription's
/// channel and nothing else, and leaves the other subscriptions running (closing the last one closes the connection instead).
/// </summary>
public class BitvavoSocketUnsubscribeTests
{
    [Fact]
    public async Task Closing_one_public_subscription_unsubscribes_its_market_only()
    {
        var (client, factory) = RecordingSocketFactory.Create();
        var first = await client.SpotApi.ExchangeData.SubscribeToTradeUpdatesAsync("ETH-EUR", _ => { }, TestContext.Current.CancellationToken);
        await client.SpotApi.ExchangeData.SubscribeToTradeUpdatesAsync("BTC-EUR", _ => { }, TestContext.Current.CancellationToken);
        first.Success.ShouldBeTrue();

        await first.Data.CloseAsync();

        var connection = factory.Connections.ShouldHaveSingleItem();
        var unsubscribe = connection.UnsubscribeFrames.ShouldHaveSingleItem();
        unsubscribe.ShouldContain("\"name\":\"trades\"");
        unsubscribe.ShouldContain("\"markets\":[\"ETH-EUR\"]");
        unsubscribe.ShouldNotContain("BTC-EUR");
        client.SpotApi.CurrentSubscriptions.ShouldBe(1);
    }

    [Fact]
    public async Task Closing_one_account_subscription_unsubscribes_its_markets_only()
    {
        var (client, factory) = RecordingSocketFactory.Create("key-a");
        var first = await client.SpotApi.Account.SubscribeToOrderUpdatesAsync(["ETH-EUR"], _ => { }, TestContext.Current.CancellationToken);
        await client.SpotApi.Account.SubscribeToFillUpdatesAsync(["BTC-EUR"], _ => { }, TestContext.Current.CancellationToken);
        first.Success.ShouldBeTrue();

        await first.Data.CloseAsync();

        var connection = factory.Connections.ShouldHaveSingleItem();
        var unsubscribe = connection.UnsubscribeFrames.ShouldHaveSingleItem();
        unsubscribe.ShouldContain("\"name\":\"account\"");
        unsubscribe.ShouldContain("\"markets\":[\"ETH-EUR\"]");
        unsubscribe.ShouldNotContain("BTC-EUR");
        client.SpotApi.CurrentSubscriptions.ShouldBe(1);
    }
}
