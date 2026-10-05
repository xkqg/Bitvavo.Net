// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Bitvavo.Net.Clients;
using Bitvavo.Net.Objects.Options;
using CryptoExchange.Net.SharedApis;
using Microsoft.Extensions.Configuration;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests;

/// <summary>
/// The library-wide options object of the DI registration: one place to set the environment, the credentials, the REST and
/// socket sections and the Shared API transport preference, built from code or from configuration, normalised so that the
/// REST and socket halves agree with the top level unless they say otherwise.
/// </summary>
public class BitvavoOptionsTests
{
    private static IConfiguration Configuration(Dictionary<string, string?> values)
        => new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    [Fact]
    public void Create_without_a_delegate_targets_live_everywhere()
    {
        var options = BitvavoOptions.Create();

        options.Rest.Environment.ShouldBeSameAs(BitvavoEnvironment.Live);
        options.Socket.Environment.ShouldBeSameAs(BitvavoEnvironment.Live);
        options.SharedApi.PreferredTransport.ShouldBe(SharedTransport.Rest);
    }

    [Fact]
    public void Create_hands_the_top_level_credentials_to_rest_and_socket()
    {
        var options = BitvavoOptions.Create(o => o.ApiCredentials = new BitvavoCredentials("top-key", "top-secret"));

        options.Rest.ApiCredentials.ShouldNotBeNull().Spot.ShouldNotBeNull().Key.ShouldBe("top-key");
        options.Socket.ApiCredentials.ShouldNotBeNull().Spot.ShouldNotBeNull().Key.ShouldBe("top-key");
    }

    [Fact]
    public void Create_keeps_explicit_rest_credentials_over_the_top_level_ones()
    {
        var options = BitvavoOptions.Create(o =>
        {
            o.ApiCredentials = new BitvavoCredentials("top-key", "top-secret");
            o.Rest.ApiCredentials = new BitvavoCredentials("rest-key", "rest-secret");
        });

        options.Rest.ApiCredentials.ShouldNotBeNull().Spot.ShouldNotBeNull().Key.ShouldBe("rest-key");
        options.Socket.ApiCredentials.ShouldNotBeNull().Spot.ShouldNotBeNull().Key.ShouldBe("top-key");
    }

    [Fact]
    public void Create_hands_the_top_level_environment_to_rest_and_socket()
    {
        var custom = new BitvavoEnvironment("custom", "https://rest.example.test", "wss://ws.example.test/v2/");

        var options = BitvavoOptions.Create(o => o.Environment = custom);

        options.Rest.Environment.ShouldBeSameAs(custom);
        options.Socket.Environment.ShouldBeSameAs(custom);
    }

    [Fact]
    public void CreateFromConfiguration_binds_the_rest_socket_and_shared_api_sections()
    {
        var options = BitvavoOptions.CreateFromConfiguration(Configuration(new Dictionary<string, string?>
        {
            ["Rest:ReceiveWindowMs"] = "7000",
            ["Rest:RequestTimeout"] = "00:00:07",
            ["Socket:ReceiveWindowMs"] = "9000",
            ["SharedApi:PreferredTransport"] = "Socket",
            ["ApiCredentials:Spot:Key"] = "cfg-key",
            ["ApiCredentials:Spot:Secret"] = "cfg-secret",
        }));

        options.Rest.ReceiveWindowMs.ShouldBe(7000);
        options.Rest.RequestTimeout.ShouldBe(TimeSpan.FromSeconds(7));
        options.Socket.ReceiveWindowMs.ShouldBe(9000);
        options.SharedApi.PreferredTransport.ShouldBe(SharedTransport.Socket);
        options.Rest.ApiCredentials.ShouldNotBeNull().Spot.ShouldNotBeNull().Key.ShouldBe("cfg-key");
        options.Socket.ApiCredentials.ShouldNotBeNull().Spot.ShouldNotBeNull().Secret.ShouldBe("cfg-secret");
    }

