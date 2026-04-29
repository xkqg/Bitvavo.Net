// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Text.Json.Serialization;
using CryptoExchange.Net.Converters.SystemTextJson;

namespace Bitvavo.Net.Objects.Models.Spot;

/// <summary>
/// 24-hour rolling OHLCV + best-quote stats for a single market as returned by
/// <c>GET /v2/ticker/24h</c>.
/// </summary>
public record BitvavoTicker24h
{
    /// <summary>Market identifier.</summary>
    [JsonPropertyName("market")]
    public string Market { get; init; } = string.Empty;

    /// <summary>Beginning of the 24h window (UTC).</summary>
    [JsonPropertyName("startTimestamp"), JsonConverter(typeof(DateTimeConverter))]
    public DateTime? StartTimestamp { get; init; }

    /// <summary>End of the 24h window (UTC).</summary>
    [JsonPropertyName("timestamp"), JsonConverter(typeof(DateTimeConverter))]
    public DateTime? Timestamp { get; init; }

    /// <summary>Open price (first traded price in the window).</summary>
    [JsonPropertyName("open"), JsonConverter(typeof(DecimalConverter))]
    public decimal? Open { get; init; }

    /// <summary>Timestamp of the open trade (UTC).</summary>
    [JsonPropertyName("openTimestamp"), JsonConverter(typeof(DateTimeConverter))]
    public DateTime? OpenTimestamp { get; init; }

    /// <summary>Highest traded price in the window.</summary>
    [JsonPropertyName("high"), JsonConverter(typeof(DecimalConverter))]
    public decimal? High { get; init; }

    /// <summary>Lowest traded price in the window.</summary>
    [JsonPropertyName("low"), JsonConverter(typeof(DecimalConverter))]
    public decimal? Low { get; init; }

    /// <summary>Last (most recent) traded price.</summary>
    [JsonPropertyName("last"), JsonConverter(typeof(DecimalConverter))]
    public decimal? Last { get; init; }

    /// <summary>Timestamp of the close (last) trade (UTC).</summary>
    [JsonPropertyName("closeTimestamp"), JsonConverter(typeof(DateTimeConverter))]
    public DateTime? CloseTimestamp { get; init; }

    /// <summary>Best bid price.</summary>
    [JsonPropertyName("bid"), JsonConverter(typeof(DecimalConverter))]
    public decimal? Bid { get; init; }

    /// <summary>Quantity at the best bid.</summary>
    [JsonPropertyName("bidSize"), JsonConverter(typeof(DecimalConverter))]
    public decimal? BidSize { get; init; }

    /// <summary>Best ask price.</summary>
    [JsonPropertyName("ask"), JsonConverter(typeof(DecimalConverter))]
    public decimal? Ask { get; init; }

    /// <summary>Quantity at the best ask.</summary>
    [JsonPropertyName("askSize"), JsonConverter(typeof(DecimalConverter))]
    public decimal? AskSize { get; init; }

    /// <summary>Total volume traded in the window, base-asset units.</summary>
    [JsonPropertyName("volume"), JsonConverter(typeof(DecimalConverter))]
    public decimal? Volume { get; init; }

    /// <summary>Total volume traded in the window, quote-asset units.</summary>
    [JsonPropertyName("volumeQuote"), JsonConverter(typeof(DecimalConverter))]
    public decimal? VolumeQuote { get; init; }
}
