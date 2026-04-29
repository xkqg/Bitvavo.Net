// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using CryptoExchange.Net.Authentication;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests;

/// <summary>
/// Drives Phase 2A — <see cref="BitvavoCredentials.Copy"/> currently re-uses the same
/// <see cref="HMACCredential"/> reference. After the fix it must construct a fresh
/// <see cref="HMACCredential"/> from the original key/secret so framework-side mutation of
/// either copy can't bleed into the other.
/// </summary>
public class BitvavoCredentialsTests
{
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

        // Phase 2A's deep-copy contract: each Copy() must yield a freshly-constructed Spot,
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
