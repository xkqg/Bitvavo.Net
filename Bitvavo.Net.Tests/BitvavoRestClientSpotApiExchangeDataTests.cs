// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Bitvavo.Net.Clients;
using Bitvavo.Net.Enums;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests;

public class BitvavoRestClientSpotApiExchangeDataTests
{
    private static BitvavoRestClient ClientReturning(string json, out StubHttpMessageHandler handler) =>
        ClientReturning(json, HttpStatusCode.OK, out handler);

    private static BitvavoRestClient ClientReturning(string json, HttpStatusCode status, out StubHttpMessageHandler handler)
    {
        handler = new StubHttpMessageHandler(json, status);
        var http = new HttpClient(handler);
        return new BitvavoRestClient(http, null, Microsoft.Extensions.Options.Options.Create(new Bitvavo.Net.Objects.Options.BitvavoRestOptions()));
    }

    // ── GetMarketsAsync ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetMarketsAsync_maps_market_status_assets_and_orderTypes()
    {
        const string json = """
        [
          { "market": "ETH-EUR", "status": "trading", "base": "ETH", "quote": "EUR",
            "pricePrecision": "5",
            "minOrderInBaseAsset": "0.001", "minOrderInQuoteAsset": "5",
            "maxOrderInBaseAsset": "1000000", "maxOrderInQuoteAsset": "1000000",
            "orderTypes": ["market","limit"] },
          { "market": "BTC-EUR", "status": "halted", "base": "BTC", "quote": "EUR",
            "pricePrecision": "2",
            "minOrderInBaseAsset": "0.0001", "minOrderInQuoteAsset": "5",
            "maxOrderInBaseAsset": "1000000", "maxOrderInQuoteAsset": "1000000",
            "orderTypes": ["limit"] }
        ]
        """;
        var client = ClientReturning(json, out var handler);

        var result = await client.SpotApi.ExchangeData.GetMarketsAsync(ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        var markets = result.Data.ToList();
        markets.Count.ShouldBe(2);
        markets[0].Market.ShouldBe("ETH-EUR");
        markets[0].Status.ShouldBe("trading");
        markets[0].BaseAsset.ShouldBe("ETH");
        markets[0].QuoteAsset.ShouldBe("EUR");
        markets[0].OrderTypes.ShouldBe(new[] { "market", "limit" });
        markets[1].Status.ShouldBe("halted");
    }

    [Fact]
    public async Task GetMarketsAsync_hits_v2_markets_endpoint()
    {
        var client = ClientReturning("[]", out var handler);

        await client.SpotApi.ExchangeData.GetMarketsAsync(ct: TestContext.Current.CancellationToken);

        handler.Requests.Count.ShouldBe(1);
        handler.Requests[0].Method.ShouldBe(HttpMethod.Get);
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/markets");
    }

    [Fact]
    public async Task GetMarketsAsync_returns_unsuccessful_result_on_http_error()
    {
        var client = ClientReturning("""{"errorCode": 305, "error": "no markets"}""", HttpStatusCode.BadRequest, out _);

        var result = await client.SpotApi.ExchangeData.GetMarketsAsync(ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
    }

    // ── GetKlinesAsync ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetKlinesAsync_maps_2D_array_to_OHLCV_records()
    {
        // Bitvavo returns candles as positional arrays: [ts_ms, open, high, low, close, volume]
        // 1714132800000 ms = 2024-04-26 12:00:00 UTC; 1714129200000 ms = 2024-04-26 11:00:00 UTC
        const string json = """
        [
          [1714132800000, "2003.35", "2004.46", "2001.95", "2004.20", "28.85"],
          [1714129200000, "1992.39", "2007.50", "1992.39", "2003.08", "201.38"]
        ]
        """;
        var client = ClientReturning(json, out _);

        var result = await client.SpotApi.ExchangeData.GetKlinesAsync("ETH-EUR", KlineInterval.OneHour, limit: 2, ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        var candles = result.Data.ToList();
        candles.Count.ShouldBe(2);
        candles[0].OpenTime.ShouldBe(new DateTime(2024, 4, 26, 12, 0, 0, DateTimeKind.Utc));
        candles[0].OpenPrice.ShouldBe(2003.35m);
        candles[0].HighPrice.ShouldBe(2004.46m);
        candles[0].LowPrice.ShouldBe(2001.95m);
        candles[0].ClosePrice.ShouldBe(2004.20m);
        candles[0].Volume.ShouldBe(28.85m);
    }

    [Fact]
    public async Task GetKlinesAsync_passes_market_in_path_and_interval_in_query()
    {
        var client = ClientReturning("[]", out var handler);

        await client.SpotApi.ExchangeData.GetKlinesAsync("BTC-EUR", KlineInterval.OneHour, limit: 100, ct: TestContext.Current.CancellationToken);

        handler.Requests.Count.ShouldBe(1);
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/BTC-EUR/candles");
        handler.Requests[0].RequestUri!.Query.ShouldContain("interval=1h");
        handler.Requests[0].RequestUri!.Query.ShouldContain("limit=100");
    }

    [Fact]
    public async Task GetKlinesAsync_passes_startTime_endTime_as_unix_milliseconds()
    {
        var client = ClientReturning("[]", out var handler);
        var start = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc);

        await client.SpotApi.ExchangeData.GetKlinesAsync("ETH-EUR", KlineInterval.OneMinute, startTime: start, endTime: end, ct: TestContext.Current.CancellationToken);

        handler.Requests[0].RequestUri!.Query.ShouldContain("start=1704067200000");
        handler.Requests[0].RequestUri!.Query.ShouldContain("end=1704153600000");
    }

    // ── GetServerTimeAsync ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetServerTimeAsync_hits_v2_time()
    {
        var client = ClientReturning("""{"time": 1714132800000, "timeNs": 1714132800000000000}""", out var handler);

        var result = await client.SpotApi.ExchangeData.GetServerTimeAsync(ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/time");
        result.Data.Time.ShouldBe(new DateTime(2024, 4, 26, 12, 0, 0, DateTimeKind.Utc));
        result.Data.TimeNs.ShouldBe(1714132800000000000L);
    }

    // ── GetAssetsAsync ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAssetsAsync_no_filter_hits_v2_assets()
    {
        var client = ClientReturning("[]", out var handler);

        await client.SpotApi.ExchangeData.GetAssetsAsync(ct: TestContext.Current.CancellationToken);

        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/assets");
        handler.Requests[0].RequestUri!.Query.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAssetsAsync_with_symbol_passes_symbol_query_and_maps()
    {
        const string json = """
        [
          { "symbol":"BTC", "name":"Bitcoin", "decimals":8, "depositFee":"0", "depositConfirmations":3,
            "depositStatus":"OK", "withdrawalFee":"0.0002", "withdrawalMinAmount":"0.001",
            "withdrawalStatus":"OK", "networks":["Mainnet"], "message":null }
        ]
        """;
        var client = ClientReturning(json, out var handler);

        var result = await client.SpotApi.ExchangeData.GetAssetsAsync(symbol: "BTC", ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        handler.Requests[0].RequestUri!.Query.ShouldContain("symbol=BTC");
        var assets = result.Data.ToList();
        assets[0].Symbol.ShouldBe("BTC");
        assets[0].Name.ShouldBe("Bitcoin");
        assets[0].Decimals.ShouldBe(8);
        assets[0].DepositConfirmations.ShouldBe(3);
        assets[0].WithdrawalFee.ShouldBe(0.0002m);
        assets[0].Networks.ShouldBe(new[] { "Mainnet" });
    }

    // ── GetTickerPricesAsync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTickerPricesAsync_hits_v2_ticker_price_and_maps()
    {
        const string json = """[{"market":"ETH-EUR","price":"2003.35"},{"market":"BTC-EUR","price":"50000"}]""";
        var client = ClientReturning(json, out var handler);

        var result = await client.SpotApi.ExchangeData.GetTickerPricesAsync(ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/ticker/price");
        var prices = result.Data.ToList();
        prices.Count.ShouldBe(2);
        prices[0].Market.ShouldBe("ETH-EUR");
        prices[0].Price.ShouldBe(2003.35m);
    }

    [Fact]
    public async Task GetTickerPricesAsync_with_market_passes_market_query()
    {
        var client = ClientReturning("[]", out var handler);

        await client.SpotApi.ExchangeData.GetTickerPricesAsync(market: "ETH-EUR", ct: TestContext.Current.CancellationToken);

        handler.Requests[0].RequestUri!.Query.ShouldContain("market=ETH-EUR");
    }

    // ── GetTickerBookAsync ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTickerBookAsync_hits_v2_ticker_book_and_maps()
    {
        const string json = """[{"market":"ETH-EUR","bid":"2002.50","bidSize":"3.5","ask":"2003.35","askSize":"5"}]""";
        var client = ClientReturning(json, out var handler);

        var result = await client.SpotApi.ExchangeData.GetTickerBookAsync(ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/ticker/book");
        var books = result.Data.ToList();
        books[0].Market.ShouldBe("ETH-EUR");
        books[0].Bid.ShouldBe(2002.50m);
        books[0].BidSize.ShouldBe(3.5m);
        books[0].Ask.ShouldBe(2003.35m);
        books[0].AskSize.ShouldBe(5m);
    }

    // ── GetTicker24hAsync ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTicker24hAsync_hits_v2_ticker_24h_and_maps_OHLCV()
    {
        const string json = """
        [
          { "market":"ETH-EUR", "startTimestamp":1714046400000, "timestamp":1714132800000,
            "open":"2000", "openTimestamp":1714046400000, "high":"2050", "low":"1980",
            "last":"2010", "closeTimestamp":1714132800000, "bid":"2009", "bidSize":"3",
            "ask":"2011", "askSize":"4", "volume":"1234.5", "volumeQuote":"2470000" }
        ]
        """;
        var client = ClientReturning(json, out var handler);

        var result = await client.SpotApi.ExchangeData.GetTicker24hAsync(ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/ticker/24h");
        var t = result.Data.First();
        t.Market.ShouldBe("ETH-EUR");
        t.Open.ShouldBe(2000m);
        t.High.ShouldBe(2050m);
        t.Low.ShouldBe(1980m);
        t.Last.ShouldBe(2010m);
        t.Volume.ShouldBe(1234.5m);
        t.VolumeQuote.ShouldBe(2470000m);
        t.StartTimestamp.ShouldBe(new DateTime(2024, 4, 25, 12, 0, 0, DateTimeKind.Utc));
    }

    // ── GetOrderBookAsync ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetOrderBookAsync_hits_v2_market_book_and_maps_positional_arrays()
    {
        const string json = """
        {
          "market":"ETH-EUR",
          "nonce":12345,
          "bids":[["2002.50","3.5"],["2002.00","10"]],
          "asks":[["2003.35","5"],["2004.00","8"]],
          "timestamp":1714132800000000000
        }
        """;
        var client = ClientReturning(json, out var handler);

        var result = await client.SpotApi.ExchangeData.GetOrderBookAsync(market: "ETH-EUR", depth: 50, ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/ETH-EUR/book");
        handler.Requests[0].RequestUri!.Query.ShouldContain("depth=50");
        result.Data.Market.ShouldBe("ETH-EUR");
        result.Data.Nonce.ShouldBe(12345L);
        result.Data.Bids.Count.ShouldBe(2);
        result.Data.Bids[0].Price.ShouldBe(2002.50m);
        result.Data.Bids[0].Size.ShouldBe(3.5m);
        result.Data.Asks[0].Price.ShouldBe(2003.35m);
    }

    // ── GetPublicTradesAsync ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetPublicTradesAsync_hits_v2_market_trades_and_maps()
    {
        const string json = """
        [
          { "id":"abc","timestamp":1714132800000,"amount":"0.5","price":"2003.35","side":"buy" },
          { "id":"def","timestamp":1714132790000,"amount":"1.2","price":"2003.00","side":"sell" }
        ]
        """;
        var client = ClientReturning(json, out var handler);

        var result = await client.SpotApi.ExchangeData.GetPublicTradesAsync(market: "ETH-EUR", limit: 10, ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        handler.Requests[0].RequestUri!.AbsolutePath.ShouldBe("/v2/ETH-EUR/trades");
        handler.Requests[0].RequestUri!.Query.ShouldContain("limit=10");
        var trades = result.Data.ToList();
        trades.Count.ShouldBe(2);
        trades[0].Id.ShouldBe("abc");
        trades[0].Amount.ShouldBe(0.5m);
        trades[0].Price.ShouldBe(2003.35m);
        trades[0].Side.ShouldBe(Bitvavo.Net.Enums.OrderSide.Buy);
        trades[1].Side.ShouldBe(Bitvavo.Net.Enums.OrderSide.Sell);
    }
}
