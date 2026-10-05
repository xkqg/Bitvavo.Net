// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Linq;
using System.Threading.Tasks;
using CryptoExchange.Net.SharedApis;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests;

/// <summary>
/// A ticker query that names ONE market is answered with a single JSON object, the same query without a market with an array.
/// The payloads are recorded from the live API (2026-10-05, <c>GET /v2/ticker/24h|book|price?market=BTC-EUR</c>); Bitvavo's OpenAPI
/// specification shows an array for both, and a client that only reads arrays fails on the live answer with a deserialization error.
/// </summary>
public class BitvavoSingleMarketAnswerTests
{
    private const string Ticker24hObject =
        """{"market":"BTC-EUR","startTimestamp":1791148854095,"timestamp":1791235254095,"open":"76242","openTimestamp":1791148863081,"high":"77488","low":"75823","last":"76479","closeTimestamp":1791235238290,"bid":"76478","bidSize":"0.09804026","ask":"76479","askSize":"0.20700057","volume":"666.54250347","volumeQuote":"51137785.39074662"}""";

    private const string TickerBookObject = """{"market":"BTC-EUR","bid":"76478","bidSize":"0.09804026","ask":"76479","askSize":"0.20700057"}""";

    private const string TickerPriceObject = """{"market":"BTC-EUR","price":"76484"}""";

    private static readonly SharedSymbol BtcEur = new(TradingMode.Spot, "BTC", "EUR");

    [Fact]
    public async Task The_24h_ticker_of_one_market_reads_the_single_object()
    {
        var handler = new StubHttpMessageHandler(Ticker24hObject);

        var result = await handler.RestClient(withCredentials: false).SpotApi.ExchangeData.GetTicker24hAsync("BTC-EUR", TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue(result.Error?.ToString());
        var ticker = result.Data.ShouldHaveSingleItem();
        ticker.Market.ShouldBe("BTC-EUR");
        ticker.Last.ShouldBe(76479m);
        ticker.VolumeQuote.ShouldBe(51137785.39074662m);
        handler.Requests.ShouldHaveSingleItem().RequestUri!.Query.ShouldContain("market=BTC-EUR");
    }

    [Fact]
    public async Task The_top_of_book_of_one_market_reads_the_single_object()
    {
        var handler = new StubHttpMessageHandler(TickerBookObject);

        var result = await handler.RestClient(withCredentials: false).SpotApi.ExchangeData.GetTickerBookAsync("BTC-EUR", TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue(result.Error?.ToString());
        var book = result.Data.ShouldHaveSingleItem();
        book.Bid.ShouldBe(76478m);
        book.AskSize.ShouldBe(0.20700057m);
    }

    [Fact]
    public async Task The_price_of_one_market_reads_the_single_object()
    {
        var handler = new StubHttpMessageHandler(TickerPriceObject);

        var result = await handler.RestClient(withCredentials: false).SpotApi.ExchangeData.GetTickerPricesAsync("BTC-EUR", TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue(result.Error?.ToString());
        result.Data.ShouldHaveSingleItem().Price.ShouldBe(76484m);
    }

    [Fact]
    public async Task The_shared_ticker_of_one_market_reads_the_single_object()
    {
        var api = new StubHttpMessageHandler(Ticker24hObject).RestClient(withCredentials: false).SpotApi.SharedApi;

        var result = await api.GetTickerAsync(new GetTickerRequest(BtcEur), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue(result.Error?.ToString());
        result.Data.LastPrice.ShouldBe(76479m);
        result.Data.Symbol.ShouldBe("BTC-EUR");
    }

    [Fact]
    public async Task The_shared_book_ticker_of_one_market_reads_the_single_object()
    {
        var api = new StubHttpMessageHandler(TickerBookObject).RestClient(withCredentials: false).SpotApi.SharedApi;

        var result = await api.GetBookTickerAsync(new GetBookTickerRequest(BtcEur), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue(result.Error?.ToString());
        result.Data.BestBidPrice.ShouldBe(76478m);
        result.Data.BestAskPrice.ShouldBe(76479m);
    }

    [Fact]
    public async Task The_answer_for_every_market_is_still_an_array()
    {
        var handler = new StubHttpMessageHandler($"[{Ticker24hObject},{Ticker24hObject.Replace("BTC-EUR", "ETH-EUR")}]");

        var result = await handler.RestClient(withCredentials: false).SpotApi.ExchangeData.GetTicker24hAsync(ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue(result.Error?.ToString());
        result.Data.Select(x => x.Market).ShouldBe(["BTC-EUR", "ETH-EUR"]);
    }
}
