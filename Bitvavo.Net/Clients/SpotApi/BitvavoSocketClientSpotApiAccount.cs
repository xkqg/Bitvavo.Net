// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Bitvavo.Net.Interfaces.Clients.SpotApi;
using Bitvavo.Net.Objects.Internal;
using Bitvavo.Net.Objects.Models.Spot.Streams;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Sockets;
using CryptoExchange.Net.Sockets.Default;
using CryptoExchange.Net.Sockets.Default.Routing;
using Microsoft.Extensions.Logging;

namespace Bitvavo.Net.Clients.SpotApi;

/// <inheritdoc />
internal sealed class BitvavoSocketClientSpotApiAccount : IBitvavoSocketClientSpotApiAccount
{
    private readonly ILogger _logger;
    private readonly BitvavoSocketClientSpotApi _client;

    internal BitvavoSocketClientSpotApiAccount(ILogger logger, BitvavoSocketClientSpotApi client)
    {
        _logger = logger;
        _client = client;
    }

    /// <inheritdoc />
    public Task<CallResult<UpdateSubscription>> SubscribeToOrderUpdatesAsync(
        string[] markets,
        Action<DataEvent<BitvavoStreamOrderUpdate>> onMessage,
        CancellationToken ct = default)
        => SubscribeAccountAsync<BitvavoStreamOrderUpdate>(
            markets,
            typeIdentifier: "order",
            onMessage: msg => onMessage(msg),
            symbolSelector: m => m.Market,
            ct: ct);

    /// <inheritdoc />
    public Task<CallResult<UpdateSubscription>> SubscribeToFillUpdatesAsync(
        string[] markets,
        Action<DataEvent<BitvavoStreamFillEvent>> onMessage,
        CancellationToken ct = default)
        => SubscribeAccountAsync<BitvavoStreamFillEvent>(
            markets,
            typeIdentifier: "fill",
            onMessage: msg => onMessage(msg),
            symbolSelector: m => m.Market,
            ct: ct);

    /// <summary>
    /// Shared subscribe path for the private <c>account</c> channel — the channel name
    /// is the same regardless of which event type the caller filters for, so this routes
    /// per-type via the <c>typeIdentifier</c> the message handler extracts from each
    /// incoming JSON's <c>event</c> field.
    /// </summary>
    private Task<CallResult<UpdateSubscription>> SubscribeAccountAsync<T>(
        string[] markets,
        string typeIdentifier,
        Action<DataEvent<T>> onMessage,
        Func<T, string> symbolSelector,
        CancellationToken ct)
    {
        var subscription = new BitvavoAccountSubscription<T>(
            _logger,
            typeIdentifier: typeIdentifier,
            markets: markets,
            handler: (receiveTime, originalData, message) =>
                onMessage(new DataEvent<T>(
                    BitvavoExchange.ExchangeName,
                    message,
                    receiveTime,
                    originalData)
                    .WithSymbol(symbolSelector(message))
                    .WithStreamId("account")));

        return _client.SubscribeInternalAsync(_client.BaseAddress, subscription, ct);
    }
}

/// <summary>
/// Authenticated subscription on the Bitvavo <c>account</c> channel. Mirrors
/// <see cref="BitvavoSubscription{T}"/> but flips <c>authenticated: true</c> so the
/// framework's <see cref="CryptoExchange.Net.Clients.SocketApiClient.GetAuthenticationRequestAsync"/>
/// is invoked before this subscription's <c>subscribe</c> payload is sent.
/// </summary>
internal sealed class BitvavoAccountSubscription<T> : Subscription
{
    private readonly Action<DateTime, string?, T> _handler;
    private readonly string[] _markets;

    public BitvavoAccountSubscription(
        ILogger logger,
        string typeIdentifier,
        string[] markets,
        Action<DateTime, string?, T> handler)
        : base(logger, authenticated: true)
    {
        _handler = handler;
        _markets = markets;

        IndividualSubscriptionCount = markets.Length;
        // Per-market routing: the message handler extracts each event's market via
        // AddTopicMapping<T>(x => x.Market); this subscription declares the market set it
        // wants, so two disjoint subscriptions never see each other's events.
        MessageRouter = MessageRouter.CreateWithTopicFilters<T>(typeIdentifier, _markets, DoHandleMessage);
    }

    protected override CryptoExchange.Net.Sockets.Query? GetSubQuery(SocketConnection connection) =>
        new BitvavoSubscribeQuery(new BitvavoSocketRequest
        {
            Action = "subscribe",
            Channels = new[]
            {
                new BitvavoSocketChannel
                {
                    Name = "account",
                    Markets = _markets,
                },
            },
        });

    protected override CryptoExchange.Net.Sockets.Query? GetUnsubQuery(SocketConnection connection) =>
        new BitvavoSubscribeQuery(new BitvavoSocketRequest
        {
            Action = "unsubscribe",
            Channels = new[]
            {
                new BitvavoSocketChannel
                {
                    Name = "account",
                    Markets = _markets,
                },
            },
        });

    public CallResult DoHandleMessage(SocketConnection connection, DateTime receiveTime, string? originalData, T message)
    {
        _handler.Invoke(receiveTime, originalData, message);
        return CallResult.SuccessResult;
    }
}
