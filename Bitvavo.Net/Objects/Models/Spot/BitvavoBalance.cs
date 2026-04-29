// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Text.Json.Serialization;
using CryptoExchange.Net.Converters.SystemTextJson;

namespace Bitvavo.Net.Objects.Models.Spot;

/// <summary>
/// Balance entry for a single asset as returned by <c>GET /v2/balance</c>. Bitvavo
/// emits monetary fields as JSON strings — the <see cref="DecimalConverter"/> lifts
/// them into <see cref="decimal"/> for callers.
/// </summary>
public record BitvavoBalance
{
    /// <summary>Asset symbol — e.g. <c>"EUR"</c>, <c>"BTC"</c>, <c>"ETH"</c>.</summary>
    [JsonPropertyName("symbol")]
    public string Symbol { get; init; } = string.Empty;

    /// <summary>Amount that's free to trade or withdraw.</summary>
    [JsonPropertyName("available"), JsonConverter(typeof(DecimalConverter))]
    public decimal? Available { get; init; }

    /// <summary>Amount reserved by open orders or pending withdrawals.</summary>
    [JsonPropertyName("inOrder"), JsonConverter(typeof(DecimalConverter))]
    public decimal? InOrder { get; init; }
}
