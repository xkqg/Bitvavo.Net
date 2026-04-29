// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Linq;
using System.Text.Json;
using Bitvavo.Net.Enums;
using CryptoExchange.Net.Converters.SystemTextJson;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests;

/// <summary>
/// Drives Phase 3A (add <c>OneWeek</c> + <c>OneMonth</c>) and Phase 3B (replace
/// <see cref="System.Text.Json.Serialization.JsonStringEnumConverter"/> with
/// <see cref="EnumConverter{T}"/> for [Map]-driven wire symmetry).
///
/// Bitvavo wire format note: candle intervals use lowercase letters except <c>"1M"</c>
/// (capital M = month) which differs from <c>"1m"</c> (lowercase = one minute). The
/// test pins both so a future copy-paste typo is caught immediately.
/// </summary>
public class KlineIntervalTests
{
    [Fact]
    public void OneWeek_GetString_returns_1w()
    {
        EnumConverter.GetString(KlineInterval.OneWeek).ShouldBe("1w");
    }

    [Fact]
    public void OneMonth_GetString_returns_1M()
    {
        EnumConverter.GetString(KlineInterval.OneMonth).ShouldBe("1M");
    }

    [Theory]
    [InlineData("1m",  KlineInterval.OneMinute)]
    [InlineData("5m",  KlineInterval.FiveMinutes)]
    [InlineData("15m", KlineInterval.FifteenMinutes)]
    [InlineData("30m", KlineInterval.ThirtyMinutes)]
    [InlineData("1h",  KlineInterval.OneHour)]
    [InlineData("2h",  KlineInterval.TwoHours)]
    [InlineData("4h",  KlineInterval.FourHours)]
    [InlineData("6h",  KlineInterval.SixHours)]
    [InlineData("8h",  KlineInterval.EightHours)]
    [InlineData("12h", KlineInterval.TwelveHours)]
    [InlineData("1d",  KlineInterval.OneDay)]
    [InlineData("1w",  KlineInterval.OneWeek)]
    [InlineData("1M",  KlineInterval.OneMonth)]
    public void GetString_matches_BitvavoWireFormat(string wire, KlineInterval interval)
    {
        EnumConverter.GetString(interval).ShouldBe(wire);
    }

    [Theory]
    [InlineData("1w",  KlineInterval.OneWeek)]
    [InlineData("1M",  KlineInterval.OneMonth)]
    public void ParseString_recovers_NewIntervals(string wire, KlineInterval expected)
    {
        EnumConverter.ParseString<KlineInterval>(wire).ShouldBe(expected);
    }

    [Fact]
    public void AllIntervals_RoundTrip_via_GetString_then_ParseString()
    {
        foreach (var value in Enum.GetValues<KlineInterval>())
        {
            var wire = EnumConverter.GetString(value);
            var roundTrip = EnumConverter.ParseString<KlineInterval>(wire!);
            roundTrip.ShouldBe(value);
        }
    }

    /// <summary>
    /// Tightens the converter contract — a JSON-emitting consumer must produce the wire
    /// strings, not the .NET enum names. Ensures the right [JsonConverter] is wired.
    /// </summary>
    [Theory]
    [InlineData(KlineInterval.OneHour,   "\"1h\"")]
    [InlineData(KlineInterval.OneDay,    "\"1d\"")]
    [InlineData(KlineInterval.OneWeek,   "\"1w\"")]
    [InlineData(KlineInterval.OneMonth,  "\"1M\"")]
    public void Serialize_via_JsonSerializer_emits_wireToken(KlineInterval value, string expectedJson)
    {
        var json = JsonSerializer.Serialize(value);
        json.ShouldBe(expectedJson);
    }

    [Theory]
    [InlineData("\"1h\"", KlineInterval.OneHour)]
    [InlineData("\"1d\"", KlineInterval.OneDay)]
    [InlineData("\"1w\"", KlineInterval.OneWeek)]
    [InlineData("\"1M\"", KlineInterval.OneMonth)]
    public void Deserialize_via_JsonSerializer_recovers_enum(string json, KlineInterval expected)
    {
        var value = JsonSerializer.Deserialize<KlineInterval>(json);
        value.ShouldBe(expected);
    }
}
