// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using CryptoExchange.Net.Authentication;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests;

/// <summary>
/// <see cref="BitvavoCredentials.Copy"/> constructs a fresh <see cref="HMACCredential"/> from the original key/secret, so
/// framework-side mutation of either copy can't bleed into the other; <see cref="BitvavoCredentials.Validate"/> accepts
/// credentials without a spot credential (public-only use) and refuses a spot credential without a key.
/// </summary>
public class BitvavoCredentialsTests
{
    [Fact]
    public void Validate_accepts_credentials_without_a_spot_credential()
    {
        Should.NotThrow(() => new BitvavoCredentials().Validate());
    }

    [Fact]
    public void Validate_accepts_a_spot_credential_with_a_key_and_a_secret()
    {
        Should.NotThrow(() => new BitvavoCredentials("test-key", "test-secret").Validate());
    }

    [Fact]
    public void Validate_refuses_a_spot_credential_without_a_key()
    {
        Should.Throw<ArgumentException>(() => new BitvavoCredentials(new HMACCredential(string.Empty, "test-secret")).Validate());
    }

    [Fact]
    public void Copy_PreservesSpotKeyAndSecret()
    {
        var original = new BitvavoCredentials("test-key", "test-secret");

        var copy = (BitvavoCredentials)original.Copy();

        copy.Spot.ShouldNotBeNull();
        copy.Spot!.Key.ShouldBe("test-key");
        copy.Spot.Secret.ShouldBe("test-secret");
    }

    [Fact]
    public void Copy_ProducesIndependentSpotCredentialInstance()
    {
        var original = new BitvavoCredentials("k", "s");

        var copy = (BitvavoCredentials)original.Copy();

        // The deep-copy contract: each Copy() must yield a freshly-constructed Spot,
        // never a shared reference, so that ApiCredentials lifecycle handling on one client
        // can't disturb another.
        copy.Spot.ShouldNotBeSameAs(original.Spot);
    }

    [Fact]
    public void Copy_EmptyCredentials_ReturnsBitvavoCredentialsWithNullSpot()
    {
        var original = new BitvavoCredentials();

        var copy = original.Copy();

        copy.ShouldBeOfType<BitvavoCredentials>();
        ((BitvavoCredentials)copy).Spot.ShouldBeNull();
    }

    [Fact]
    public void Copy_WithExplicitHmacCredential_PreservesKeyAndSecret()
    {
        var hmac = new HMACCredential("explicit-key", "explicit-secret");
        var original = new BitvavoCredentials(hmac);

        var copy = (BitvavoCredentials)original.Copy();

        copy.Spot.ShouldNotBeNull();
        copy.Spot!.Key.ShouldBe("explicit-key");
        copy.Spot.Secret.ShouldBe("explicit-secret");
    }
}
