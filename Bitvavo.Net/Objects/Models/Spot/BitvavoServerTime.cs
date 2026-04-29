// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Text.Json.Serialization;
using CryptoExchange.Net.Converters.SystemTextJson;

namespace Bitvavo.Net.Objects.Models.Spot;

/// <summary>Bitvavo server time as returned by <c>GET /v2/time</c>.</summary>
public record BitvavoServerTime
{
    /// <summary>Server time (UTC) — Bitvavo sends Unix milliseconds.</summary>
    [JsonPropertyName("time"), JsonConverter(typeof(DateTimeConverter))]
    public DateTime Time { get; init; }

    /// <summary>Server time in nanoseconds since Unix epoch — preserved as <see cref="long"/> because <see cref="DateTime"/> only resolves to 100-nanosecond ticks.</summary>
    [JsonPropertyName("timeNs")]
    public long TimeNs { get; init; }
}
