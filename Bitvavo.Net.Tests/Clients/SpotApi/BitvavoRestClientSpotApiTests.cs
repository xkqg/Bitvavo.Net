// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Net.Http;
using System.Threading.Tasks;
using Bitvavo.Net.Clients;
using Bitvavo.Net.Objects.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests.Clients.SpotApi;

/// <summary>
/// End-to-end wiring tests for <c>BitvavoRestClientSpotApi</c> — verifies that the
/// container exposes <c>Account</c>, <c>Trading</c>, <c>Funding</c> peers and that
/// dispatching through them flows the signed-request pipeline (signature header
/// landed on the wire).
/// </summary>
public class BitvavoRestClientSpotApiTests
{
    private static BitvavoRestClient ClientReturning(string json, out StubHttpMessageHandler handler)
    {
        handler = new StubHttpMessageHandler(json);
        var http = new HttpClient(handler);
        var opts = new BitvavoRestOptions
        {
            ApiCredentials = new BitvavoCredentials("test-key", "test-secret"),
        };
        return new BitvavoRestClient(http, null, Options.Create(opts));
    }

    [Fact]
    public void SpotApi_exposes_Account_Trading_Funding_peers()
    {
        var client = ClientReturning("{}", out _);

        client.SpotApi.Account.ShouldNotBeNull();
        client.SpotApi.Trading.ShouldNotBeNull();
        client.SpotApi.Funding.ShouldNotBeNull();
    }

    [Fact]
    public async Task SpotApi_Account_GetAccountInfoAsync_signs_and_dispatches()
    {
        var client = ClientReturning("""{"fees":{"taker":"0.0025","maker":"0.0015","volume":"0"},"capabilities":["view"]}""", out var handler);

        var result = await client.SpotApi.Account.GetAccountInfoAsync(ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        handler.Requests.Count.ShouldBe(1);
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/account");
        handler.Requests[0].Headers.GetValues("Bitvavo-Access-Signature").ShouldHaveSingleItem().Length.ShouldBe(64);
        handler.Requests[0].Headers.GetValues("Bitvavo-Access-Key").ShouldHaveSingleItem().ShouldBe("test-key");
    }
}
