// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using CryptoExchange.Net.Objects.Options;

namespace Bitvavo.Net.Objects.Options;

/// <summary>
/// Options for the <see cref="Bitvavo.Net.Clients.BitvavoRestClient"/>. Mirrors
/// <c>KrakenRestOptions</c> 1-on-1 — generic over <see cref="BitvavoEnvironment"/> +
/// <see cref="BitvavoCredentials"/>.
/// </summary>
public class BitvavoRestOptions : RestExchangeOptions<BitvavoEnvironment, BitvavoCredentials>
{
    /// <summary>Default options applied to every new <see cref="Bitvavo.Net.Clients.BitvavoRestClient"/>.</summary>
    internal static BitvavoRestOptions Default { get; set; } = new()
    {
        Environment = BitvavoEnvironment.Live,
    };

    /// <summary>Construct with defaults applied (Bitvavo Live environment).</summary>
    public BitvavoRestOptions()
    {
        Default?.Set(this);
    }

    /// <summary>Spot-API specific configuration.</summary>
    public RestApiOptions SpotOptions { get; private set; } = new RestApiOptions();

    /// <summary>
    /// Window (in milliseconds) used in the Bitvavo-Access-Window header on signed REST
    /// requests — the server rejects a signed request if its <c>Bitvavo-Access-Timestamp</c>
    /// drifts outside <c>[server_now - window, server_now + window]</c>. Default 10000ms
    /// matches Bitvavo's documented default; raise it (e.g. 60000) on systems with
    /// suspect clock drift.
    /// </summary>
    public int ReceiveWindowMs { get; set; } = 10_000;

    internal BitvavoRestOptions Set(BitvavoRestOptions targetOptions)
    {
        targetOptions = base.Set(targetOptions);
        targetOptions.SpotOptions = SpotOptions.Set(targetOptions.SpotOptions);
        targetOptions.ReceiveWindowMs = ReceiveWindowMs;
        return targetOptions;
    }
}
