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
///   <item><term>body</term><description>Exact JSON body for POST/PUT, empty for GET/DELETE.</description></item>
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

    /// <inheritdoc />
    public override void ProcessRequest(RestApiClient apiClient, RestRequestConfiguration request)
    {
        if (!request.Authenticated) return;

        var timestamp = GetMillisecondTimestamp(apiClient, false);
        var method = request.Method.Method.ToUpperInvariant();
        var path = "/" + request.Path.TrimStart('/');

        var body = string.Empty;
        if ((request.Method == HttpMethod.Post || request.Method == HttpMethod.Put)
            && request.BodyParameters is { Count: > 0 } bodyParams)
        {
            body = GetSerializedBody(_serializer, bodyParams);
            request.SetBodyContent(body);
        }

        var query = request.GetQueryString(urlEncode: true);
        var url = string.IsNullOrEmpty(query) ? path : path + "?" + query;

        var payload = timestamp + method + url + body;
        var signature = SignHMACSHA256(payload, SignOutputType.Hex)!.ToLowerInvariant();

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
    /// Bitvavo's WebSocket Introduction docs.
    /// </summary>
    /// <param name="receiveWindowMs">
    /// Optional override for the per-request receive window. Defaults to the value passed
    /// to this provider's constructor (typically <see cref="Options.BitvavoSocketOptions.ReceiveWindowMs"/>).
    /// </param>
    public Dictionary<string, object> BuildSocketAuth(int? receiveWindowMs = null)
    {
        var window = receiveWindowMs ?? _receiveWindowMs;
        var timestamp = (System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()).ToString(CultureInfo.InvariantCulture);
        var signature = SignHMACSHA256(timestamp + "GET/v2/websocket", SignOutputType.Hex)!.ToLowerInvariant();

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
