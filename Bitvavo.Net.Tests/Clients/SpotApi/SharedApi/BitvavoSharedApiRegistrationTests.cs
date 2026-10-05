// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Bitvavo.Net.Interfaces.Clients.SpotApi;
using CryptoExchange.Net.SharedApis;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests.Clients.SpotApi.SharedApi;

/// <summary>
/// The extension seam of the Shared API is open for extension and closed for modification: a capability is a partial file, one
/// interface in each aggregate, and one options object in <c>SetCapabilities</c>. These tests make the three agree — a capability
/// that is implemented but not registered would silently vanish from <c>Capabilities</c>, <c>Discover()</c> and the DI lookups.
/// </summary>
public class BitvavoSharedApiRegistrationTests
{
    private const string SharedNamespace = "CryptoExchange.Net.SharedApis";

    private sealed record ApiUnderTest(string Name, ISharedApi Api, Type LegacyAggregate, Type CapabilityAggregate);

    /// <summary>A [V2] capability: an interface of the Shared API that is a REST or socket capability but none of the infrastructure markers.</summary>
    private static bool IsCapabilityInterface(Type i)
        => i != typeof(ISharedRest) && i != typeof(ISharedSocket) && i != typeof(ISharedSubscription)
            && (typeof(ISharedRest).IsAssignableFrom(i) || typeof(ISharedSocket).IsAssignableFrom(i));

    private static ApiUnderTest Rest()
    {
        var api = new StubHttpMessageHandler("[]").RestClient().SpotApi;
        return new ApiUnderTest("REST", api.SharedApi, typeof(IBitvavoRestClientSpotApiShared), typeof(IBitvavoRestClientSpotSharedApi));
    }

    private static ApiUnderTest Socket()
    {
        var client = new Bitvavo.Net.Clients.BitvavoSocketClient(
            new Microsoft.Extensions.Logging.LoggerFactory(),
            Microsoft.Extensions.Options.Options.Create(new Bitvavo.Net.Objects.Options.BitvavoSocketOptions()));
        return new ApiUnderTest("Socket", client.SpotApi.SharedApi, typeof(IBitvavoSocketClientSpotApiShared), typeof(IBitvavoSocketClientSpotSharedApi));
    }

    public static IEnumerable<object[]> Transports() => [["REST"], ["Socket"]];

    private static ApiUnderTest Create(string transport) => transport == "REST" ? Rest() : Socket();

    /// <summary>Every options object an implemented interface exposes (V1 names included) is registered, and every registered one is exposed.</summary>
    [Theory]
    [MemberData(nameof(Transports))]
    public void Every_implemented_capability_is_registered(string transport)
    {
        var under = Create(transport);

        var exposed = under.Api.GetType().GetInterfaces()
            .SelectMany(i => i.GetProperties())
            .Where(p => typeof(CapabilityOptions).IsAssignableFrom(p.PropertyType))
            .Select(p => (CapabilityOptions)p.GetValue(under.Api)!)
            .Distinct(ReferenceEqualityComparer.Instance)
            .Cast<CapabilityOptions>()
            .Select(o => o.OperationName)
            .Order()
            .ToArray();
        var registered = under.Api.Capabilities.Select(o => o.OperationName).Order().ToArray();

        registered.ShouldBe(exposed);
        registered.Distinct().Count().ShouldBe(registered.Length, "an operation is registered once");
    }

    /// <summary>The [V2] aggregate lists every capability interface the class implements and the [V1] aggregate every legacy interface.</summary>
    [Theory]
    [MemberData(nameof(Transports))]
    public void The_aggregate_interfaces_list_exactly_what_the_class_implements(string transport)
    {
        var under = Create(transport);
        var implemented = under.Api.GetType().GetInterfaces();

        var capabilities = implemented
            .Where(i => i.Namespace == SharedNamespace && IsCapabilityInterface(i))
            .OrderBy(i => i.Name)
            .ToArray();
        var legacy = implemented
            .Where(i => i.Namespace == SharedNamespace && i != typeof(ISharedClient) && typeof(ISharedClient).IsAssignableFrom(i))
            .OrderBy(i => i.Name)
            .ToArray();

        under.CapabilityAggregate.GetInterfaces().Where(i => capabilities.Contains(i)).OrderBy(i => i.Name).ShouldBe(capabilities);
        under.LegacyAggregate.GetInterfaces().Where(i => legacy.Contains(i)).OrderBy(i => i.Name).ShouldBe(legacy);
    }

    [Theory]
    [MemberData(nameof(Transports))]
    public void Discover_describes_the_registered_capabilities(string transport)
    {
        var under = Create(transport);

        var info = under.Api.Discover();

        info.Exchange.ShouldBe("Bitvavo");
        info.Transport.ShouldBe(under.Api.Transport);
        info.SupportedTradingModes.ShouldBe([TradingMode.Spot]);
        info.SupportedEnvironments.ShouldBe([BitvavoEnvironment.Live.Name]);
        info.Capabilities.Length.ShouldBe(under.Api.Capabilities.Count);
    }

    [Fact]
    public void SharedClient_and_SharedApi_are_one_instance_and_not_the_api_client_itself()
    {
        var api = new StubHttpMessageHandler("[]").RestClient().SpotApi;

        ((object)api.SharedClient).ShouldBeSameAs(api.SharedApi);
        ((object)api.SharedApi).ShouldNotBeSameAs(api);
    }

    /// <summary>The default exchange parameters of one exchange are cleared without touching the defaults of another exchange in the same process.</summary>
    [Fact]
    public void ResetDefaultExchangeParameters_leaves_other_exchanges_untouched()
    {
        var api = new StubHttpMessageHandler("[]").RestClient().SpotApi.SharedApi;
        ExchangeParameters.SetStaticParameter("OtherExchange", "recvWindow", 5000);
        try
        {
            api.SetDefaultExchangeParameter("OperatorId", 7L);
            ExchangeParameters.GetValue<long?>(null, "Bitvavo", "OperatorId").ShouldBe(7L);

            api.ResetDefaultExchangeParameters();

            ExchangeParameters.GetValue<long?>(null, "Bitvavo", "OperatorId").ShouldBeNull();
            ExchangeParameters.GetValue<int?>(null, "OtherExchange", "recvWindow").ShouldBe(5000);
        }
        finally
        {
            ExchangeParameters.ResetStaticExchangeParameters("OtherExchange");
            ExchangeParameters.ResetStaticExchangeParameters("Bitvavo");
        }
    }
}
