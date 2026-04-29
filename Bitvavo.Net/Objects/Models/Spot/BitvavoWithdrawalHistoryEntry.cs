// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Text.Json.Serialization;
using CryptoExchange.Net.Converters.SystemTextJson;

namespace Bitvavo.Net.Objects.Models.Spot;

/// <summary>One withdrawal-history entry as returned by <c>GET /v2/withdrawalHistory</c>.</summary>
public record BitvavoWithdrawalHistoryEntry
{
    /// <summary>When Bitvavo recorded the withdrawal request (UTC).</summary>
    [JsonPropertyName("timestamp"), JsonConverter(typeof(DateTimeConverter))]
    public DateTime Timestamp { get; init; }

    /// <summary>Asset symbol — e.g. <c>"BTC"</c>, <c>"EUR"</c>.</summary>
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Net amount debited from the account.</summary>
    [JsonPropertyName("amount"), JsonConverter(typeof(DecimalConverter))]
    public decimal? Amount { get; init; }

    /// <summary>Destination on-chain address. Null for fiat withdrawals.</summary>
    [JsonPropertyName("address")]
    public string? Address { get; init; }

    /// <summary>Optional payment-reference / memo attached by Bitvavo or the withdrawer.</summary>
    [JsonPropertyName("paymentId")]
    public string? PaymentReference { get; init; }

    /// <summary>On-chain transaction id. Null for fiat withdrawals.</summary>
    [JsonPropertyName("txId")]
    public string? TxId { get; init; }

    /// <summary>Network / processing fee deducted by Bitvavo.</summary>
    [JsonPropertyName("fee"), JsonConverter(typeof(DecimalConverter))]
    public decimal? Fee { get; init; }

    /// <summary>Status — e.g. <c>"completed"</c>, <c>"awaiting_processing"</c>, <c>"awaiting_email_confirmation"</c>, <c>"canceled"</c>.</summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;
}
