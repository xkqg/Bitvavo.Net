// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Text.Json;
using Bitvavo.Net.Objects.Models.Spot.Streams;
using CryptoExchange.Net.Converters.MessageParsing.DynamicConverters;
using CryptoExchange.Net.Converters.SystemTextJson.MessageHandlers;

namespace Bitvavo.Net.Clients.MessageHandlers;

/// <summary>
/// Bitvavo spot WebSocket message handler. Extracts the type identifier from the
/// incoming JSON's <c>event</c> field (e.g. <c>candle</c>, <c>trade</c>, <c>subscribed</c>,
/// <c>error</c>), and the per-subscription topic key from the deserialized DTO via
/// <c>AddTopicMapping</c> — composite (market+interval) for candles, market for trades.
/// </summary>
internal sealed class BitvavoSocketSpotMessageHandler : JsonSocketMessageHandler
{
    public override JsonSerializerOptions Options { get; } = new()
    {
        PropertyNameCaseInsensitive = true,
        // Bitvavo emits prices/amounts as JSON strings (e.g. "2996.3"); allow STJ to parse them into decimal/double properties.
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
    };

    public BitvavoSocketSpotMessageHandler()
    {
        AddTopicMapping<BitvavoStreamCandleEvent>(x => x.Market + x.Interval);
        AddTopicMapping<BitvavoStreamTrade>(x => x.Market);
        // Private (account-channel) events: route per-market so two subscriptions on disjoint
        // markets don't see each other's events. The market is always present in the event body.
        AddTopicMapping<BitvavoStreamOrderUpdate>(x => x.Market);
        AddTopicMapping<BitvavoStreamFillEvent>(x => x.Market);
        AddTopicMapping<Bitvavo.Net.Objects.Internal.BitvavoSocketAuthResponse>(_ => string.Empty);
    }

    protected override MessageTypeDefinition[] TypeEvaluators { get; } = new[]
    {
        new MessageTypeDefinition
        {
            Fields = [new PropertyFieldReference("event")],
            TypeIdentifierCallback = x => x.FieldValue("event")!,
        },
    };
}
