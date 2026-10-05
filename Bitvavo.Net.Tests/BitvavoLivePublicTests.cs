// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bitvavo.Net.Clients;
using Bitvavo.Net.Enums;
using Bitvavo.Net.Objects.Models.Spot.Streams;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Sockets;
using CryptoExchange.Net.SharedApis;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests;

/// <summary>
/// The public surface against the live Bitvavo API — no credentials, a handful of requests (about 60 weight points in all), skipped
/// unless <c>BITVAVO_LIVE_PUBLIC=1</c> is set. The recorded payloads of the other tests prove the mapping against what Bitvavo's
/// documentation says; this class proves it against what Bitvavo answers (a single-market ticker is an object although the
/// specification shows an array — only a live call showed it). Signed endpoints are never called here.
/// <code>
/// $env:BITVAVO_LIVE_PUBLIC = "1"; dotnet run --project Bitvavo.Net.Tests --framework net10.0 -- -class Bitvavo.Net.Tests.BitvavoLivePublicTests
/// </code>
/// </summary>
public class BitvavoLivePublicTests
{
    public static bool Enabled => Environment.GetEnvironmentVariable("BITVAVO_LIVE_PUBLIC") == "1";

    private const string Market = "BTC-EUR";

    private static readonly SharedSymbol BtcEur = new(TradingMode.Spot, "BTC", "EUR");

    private static BitvavoRestClient NewClient() => new();

    [Fact(SkipUnless = nameof(Enabled), SkipType = typeof(BitvavoLivePublicTests), Skip = "Set BITVAVO_LIVE_PUBLIC=1 to call the live Bitvavo API")]
    public async Task The_market_list_maps_and_BTC_EUR_is_trading()
    {
        using var client = NewClient();

        var markets = await client.SpotApi.ExchangeData.GetMarketsAsync(TestContext.Current.CancellationToken);

        markets.Success.ShouldBeTrue(markets.Error?.ToString());
        var btc = markets.Data.Single(m => m.Market == Market);
        btc.Status.ShouldBe(BitvavoMarketStatus.Trading);
        btc.TickSize.ShouldNotBeNull().ShouldBeGreaterThan(0m);
        btc.QuantityDecimals.ShouldNotBeNull();
        markets.Data.Count().ShouldBeGreaterThan(50);
    }

    [Fact(SkipUnless = nameof(Enabled), SkipType = typeof(BitvavoLivePublicTests), Skip = "Set BITVAVO_LIVE_PUBLIC=1 to call the live Bitvavo API")]
    public async Task A_query_that_names_one_market_or_asset_is_read_whatever_its_shape()
    {
        using var client = NewClient();
        var exchange = client.SpotApi.ExchangeData;
        var ct = TestContext.Current.CancellationToken;

        var ticker = await exchange.GetTicker24hAsync(Market, ct);
        var book = await exchange.GetTickerBookAsync(Market, ct);
        var price = await exchange.GetTickerPricesAsync(Market, ct);
        var asset = await exchange.GetAssetsAsync("BTC", ct);

        ticker.Success.ShouldBeTrue(ticker.Error?.ToString());
        ticker.Data.ShouldHaveSingleItem().Last.ShouldNotBeNull();
        book.Success.ShouldBeTrue(book.Error?.ToString());
        book.Data.ShouldHaveSingleItem().Bid.ShouldNotBeNull();
        price.Success.ShouldBeTrue(price.Error?.ToString());
        price.Data.ShouldHaveSingleItem().Price.ShouldNotBeNull();
        asset.Success.ShouldBeTrue(asset.Error?.ToString());
        asset.Data.ShouldHaveSingleItem().Symbol.ShouldBe("BTC");
    }

