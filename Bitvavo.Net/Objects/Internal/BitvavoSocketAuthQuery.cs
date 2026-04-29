// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Collections.Generic;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Sockets;
using CryptoExchange.Net.Sockets.Default;
using CryptoExchange.Net.Sockets.Default.Routing;

namespace Bitvavo.Net.Objects.Internal;

/// <summary>
/// One-shot query that authenticates a Bitvavo WebSocket connection. Sends the
/// pre-built auth payload and resolves on the server's
/// <c>{ "event": "authenticate", "authenticated": true }</c> reply.
/// </summary>
internal sealed class BitvavoSocketAuthQuery : Query<BitvavoSocketAuthResponse>
{
    public BitvavoSocketAuthQuery(Dictionary<string, object> authPayload)
        : base(authPayload, authenticated: false, weight: 1)
    {
        // The message handler's TypeIdentifierCallback extracts the JSON's "event" field.
        // Bitvavo replies with event="authenticate" — route on that.
        MessageRouter = MessageRouter.CreateWithoutTopicFilter<BitvavoSocketAuthResponse>(
            "authenticate",
            HandleAuthResponse);
    }

    private CallResult HandleAuthResponse(SocketConnection connection, System.DateTime receiveTime, string? originalData, BitvavoSocketAuthResponse message)
    {
        // Forward to the base Query<T>.Handle so the framework's wait-handle / completion
        // plumbing fires (it returns bool — we ignore it; the route succeeds either way).
        Handle("authenticate", string.Empty, connection, receiveTime, originalData ?? string.Empty, message);
        return CallResult.SuccessResult;
    }
}
