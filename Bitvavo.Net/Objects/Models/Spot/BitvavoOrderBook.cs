// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Bitvavo.Net.Objects.Models.Spot;

/// <summary>
/// Snapshot of the order book for a single market as returned by
/// <c>GET /v2/{market}/book</c>.
/// </summary>
public record BitvavoOrderBook
{
    /// <summary>Market identifier.</summary>
    [JsonPropertyName("market")]
    public string Market { get; init; } = string.Empty;

    /// <summary>
    /// Monotonically-increasing identifier — every order-book change increments it. Use it
    /// to detect missed updates when reconciling against the WebSocket book channel.
    /// </summary>
    [JsonPropertyName("nonce")]
    public long Nonce { get; init; }

    /// <summary>Buy orders, sorted by descending price (best bid first).</summary>
    [JsonPropertyName("bids")]
    public IReadOnlyList<BitvavoOrderBookEntry> Bids { get; init; } = new List<BitvavoOrderBookEntry>();

    /// <summary>Sell orders, sorted by ascending price (best ask first).</summary>
    [JsonPropertyName("asks")]
    public IReadOnlyList<BitvavoOrderBookEntry> Asks { get; init; } = new List<BitvavoOrderBookEntry>();
}
