// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Bitvavo.Net.Objects.Models.Spot.Streams;

/// <summary>
/// Bitvavo <c>candle</c> WebSocket event. Bitvavo emits one event per trade with the
/// updated candle (the <see cref="Candle"/> array typically holds one inner array — the
/// latest snapshot — but the protocol allows batching). Inner array shape matches the
/// REST <see cref="BitvavoKline"/> DTO byte-for-byte.
/// </summary>
public record BitvavoStreamCandleEvent
{
    /// <summary>Always <c>"candle"</c> for this stream.</summary>
    [JsonPropertyName("event")]
    public string Event { get; init; } = string.Empty;

    /// <summary>Market identifier, e.g. <c>"ETH-EUR"</c>.</summary>
    [JsonPropertyName("market")]
    public string Market { get; init; } = string.Empty;

    /// <summary>Candle interval as a wire-format string, e.g. <c>"1h"</c>, <c>"1m"</c>.</summary>
    [JsonPropertyName("interval")]
    public string Interval { get; init; } = string.Empty;

    /// <summary>One or more candle snapshots (positional arrays under the hood).</summary>
    [JsonPropertyName("candle")]
    public BitvavoKline[] Candle { get; init; } = System.Array.Empty<BitvavoKline>();
}
