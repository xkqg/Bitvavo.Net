// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Linq;
using Bitvavo.Net.Objects.Internal;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests;

/// <summary>
/// The rule that decides which entries of a channel an unsubscribe frame may name: those no other live subscription on the same
/// connection still needs, grouped back into one channel per candle interval.
/// </summary>
public class BitvavoChannelDemandTests
{
    private static BitvavoSocketChannel Trades(params string[] markets) => new() { Name = "trades", Markets = markets };

    private static BitvavoSocketChannel Candles(string interval, params string[] markets) => new() { Name = "candles", Interval = [interval], Markets = markets };

    /// <summary>Nobody else needs anything: the whole channel goes, unchanged in name, interval and market order.</summary>
    [Fact]
    public void Without_other_subscriptions_the_whole_channel_is_unsubscribed()
    {
        var result = BitvavoChannelDemand.Unneeded(Candles("1h", "ETH-EUR", "BTC-EUR"), []);

        var channel = result.ShouldHaveSingleItem();
        channel.Name.ShouldBe("candles");
        channel.Interval.ShouldBe(["1h"]);
        channel.Markets.ShouldBe(["ETH-EUR", "BTC-EUR"]);
    }

    /// <summary>A channel without an interval keeps none: the property stays absent on the wire.</summary>
    [Fact]
    public void A_channel_without_interval_is_unsubscribed_without_interval()
    {
        BitvavoChannelDemand.Unneeded(Trades("ETH-EUR"), []).ShouldHaveSingleItem().Interval.ShouldBeNull();
    }

    /// <summary>A subset subscription still needs its markets: only the rest is unsubscribed.</summary>
    [Fact]
    public void Markets_another_subscription_still_needs_are_left_out()
    {
        var result = BitvavoChannelDemand.Unneeded(Trades("BTC-EUR", "ETH-EUR"), [Trades("ETH-EUR")]);

        result.ShouldHaveSingleItem().Markets.ShouldBe(["BTC-EUR"]);
    }

    /// <summary>A larger subscription needs everything the closing one has: nothing is unsubscribed, so no frame is sent.</summary>
    [Fact]
    public void Nothing_is_unsubscribed_when_another_subscription_needs_it_all()
    {
        BitvavoChannelDemand.Unneeded(Trades("ETH-EUR"), [Trades("BTC-EUR", "ETH-EUR")]).ShouldBeEmpty();
    }

    /// <summary>The needs of several subscriptions add up.</summary>
    [Fact]
    public void The_needs_of_several_subscriptions_add_up()
    {
        var result = BitvavoChannelDemand.Unneeded(Trades("A-EUR", "B-EUR", "C-EUR"), [Trades("A-EUR"), Trades("B-EUR")]);

        result.ShouldHaveSingleItem().Markets.ShouldBe(["C-EUR"]);
    }

    /// <summary>The same market in another channel, or another candle interval, is another entry of the server's set.</summary>
    [Fact]
    public void Other_channels_and_other_intervals_do_not_count()
    {
        var closing = Candles("1h", "ETH-EUR");

        BitvavoChannelDemand.Unneeded(closing, [Trades("ETH-EUR"), Candles("1d", "ETH-EUR")]).ShouldHaveSingleItem().Markets.ShouldBe(["ETH-EUR"]);
        BitvavoChannelDemand.Unneeded(closing, [Candles("1h", "ETH-EUR")]).ShouldBeEmpty();
    }

    /// <summary>The same market and interval under another channel name is another entry: only the name tells them apart.</summary>
    [Fact]
    public void The_same_market_in_another_channel_does_not_count()
    {
        var book = new BitvavoSocketChannel { Name = "book", Markets = ["ETH-EUR"] };

        BitvavoChannelDemand.Unneeded(Trades("ETH-EUR"), [book]).ShouldHaveSingleItem().Markets.ShouldBe(["ETH-EUR"]);
    }

    /// <summary>A channel with several intervals is split back into one channel per interval, each with the markets that remain.</summary>
    [Fact]
    public void A_channel_with_several_intervals_is_regrouped_per_interval()
    {
        var closing = new BitvavoSocketChannel { Name = "candles", Interval = ["1h", "1d"], Markets = ["ETH-EUR", "BTC-EUR"] };

        var result = BitvavoChannelDemand.Unneeded(closing, [Candles("1d", "BTC-EUR")]);

        result.Length.ShouldBe(2);
        var hour = result.Single(channel => channel.Interval!.Single() == "1h");
        hour.Markets.ShouldBe(["ETH-EUR", "BTC-EUR"]);
        var day = result.Single(channel => channel.Interval!.Single() == "1d");
        day.Markets.ShouldBe(["ETH-EUR"]);
    }

    /// <summary>A market listed twice is named once.</summary>
    [Fact]
    public void A_market_listed_twice_is_named_once()
    {
        BitvavoChannelDemand.Unneeded(Trades("ETH-EUR", "ETH-EUR"), []).ShouldHaveSingleItem().Markets.ShouldBe(["ETH-EUR"]);
    }
}
