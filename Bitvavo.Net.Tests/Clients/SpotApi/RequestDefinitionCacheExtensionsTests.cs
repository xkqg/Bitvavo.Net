// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Net.Http;
using Bitvavo.Net.Clients.SpotApi;
using CryptoExchange.Net.Objects;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests.Clients.SpotApi;

/// <summary>
/// Unit tests for <see cref="RequestDefinitionCacheExtensions"/> — the Bitvavo-internal
/// "InUri-DELETE" cache helper. The 5-arg overload added in P1-bis threads the per-host
/// <see cref="BitvavoRestClientSpotApi.RateLimitGate"/> and a per-endpoint weight through
/// <c>GetOrCreate</c> so InUri DELETEs are rate-limit-gated like every other endpoint.
/// </summary>
public class RequestDefinitionCacheExtensionsTests
{
    [Fact]
    public void GetOrCreateInUri_Gated_SetsGateWeightAndInUriPosition()
    {
        var cache = new RequestDefinitionCache();

        var def = cache.GetOrCreateInUri(
            HttpMethod.Delete, "v2/order", BitvavoRestClientSpotApi.RateLimitGate,
            weight: 1, authenticated: true);

        def.RateLimitGate.ShouldBeSameAs(BitvavoRestClientSpotApi.RateLimitGate);
        def.Weight.ShouldBe(1);
        def.ParameterPosition.ShouldBe(HttpMethodParameterPosition.InUri);
        def.Authenticated.ShouldBeTrue();
        def.Method.ShouldBe(HttpMethod.Delete);
        // RequestDefinitionCache normalises the path to a leading-slash form.
        def.Path.ShouldEndWith("v2/order");
    }

    [Fact]
    public void GetOrCreateInUri_Gated_IsIdempotent_ReturnsSameInstance()
    {
        var cache = new RequestDefinitionCache();

        var first = cache.GetOrCreateInUri(
            HttpMethod.Delete, "v2/orders", BitvavoRestClientSpotApi.RateLimitGate,
            weight: 100, authenticated: true);
        var second = cache.GetOrCreateInUri(
            HttpMethod.Delete, "v2/orders", BitvavoRestClientSpotApi.RateLimitGate,
            weight: 100, authenticated: true);

        second.ShouldBeSameAs(first);
    }

    [Fact]
    public void GetOrCreateInUri_Gated_CarriesEndpointWeight()
    {
        var cache = new RequestDefinitionCache();

        var def = cache.GetOrCreateInUri(
            HttpMethod.Delete, "v2/orders", BitvavoRestClientSpotApi.RateLimitGate,
            weight: 100, authenticated: true);

        def.Weight.ShouldBe(100);
    }
}
