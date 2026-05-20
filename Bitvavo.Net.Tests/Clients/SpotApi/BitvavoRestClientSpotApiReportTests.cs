// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Bitvavo.Net.Clients;
using Bitvavo.Net.Clients.SpotApi;
using Bitvavo.Net.Objects.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests.Clients.SpotApi;

/// <summary>
/// Unit tests for <see cref="BitvavoRestClientSpotApiReport"/> — covers the two MiCA
/// regulatory-reporting endpoints (trades report, order-book report). Verifies path,
/// method, signed headers, query shape, and DTO mapping.
/// </summary>
public class BitvavoRestClientSpotApiReportTests
{
    private static BitvavoRestClientSpotApiReport ReportClientReturning(
        string json,
        out StubHttpMessageHandler handler,
        HttpStatusCode status = HttpStatusCode.OK)
    {
        handler = new StubHttpMessageHandler(json, status);
        var http = new HttpClient(handler);
        var opts = new BitvavoRestOptions
        {
            ApiCredentials = new BitvavoCredentials("test-key", "test-secret"),
        };
        var client = new BitvavoRestClient(http, null, Options.Create(opts));
        var apiClient = (BitvavoRestClientSpotApi)client.SpotApi;
        return new BitvavoRestClientSpotApiReport(apiClient);
    }

    // ── GetTradesReportAsync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTradesReportAsync_hits_v2_report_market_trades_with_paging_params()
    {
        var report = ReportClientReturning("[]", out var handler);
        var start = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2024, 6, 1, 12, 0, 0, DateTimeKind.Utc);

        await report.GetTradesReportAsync("BTC-EUR", limit: 100, startTime: start, endTime: end, tradeIdFrom: "tf-1", tradeIdTo: "tt-1", ct: TestContext.Current.CancellationToken);

        handler.Requests[0].Method.ShouldBe(HttpMethod.Get);
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/report/BTC-EUR/trades");
        var q = handler.Requests[0].RequestUri!.Query;
        q.ShouldContain("limit=100");
        q.ShouldContain("start=1717200000000");
        q.ShouldContain("end=1717243200000");
        q.ShouldContain("tradeIdFrom=tf-1");
        q.ShouldContain("tradeIdTo=tt-1");
        handler.Requests[0].Headers.GetValues("Bitvavo-Access-Signature").ShouldHaveSingleItem().Length.ShouldBe(64);
    }

    [Fact]
    public async Task GetTradesReportAsync_no_filter_passes_empty_query()
    {
        var report = ReportClientReturning("[]", out var handler);

        await report.GetTradesReportAsync("ETH-EUR", ct: TestContext.Current.CancellationToken);

        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/report/ETH-EUR/trades");
        handler.Requests[0].RequestUri!.Query.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetTradesReportAsync_maps_array_response()
    {
        const string json = """
        [
          {
            "tradeId": "tr-1", "transactTimestamp": 1717200000000,
            "assetCode": "BTC", "assetName": "Bitcoin",
            "price": "60000.5", "missingPrice": null, "priceNotation": "MONE", "priceCurrency": "EUR",
            "quantity": "0.25", "quantityCurrency": "BTC", "quantityNotation": "CRYP",
            "venue": "VAVO", "publicationTimestamp": 1717200001000, "publicationVenue": "VAVO"
          }
        ]
        """;
        var report = ReportClientReturning(json, out _);

        var result = await report.GetTradesReportAsync("BTC-EUR", ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        var trades = result.Data.ToList();
        trades.Count.ShouldBe(1);
        trades[0].TradeId.ShouldBe("tr-1");
        trades[0].AssetCode.ShouldBe("BTC");
        trades[0].AssetName.ShouldBe("Bitcoin");
        trades[0].Price.ShouldBe(60000.5m);
        trades[0].PriceNotation.ShouldBe("MONE");
        trades[0].Quantity.ShouldBe(0.25m);
        trades[0].QuantityNotation.ShouldBe("CRYP");
        trades[0].Venue.ShouldBe("VAVO");
        trades[0].PublicationVenue.ShouldBe("VAVO");
    }

    // ── GetBookReportAsync ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetBookReportAsync_hits_v2_report_market_book_with_depth_query()
    {
        var report = ReportClientReturning("""{"submissionTimestamp":0,"assetCode":"BTC","assetName":"Bitcoin","priceCurrency":"EUR","priceNotation":"MONE","quantityCurrency":"BTC","quantityNotation":"CRYP","venue":"VAVO","tradingSystem":"VAVO","publicationTimestamp":0,"bids":[],"asks":[]}""", out var handler);

        await report.GetBookReportAsync("BTC-EUR", depth: 50, ct: TestContext.Current.CancellationToken);

        handler.Requests[0].Method.ShouldBe(HttpMethod.Get);
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/report/BTC-EUR/book");
        handler.Requests[0].RequestUri!.Query.ShouldContain("depth=50");
        handler.Requests[0].Headers.GetValues("Bitvavo-Access-Signature").ShouldHaveSingleItem().Length.ShouldBe(64);
    }

    [Fact]
    public async Task GetBookReportAsync_maps_bids_and_asks()
    {
        const string json = """
        {
          "submissionTimestamp": 1717200000000,
          "assetCode": "BTC", "assetName": "Bitcoin",
          "priceCurrency": "EUR", "priceNotation": "MONE",
          "quantityCurrency": "BTC", "quantityNotation": "CRYP",
          "venue": "VAVO", "tradingSystem": "VAVO",
          "publicationTimestamp": 1717200001000,
          "bids": [ { "side": "BUYI", "price": "59999", "quantity": "1.5", "numOrders": 3 } ],
          "asks": [ { "side": "SELL", "price": "60001", "quantity": "2.0", "numOrders": 5 } ]
        }
        """;
        var report = ReportClientReturning(json, out _);

        var result = await report.GetBookReportAsync("BTC-EUR", ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.AssetCode.ShouldBe("BTC");
        result.Data.Venue.ShouldBe("VAVO");
        result.Data.TradingSystem.ShouldBe("VAVO");
        result.Data.Bids.Count.ShouldBe(1);
        result.Data.Bids[0].Side.ShouldBe("BUYI");
        result.Data.Bids[0].Price.ShouldBe(59999m);
        result.Data.Bids[0].Quantity.ShouldBe(1.5m);
        result.Data.Bids[0].NumOrders.ShouldBe(3);
        result.Data.Asks.Count.ShouldBe(1);
        result.Data.Asks[0].Side.ShouldBe("SELL");
        result.Data.Asks[0].Price.ShouldBe(60001m);
        result.Data.Asks[0].NumOrders.ShouldBe(5);
    }
}
