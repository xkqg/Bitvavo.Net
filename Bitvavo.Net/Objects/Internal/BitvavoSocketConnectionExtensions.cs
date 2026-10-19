// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Linq;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Sockets;
using CryptoExchange.Net.Sockets.Default;

namespace Bitvavo.Net.Objects.Internal;

/// <summary>What the Bitvavo subscriptions on a framework connection need of each other.</summary>
internal static class BitvavoSocketConnectionExtensions
{
    /// <summary>
    /// The channels <paramref name="closing"/> must unsubscribe from <paramref name="connection"/>, see
    /// <see cref="BitvavoChannelDemand.Unneeded"/>. The other live subscriptions are the connection's user subscriptions that
    /// are neither <paramref name="closing"/> nor closing or closed themselves.
    /// </summary>
    internal static BitvavoSocketChannel[] ChannelsToUnsubscribe(this SocketConnection connection, Subscription closing, BitvavoSocketChannel channel)
        => BitvavoChannelDemand.Unneeded(
            channel,
            connection.Subscriptions
                .Where(other => other != closing && other.Status != SubscriptionStatus.Closing && other.Status != SubscriptionStatus.Closed)
                .OfType<IBitvavoChannelSubscription>()
                .Select(other => other.Channel));
}
