// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Text.Json.Serialization;
using CryptoExchange.Net.Converters.SystemTextJson;

namespace Bitvavo.Net.Objects.Models.Spot;

/// <summary>
/// Market-specific fee schedule as returned by <c>GET /v2/account/fees</c>. Bitvavo
/// returns one entry covering the whole 30-day-volume tier the account is on.
/// </summary>
public record BitvavoMarketFee
{
    /// <summary>Volume tier label (e.g. <c>"0"</c>, <c>"1"</c>, ...). Bitvavo emits this as a string.</summary>
    [JsonPropertyName("tier")]
    public string Tier { get; init; } = string.Empty;

    /// <summary>30-day rolling trading volume in the quote currency.</summary>
    [JsonPropertyName("volume"), JsonConverter(typeof(DecimalConverter))]
    public decimal? Volume { get; init; }

    /// <summary>Taker fee rate (decimal fraction) for this tier.</summary>
    [JsonPropertyName("taker"), JsonConverter(typeof(DecimalConverter))]
    public decimal? Taker { get; init; }

    /// <summary>Maker fee rate (decimal fraction) for this tier.</summary>
    [JsonPropertyName("maker"), JsonConverter(typeof(DecimalConverter))]
    public decimal? Maker { get; init; }
}
