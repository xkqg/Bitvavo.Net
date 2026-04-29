// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using CryptoExchange.Net.Converters.SystemTextJson;

namespace Bitvavo.Net.Objects.Models.Spot;

/// <summary>
/// Single asset descriptor as returned by <c>GET /v2/assets</c> — covers static metadata
/// (name, decimals, networks) plus deposit / withdrawal lifecycle state.
/// </summary>
public record BitvavoAsset
{
    /// <summary>Asset symbol — e.g. <c>"BTC"</c>, <c>"EUR"</c>.</summary>
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Full asset name — e.g. <c>"Bitcoin"</c>.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>Number of significant decimal places used by Bitvavo for this asset.</summary>
    [JsonPropertyName("decimals")]
    public int Decimals { get; init; }

    /// <summary>Fee Bitvavo charges per deposit (typically 0 for crypto, non-zero for some fiat methods).</summary>
    [JsonPropertyName("depositFee"), JsonConverter(typeof(DecimalConverter))]
    public decimal? DepositFee { get; init; }

    /// <summary>Minimum number of network confirmations Bitvavo waits for before crediting a deposit.</summary>
    [JsonPropertyName("depositConfirmations")]
    public int DepositConfirmations { get; init; }

    /// <summary>Current deposit availability — typical values <c>"OK"</c>, <c>"MAINTENANCE"</c>, <c>"DELISTED"</c>.</summary>
    [JsonPropertyName("depositStatus")]
    public string DepositStatus { get; init; } = string.Empty;

    /// <summary>Fee Bitvavo charges per withdrawal.</summary>
    [JsonPropertyName("withdrawalFee"), JsonConverter(typeof(DecimalConverter))]
    public decimal? WithdrawalFee { get; init; }

    /// <summary>Minimum withdrawal amount.</summary>
    [JsonPropertyName("withdrawalMinAmount"), JsonConverter(typeof(DecimalConverter))]
    public decimal? WithdrawalMinAmount { get; init; }

    /// <summary>Current withdrawal availability — typical values <c>"OK"</c>, <c>"MAINTENANCE"</c>, <c>"DELISTED"</c>.</summary>
    [JsonPropertyName("withdrawalStatus")]
    public string WithdrawalStatus { get; init; } = string.Empty;

    /// <summary>Supported blockchain networks for crypto assets (e.g. <c>["Bitcoin"]</c>, <c>["Mainnet", "BSC"]</c>).</summary>
    [JsonPropertyName("networks")]
    public IReadOnlyList<string> Networks { get; init; } = new List<string>();

    /// <summary>Free-text explanation when <see cref="DepositStatus"/> or <see cref="WithdrawalStatus"/> is non-OK.</summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }
}
