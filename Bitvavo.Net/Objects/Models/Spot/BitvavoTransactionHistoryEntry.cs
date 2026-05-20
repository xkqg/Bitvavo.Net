// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Text.Json.Serialization;
using CryptoExchange.Net.Converters.SystemTextJson;

namespace Bitvavo.Net.Objects.Models.Spot;

/// <summary>
/// One transaction-history entry as returned in the <c>items</c> array of
/// <c>GET /v2/account/history</c>. A transaction is any account-ledger movement —
/// trades, deposits, withdrawals, staking rewards, affiliate payouts, internal
/// transfers, rebates, etc. — distinguished by <see cref="Type"/>.
/// </summary>
public record BitvavoTransactionHistoryEntry
{
    /// <summary>Unique identifier of the transaction.</summary>
    [JsonPropertyName("transactionId")]
    public string TransactionId { get; init; } = string.Empty;

    /// <summary>When Bitvavo executed the transaction (UTC).</summary>
    [JsonPropertyName("executedAt"), JsonConverter(typeof(DateTimeConverter))]
    public DateTime ExecutedAt { get; init; }

    /// <summary>
    /// Transaction-type classification — one of <c>sell</c>, <c>buy</c>, <c>staking</c>,
    /// <c>fixed_staking</c>, <c>deposit</c>, <c>withdrawal</c>, <c>affiliate</c>,
    /// <c>distribution</c>, <c>internal_transfer</c>, <c>withdrawal_cancelled</c>,
    /// <c>rebate</c>, <c>loan</c>, <c>external_transferred_funds</c>, <c>manually_assigned</c>.
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    /// <summary>Currency in which the transaction is priced.</summary>
    [JsonPropertyName("priceCurrency")]
    public string? PriceCurrency { get; init; }

    /// <summary>Transaction price/value amount.</summary>
    [JsonPropertyName("priceAmount"), JsonConverter(typeof(DecimalConverter))]
    public decimal? PriceAmount { get; init; }

    /// <summary>Currency sent in the transaction.</summary>
    [JsonPropertyName("sentCurrency")]
    public string? SentCurrency { get; init; }

    /// <summary>Amount sent.</summary>
    [JsonPropertyName("sentAmount"), JsonConverter(typeof(DecimalConverter))]
    public decimal? SentAmount { get; init; }

    /// <summary>Currency received from the transaction.</summary>
    [JsonPropertyName("receivedCurrency")]
    public string? ReceivedCurrency { get; init; }

    /// <summary>Amount received.</summary>
    [JsonPropertyName("receivedAmount"), JsonConverter(typeof(DecimalConverter))]
    public decimal? ReceivedAmount { get; init; }

    /// <summary>Currency the fees were charged in.</summary>
    [JsonPropertyName("feesCurrency")]
    public string? FeesCurrency { get; init; }

    /// <summary>Fee amount charged on the transaction.</summary>
    [JsonPropertyName("feesAmount"), JsonConverter(typeof(DecimalConverter))]
    public decimal? FeesAmount { get; init; }

    /// <summary>On-chain or external address involved in the transaction. Null when not applicable.</summary>
    [JsonPropertyName("address")]
    public string? Address { get; init; }
}
