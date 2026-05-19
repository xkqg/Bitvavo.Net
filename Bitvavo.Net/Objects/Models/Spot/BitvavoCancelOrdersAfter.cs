// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Bitvavo.Net.Objects.Models.Spot;

/// <summary>
/// Response payload from <c>POST /v2/cancelOrdersAfter</c>. Bitvavo echoes the group + the
/// deadline timestamp at which the broker will cancel every open order in the group.
/// </summary>
public sealed record BitvavoCancelOrdersAfter
{
    /// <summary>Group identifier echoed back by the broker.</summary>
    [JsonPropertyName("codGroupId")]
    public string CodGroupId { get; init; } = string.Empty;

    /// <summary>Unix-ms deadline at which the broker cancels every order in the group.</summary>
    [JsonPropertyName("expiresAt")]
    public long ExpiresAt { get; init; }
}
