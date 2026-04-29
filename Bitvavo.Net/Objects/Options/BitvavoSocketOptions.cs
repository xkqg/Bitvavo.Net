// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using CryptoExchange.Net.Objects.Options;

namespace Bitvavo.Net.Objects.Options;

/// <summary>
/// Options for the <see cref="Bitvavo.Net.Clients.BitvavoSocketClient"/>. Peer of
/// <see cref="BitvavoRestOptions"/>; mirrors <c>KrakenSocketOptions</c> 1-on-1.
/// </summary>
public class BitvavoSocketOptions : SocketExchangeOptions<BitvavoEnvironment, BitvavoCredentials>
{
    /// <summary>Default options applied to every new <see cref="Bitvavo.Net.Clients.BitvavoSocketClient"/>.</summary>
    internal static BitvavoSocketOptions Default { get; set; } = new()
    {
        Environment = BitvavoEnvironment.Live,
        // Bitvavo's WS supports many channels on one socket (rate limit is 5000 msg/sec/session).
        // Combining subscriptions on one connection avoids opening a new socket per subscribe call.
        SocketSubscriptionsCombineTarget = 10,
    };

    /// <summary>Construct with defaults applied (Bitvavo Live environment).</summary>
    public BitvavoSocketOptions()
    {
        Default?.Set(this);
    }

    /// <summary>Spot-API specific configuration.</summary>
    public SocketApiOptions SpotOptions { get; private set; } = new SocketApiOptions();

    /// <summary>
    /// Window (ms) used in the <c>window</c> field of Bitvavo's WebSocket authentication
    /// message. Server rejects an auth payload whose <c>timestamp</c> drifts outside
    /// <c>[server_now - window, server_now + window]</c>. Default 10000ms; raise on systems
    /// with suspect clock drift (max 60000 per Bitvavo docs).
    /// </summary>
    public int ReceiveWindowMs { get; set; } = 10_000;

    internal BitvavoSocketOptions Set(BitvavoSocketOptions targetOptions)
    {
        targetOptions = base.Set(targetOptions);
        targetOptions.SpotOptions = SpotOptions.Set(targetOptions.SpotOptions);
        targetOptions.ReceiveWindowMs = ReceiveWindowMs;
        return targetOptions;
    }
}
