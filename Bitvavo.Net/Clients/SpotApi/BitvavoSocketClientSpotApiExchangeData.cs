// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Bitvavo.Net.Enums;
using Bitvavo.Net.Interfaces.Clients.SpotApi;
using Bitvavo.Net.Objects.Internal;
using Bitvavo.Net.Objects.Models.Spot.Streams;
using CryptoExchange.Net.Converters.SystemTextJson;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Sockets;
using Microsoft.Extensions.Logging;

namespace Bitvavo.Net.Clients.SpotApi;

/// <inheritdoc />
internal sealed class BitvavoSocketClientSpotApiExchangeData : IBitvavoSocketClientSpotApiExchangeData
{
    private readonly ILogger _logger;
    private readonly BitvavoSocketClientSpotApi _client;

    internal BitvavoSocketClientSpotApiExchangeData(ILogger logger, BitvavoSocketClientSpotApi client)
    {
        _logger = logger;
        _client = client;
    }

    /// <inheritdoc />
    public Task<CallResult<UpdateSubscription>> SubscribeToKlineUpdatesAsync(
        string market,
        KlineInterval interval,
        Action<DataEvent<BitvavoStreamCandleEvent>> onMessage,
        CancellationToken ct = default)
    {
        var intervalWire = EnumConverter.GetString(interval);
        var subscription = new BitvavoSubscription<BitvavoStreamCandleEvent>(
            _logger,
            typeIdentifier: "candle",
            topic: market + intervalWire,
            channel: new BitvavoSocketChannel
            {
                Name = "candles",
                Interval = new[] { intervalWire },
                Markets = new[] { market },
            },
            handler: (receiveTime, originalData, message) =>
                onMessage(new DataEvent<BitvavoStreamCandleEvent>(
                    BitvavoExchange.ExchangeName,
                    message,
                    receiveTime,
                    originalData)
                    .WithSymbol(message.Market)
                    .WithStreamId("candles")));

        return _client.SubscribeInternalAsync(_client.BaseAddress, subscription, ct);
    }

    /// <inheritdoc />
    public Task<CallResult<UpdateSubscription>> SubscribeToTradeUpdatesAsync(
        string market,
        Action<DataEvent<BitvavoStreamTrade>> onMessage,
        CancellationToken ct = default)
    {
        var subscription = new BitvavoSubscription<BitvavoStreamTrade>(
            _logger,
            typeIdentifier: "trade",
            topic: market,
            channel: new BitvavoSocketChannel
            {
                Name = "trades",
                Markets = new[] { market },
            },
            handler: (receiveTime, originalData, message) =>
                onMessage(new DataEvent<BitvavoStreamTrade>(
                    BitvavoExchange.ExchangeName,
                    message,
                    receiveTime,
                    originalData)
                    .WithSymbol(message.Market)
                    .WithStreamId("trades")));

        return _client.SubscribeInternalAsync(_client.BaseAddress, subscription, ct);
    }
}
