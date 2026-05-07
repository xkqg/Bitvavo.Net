// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Sockets;
using CryptoExchange.Net.Sockets.Default;
using CryptoExchange.Net.Sockets.Default.Routing;
using Microsoft.Extensions.Logging;

namespace Bitvavo.Net.Objects.Internal;

/// <summary>
/// Generic Bitvavo WebSocket subscription. Holds the user's update handler, configures the
/// <see cref="MessageRouter"/> by event type + topic key (extracted by
/// <see cref="Clients.MessageHandlers.BitvavoSocketSpotMessageHandler"/>), and emits a
/// fire-and-forget subscribe / unsubscribe query in the canonical Bitvavo wire envelope.
/// </summary>
internal sealed class BitvavoSubscription<T> : Subscription
{
    private readonly Action<DateTime, string?, T> _handler;
    private readonly BitvavoSocketChannel _channel;

    public BitvavoSubscription(
        ILogger logger,
        string typeIdentifier,
        string topic,
        BitvavoSocketChannel channel,
        Action<DateTime, string?, T> handler)
        : base(logger, authenticated: false)
    {
        _handler = handler;
        _channel = channel;

        IndividualSubscriptionCount = channel.Markets.Length;
        MessageRouter = MessageRouter.CreateWithTopicFilter<T>(typeIdentifier, topic, DoHandleMessage);
    }

    /// <summary>
    /// Multi-topic variant: routes events whose topic matches any value in
    /// <paramref name="topics"/> to this subscription's handler. Use for set-based
    /// subscriptions where one <see cref="BitvavoSubscription{T}"/> covers multiple markets.
    /// </summary>
    public BitvavoSubscription(
        ILogger logger,
        string typeIdentifier,
        string[] topics,
        BitvavoSocketChannel channel,
        Action<DateTime, string?, T> handler)
        : base(logger, authenticated: false)
    {
        _handler = handler;
        _channel = channel;

        IndividualSubscriptionCount = channel.Markets.Length;
        MessageRouter = MessageRouter.CreateWithTopicFilters<T>(typeIdentifier, topics, DoHandleMessage);
    }

    protected override Query? GetSubQuery(SocketConnection connection) =>
        new BitvavoSubscribeQuery(new BitvavoSocketRequest
        {
            Action = "subscribe",
            Channels = new[] { _channel },
        });

    protected override Query? GetUnsubQuery(SocketConnection connection) =>
        new BitvavoSubscribeQuery(new BitvavoSocketRequest
        {
            Action = "unsubscribe",
            Channels = new[] { _channel },
        });

    public CallResult DoHandleMessage(SocketConnection connection, DateTime receiveTime, string? originalData, T message)
    {
        _handler.Invoke(receiveTime, originalData, message);
        return CallResult.SuccessResult;
    }
}
