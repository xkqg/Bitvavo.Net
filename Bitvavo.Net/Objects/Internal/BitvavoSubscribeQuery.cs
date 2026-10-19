// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Sockets;
using CryptoExchange.Net.Sockets.Default;
using CryptoExchange.Net.Sockets.Default.Routing;

namespace Bitvavo.Net.Objects.Internal;

/// <summary>
/// Fire-and-forget subscribe / unsubscribe query for Bitvavo's WebSocket.
///
/// <para>Bitvavo's protocol does not echo a per-request acknowledgment for subscribes — it
/// emits a single aggregated <c>{ "event": "subscribed", "subscriptions": {...} }</c> at
/// arbitrary points listing all currently-subscribed channels. The official Go + Python
/// SDKs ignore that event entirely and treat subscribe-send as success. We follow the same
/// pattern via <see cref="Query.ExpectsResponse"/> = false — the framework completes the
/// query immediately after <c>IsSend</c> fires.</para>
///
/// <para>From CryptoExchange.Net 13.2 on, <c>IsSend</c> completes a response-less query itself and records the success
/// (<c>Query.Complete</c>), so the subscription becomes <c>Subscribed</c> and registers its cancellation token. Before 13.2
/// the query completed without a <c>Result</c>, which reads as "not successful" and had to be preset here.</para>
///
/// <para><see cref="MessageRouter"/> still must be non-null (the framework's
/// <c>AddMessageProcessor</c> dereferences it), so we register a synthetic route on a
/// type-identifier nothing else uses; with <see cref="Query.ExpectsResponse"/> = false the
/// router is never invoked.</para>
/// </summary>
internal sealed class BitvavoSubscribeQuery : Query<object>
{
    public BitvavoSubscribeQuery(BitvavoSocketRequest request)
        : base(request, authenticated: false, weight: 1)
    {
        ExpectsResponse = false;
        MessageRouter = MessageRouter.CreateVoid<object>($"bitvavo-subscribe-{Id}");
    }

    /// <summary>The frame that subscribes <paramref name="channel"/>.</summary>
    internal static BitvavoSubscribeQuery Subscribe(BitvavoSocketChannel channel) =>
        new(new BitvavoSocketRequest { Action = "subscribe", Channels = [channel] });

    /// <summary>
    /// The frame that unsubscribes what <paramref name="closing"/> no longer shares with another live subscription on
    /// <paramref name="connection"/>, or null when it shares all of it and no frame may be sent
    /// (see <see cref="BitvavoChannelDemand"/>).
    /// </summary>
    internal static BitvavoSubscribeQuery? Unsubscribe(SocketConnection connection, Subscription closing, BitvavoSocketChannel channel)
    {
        var channels = connection.ChannelsToUnsubscribe(closing, channel);
        return channels.Length == 0
            ? null
            : new BitvavoSubscribeQuery(new BitvavoSocketRequest { Action = "unsubscribe", Channels = channels });
    }
}
