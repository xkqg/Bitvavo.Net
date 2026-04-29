// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Text.Json.Serialization;
using CryptoExchange.Net.Converters.SystemTextJson;

namespace Bitvavo.Net.Objects.Models.Spot;

/// <summary>One deposit-history entry as returned by <c>GET /v2/depositHistory</c>.</summary>
public record BitvavoDepositHistoryEntry
{
    /// <summary>When Bitvavo recorded the deposit (UTC).</summary>
    [JsonPropertyName("timestamp"), JsonConverter(typeof(DateTimeConverter))]
    public DateTime Timestamp { get; init; }

    /// <summary>Asset symbol — e.g. <c>"BTC"</c>, <c>"EUR"</c>.</summary>
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Net amount credited to the account.</summary>
    [JsonPropertyName("amount"), JsonConverter(typeof(DecimalConverter))]
    public decimal? Amount { get; init; }

    /// <summary>Source on-chain address. Null for fiat deposits.</summary>
    [JsonPropertyName("address")]
    public string? Address { get; init; }

    /// <summary>Optional payment-reference / memo set by the depositor.</summary>
    [JsonPropertyName("paymentId")]
    public string? PaymentReference { get; init; }

    /// <summary>On-chain transaction id. Null for fiat deposits.</summary>
    [JsonPropertyName("txId")]
    public string? TxId { get; init; }

    /// <summary>Bitvavo's deposit fee (0 for crypto, non-zero for some fiat methods).</summary>
    [JsonPropertyName("fee"), JsonConverter(typeof(DecimalConverter))]
    public decimal? Fee { get; init; }

    /// <summary>Status — e.g. <c>"completed"</c>, <c>"awaiting_processing"</c>, <c>"awaiting_email_confirmation"</c>.</summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;
}
