// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Text.Json.Serialization;
using CryptoExchange.Net.Converters.SystemTextJson;

namespace Bitvavo.Net.Objects.Models.Spot;

/// <summary>The accepted crypto withdrawal Bitvavo returns (HTTP 201) from <c>POST /v2/crypto/withdrawal</c>.</summary>
public record BitvavoCryptoWithdrawal
{
    /// <summary>Server-issued withdrawal id; the handle for finding the withdrawal in the history.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>The withdrawn asset.</summary>
    [JsonPropertyName("asset")]
    public string Asset { get; init; } = string.Empty;

    /// <summary>The blockchain network the withdrawal is sent on.</summary>
    [JsonPropertyName("network")]
    public string Network { get; init; } = string.Empty;

    /// <summary>The destination address.</summary>
    [JsonPropertyName("address")]
    public string Address { get; init; } = string.Empty;

    /// <summary>The withdrawn amount.</summary>
    [JsonPropertyName("amount"), JsonConverter(typeof(DecimalConverter))]
    public decimal? Amount { get; init; }

    /// <summary>The network fee.</summary>
    [JsonPropertyName("fee"), JsonConverter(typeof(DecimalConverter))]
    public decimal? Fee { get; init; }

    /// <summary>When Bitvavo accepted the withdrawal (UTC).</summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; init; }
}
