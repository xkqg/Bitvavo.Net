// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using Bitvavo.Net.Objects.Internal;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests;

/// <summary>
/// Unit tests for <see cref="BitvavoSocketAuthQuery"/>'s auth-response evaluation.
///
/// <para>Bitvavo's WebSocket auth handshake resolves on
/// <c>{ "event": "authenticate", "authenticated": &lt;bool&gt; }</c>. The query's route
/// handler must be terminal — it evaluates the reply and returns a <see cref="CallResult"/>;
/// it must NOT re-enter <c>Query.Handle</c> (doing so re-dispatches the same message into
/// the router and recurses until <c>StackOverflowException</c>).</para>
///
/// <para>These tests pin the pure decision via <see cref="BitvavoSocketAuthQuery.EvaluateAuthResponse"/>:
/// a server reply of <c>authenticated: false</c> must surface as a failed result so the
/// socket rejects the connection instead of proceeding unauthenticated.</para>
/// </summary>
public class BitvavoSocketAuthQueryTests
{
    [Fact]
    public void EvaluateAuthResponse_WhenAuthenticated_ReturnsSuccess()
    {
        var message = new BitvavoSocketAuthResponse { Event = "authenticate", Authenticated = true };

        var result = BitvavoSocketAuthQuery.EvaluateAuthResponse(message);

        result.Success.ShouldBeTrue();
        result.Error.ShouldBeNull();
    }

    [Fact]
    public void EvaluateAuthResponse_WhenNotAuthenticated_ReturnsFailure()
    {
        var message = new BitvavoSocketAuthResponse { Event = "authenticate", Authenticated = false };

        var result = BitvavoSocketAuthQuery.EvaluateAuthResponse(message);

        result.Success.ShouldBeFalse();
        result.Error.ShouldNotBeNull();
    }
}