    [Theory]
    [InlineData("live")]
    [InlineData("Live")]
    [InlineData("LIVE")]
    public void CreateFromConfiguration_resolves_the_environment_name_whatever_the_casing(string name)
    {
        var options = BitvavoOptions.CreateFromConfiguration(Configuration(new Dictionary<string, string?>
        {
            ["Environment:Name"] = name,
            ["Rest:Environment:Name"] = name,
            ["Socket:Environment:Name"] = name,
        }));

        options.Environment.ShouldBeSameAs(BitvavoEnvironment.Live);
        options.Rest.Environment.ShouldBeSameAs(BitvavoEnvironment.Live);
        options.Socket.Environment.ShouldBeSameAs(BitvavoEnvironment.Live);
    }

    [Theory]
    [InlineData("Environment:Name")]
    [InlineData("Rest:Environment:Name")]
    [InlineData("Socket:Environment:Name")]
    public void Unknown_environment_name_throws(string key)
    {
        var configuration = Configuration(new Dictionary<string, string?> { [key] = "staging" });

        var error = Should.Throw<InvalidOperationException>(() => BitvavoOptions.CreateFromConfiguration(configuration));

        error.Message.ShouldContain("staging");
        error.Message.ShouldContain(BitvavoEnvironment.Live.Name);
    }

    [Fact]
    public void CreateFromConfiguration_wraps_a_binding_failure()
    {
        var configuration = Configuration(new Dictionary<string, string?> { ["Rest:ReceiveWindowMs"] = "not-a-number" });

        var error = Should.Throw<InvalidOperationException>(() => BitvavoOptions.CreateFromConfiguration(configuration));

        error.Message.ShouldContain("Bitvavo");
        error.InnerException.ShouldNotBeNull();
    }

    [Fact]
    public void CreateFromConfiguration_rejects_a_missing_configuration()
    {
        Should.Throw<ArgumentNullException>(() => BitvavoOptions.CreateFromConfiguration(null!));
    }

    [Fact]
    public void Create_rejects_a_null_rest_section()
    {
        var error = Should.Throw<ArgumentException>(() => BitvavoOptions.Create(o => o.Rest = null!));

        error.Message.ShouldContain("REST");
    }

    [Fact]
    public void Create_rejects_a_null_socket_section()
    {
        var error = Should.Throw<ArgumentException>(() => BitvavoOptions.Create(o => o.Socket = null!));

        error.Message.ShouldContain("Socket");
    }

    /// <summary>
    /// Runs <paramref name="body"/> with process-wide defaults (<c>SetDefaultOptions</c>) that carry the credentials of
    /// <paramref name="key"/> and <paramref name="environment"/>, and puts the previous defaults back — they are static state.
    /// </summary>
    private static void WithProcessWideDefaults(string key, BitvavoEnvironment environment, Action body)
    {
        var previousRest = BitvavoRestOptions.Default;
        var previousSocket = BitvavoSocketOptions.Default;
        try
        {
            BitvavoRestClient.SetDefaultOptions(o =>
            {
                o.ApiCredentials = new BitvavoCredentials(key, "default-secret");
                o.Environment = environment;
            });
            BitvavoSocketClient.SetDefaultOptions(o =>
            {
                o.ApiCredentials = new BitvavoCredentials(key, "default-secret");
                o.Environment = environment;
            });
            body();
        }
        finally
        {
            BitvavoRestOptions.Default = previousRest;
            BitvavoSocketOptions.Default = previousSocket;
        }
    }

    private static readonly BitvavoEnvironment DefaultEnvironment = new("default-env", "https://rest.default.test", "wss://ws.default.test/v2/");

    /// <summary>What the caller sets beats the process-wide defaults, as it does on the legacy overload: the defaults are the lowest layer.</summary>
    [Fact]
    public void Create_prefers_the_top_level_credentials_over_the_process_wide_defaults()
    {
        WithProcessWideDefaults("default-key", DefaultEnvironment, () =>
        {
            var options = BitvavoOptions.Create(o => o.ApiCredentials = new BitvavoCredentials("top-key", "top-secret"));

            options.Rest.ApiCredentials.ShouldNotBeNull().Spot.ShouldNotBeNull().Key.ShouldBe("top-key");
            options.Socket.ApiCredentials.ShouldNotBeNull().Spot.ShouldNotBeNull().Key.ShouldBe("top-key");
        });
    }

