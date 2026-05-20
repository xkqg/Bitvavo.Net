// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Bitvavo.Net.Objects.Models.Spot;

/// <summary>
/// Response payload from <c>POST /v2/cancelOrdersAfter</c>. Bitvavo echoes the group + the
/// deadline timestamp at which the broker will cancel every open order in the group.
/// </summary>
public sealed record BitvavoCancelOrdersAfter
{
    /// <summary>Numeric group identifier echoed back by the broker.</summary>
    [JsonPropertyName("codGroupId")]
    public int CodGroupId { get; init; }

    /// <summary>Unix-SECONDS deadline at which the broker cancels every open order in the group.</summary>
    [JsonPropertyName("timeOfExpirySeconds")]
    public long TimeOfExpirySeconds { get; init; }
}
