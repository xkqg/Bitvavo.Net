// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Bitvavo.Net.Objects.Models.Spot;

/// <summary>
/// A subaccount under an institutional main account, as returned by
/// <c>POST /v2/subaccounts</c> and elements of <c>GET /v2/subaccounts</c>.
/// </summary>
public record BitvavoSubaccount
{
    /// <summary>Unique identifier (UUID) of the subaccount.</summary>
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>Subaccount type — <c>spot</c> or <c>margin</c>.</summary>
    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    /// <summary>Current status — <c>open</c> or <c>closed</c>.</summary>
    [JsonPropertyName("status")]
    public string Status { get; init; } = string.Empty;

    /// <summary>Caller-supplied description of the subaccount (up to 100 characters). Null when none was set.</summary>
    [JsonPropertyName("label")]
    public string? Label { get; init; }
}

/// <summary>
/// Paginated list of subaccounts as returned by <c>GET /v2/subaccounts</c>.
/// </summary>
public record BitvavoSubaccountList
{
    /// <summary>The subaccounts on the current page.</summary>
    [JsonPropertyName("items")]
    public IReadOnlyList<BitvavoSubaccount> Items { get; init; } = new List<BitvavoSubaccount>();

    /// <summary>One-based index of the page returned.</summary>
    [JsonPropertyName("currentPage")]
    public int CurrentPage { get; init; }

    /// <summary>Total number of pages available.</summary>
    [JsonPropertyName("totalPages")]
    public int TotalPages { get; init; }

    /// <summary>Maximum number of items per page applied to this response.</summary>
    [JsonPropertyName("maxItems")]
    public int MaxItems { get; init; }
}