    [Fact(SkipUnless = nameof(Enabled), SkipType = typeof(BitvavoLivePublicTests), Skip = "Set BITVAVO_LIVE_PUBLIC=1 to call the live Bitvavo API")]
    public async Task Candles_order_book_trades_time_and_the_reports_map()
    {
        using var client = NewClient();
        var exchange = client.SpotApi.ExchangeData;
        var ct = TestContext.Current.CancellationToken;

        var candles = await exchange.GetKlinesAsync(Market, KlineInterval.OneHour, 2, ct: ct);
        var book = await exchange.GetOrderBookAsync(Market, 2, ct);
        var trades = await exchange.GetPublicTradesAsync(Market, 2, ct: ct);
        var time = await exchange.GetServerTimeAsync(ct);
        var bookReport = await client.SpotApi.Report.GetBookReportAsync(Market, ct: ct);
        var tradesReport = await client.SpotApi.Report.GetTradesReportAsync(Market, 1, ct: ct);

        candles.Success.ShouldBeTrue(candles.Error?.ToString());
        candles.Data.Count().ShouldBe(2);
        candles.Data.First().ClosePrice.ShouldBeGreaterThan(0m);
        book.Success.ShouldBeTrue(book.Error?.ToString());
        book.Data.Bids.ShouldNotBeEmpty();
        book.Data.Asks.ShouldNotBeEmpty();
        trades.Success.ShouldBeTrue(trades.Error?.ToString());
        trades.Data.Count().ShouldBe(2);
        time.Success.ShouldBeTrue(time.Error?.ToString());
        (DateTime.UtcNow - time.Data.Time).Duration().ShouldBeLessThan(TimeSpan.FromMinutes(1));
        bookReport.Success.ShouldBeTrue(bookReport.Error?.ToString());
        tradesReport.Success.ShouldBeTrue(tradesReport.Error?.ToString());
        tradesReport.Data.ShouldHaveSingleItem();
    }

    [Fact(SkipUnless = nameof(Enabled), SkipType = typeof(BitvavoLivePublicTests), Skip = "Set BITVAVO_LIVE_PUBLIC=1 to call the live Bitvavo API")]
    public async Task The_shared_api_reads_symbols_tickers_book_candles_and_trades()
    {
        using var client = NewClient();
        var api = client.SpotApi.SharedApi;
        var ct = TestContext.Current.CancellationToken;

        var symbols = await api.GetSpotSymbolsAsync(new GetSymbolsRequest(), ct);
        var ticker = await api.GetTickerAsync(new GetTickerRequest(BtcEur), ct);
        var tickers = await api.GetAllTickersAsync(new GetTickersRequest(TradingMode.Spot), ct);
        var book = await api.GetOrderBookAsync(new GetOrderBookRequest(BtcEur, 5), ct);
        var bookTicker = await api.GetBookTickerAsync(new GetBookTickerRequest(BtcEur), ct);
        var trades = await api.GetRecentTradesAsync(new GetRecentTradesRequest(BtcEur, 5), ct);
        var klines = await api.GetKlinesAsync(new GetKlinesRequest(BtcEur, SharedKlineInterval.OneHour, limit: 5), ct: ct);
        var asset = await api.GetAssetAsync(new GetAssetRequest("BTC"), ct);

        symbols.Success.ShouldBeTrue(symbols.Error?.ToString());
        symbols.Data.Single(s => s.Name == Market).Trading.ShouldBeTrue();
        symbols.Data.Single(s => s.Name == Market).PriceDecimals.ShouldNotBeNull();
        ticker.Success.ShouldBeTrue(ticker.Error?.ToString());
        ticker.Data.LastPrice.ShouldNotBeNull();
        tickers.Success.ShouldBeTrue(tickers.Error?.ToString());
        tickers.Data.Length.ShouldBeGreaterThan(50);
        book.Success.ShouldBeTrue(book.Error?.ToString());
        book.Data.Bids.Length.ShouldBeGreaterThan(0);
        bookTicker.Success.ShouldBeTrue(bookTicker.Error?.ToString());
        bookTicker.Data.BestAskPrice.ShouldBeGreaterThan(0m);
        trades.Success.ShouldBeTrue(trades.Error?.ToString());
        trades.Data.Length.ShouldBe(5);
        klines.Success.ShouldBeTrue(klines.Error?.ToString());
        klines.Data.Length.ShouldBeGreaterThan(0);
        asset.Success.ShouldBeTrue(asset.Error?.ToString());
        asset.Data.Name.ShouldBe("BTC");
    }

