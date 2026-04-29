// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Bitvavo.Net.Objects.Models.Spot;

/// <summary>
/// Account-wide information as returned by <c>GET /v2/account</c>: the active fee tier
/// (taker/maker rates + 30-day volume) and the list of capabilities granted to the API key.
/// </summary>
public record BitvavoAccountInfo
{
    /// <summary>Active fee tier (taker, maker, 30-day volume).</summary>
    [JsonPropertyName("fees")]
    public BitvavoFeeTier Fees { get; init; } = new();

    /// <summary>Capabilities granted to the API key — e.g. <c>["buy", "sell", "view"]</c>.</summary>
    [JsonPropertyName("capabilities")]
    public IReadOnlyList<string> Capabilities { get; init; } = new List<string>();
}
