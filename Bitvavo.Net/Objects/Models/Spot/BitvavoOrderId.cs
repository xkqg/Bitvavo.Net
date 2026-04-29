// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Bitvavo.Net.Objects.Models.Spot;

/// <summary>
/// Minimal order-identifier echo returned by cancel endpoints. Used by both
/// <c>DELETE /v2/order</c> (single object) and <c>DELETE /v2/orders</c> (array).
/// </summary>
public record BitvavoOrderId
{
    /// <summary>Server-assigned order id (UUID) of the cancelled order.</summary>
    [JsonPropertyName("orderId")]
    public string OrderId { get; init; } = string.Empty;

    /// <summary>Market the cancelled order belonged to. Bitvavo includes this on batch-cancel responses; nullable for safety.</summary>
    [JsonPropertyName("market")]
    public string? Market { get; init; }
}
