// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Text.Json.Serialization;
using CryptoExchange.Net.Converters.SystemTextJson;

namespace Bitvavo.Net.Objects.Models.Spot;

/// <summary>Acknowledgement Bitvavo emits after accepting a <c>POST /v2/withdrawal</c> request.</summary>
public record BitvavoWithdrawalResult
{
    /// <summary>Indicates Bitvavo accepted the withdrawal request. Final settlement still goes through the normal pending → completed lifecycle visible via <c>GetWithdrawalHistoryAsync</c>.</summary>
    [JsonPropertyName("success")]
    public bool Success { get; init; }

    /// <summary>Asset that was withdrawn — echoed back from the request.</summary>
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Total amount debited from the account (request amount + fee if <c>AddWithdrawalFee</c>=true).</summary>
    [JsonPropertyName("amount"), JsonConverter(typeof(DecimalConverter))]
    public decimal? Amount { get; init; }
}
