// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using CryptoExchange.Net;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests.Clients.SpotApi;

/// <summary>
/// <c>AutoTimestamp</c> asks the REST client for the exchange clock before the first signed request. Bitvavo's clock is on
/// <c>GET /v2/time</c>, a public endpoint: a time request that had to be signed would need the time sync it is trying to perform.
/// </summary>
public class BitvavoRestClientSpotApiTimeSyncTests
{
    private static HttpResponseMessage Json(string content)
        => new(HttpStatusCode.OK) { Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json") };

    [Fact]
    public async Task GetServerTimestamp_uses_the_unauthenticated_definition()
    {
        var handler = new StubHttpMessageHandler(request => request.RequestUri!.AbsolutePath == "/v2/time"
            ? Json($"{{\"time\":{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()},\"timeNs\":0}}")
            : Json("[]"));
        var client = handler.RestClient(configure: o => o.AutoTimestamp = true);
        // The offset registry is process-wide and keyed by client name: forget earlier syncs, so this call performs one.
        TimeOffsetManager.ResetRestUpdateTime(client.SpotApi.GetType().Name);

        var balances = await client.SpotApi.Account.GetBalancesAsync(ct: TestContext.Current.CancellationToken);

        balances.Success.ShouldBeTrue(balances.Error?.ToString());
        var timeRequests = handler.Requests.Where(r => r.RequestUri!.AbsolutePath == "/v2/time").ToList();
        timeRequests.ShouldNotBeEmpty("the signed call waited for the first time sync");
        timeRequests.ShouldAllBe(r => !r.Headers.Contains("Bitvavo-Access-Key"), "the time request is public: it carries no signature");
        handler.Requests[^1].RequestUri!.AbsolutePath.ShouldBe("/v2/balance");
        handler.Requests[^1].Headers.GetValues("Bitvavo-Access-Key").ShouldBe(["test-key"]);
    }

    [Fact]
    public async Task A_failing_time_request_does_not_block_the_signed_call()
    {
        var handler = new StubHttpMessageHandler(request => request.RequestUri!.AbsolutePath == "/v2/time"
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("{\"errorCode\":107,\"error\":\"timeout\"}") }
            : Json("[]"));
        var client = handler.RestClient(configure: o => o.AutoTimestamp = true);
        TimeOffsetManager.ResetRestUpdateTime(client.SpotApi.GetType().Name);

        var balances = await client.SpotApi.Account.GetBalancesAsync(ct: TestContext.Current.CancellationToken);

        balances.Success.ShouldBeTrue(balances.Error?.ToString());
    }
}
