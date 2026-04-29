// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Bitvavo.Net.Clients;
using Bitvavo.Net.Clients.SpotApi;
using Bitvavo.Net.Objects.Internal;
using Bitvavo.Net.Objects.Options;
using CryptoExchange.Net.Authentication;
using CryptoExchange.Net.Objects;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests;

/// <summary>
/// Unit tests for <see cref="BitvavoAuthenticationProvider"/> — covers the canonical
/// signing-string construction (timestamp + method + url + body), HMAC-SHA256 hex
/// signature, the four required headers, body-pinning for POST/PUT, and the
/// <c>ReceiveWindowMs</c> override.
/// </summary>
public class BitvavoAuthenticationProviderTests
{
    private const string TestKey = "test-key";
    private const string TestSecret = "test-secret";

    private readonly record struct AuthProviderContext(BitvavoAuthenticationProvider Provider, BitvavoRestClientSpotApi ApiClient);

    // ── helpers ───────────────────────────────────────────────────────────────────────────

    private static AuthProviderContext CreateProvider(int receiveWindowMs = 10_000)
    {
        var handler = new StubHttpMessageHandler("{}");
        var http = new HttpClient(handler);
        var opts = new BitvavoRestOptions
        {
            ApiCredentials = new BitvavoCredentials(TestKey, TestSecret),
        };
        var client = new BitvavoRestClient(http, null, Options.Create(opts));
        var apiClient = (BitvavoRestClientSpotApi)client.SpotApi;
        var provider = new BitvavoAuthenticationProvider(new BitvavoCredentials(TestKey, TestSecret), receiveWindowMs);
        return new AuthProviderContext(provider, apiClient);
    }

    private static RestRequestConfiguration BuildConfig(
        string path,
        HttpMethod method,
        bool authenticated,
        ParameterCollection? query = null,
        ParameterCollection? body = null)
    {
        var def = new RequestDefinition(path, method) { Authenticated = authenticated };
        return new RestRequestConfiguration(
            def,
            "https://api.bitvavo.com",
            query ?? new ParameterCollection(),
            body ?? new ParameterCollection(),
            new Dictionary<string, string>(),
            ArrayParametersSerialization.Array,
            HttpMethodParameterPosition.InUri,
            RequestBodyFormat.Json);
    }

    private static string HmacSha256Hex(string secret, string payload)
    {
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = h.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexStringLower(hash);
    }

    // ── tests ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ProcessRequest_does_nothing_when_not_authenticated()
    {
        var (provider, apiClient) = CreateProvider();
        var cfg = BuildConfig("v2/account", HttpMethod.Get, authenticated: false);

        provider.ProcessRequest(apiClient, cfg);

        cfg.Headers!.Count.ShouldBe(0);
        cfg.GetBodyContent().ShouldBeNull();
    }

    [Fact]
    public void ProcessRequest_writes_four_headers_with_correct_key_value()
    {
        var (provider, apiClient) = CreateProvider();
        var cfg = BuildConfig("v2/account", HttpMethod.Get, authenticated: true);

        provider.ProcessRequest(apiClient, cfg);

        cfg.Headers!.Keys.ShouldContain("Bitvavo-Access-Key");
        cfg.Headers!.Keys.ShouldContain("Bitvavo-Access-Signature");
        cfg.Headers!.Keys.ShouldContain("Bitvavo-Access-Timestamp");
        cfg.Headers!.Keys.ShouldContain("Bitvavo-Access-Window");
        cfg.Headers!["Bitvavo-Access-Key"].ShouldBe(TestKey);
    }

    [Fact]
    public void ProcessRequest_signature_is_64_lowercase_hex_chars()
    {
        var (provider, apiClient) = CreateProvider();
        var cfg = BuildConfig("v2/account", HttpMethod.Get, authenticated: true);

        provider.ProcessRequest(apiClient, cfg);

        var sig = cfg.Headers!["Bitvavo-Access-Signature"];
        sig.Length.ShouldBe(64);
        sig.ShouldBe(sig.ToLowerInvariant());
        Regex.IsMatch(sig, "^[0-9a-f]{64}$").ShouldBeTrue();
    }

    [Fact]
    public void ProcessRequest_window_defaults_to_10000_and_honours_options_override()
    {
        var (p1, c1) = CreateProvider();
        var cfg1 = BuildConfig("v2/account", HttpMethod.Get, authenticated: true);
        p1.ProcessRequest(c1, cfg1);
        cfg1.Headers!["Bitvavo-Access-Window"].ShouldBe("10000");

        var (p2, c2) = CreateProvider(receiveWindowMs: 60_000);
        var cfg2 = BuildConfig("v2/account", HttpMethod.Get, authenticated: true);
        p2.ProcessRequest(c2, cfg2);
        cfg2.Headers!["Bitvavo-Access-Window"].ShouldBe("60000");
    }