    [Fact]
    public void Create_falls_back_to_the_process_wide_default_credentials_when_nothing_else_is_set()
    {
        WithProcessWideDefaults("default-key", DefaultEnvironment, () =>
        {
            var options = BitvavoOptions.Create();

            options.Rest.ApiCredentials.ShouldNotBeNull().Spot.ShouldNotBeNull().Key.ShouldBe("default-key");
            options.Socket.ApiCredentials.ShouldNotBeNull().Spot.ShouldNotBeNull().Key.ShouldBe("default-key");
        });
    }

    [Fact]
    public void Create_keeps_explicit_section_credentials_over_the_process_wide_defaults_and_the_top_level()
    {
        WithProcessWideDefaults("default-key", DefaultEnvironment, () =>
        {
            var options = BitvavoOptions.Create(o =>
            {
                o.ApiCredentials = new BitvavoCredentials("top-key", "top-secret");
                o.Rest.ApiCredentials = new BitvavoCredentials("rest-key", "rest-secret");
            });

            options.Rest.ApiCredentials.ShouldNotBeNull().Spot.ShouldNotBeNull().Key.ShouldBe("rest-key");
            options.Socket.ApiCredentials.ShouldNotBeNull().Spot.ShouldNotBeNull().Key.ShouldBe("top-key");
        });
    }

    [Fact]
    public void CreateFromConfiguration_prefers_the_configured_credentials_over_the_process_wide_defaults()
    {
        WithProcessWideDefaults("default-key", DefaultEnvironment, () =>
        {
            var options = BitvavoOptions.CreateFromConfiguration(Configuration(new Dictionary<string, string?>
            {
                ["ApiCredentials:Spot:Key"] = "cfg-key",
                ["ApiCredentials:Spot:Secret"] = "cfg-secret",
            }));

            options.Rest.ApiCredentials.ShouldNotBeNull().Spot.ShouldNotBeNull().Key.ShouldBe("cfg-key");
            options.Socket.ApiCredentials.ShouldNotBeNull().Spot.ShouldNotBeNull().Key.ShouldBe("cfg-key");
        });
    }

    [Fact]
    public void Create_falls_back_to_the_process_wide_default_environment_when_nothing_else_is_set()
    {
        WithProcessWideDefaults("default-key", DefaultEnvironment, () =>
        {
            var options = BitvavoOptions.Create();

            options.Rest.Environment.ShouldBeSameAs(DefaultEnvironment);
            options.Socket.Environment.ShouldBeSameAs(DefaultEnvironment);
        });
    }

    [Fact]
    public void Create_prefers_the_top_level_environment_over_the_process_wide_default()
    {
        var custom = new BitvavoEnvironment("custom", "https://rest.example.test", "wss://ws.example.test/v2/");

        WithProcessWideDefaults("default-key", DefaultEnvironment, () =>
        {
            var options = BitvavoOptions.Create(o => o.Environment = custom);

            options.Rest.Environment.ShouldBeSameAs(custom);
            options.Socket.Environment.ShouldBeSameAs(custom);
        });
    }

    [Fact]
    public void CopyTo_carries_the_environment_the_credentials_the_sections_and_the_shared_api_options()
    {
        var source = BitvavoOptions.Create(o =>
        {
            o.ApiCredentials = new BitvavoCredentials("copy-key", "copy-secret");
            o.SocketClientLifeTime = Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped;
            o.Rest.ReceiveWindowMs = 4_321;
            o.Socket.ReceiveWindowMs = 8_765;
            o.SharedApi.PreferredTransport = SharedTransport.Socket;
        });
        var target = new BitvavoOptions();

        source.CopyTo(target);

        target.ApiCredentials.ShouldNotBeNull().Spot.ShouldNotBeNull().Key.ShouldBe("copy-key");
        target.ApiCredentials.ShouldNotBeSameAs(source.ApiCredentials, "the credentials are copied, not shared");
        target.SocketClientLifeTime.ShouldBe(Microsoft.Extensions.DependencyInjection.ServiceLifetime.Scoped);
        target.Rest.ReceiveWindowMs.ShouldBe(4_321);
        target.Socket.ReceiveWindowMs.ShouldBe(8_765);
        target.SharedApi.PreferredTransport.ShouldBe(SharedTransport.Socket);
        target.SharedApi.ShouldNotBeSameAs(source.SharedApi);
    }
}
