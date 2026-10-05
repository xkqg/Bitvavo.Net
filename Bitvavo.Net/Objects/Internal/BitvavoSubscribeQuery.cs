// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Sockets;
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
/// <para>CryptoExchange.Net completes a response-less query at send time without a
/// <c>Result</c>, and an absent <c>Result</c> reads as "not successful": the subscription would stay
/// <c>Pending</c> and never register its cancellation token. Send = success is this protocol's contract, so the
/// success is stated up front.</para>
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
        Result = CallResult<object>.Ok(default!);
        MessageRouter = MessageRouter.CreateVoid<object>($"bitvavo-subscribe-{Id}");
    }
}