    [Fact]
    public void ProcessRequest_signing_payload_for_GET_with_query_matches_HMAC()
    {
        var (provider, apiClient) = CreateProvider();
        var query = new ParameterCollection();
        query.Add("market", "ETH-EUR");
        query.Add("limit", 10);
        var cfg = BuildConfig("v2/orders", HttpMethod.Get, authenticated: true, query: query);

        provider.ProcessRequest(apiClient, cfg);

        var ts = cfg.Headers!["Bitvavo-Access-Timestamp"];
        var queryStr = cfg.GetQueryString(urlEncode: true);
        queryStr.ShouldNotBeNullOrEmpty();
        var expectedPayload = ts + "GET/v2/orders?" + queryStr;
        var expectedSig = HmacSha256Hex(TestSecret, expectedPayload);

        cfg.Headers!["Bitvavo-Access-Signature"].ShouldBe(expectedSig);
    }

    [Fact]
    public void ProcessRequest_signing_payload_for_POST_with_body_matches_HMAC_and_pins_body()
    {
        var (provider, apiClient) = CreateProvider();
        var body = new ParameterCollection();
        body.Add("market", "ETH-EUR");
        body.Add("side", "buy");
        body.Add("orderType", "limit");
        body.Add("amount", "0.5");
        body.Add("price", "1500");
        var cfg = BuildConfig("v2/order", HttpMethod.Post, authenticated: true, body: body);

        provider.ProcessRequest(apiClient, cfg);

        var ts = cfg.Headers!["Bitvavo-Access-Timestamp"];
        var pinnedBody = cfg.GetBodyContent();
        pinnedBody.ShouldNotBeNull();
        pinnedBody!.ShouldContain("\"market\":\"ETH-EUR\"");
        pinnedBody.ShouldContain("\"side\":\"buy\"");

        var expectedPayload = ts + "POST/v2/order" + pinnedBody;
        var expectedSig = HmacSha256Hex(TestSecret, expectedPayload);

        cfg.Headers!["Bitvavo-Access-Signature"].ShouldBe(expectedSig);
    }

    [Fact]
    public void ProcessRequest_uses_empty_body_for_GET_even_if_BodyParameters_non_empty()
    {
        // Defensive: GET requests must sign with empty body even if the framework
        // accidentally populates BodyParameters — Bitvavo's spec is "body = empty for GET/DELETE".
        var (provider, apiClient) = CreateProvider();
        var stowaway = new ParameterCollection();
        stowaway.Add("dummy", "x");
        var cfg = BuildConfig("v2/account", HttpMethod.Get, authenticated: true, body: stowaway);

        provider.ProcessRequest(apiClient, cfg);

        var ts = cfg.Headers!["Bitvavo-Access-Timestamp"];
        var expectedPayload = ts + "GET/v2/account";
        var expectedSig = HmacSha256Hex(TestSecret, expectedPayload);

        cfg.Headers!["Bitvavo-Access-Signature"].ShouldBe(expectedSig);
        cfg.GetBodyContent().ShouldBeNull();
    }

    // ── BuildSocketAuth (signed WebSocket handshake) ──────────────────────────────────────

    [Fact]
    public void BuildSocketAuth_returns_action_authenticate_with_key_and_default_window()
    {
        var provider = new BitvavoAuthenticationProvider(new BitvavoCredentials(TestKey, TestSecret));

        var msg = provider.BuildSocketAuth();

        msg["action"].ShouldBe("authenticate");
        msg["key"].ShouldBe(TestKey);
        msg["window"].ShouldBe(10_000);
        msg.ContainsKey("timestamp").ShouldBeTrue();
        msg.ContainsKey("signature").ShouldBeTrue();
    }

    [Fact]
    public void BuildSocketAuth_signature_is_HMAC_of_timestamp_GET_v2_websocket()
    {
        var provider = new BitvavoAuthenticationProvider(new BitvavoCredentials(TestKey, TestSecret));

        var msg = provider.BuildSocketAuth();

        var ts = msg["timestamp"].ToString();
        var expectedSig = HmacSha256Hex(TestSecret, ts + "GET/v2/websocket");
        msg["signature"].ShouldBe(expectedSig);
    }

    [Fact]
    public void BuildSocketAuth_honours_explicit_window_override()
    {
        var provider = new BitvavoAuthenticationProvider(new BitvavoCredentials(TestKey, TestSecret), receiveWindowMs: 30_000);

        var defaultMsg = provider.BuildSocketAuth();
        var overrideMsg = provider.BuildSocketAuth(receiveWindowMs: 60_000);

        defaultMsg["window"].ShouldBe(30_000);
        overrideMsg["window"].ShouldBe(60_000);
    }

    [Fact]
    public void ProcessRequest_uses_empty_body_for_DELETE_even_if_BodyParameters_non_empty()
    {
        var (provider, apiClient) = CreateProvider();
        var stowaway = new ParameterCollection();
        stowaway.Add("dummy", "x");
        var cfg = BuildConfig("v2/order", HttpMethod.Delete, authenticated: true, body: stowaway);

        provider.ProcessRequest(apiClient, cfg);

        var ts = cfg.Headers!["Bitvavo-Access-Timestamp"];
        var expectedPayload = ts + "DELETE/v2/order";
        var expectedSig = HmacSha256Hex(TestSecret, expectedPayload);

        cfg.Headers!["Bitvavo-Access-Signature"].ShouldBe(expectedSig);
        cfg.GetBodyContent().ShouldBeNull();
    }
}
