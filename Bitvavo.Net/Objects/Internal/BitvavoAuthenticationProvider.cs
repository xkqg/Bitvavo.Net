// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Clients;
using CryptoExchange.Net.Converters.SystemTextJson;
using CryptoExchange.Net.Interfaces;
using CryptoExchange.Net.Objects;

namespace Bitvavo.Net.Objects.Internal;

/// <summary>
/// Bitvavo HMAC-SHA256 authentication provider. Adds the four headers Bitvavo's signed
/// REST API requires: <c>Bitvavo-Access-Key</c>, <c>Bitvavo-Access-Signature</c>,
/// <c>Bitvavo-Access-Timestamp</c>, <c>Bitvavo-Access-Window</c>.
/// </summary>
/// <remarks>
/// Signature payload is the concatenation <c>timestamp + METHOD + url + body</c>:
/// <list type="bullet">
///   <item><term>timestamp</term><description>Unix milliseconds.</description></item>
///   <item><term>METHOD</term><description>HTTP verb upper-case.</description></item>
///   <item><term>url</term><description><c>"/" + path</c> with optional <c>"?" + queryString</c>.</description></item>
///   <item><term>body</term><description>The exact JSON body that is sent — present whenever the request's parameters travel in the body (POST, PUT, the institutional DELETEs), empty otherwise.</description></item>
/// </list>
/// HMAC-SHA256 hex (lower-case) of that payload is the signature.
/// </remarks>
internal sealed class BitvavoAuthenticationProvider : AuthenticationProvider<BitvavoCredentials, HMACCredential>
{
    private readonly int _receiveWindowMs;
    private readonly IMessageSerializer _serializer;

    public BitvavoAuthenticationProvider(BitvavoCredentials credentials, int receiveWindowMs = 10_000)
        : base(credentials, credentials.Spot ?? new HMACCredential(string.Empty, string.Empty))
    {
        _receiveWindowMs = receiveWindowMs;
        _serializer = new SystemTextJsonMessageSerializer(new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }

    /// <summary>
    /// The one signing point of the provider (REST and WebSocket): HMAC-SHA256 over <paramref name="payload"/> with the
    /// API secret, as lower-case hex — the form Bitvavo's documentation specifies for both signature examples.
    /// </summary>
    /// <param name="payload"><c>timestamp + METHOD + path[?query] + body</c> for REST, <c>timestamp + "GET/v2/websocket"</c> for the WebSocket authenticate action.</param>
    /// <returns>The 64-character lower-case hex signature.</returns>
    internal string Sign(string payload) => SignHMACSHA256(payload, SignOutputType.Hex)!.ToLowerInvariant();

    /// <inheritdoc />
    public override void ProcessRequest(RestApiClient apiClient, RestRequestConfiguration request)
    {
        if (!request.RequestDefinition.Authenticated)
        {
            return;
        }

        var timestamp = GetMillisecondTimestamp(apiClient, false);
        var method = request.RequestDefinition.Method.Method.ToUpperInvariant();
        var path = "/" + request.RequestDefinition.Path.TrimStart('/');

        // The signature covers the body that is sent, whichever verb carries it: the framework decides per request where the
        // parameters travel (ParameterPosition). Deciding by verb would leave a DELETE with a JSON body signed without it.
        var body = string.Empty;
        if (request.ParameterPosition == HttpMethodParameterPosition.InBody && request.BodyParameters is { Count: > 0 } bodyParams)
        {
            body = GetSerializedBody(_serializer, bodyParams);
            request.SetBodyContent(body);
        }

        var query = request.GetQueryString(urlEncode: true);
        var url = string.IsNullOrEmpty(query) ? path : path + "?" + query;

        var signature = Sign(timestamp + method + url + body);

        var headers = request.Headers!;
        headers["Bitvavo-Access-Key"] = Key!;
        headers["Bitvavo-Access-Signature"] = signature;
        headers["Bitvavo-Access-Timestamp"] = timestamp;
        headers["Bitvavo-Access-Window"] = _receiveWindowMs.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Build the JSON payload for Bitvavo's WebSocket authentication. Wire shape:
    /// <code>{ "action": "authenticate", "key": "...", "signature": "&lt;hex&gt;", "timestamp": &lt;ms&gt;, "window": &lt;ms&gt; }</code>
    /// Signature = HMAC-SHA256-hex(secret, timestamp + "GET" + "/v2/websocket"). Per
    /// Bitvavo's WebSocket Introduction docs. The timestamp comes from the same framework source as REST signing
    /// (<c>GetMillisecondTimestamp</c>), so the API client's time offset applies to both transports.
    /// </summary>
    /// <param name="apiClient">The socket API client whose time offset aligns the signed timestamp with Bitvavo's clock.</param>
    /// <param name="receiveWindowMs">
    /// Optional override for the per-request receive window. Defaults to the value passed
    /// to this provider's constructor (typically <see cref="Options.BitvavoSocketOptions.ReceiveWindowMs"/>).
    /// </param>
    public Dictionary<string, object> BuildSocketAuth(SocketApiClient apiClient, int? receiveWindowMs = null)
    {
        var window = receiveWindowMs ?? _receiveWindowMs;
        var timestamp = GetMillisecondTimestamp(apiClient, false);
        var signature = Sign(timestamp + "GET/v2/websocket");

        return new Dictionary<string, object>
        {
            ["action"] = "authenticate",
            ["key"] = Key!,
            ["signature"] = signature,
            ["timestamp"] = long.Parse(timestamp, CultureInfo.InvariantCulture),
            ["window"] = window,
        };
    }
}
