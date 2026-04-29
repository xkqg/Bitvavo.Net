// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Bitvavo.Net.Objects.Internal;

/// <summary>
/// Server-emitted acknowledgment of a successful WebSocket authentication. Bitvavo
/// replies with <c>{ "event": "authenticate", "authenticated": true }</c>.
/// </summary>
internal sealed record BitvavoSocketAuthResponse
{
    [JsonPropertyName("event")]
    public string Event { get; init; } = string.Empty;

    [JsonPropertyName("authenticated")]
    public bool Authenticated { get; init; }
}
