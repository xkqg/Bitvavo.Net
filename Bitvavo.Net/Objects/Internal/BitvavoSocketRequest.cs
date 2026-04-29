// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Bitvavo.Net.Objects.Internal;

/// <summary>
/// Bitvavo WebSocket subscribe / unsubscribe envelope. Wire format:
/// <code>{ "action": "subscribe" | "unsubscribe", "channels": [ ...channels... ] }</code>
/// Mirrors the wire shape from Bitvavo's official Go + Python SDKs.
/// </summary>
internal sealed class BitvavoSocketRequest
{
    [JsonPropertyName("action")]
    public string Action { get; set; } = string.Empty;

    [JsonPropertyName("channels")]
    public BitvavoSocketChannel[] Channels { get; set; } = System.Array.Empty<BitvavoSocketChannel>();
}

/// <summary>
/// Single channel descriptor inside a <see cref="BitvavoSocketRequest"/>. <see cref="Interval"/>
/// is only emitted for the <c>candles</c> channel; null suppresses the property entirely
/// (matches the trades / ticker / book envelope shape).
/// </summary>
internal sealed class BitvavoSocketChannel
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("interval"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string[]? Interval { get; set; }

    [JsonPropertyName("markets")]
    public string[] Markets { get; set; } = System.Array.Empty<string>();
}
