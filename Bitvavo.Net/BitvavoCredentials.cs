// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using CryptoExchange.Net.Authentication;

namespace Bitvavo.Net;

/// <summary>
/// API credentials for Bitvavo signed endpoints. Mirrors <c>KrakenCredentials</c> shape —
/// container class that holds an <see cref="HMACCredential"/> for the spot API. v0.1 is
/// public-only, so the spot credential is optional.
/// </summary>
public class BitvavoCredentials : ApiCredentials
{
    /// <summary>HMAC credentials for the spot API. Required only for signed endpoints.</summary>
    public HMACCredential? Spot { get; set; }

    /// <summary>Construct empty credentials — fine for public-only usage.</summary>
    public BitvavoCredentials() { }

    /// <summary>Construct with spot HMAC credentials.</summary>
    public BitvavoCredentials(HMACCredential spotCredential)
    {
        Spot = spotCredential;
    }

    /// <summary>Convenience overload — wraps the (key, secret) pair as an HMAC credential for the spot API.</summary>
    public BitvavoCredentials(string apiKey, string apiSecret)
    {
        Spot = new HMACCredential(apiKey, apiSecret);
    }

    /// <inheritdoc />
    public override ApiCredentials Copy() => new BitvavoCredentials { Spot = Spot };

    /// <inheritdoc />
    public override void Validate() => Spot?.Validate();
}
