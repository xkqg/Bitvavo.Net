// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Bitvavo.Net.Objects.Models.Spot;

/// <summary>
/// Balance snapshot for a subaccount (or the main account) as returned by
/// <c>GET /v2/institutional/subaccounts/balance</c>. The per-asset entry shape is the
/// regular <see cref="BitvavoBalance"/>; this wrapper carries the <c>balances</c> array.
/// </summary>
public record BitvavoSubaccountBalances
{
    /// <summary>Per-asset balances for the requested subaccount or main account.</summary>
    [JsonPropertyName("balances")]
    public IReadOnlyList<BitvavoBalance> Balances { get; init; } = new List<BitvavoBalance>();
}
