// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Text.Json.Serialization;

namespace Bitvavo.Net.Objects.Models.Spot;

/// <summary>
/// Deposit address (or fiat banking instructions) as returned by <c>GET /v2/deposit</c>.
/// Bitvavo returns either crypto fields (<see cref="Address"/> + <see cref="PaymentReference"/>)
/// or fiat fields (<see cref="Iban"/> + <see cref="Bic"/> + <see cref="PaymentReference"/>),
/// depending on the asset. All fields are nullable to cover both shapes.
/// </summary>
public record BitvavoDepositAddress
{
    /// <summary>On-chain deposit address. Present for crypto assets.</summary>
    [JsonPropertyName("address")]
    public string? Address { get; init; }

    /// <summary>IBAN of the Bitvavo segregated bank account. Present for fiat assets.</summary>
    [JsonPropertyName("iban")]
    public string? Iban { get; init; }

    /// <summary>BIC/SWIFT of the Bitvavo segregated bank account. Present for fiat assets.</summary>
    [JsonPropertyName("bic")]
    public string? Bic { get; init; }

    /// <summary>Description / memo / payment reference Bitvavo expects on the deposit so the funds attribute correctly.</summary>
    [JsonPropertyName("paymentId")]
    public string? PaymentReference { get; init; }
}
