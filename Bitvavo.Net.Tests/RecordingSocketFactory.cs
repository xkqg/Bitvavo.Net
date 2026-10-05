// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.IO.Pipelines;
using System.Linq;
using System.Text.RegularExpressions;
using Bitvavo.Net.Clients;
using Bitvavo.Net.Clients.SpotApi;
using Bitvavo.Net.Objects.Options;
using CryptoExchange.Net.Objects.Sockets;
using CryptoExchange.Net.Sockets.Default;
using CryptoExchange.Net.Sockets.Default.Interfaces;
using CryptoExchange.Net.Sockets.HighPerf.Interfaces;
using CryptoExchange.Net.Testing.Implementations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bitvavo.Net.Tests;

/// <summary>
/// The WebSocket transport seam for tests that need more than one connection: every connection the client opens gets its own
/// <see cref="TestSocket"/>, which records the frames the client sends and answers Bitvavo's <c>authenticate</c> action like the
/// server does (<c>{"event":"authenticate","authenticated":true}</c>). Nothing of the library is replaced.
/// </summary>
internal sealed partial class RecordingSocketFactory : IWebsocketFactory
{
    private readonly List<RecordedConnection> _connections = [];

    /// <summary>A socket client routed through a recording transport.</summary>
    public readonly record struct Rig(BitvavoSocketClient Client, RecordingSocketFactory Factory);

    /// <summary>A socket client — holding the credentials of <paramref name="apiKey"/> when one is given — routed through a new recording transport.</summary>
    public static Rig Create(string? apiKey = null)
    {
        var options = new BitvavoSocketOptions();
        if (apiKey != null)
        {
            options.ApiCredentials = new BitvavoCredentials(apiKey, "secret-" + apiKey);
        }

        var client = new BitvavoSocketClient(new LoggerFactory(), Options.Create(options));
        var factory = new RecordingSocketFactory();
        factory.Attach(client);
        return new Rig(client, factory);
    }

    /// <summary>The connections the client opened, in the order it opened them.</summary>
    public IReadOnlyList<RecordedConnection> Connections => _connections;

    /// <summary>Routes every connection the spot API of <paramref name="client"/> opens through this factory.</summary>
    public void Attach(BitvavoSocketClient client)
    {
        ((BitvavoSocketClientSpotApi)client.SpotApi).SocketFactory = this;
    }

    /// <inheritdoc />
    public IWebsocket CreateWebsocket(ILogger logger, SocketConnection connection, WebSocketParameters parameters)
    {
        var socket = new TestSocket(parameters.Uri.ToString()) { Connection = connection };
        var recorded = new RecordedConnection(socket);
        socket.OnMessageSend += frame =>
        {
            recorded.Frames.Add(frame);
            if (frame.Contains("\"action\":\"authenticate\"", StringComparison.Ordinal))
            {
                socket.InvokeMessage("{\"event\":\"authenticate\",\"authenticated\":true}");
            }
        };

        _connections.Add(recorded);
        return socket;
    }

    /// <inheritdoc />
    public IHighPerfWebsocket CreateHighPerfWebsocket(ILogger logger, WebSocketParameters parameters, PipeWriter pipeWriter)
        => throw new NotSupportedException("Bitvavo.Net opens no high-performance connections.");

    /// <summary>One connection of the client and everything it sent.</summary>
    internal sealed partial class RecordedConnection(TestSocket socket)
    {
        /// <summary>The transport behind the connection.</summary>
        public TestSocket Socket { get; } = socket;

        /// <summary>Every frame the client sent on this connection.</summary>
        public List<string> Frames { get; } = [];

        /// <summary>The API key of every <c>authenticate</c> frame sent on this connection, in order.</summary>
        public IEnumerable<string> AuthenticatedKeys => Frames
            .Where(frame => frame.Contains("\"action\":\"authenticate\"", StringComparison.Ordinal))
            .Select(frame => KeyPattern().Match(frame).Groups[1].Value);

        /// <summary>Every <c>unsubscribe</c> frame sent on this connection.</summary>
        public IEnumerable<string> UnsubscribeFrames => Frames
            .Where(frame => frame.Contains("\"action\":\"unsubscribe\"", StringComparison.Ordinal));

        [GeneratedRegex("\"key\":\"([^\"]+)\"")]
        private static partial Regex KeyPattern();
    }
}
