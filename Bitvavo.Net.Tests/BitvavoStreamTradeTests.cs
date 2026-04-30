// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Text.Json;
using Bitvavo.Net.Enums;
using Bitvavo.Net.Objects.Models.Spot.Streams;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests;

/// <summary>
/// Pins that <see cref="BitvavoStreamTrade.Side"/> is a strongly-typed
/// <see cref="OrderSide"/> enum that round-trips against the Bitvavo wire form
/// (<c>"buy"</c> / <c>"sell"</c>). Drives the migration from the previous
/// stringly-typed <c>Side</c> property to enum-typed for parity with every
/// other Bitvavo stream model (orders, fills, public trades).
/// </summary>
public class BitvavoStreamTradeTests
{
    private const string SampleBuyTradeJson = """
        {"event":"trade","timestamp":1548685870299,"market":"BTC-EUR","id":"616bfa4e-b3ff-4b3f-a394-1538a49eb9bc","amount":"1","price":"2996","side":"buy"}
        """;

    private const string SampleSellTradeJson = """
        {"event":"trade","timestamp":1548685870299,"market":"BTC-EUR","id":"616bfa4e-b3ff-4b3f-a394-1538a49eb9bc","amount":"1","price":"2996","side":"sell"}
        """;

    private static JsonSerializerOptions Options => new()
    {
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
    };

    [Fact]
    public void Deserialize_buyTrade_yields_OrderSide_Buy()
    {
        var trade = JsonSerializer.Deserialize<BitvavoStreamTrade>(SampleBuyTradeJson, Options);
        trade.ShouldNotBeNull();
        trade.Side.ShouldBe(OrderSide.Buy);
    }

    [Fact]
    public void Deserialize_sellTrade_yields_OrderSide_Sell()
    {
        var trade = JsonSerializer.Deserialize<BitvavoStreamTrade>(SampleSellTradeJson, Options);
        trade.ShouldNotBeNull();
        trade.Side.ShouldBe(OrderSide.Sell);
    }
}
