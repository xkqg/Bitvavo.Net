// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Text.Json.Serialization;
using CryptoExchange.Net.Converters.SystemTextJson;

namespace Bitvavo.Net.Objects.Models.Spot;

/// <summary>
/// Per-side fee schedule (taker + maker) applicable to the current 30-day rolling
/// volume tier. Embedded inside <see cref="BitvavoAccountInfo"/>.
/// </summary>
public record BitvavoFeeTier
{
    /// <summary>Fee rate (decimal fraction, e.g. <c>0.0025</c> = 0.25%) charged when this account is the taker.</summary>
    [JsonPropertyName("taker"), JsonConverter(typeof(DecimalConverter))]
    public decimal? Taker { get; init; }

    /// <summary>Fee rate (decimal fraction) charged when this account is the maker.</summary>
    [JsonPropertyName("maker"), JsonConverter(typeof(DecimalConverter))]
    public decimal? Maker { get; init; }

    /// <summary>30-day rolling trading volume in EUR — drives the tier the account is on.</summary>
    [JsonPropertyName("volume"), JsonConverter(typeof(DecimalConverter))]
    public decimal? Volume { get; init; }
}
