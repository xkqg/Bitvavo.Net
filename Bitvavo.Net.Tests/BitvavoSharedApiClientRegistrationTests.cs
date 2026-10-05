// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Linq;
using Bitvavo.Net.Clients;
using Bitvavo.Net.Extensions;
using Bitvavo.Net.Interfaces.Clients;
using Bitvavo.Net.Interfaces.Clients.SpotApi;
using Bitvavo.Net.Objects.Options;
using CryptoExchange.Net.SharedApis;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests;

/// <summary>
/// The Shared API client of the DI registration: one object that holds the REST and the socket Shared API and answers
/// "which capability, over which transport" — and a container that hands out every capability interface the Shared classes
/// implement, V2 and the legacy V1 names alike.
/// </summary>
public class BitvavoSharedApiClientRegistrationTests
{
    private static bool IsV2Capability(Type type)
        => typeof(ISharedApiCapability).IsAssignableFrom(type)
            && type != typeof(ISharedApiCapability) && type != typeof(ISharedRest)
            && type != typeof(ISharedSocket) && type != typeof(ISharedSubscription);

    private static bool IsV1Capability(Type type)
        => type.Namespace == "CryptoExchange.Net.SharedApis" && type != typeof(ISharedClient) && typeof(ISharedClient).IsAssignableFrom(type);

    /// <summary>
    /// A capability that a Shared class implements but the registration does not hand out would resolve to nothing: the
    /// interface is on the aggregate, so a consumer asking the container for it expects an instance.
    /// </summary>
    [Fact]
    public void Shared_api_client_resolves_every_capability_from_DI()
    {
        using var provider = new ServiceCollection().AddBitvavo().BuildServiceProvider();

        provider.GetRequiredService<IBitvavoSharedApiClient>().ShouldBeOfType<BitvavoSharedApiClient>();
        provider.GetRequiredService<ISharedApiClientBase>().ShouldBeOfType<BitvavoSharedApiClient>();
        provider.GetRequiredService<IBitvavoRestClientSpotSharedApi>().ShouldNotBeNull();
        provider.GetRequiredService<IBitvavoSocketClientSpotSharedApi>().ShouldNotBeNull();

        var v2 = typeof(IBitvavoRestClientSpotSharedApi).GetInterfaces()
            .Concat(typeof(IBitvavoSocketClientSpotSharedApi).GetInterfaces())
            .Where(IsV2Capability)
            .Distinct()
            .ToArray();
        var v1 = typeof(IBitvavoRestClientSpotApiShared).GetInterfaces()
            .Concat(typeof(IBitvavoSocketClientSpotApiShared).GetInterfaces())
            .Where(IsV1Capability)
            .Distinct()
            .ToArray();

        v2.ShouldNotBeEmpty();
        v1.ShouldNotBeEmpty();
        foreach (var capability in v2.Concat(v1))
        {
            provider.GetService(capability).ShouldNotBeNull(capability.Name);
        }
    }

    [Fact]
    public void Shared_api_client_exposes_the_rest_and_the_socket_shared_api()
    {
        using var provider = new ServiceCollection().AddBitvavo().BuildServiceProvider();

        var client = provider.GetRequiredService<IBitvavoSharedApiClient>();

        client.SharedApis.Count.ShouldBe(2);
        client.SpotRest.Transport.ShouldBe(SharedTransport.Rest);
        client.SpotSocket.Transport.ShouldBe(SharedTransport.Socket);
        client.Exchange.ShouldBe("Bitvavo");
        client.PreferredTransport.ShouldBe(SharedTransport.Rest);
    }

    [Fact]
    public void Shared_api_client_follows_the_configured_transport_preference()
    {
        using var provider = new ServiceCollection()
            .AddBitvavo((BitvavoOptions o) => o.SharedApi.PreferredTransport = SharedTransport.Socket)
            .BuildServiceProvider();

        provider.GetRequiredService<IBitvavoSharedApiClient>().PreferredTransport.ShouldBe(SharedTransport.Socket);
    }

    [Fact]
    public void Shared_api_client_describes_both_transports_through_Discover()
    {
        using var provider = new ServiceCollection().AddBitvavo().BuildServiceProvider();

        var info = provider.GetRequiredService<IBitvavoSharedApiClient>().Discover();

        info.SharedApis.Select(api => api.Transport).ShouldBe([SharedTransport.Rest, SharedTransport.Socket], ignoreOrder: true);
    }
}
