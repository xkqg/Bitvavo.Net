// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Text.Json.Serialization;
using CryptoExchange.Net.Converters.SystemTextJson;

namespace Bitvavo.Net.Objects.Models.Spot;

/// <summary>The staked amount of one asset, as returned by <c>GET /v2/stakingBalance</c>.</summary>
public record BitvavoStakingBalance
{
    /// <summary>Asset symbol (e.g. <c>"ADA"</c>).</summary>
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Amount of the asset currently staked.</summary>
    [JsonPropertyName("amount"), JsonConverter(typeof(DecimalConverter))]
    public decimal? Amount { get; init; }
}