    /// <summary>Candles page backwards from the newest: the second page continues where the first stopped, without overlap.</summary>
    [Fact(SkipUnless = nameof(Enabled), SkipType = typeof(BitvavoLivePublicTests), Skip = "Set BITVAVO_LIVE_PUBLIC=1 to call the live Bitvavo API")]
    public async Task The_shared_klines_page_backwards_without_overlap()
    {
        using var client = NewClient();
        var api = client.SpotApi.SharedApi;
        var ct = TestContext.Current.CancellationToken;
        var request = new GetKlinesRequest(BtcEur, SharedKlineInterval.OneHour, startTime: DateTime.UtcNow.AddDays(-3), limit: 10);

        var first = await api.GetKlinesAsync(request, ct: ct);
        first.Success.ShouldBeTrue(first.Error?.ToString());
        first.Data.Length.ShouldBe(10);
        var next = first.NextPageRequest.ShouldNotBeNull();
        var second = await api.GetKlinesAsync(request, next, ct);

        second.Success.ShouldBeTrue(second.Error?.ToString());
        second.Data.Length.ShouldBeGreaterThan(0);
        second.Data.Max(k => k.OpenTime).ShouldBeLessThan(first.Data.Min(k => k.OpenTime));
    }

    /// <summary>The public WebSocket delivers a trade of the busiest market within a minute — through the real connection, the real framing and the real message handler.</summary>
    [Fact(SkipUnless = nameof(Enabled), SkipType = typeof(BitvavoLivePublicTests), Skip = "Set BITVAVO_LIVE_PUBLIC=1 to call the live Bitvavo API")]
    public async Task The_public_websocket_delivers_a_trade()
    {
        using var socketClient = new BitvavoSocketClient();
        var received = new TaskCompletionSource<BitvavoStreamTrade>(TaskCreationOptions.RunContinuationsAsynchronously);

        var subscription = await socketClient.SpotApi.ExchangeData.SubscribeToTradeUpdatesAsync(
            Market, update => received.TrySetResult(update.Data), TestContext.Current.CancellationToken);

        subscription.Success.ShouldBeTrue(subscription.Error?.ToString());
        var trade = await received.Task.WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);
        trade.Market.ShouldBe(Market);
        trade.Price.ShouldBeGreaterThan(0m);
        await subscription.Data.CloseAsync();
    }

    /// <summary>A one-minute candle of the busiest market arrives within a minute: the nested candle arrays, through the real connection.</summary>
    [Fact(SkipUnless = nameof(Enabled), SkipType = typeof(BitvavoLivePublicTests), Skip = "Set BITVAVO_LIVE_PUBLIC=1 to call the live Bitvavo API")]
    public async Task The_public_websocket_delivers_a_candle()
    {
        using var socketClient = new BitvavoSocketClient();
        var received = new TaskCompletionSource<BitvavoStreamCandleEvent>(TaskCreationOptions.RunContinuationsAsynchronously);

        var subscription = await socketClient.SpotApi.ExchangeData.SubscribeToKlineUpdatesAsync(
            Market, KlineInterval.OneMinute, update => received.TrySetResult(update.Data), TestContext.Current.CancellationToken);

        subscription.Success.ShouldBeTrue(subscription.Error?.ToString());
        var candle = await received.Task.WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);
        candle.Market.ShouldBe(Market);
        candle.Candle.ShouldNotBeEmpty();
        candle.Candle.First().ClosePrice.ShouldBeGreaterThan(0m);
        await subscription.Data.CloseAsync();
    }

    /// <summary>The Shared API's WebSocket capabilities on the live connection: one trade arrives as a <see cref="SharedTrade"/>.</summary>
    [Fact(SkipUnless = nameof(Enabled), SkipType = typeof(BitvavoLivePublicTests), Skip = "Set BITVAVO_LIVE_PUBLIC=1 to call the live Bitvavo API")]
    public async Task The_shared_websocket_delivers_a_trade()
    {
        using var socketClient = new BitvavoSocketClient();
        var received = new TaskCompletionSource<SharedTrade>(TaskCreationOptions.RunContinuationsAsynchronously);

        var subscription = await socketClient.SpotApi.SharedApi.SubscribeToTradeUpdatesAsync(
            new SubscribeTradeRequest(BtcEur),
            update => received.TrySetResult(update.Data.First()),
            TestContext.Current.CancellationToken);

        subscription.Success.ShouldBeTrue(subscription.Error?.ToString());
        var trade = await received.Task.WaitAsync(TimeSpan.FromSeconds(60), TestContext.Current.CancellationToken);
        trade.Symbol.ShouldBe(Market);
        trade.Price.ShouldBeGreaterThan(0m);
        await subscription.Data.CloseAsync();
    }
}
