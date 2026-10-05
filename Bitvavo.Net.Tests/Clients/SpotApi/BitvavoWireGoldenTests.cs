// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Bitvavo.Net.Clients;
using Bitvavo.Net.Objects.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests.Clients.SpotApi;

/// <summary>
/// Wire goldens: for every REST endpoint the library exposes, the exact HTTP request that leaves the client
/// (method, path + query, JSON body) and — for signed endpoints — that the <c>Bitvavo-Access-Signature</c> equals
/// HMAC-SHA256(secret, timestamp + METHOD + path-and-query + body) recomputed FROM THE SENT REQUEST. Baseline =
/// what 0.4.0 on CryptoExchange.Net 11.1.1 sent (measured 2026-10-05); the CE.Net 13 port must not change a byte.
/// Bitvavo signs the body of every non-GET request (docs: "for every other method, the request body as a string").
/// </summary>
public class BitvavoWireGoldenTests
{
    private const string Secret = "test-secret";

    public static IEnumerable<object[]> CaseNames() => BitvavoEndpointContracts.All.Select(c => new object[] { c.Name });

    [Theory]
    [MemberData(nameof(CaseNames))]
    public async Task Endpoint_sends_the_golden_request_and_signs_exactly_what_it_sends(string name)
    {
        var wire = BitvavoEndpointContracts.All.Single(c => c.Name == name);
        var handler = new StubHttpMessageHandler("[]");
        var options = new BitvavoRestOptions { ApiCredentials = new BitvavoCredentials("test-key", Secret) };
        var client = new BitvavoRestClient(new HttpClient(handler), null, Options.Create(options));

        await wire.Call(client.SpotApi);

        var request = handler.Requests.ShouldHaveSingleItem();
        request.Method.Method.ShouldBe(wire.Method);
        request.RequestUri!.PathAndQuery.ShouldBe(wire.PathAndQuery);
        var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldBe(wire.Body);

        request.Headers.TryGetValues("Bitvavo-Access-Signature", out var signatures).ShouldBe(wire.Signed);
        if (!wire.Signed)
        {
            return;
        }

        var timestamp = request.Headers.GetValues("Bitvavo-Access-Timestamp").Single();
        var expected = HmacSha256Hex(Secret, timestamp + wire.Method + request.RequestUri.PathAndQuery + body);
        signatures!.Single().ShouldBe(expected);
    }

    private static string HmacSha256Hex(string secret, string payload)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }
}
