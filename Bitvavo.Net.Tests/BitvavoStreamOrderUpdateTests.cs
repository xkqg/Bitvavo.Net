// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Text.Json;
using Bitvavo.Net.Objects.Models.Spot.Streams;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests;

/// <summary>
/// Pins that <see cref="BitvavoStreamOrderUpdate.Visible"/> deserializes the Bitvavo
/// account-channel <c>order</c> event's boolean <c>visible</c> field. It was mistyped as
/// <c>decimal?</c>, which threw <c>DeserializationFailed</c> on every real order event — the
/// €5 LIVE smoke surfaced the identical bug in the REST
/// <see cref="Bitvavo.Net.Objects.Models.Spot.BitvavoOrder"/> model.
/// </summary>
public class BitvavoStreamOrderUpdateTests
{
    private static JsonSerializerOptions Options => new()
    {
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
    };

    [Fact]
    public void Deserialize_orderEvent_maps_visible_boolean()
    {
        const string json = """
            {"event":"order","orderId":"o-1","market":"ETH-EUR","visible":true}
            """;

        var update = JsonSerializer.Deserialize<BitvavoStreamOrderUpdate>(json, Options);

        update.ShouldNotBeNull();
        update.OrderId.ShouldBe("o-1");
        update.Visible.ShouldBe(true);
    }
}
