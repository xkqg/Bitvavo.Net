// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using Bitvavo.Net.Clients;
using Bitvavo.Net.Clients.SpotApi;
using Bitvavo.Net.Objects.Options;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests;

/// <summary>
/// The clients built without a container: with an options delegate, with none, and with the process-wide defaults
/// (<c>SetDefaultOptions</c>) that every new client starts from. The defaults are static state, so every test that changes them
/// puts them back.
/// </summary>
public class BitvavoClientDefaultsTests
{
    [Fact]
    public void A_rest_client_without_a_delegate_starts_from_the_library_defaults()
    {
        using var client = new BitvavoRestClient();

        var options = ((BitvavoRestClientSpotApi)client.SpotApi).ClientOptions;
        options.ReceiveWindowMs.ShouldBe(10_000);
        options.Environment.ShouldBeSameAs(BitvavoEnvironment.Live);
    }

    [Fact]
    public void A_rest_client_applies_its_options_delegate()
    {
        using var client = new BitvavoRestClient(o => o.ReceiveWindowMs = 4_321);

        ((BitvavoRestClientSpotApi)client.SpotApi).ClientOptions.ReceiveWindowMs.ShouldBe(4_321);
    }

    [Fact]
    public void SetDefaultOptions_changes_the_start_of_every_new_rest_client()
    {
        var previous = BitvavoRestOptions.Default;
        try
        {
            BitvavoRestClient.SetDefaultOptions(o => o.ReceiveWindowMs = 5_555);

            using var client = new BitvavoRestClient();

            ((BitvavoRestClientSpotApi)client.SpotApi).ClientOptions.ReceiveWindowMs.ShouldBe(5_555);
        }
        finally
        {
            BitvavoRestOptions.Default = previous;
        }
    }

    [Fact]
    public void A_socket_client_without_a_delegate_starts_from_the_library_defaults()
    {
        using var client = new BitvavoSocketClient();

        var options = ((BitvavoSocketClientSpotApi)client.SpotApi).ClientOptions;
        options.ReceiveWindowMs.ShouldBe(10_000);
        options.SocketSubscriptionsCombineTarget.ShouldBe(10);
        options.Environment.ShouldBeSameAs(BitvavoEnvironment.Live);
    }

    [Fact]
    public void A_socket_client_applies_its_options_delegate()
    {
        using var client = new BitvavoSocketClient(o => o.ReceiveWindowMs = 8_765);

        ((BitvavoSocketClientSpotApi)client.SpotApi).ClientOptions.ReceiveWindowMs.ShouldBe(8_765);
    }

    [Fact]
    public void SetDefaultOptions_changes_the_start_of_every_new_socket_client()
    {
        var previous = BitvavoSocketOptions.Default;
        try
        {
            BitvavoSocketClient.SetDefaultOptions(o => o.ReceiveWindowMs = 6_666);

            using var client = new BitvavoSocketClient();

            ((BitvavoSocketClientSpotApi)client.SpotApi).ClientOptions.ReceiveWindowMs.ShouldBe(6_666);
        }
        finally
        {
            BitvavoSocketOptions.Default = previous;
        }
    }
}
