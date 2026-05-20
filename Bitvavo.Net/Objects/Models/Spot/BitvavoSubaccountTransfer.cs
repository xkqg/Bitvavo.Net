// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;
using CryptoExchange.Net.Converters.SystemTextJson;

namespace Bitvavo.Net.Objects.Models.Spot;

/// <summary>
/// An asset transfer between an institutional main account and one of its subaccounts,
/// as returned by <c>POST /v2/subaccounts/transfers</c> and
/// <c>GET /v2/subaccounts/transfers/{transferId}</c>.
/// </summary>
public record BitvavoSubaccountTransfer
{
    /// <summary>Bitvavo-assigned identifier (UUID) of the transfer.</summary>
    [JsonPropertyName("transferId")]
    public string TransferId { get; init; } = string.Empty;

    /// <summary>Caller-supplied idempotency identifier (UUID), if one was passed on creation.</summary>
    [JsonPropertyName("clientRequestId")]
    public string? ClientRequestId { get; init; }

    /// <summary>Identifier (UUID) of the subaccount involved in the transfer.</summary>
    [JsonPropertyName("subaccountId")]
    public string SubaccountId { get; init; } = string.Empty;

    /// <summary>Transfer direction — <c>masterToSub</c> or <c>subToMaster</c>.</summary>
    [JsonPropertyName("direction")]
    public string Direction { get; init; } = string.Empty;

    /// <summary>Asset symbol that was transferred.</summary>
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Quantity of the asset transferred.</summary>
    [JsonPropertyName("amount"), JsonConverter(typeof(DecimalConverter))]
    public decimal? Amount { get; init; }

    /// <summary>Transfer status — <c>failed</c>, <c>pending</c>, or <c>completed</c>.</summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    /// <summary>When the transfer was created (UTC).</summary>
    [JsonPropertyName("createdAt"), JsonConverter(typeof(DateTimeConverter))]
    public System.DateTime CreatedAt { get; init; }
}

/// <summary>
/// Paginated list of subaccount transfers as returned by <c>GET /v2/subaccounts/transfers</c>.
/// This endpoint paginates by time-window cursor (<see cref="Start"/> / <see cref="End"/>) and
/// page size (<see cref="Limit"/>) rather than page number.
/// </summary>
public record BitvavoSubaccountTransferList
{
    /// <summary>The transfers in the requested window.</summary>
    [JsonPropertyName("items")]
    public IReadOnlyList<BitvavoSubaccountTransfer> Items { get; init; } = new List<BitvavoSubaccountTransfer>();

    /// <summary>Unix-ms lower bound of the returned window.</summary>
    [JsonPropertyName("start")]
    public long Start { get; init; }

    /// <summary>Unix-ms upper bound of the returned window.</summary>
    [JsonPropertyName("end")]
    public long End { get; init; }

    /// <summary>Maximum number of items applied to this response (1–1000, default 25).</summary>
    [JsonPropertyName("limit")]
    public int Limit { get; init; }
}
