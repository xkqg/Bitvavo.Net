// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Linq;
using System.Reflection;
using Bitvavo.Net.Clients;
using Bitvavo.Net.Interfaces.Clients;
using Bitvavo.Net.Objects.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests;

/// <summary>
/// Drives Phase 2B — the README references <c>services.AddBitvavo()</c> but the extension
/// method doesn't exist yet. These tests resolve the type by name (reflection) so the file
/// compiles even before Phase 2B lands; they fail RED until the extension is implemented,
/// turn GREEN once the registrations are wired correctly.
/// </summary>
public class BitvavoServiceCollectionExtensionsTests
{
    private static MethodInfo? FindAddBitvavoMethod()
    {
        // Expected location after Phase 2B: Bitvavo.Net.Extensions.BitvavoServiceCollectionExtensions.AddBitvavo
        var assembly = typeof(BitvavoRestClient).Assembly;
        var ext = assembly.GetType("Bitvavo.Net.Extensions.BitvavoServiceCollectionExtensions");
        if (ext == null) return null;
        return ext.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "AddBitvavo" && m.GetParameters().Length >= 1);
    }

    private static IServiceCollection InvokeAddBitvavo(
        IServiceCollection services,
        Action<BitvavoRestOptions>? rest = null,
        Action<BitvavoSocketOptions>? socket = null)
    {
        var method = FindAddBitvavoMethod()
            ?? throw new MissingMethodException("Bitvavo.Net.Extensions.BitvavoServiceCollectionExtensions.AddBitvavo");

        // Allow either AddBitvavo(IServiceCollection) or AddBitvavo(IServiceCollection, Action<RestOpts>, Action<SocketOpts>).
        var paramCount = method.GetParameters().Length;
        object?[] args = paramCount switch
        {
            1 => new object?[] { services },
            2 => new object?[] { services, rest },
            3 => new object?[] { services, rest, socket },
            _ => throw new InvalidOperationException($"Unexpected AddBitvavo arity: {paramCount}"),
        };
        return (IServiceCollection)method.Invoke(null, args)!;
    }

    [Fact]
    public void AddBitvavo_RegistersIBitvavoRestClient()
    {
        var services = new ServiceCollection();

        InvokeAddBitvavo(services);

        var sp = services.BuildServiceProvider();
        sp.GetService<IBitvavoRestClient>().ShouldNotBeNull();
    }

    [Fact]
    public void AddBitvavo_RegistersIBitvavoSocketClient()
    {
        var services = new ServiceCollection();

        InvokeAddBitvavo(services);

        var sp = services.BuildServiceProvider();
        sp.GetService<IBitvavoSocketClient>().ShouldNotBeNull();
    }

    [Fact]
    public void AddBitvavo_WithRestOptions_AppliesConfiguration()
    {
        var services = new ServiceCollection();

        InvokeAddBitvavo(services, rest: o => o.ReceiveWindowMs = 7_000);

        var sp = services.BuildServiceProvider();
        var opts = sp.GetRequiredService<IOptions<BitvavoRestOptions>>().Value;
        opts.ReceiveWindowMs.ShouldBe(7_000);
    }

    [Fact]
    public void AddBitvavo_RestClient_IsRegisteredScopedOrTransient_NotSingleton()
    {
        var services = new ServiceCollection();

        InvokeAddBitvavo(services);

        // JKorf convention: REST client is per-resolution (Scoped or Transient), not Singleton.
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IBitvavoRestClient));
        descriptor.ShouldNotBeNull();
        descriptor!.Lifetime.ShouldNotBe(ServiceLifetime.Singleton);
    }

    [Fact]
    public void AddBitvavo_SocketClient_IsRegisteredAsSingleton()
    {
        var services = new ServiceCollection();

        InvokeAddBitvavo(services);

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IBitvavoSocketClient));
        descriptor.ShouldNotBeNull();
        descriptor!.Lifetime.ShouldBe(ServiceLifetime.Singleton);
    }
}
