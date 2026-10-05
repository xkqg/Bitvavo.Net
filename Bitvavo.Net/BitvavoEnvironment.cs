// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using CryptoExchange.Net.Objects;

namespace Bitvavo.Net;

/// <summary>
/// Bitvavo trading environment. Bitvavo doesn't operate a separate testnet/sandbox, so
/// <see cref="Live"/> is the only environment.
/// </summary>
public class BitvavoEnvironment : TradeEnvironment
{
    /// <summary>Spot REST base URL.</summary>
    public string SpotRestBaseAddress { get; }

    /// <summary>Spot WebSocket public-stream URL (no auth required).</summary>
    public string SpotSocketPublicAddress { get; }

    internal BitvavoEnvironment(string name, string spotRestBaseAddress, string spotSocketPublicAddress)
        : base(name)
    {
        SpotRestBaseAddress = spotRestBaseAddress;
        SpotSocketPublicAddress = spotSocketPublicAddress;
    }

#pragma warning disable CS8618
    /// <summary>Parameterless ctor for DI / configuration binding. Use <see cref="Live"/> for normal construction.</summary>
    public BitvavoEnvironment() : base(TradeEnvironmentNames.Live) { }
#pragma warning restore CS8618

    /// <summary>Production environment — the only Bitvavo environment.</summary>
    public static BitvavoEnvironment Live { get; } = new(
        TradeEnvironmentNames.Live,
        spotRestBaseAddress: "https://api.bitvavo.com",
        spotSocketPublicAddress: "wss://ws.bitvavo.com/v2/");

    /// <summary>
    /// Look up an environment by name, ignoring case (a <c>"Live"</c> from an appsettings file finds the same environment as
    /// <c>"live"</c>). Returns <see cref="Live"/> for a null or empty name, and null for a name that is no environment.
    /// </summary>
    public static BitvavoEnvironment? GetEnvironmentByName(string? name)
        => string.IsNullOrEmpty(name) || string.Equals(name, Live.Name, StringComparison.OrdinalIgnoreCase) ? Live : null;

    /// <summary>Available environment names.</summary>
    public static string[] All => new[] { Live.Name };
}
