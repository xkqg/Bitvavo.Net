// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Collections.Generic;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Errors;
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

    // A CryptoExchange.Net Query route-handler is TERMINAL: it processes the routed message,
    // returns a CallResult, and the framework's Query machinery completes the pending query
    // from that return value. It must NEVER call Query.Handle — that is the router-dispatch
    // entry point, so re-entering it from inside a route handler re-routes the same message
    // straight back here and recurses until StackOverflowException.
    private CallResult HandleAuthResponse(SocketConnection connection, System.DateTime receiveTime, string? originalData, BitvavoSocketAuthResponse message)
        => EvaluateAuthResponse(message);

    /// <summary>
    /// Maps a parsed Bitvavo <c>authenticate</c> reply onto the query's terminal
    /// <see cref="CallResult"/>: success when the server confirms
    /// <see cref="BitvavoSocketAuthResponse.Authenticated"/>, otherwise a failed result
    /// carrying a <see cref="ServerError"/> of <see cref="ErrorType.Unauthorized"/> so the
    /// socket surfaces the rejection instead of proceeding unauthenticated.
    /// </summary>
    /// <param name="message">The deserialised <c>{ "event": "authenticate", "authenticated": &lt;bool&gt; }</c> reply.</param>
    /// <returns>
    /// <see cref="CallResult.SuccessResult"/> when <paramref name="message"/> reports
    /// <see cref="BitvavoSocketAuthResponse.Authenticated"/> = <see langword="true"/>;
    /// a failed <see cref="CallResult"/> carrying an <see cref="ServerError"/> otherwise.
    /// </returns>
    internal static CallResult EvaluateAuthResponse(BitvavoSocketAuthResponse message)
        => message.Authenticated
            ? CallResult.SuccessResult
            : new CallResult(new ServerError(new ErrorInfo(ErrorType.Unauthorized, "Bitvavo WebSocket authentication was rejected by the server")));
}
