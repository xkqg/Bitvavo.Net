// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Bitvavo.Net.Clients;
using Bitvavo.Net.Clients.SpotApi;
using Bitvavo.Net.Objects.Options;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Errors;
using CryptoExchange.Net.SharedApis;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests.Clients.SpotApi.SharedApi;

/// <summary>
/// The public market-data Shared capabilities on the REST API: spot symbols, tickers (one and all), order book, book ticker and
/// recent trades. [V2] (<c>client.SpotApi.SharedApi</c>) and the legacy [V1] interfaces (<c>client.SpotApi.SharedClient</c>) are two
/// views of one instance, so each capability is proved through the V2 name and every legacy interface once more through its own.
/// None of them needs credentials. Payloads have the shape of the Bitvavo specification and of the live API (2026-10-05).
/// </summary>
public class BitvavoRestSharedMarketDataTests
{
    /// <summary>The BTC-EUR market as the live API returns it: <c>pricePrecision</c> is null on every market, the tick size is the price grid.</summary>
    private const string BtcEurMarket = """
        {"market":"BTC-EUR","status":"trading","base":"BTC","quote":"EUR","pricePrecision":null,
         "minOrderInBaseAsset":"0.00006607","minOrderInQuoteAsset":"5.00","maxOrderInBaseAsset":"1000000000","maxOrderInQuoteAsset":"1000000000",
         "quantityDecimals":8,"notionalDecimals":2,"tickSize":"1.00","maxOpenOrders":400,"feeCategory":"A",
         "orderTypes":["market","limit","stopLoss","stopLossLimit","takeProfit","takeProfitLimit"]}
        """;

    private const string EthEurTicker = """{"market":"ETH-EUR","open":"3000","high":"3100","low":"2900","last":"3060","volume":"500","volumeQuote":"1500000"}""";
    private const string BtcEurTicker = """{"market":"BTC-EUR","open":"60000","high":"61000","low":"59000","last":"60500","volume":"30"}""";
    private const string EthEurBookTicker = """{"market":"ETH-EUR","bid":"2999","bidSize":"1.5","ask":"3001","askSize":"2.5"}""";
    private const string EthEurOrderBook = """{"market":"ETH-EUR","nonce":42,"bids":[["2990","1.0"],["2989.5","2"]],"asks":[["3010","2.0"],["3011","0.5"]],"timestamp":1714132800000000000}""";
    private const string EthEurTrades = """[{"id":"t1","timestamp":1714132800000,"amount":"0.5","price":"3000","side":"buy"},{"id":"t2","timestamp":1714132790000,"amount":"1.2","price":"2999","side":"sell"}]""";
    private const string InvalidParameterBody = """{"errorCode":205,"error":"You provided an invalid parameter value."}""";

    private static SharedSymbol EthEur => new(TradingMode.Spot, "ETH", "EUR");

    /// <summary>A market in the live shape; the arguments are the fields the tests vary.</summary>
    private static string MarketJson(string baseAsset, string quoteAsset, string status = "trading", string tickSize = "0.01", string quantityDecimals = "8") =>
        $$"""{"market":"{{baseAsset}}-{{quoteAsset}}","status":"{{status}}","base":"{{baseAsset}}","quote":"{{quoteAsset}}","pricePrecision":null,"minOrderInBaseAsset":"0.001","minOrderInQuoteAsset":"5.00","maxOrderInBaseAsset":"1000000","maxOrderInQuoteAsset":"1000000","quantityDecimals":{{quantityDecimals}},"notionalDecimals":2,"tickSize":"{{tickSize}}","maxOpenOrders":100,"feeCategory":"A","orderTypes":["market","limit"]}""";

    private static string Markets(params string[] markets) => "[" + string.Join(",", markets) + "]";

    /// <summary>
    /// A client in an environment of its own. The symbol cache behind <c>SpotSymbolCatalog</c> and the legacy lookups is process-wide
    /// and keyed by environment, so a test that fills it or asserts on it must not share its environment with any other test.
    /// </summary>
    private static BitvavoRestClient ClientWithItsOwnSymbolCache(StubHttpMessageHandler handler, bool withCredentials = true)
    {
        var options = new BitvavoRestOptions
        {
            Environment = new BitvavoEnvironment("market-data-tests-" + Guid.NewGuid().ToString("N"), "https://api.bitvavo.com", "wss://ws.bitvavo.com/v2/"),
        };
        if (withCredentials)
        {
            options.ApiCredentials = new BitvavoCredentials("test-key", "test-secret");
        }

        return new BitvavoRestClient(new HttpClient(handler), null, Microsoft.Extensions.Options.Options.Create(options));
    }

    /// <summary>What each public endpoint answers, so one client can call all six capabilities.</summary>
    private static HttpResponseMessage PublicAnswer(HttpRequestMessage request)
    {
        var json = request.RequestUri!.AbsolutePath switch
        {
            "/v2/markets" => Markets(BtcEurMarket),
            "/v2/ticker/24h" => "[" + EthEurTicker + "]",
            "/v2/ticker/book" => "[" + EthEurBookTicker + "]",
            "/v2/ETH-EUR/book" => EthEurOrderBook,
            "/v2/ETH-EUR/trades" => EthEurTrades,
            _ => "{}",
        };

        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
    }

    // ── spot symbols ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Spot_symbols_are_mapped_from_the_live_markets_payload()
    {
        var handler = new StubHttpMessageHandler(Markets(BtcEurMarket));
        var api = ClientWithItsOwnSymbolCache(handler).SpotApi.SharedApi;

        var result = await api.GetSpotSymbolsAsync(new GetSymbolsRequest(TradingMode.Spot), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        var symbol = result.Data.ShouldHaveSingleItem();
        symbol.Name.ShouldBe("BTC-EUR");
        symbol.BaseAsset.ShouldBe("BTC");
        symbol.QuoteAsset.ShouldBe("EUR");
        symbol.TradingMode.ShouldBe(TradingMode.Spot);
        symbol.Trading.ShouldBeTrue();
        symbol.MinTradeQuantity.ShouldBe(0.00006607m);
        symbol.MaxTradeQuantity.ShouldBe(1000000000m);
        symbol.MinNotionalValue.ShouldBe(5.00m);
        symbol.QuantityDecimals.ShouldBe(8);
        symbol.QuantityStep.ShouldBe(0.00000001m);
        symbol.PriceStep.ShouldBe(1.00m);
        symbol.PriceDecimals.ShouldBe(0);
        symbol.SharedSymbol.ShouldBe(new SharedSymbol(TradingMode.Spot, "BTC", "EUR") { SymbolName = "BTC-EUR" });
        var request = handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Get);
        request.RequestUri!.AbsolutePath.ShouldBe("/v2/markets");
        request.RequestUri.Query.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("trading", true)]
    [InlineData("halted", false)]
    [InlineData("auction", false)]
    [InlineData("auctionMatching", false)]
    [InlineData("cancelOnly", false)]
    [InlineData("aStatusBitvavoAddsLater", false)]
    public async Task A_symbol_is_trading_only_while_its_market_status_is_trading(string status, bool trading)
    {
        var api = ClientWithItsOwnSymbolCache(new StubHttpMessageHandler(Markets(MarketJson("BTC", "EUR", status)))).SpotApi.SharedApi;

        var result = await api.GetSpotSymbolsAsync(new GetSymbolsRequest(), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.ShouldHaveSingleItem().Trading.ShouldBe(trading);
    }

    [Fact]
    public async Task A_market_without_a_status_is_not_trading()
    {
        var api = ClientWithItsOwnSymbolCache(new StubHttpMessageHandler("""[{"market":"XYZ-EUR","base":"XYZ","quote":"EUR"}]""")).SpotApi.SharedApi;

        var result = await api.GetSpotSymbolsAsync(new GetSymbolsRequest(), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.ShouldHaveSingleItem().Trading.ShouldBeFalse();
    }

    [Theory]
    [InlineData("1.00", 0)]
    [InlineData("0.01", 2)]
    [InlineData("0.5", 1)]
    [InlineData("0.00001", 5)]
    [InlineData("0.00000001", 8)]
    public async Task The_tick_size_is_the_price_grid_and_gives_the_price_decimals(string tickSize, int priceDecimals)
    {
        var api = ClientWithItsOwnSymbolCache(new StubHttpMessageHandler(Markets(MarketJson("ETH", "BTC", tickSize: tickSize)))).SpotApi.SharedApi;

        var result = await api.GetSpotSymbolsAsync(new GetSymbolsRequest(), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        var symbol = result.Data.ShouldHaveSingleItem();
        symbol.PriceStep.ShouldBe(decimal.Parse(tickSize, CultureInfo.InvariantCulture));
        symbol.PriceDecimals.ShouldBe(priceDecimals);
    }

    [Theory]
    [InlineData("""{"market":"XYZ-EUR","status":"trading","base":"XYZ","quote":"EUR"}""")]
    [InlineData("""{"market":"XYZ-EUR","status":"trading","base":"XYZ","quote":"EUR","tickSize":null}""")]
    [InlineData("""{"market":"XYZ-EUR","status":"trading","base":"XYZ","quote":"EUR","tickSize":"0"}""")]
    public async Task A_market_without_a_usable_tick_size_has_no_price_grid(string market)
    {
        var api = ClientWithItsOwnSymbolCache(new StubHttpMessageHandler(Markets(market))).SpotApi.SharedApi;

        var result = await api.GetSpotSymbolsAsync(new GetSymbolsRequest(), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        var symbol = result.Data.ShouldHaveSingleItem();
        symbol.PriceStep.ShouldBeNull();
        symbol.PriceDecimals.ShouldBeNull();
    }

    /// <summary>
    /// 0.4.0 filled the significant figures from <c>pricePrecision</c>, which the live API returns as null on every market and the
    /// specification marks deprecated: the price grid is the tick size, and the significant figures stay unset.
    /// </summary>
    [Theory]
    [InlineData("null")]
    [InlineData("5")]
    [InlineData("\"5\"")]
    public async Task The_price_significant_figures_stay_unset_whatever_the_deprecated_price_precision_says(string pricePrecision)
    {
        var market = $$"""{"market":"XYZ-EUR","status":"trading","base":"XYZ","quote":"EUR","pricePrecision":{{pricePrecision}},"tickSize":"0.00001"}""";
        var api = ClientWithItsOwnSymbolCache(new StubHttpMessageHandler(Markets(market))).SpotApi.SharedApi;

        var result = await api.GetSpotSymbolsAsync(new GetSymbolsRequest(), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        var symbol = result.Data.ShouldHaveSingleItem();
        symbol.PriceSignificantFigures.ShouldBeNull();
        symbol.PriceDecimals.ShouldBe(5);
    }

    [Theory]
    [InlineData("8", "0.00000001")]
    [InlineData("2", "0.01")]
    [InlineData("0", "1")]
    public async Task The_quantity_step_is_the_smallest_increment_the_quantity_decimals_allow(string quantityDecimals, string quantityStep)
    {
        var api = ClientWithItsOwnSymbolCache(new StubHttpMessageHandler(Markets(MarketJson("ETH", "EUR", quantityDecimals: quantityDecimals)))).SpotApi.SharedApi;

        var result = await api.GetSpotSymbolsAsync(new GetSymbolsRequest(), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        var symbol = result.Data.ShouldHaveSingleItem();
        symbol.QuantityDecimals.ShouldBe(int.Parse(quantityDecimals, CultureInfo.InvariantCulture));
        symbol.QuantityStep.ShouldBe(decimal.Parse(quantityStep, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("BTC", "EUR", SharedAssetType.Fiat, null, null)]
    [InlineData("BTC", "USDC", SharedAssetType.Crypto, SharedAssetSubType.StableCoin, null)]
    [InlineData("ETH", "BTC", SharedAssetType.Crypto, null, null)]
    [InlineData("USDC", "EUR", SharedAssetType.Fiat, null, SharedAssetSubType.StableCoin)]
    public async Task Asset_types_follow_what_the_quote_asset_and_the_base_asset_are(
        string baseAsset,
        string quoteAsset,
        SharedAssetType quoteType,
        SharedAssetSubType? quoteSubType,
        SharedAssetSubType? baseSubType)
    {
        var api = ClientWithItsOwnSymbolCache(new StubHttpMessageHandler(Markets(MarketJson(baseAsset, quoteAsset)))).SpotApi.SharedApi;

        var result = await api.GetSpotSymbolsAsync(new GetSymbolsRequest(), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        var symbol = result.Data.ShouldHaveSingleItem();
        symbol.BaseAssetType.ShouldBe(SharedAssetType.Crypto);
        symbol.BaseAssetSubType.ShouldBe(baseSubType);
        symbol.QuoteAssetType.ShouldBe(quoteType);
        symbol.QuoteAssetSubType.ShouldBe(quoteSubType);
    }

    [Fact]
    public async Task Missing_or_unreadable_optional_market_fields_leave_the_symbol_fields_unset()
    {
        const string market = """{"market":"XYZ-EUR","status":"trading","base":"XYZ","quote":"EUR","minOrderInBaseAsset":"n/a","maxOrderInBaseAsset":""}""";
        var api = ClientWithItsOwnSymbolCache(new StubHttpMessageHandler(Markets(market))).SpotApi.SharedApi;

        var result = await api.GetSpotSymbolsAsync(new GetSymbolsRequest(), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        var symbol = result.Data.ShouldHaveSingleItem();
        symbol.Name.ShouldBe("XYZ-EUR");
        symbol.Trading.ShouldBeTrue();
        symbol.MinTradeQuantity.ShouldBeNull();
        symbol.MaxTradeQuantity.ShouldBeNull();
        symbol.MinNotionalValue.ShouldBeNull();
        symbol.QuantityDecimals.ShouldBeNull();
        symbol.QuantityStep.ShouldBeNull();
        symbol.PriceSignificantFigures.ShouldBeNull();
    }

    [Fact]
    public async Task A_market_listed_twice_is_returned_once()
    {
        var api = ClientWithItsOwnSymbolCache(new StubHttpMessageHandler(Markets(MarketJson("ETH", "EUR"), MarketJson("ETH", "EUR")))).SpotApi.SharedApi;

        var result = await api.GetSpotSymbolsAsync(new GetSymbolsRequest(), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.ShouldHaveSingleItem().Name.ShouldBe("ETH-EUR");
    }

    [Fact]
    public async Task The_request_filters_the_symbols_on_the_asset_types()
    {
        var markets = Markets(BtcEurMarket, MarketJson("BTC", "USDC"), MarketJson("ETH", "BTC"), MarketJson("USDC", "EUR"));
        var api = ClientWithItsOwnSymbolCache(new StubHttpMessageHandler(markets)).SpotApi.SharedApi;

        var fiatQuoted = await api.GetSpotSymbolsAsync(new GetSymbolsRequest(quoteAssetType: SharedAssetType.Fiat), TestContext.Current.CancellationToken);
        var stableQuoted = await api.GetSpotSymbolsAsync(new GetSymbolsRequest(quoteAssetSubType: SharedAssetSubType.StableCoin), TestContext.Current.CancellationToken);
        var stableBased = await api.GetSpotSymbolsAsync(new GetSymbolsRequest(baseAssetSubType: SharedAssetSubType.StableCoin), TestContext.Current.CancellationToken);

        fiatQuoted.Success.ShouldBeTrue();
        stableQuoted.Success.ShouldBeTrue();
        stableBased.Success.ShouldBeTrue();
        fiatQuoted.Data.Select(x => x.Name).ShouldBe(new[] { "BTC-EUR", "USDC-EUR" }, ignoreOrder: true);
        stableQuoted.Data.Select(x => x.Name).ShouldBe(new[] { "BTC-USDC" });
        stableBased.Data.Select(x => x.Name).ShouldBe(new[] { "USDC-EUR" });
    }

    [Fact]
    public void The_symbol_catalog_is_unavailable_until_the_symbols_have_been_read()
    {
        var api = ClientWithItsOwnSymbolCache(new StubHttpMessageHandler("[]")).SpotApi.SharedApi;

        api.SpotSymbolCatalog.ShouldBeNull();
    }

    [Fact]
    public async Task After_the_symbols_are_read_the_catalog_lists_every_symbol_and_asset_with_its_type()
    {
        var api = ClientWithItsOwnSymbolCache(new StubHttpMessageHandler(Markets(BtcEurMarket, MarketJson("BTC", "USDC")))).SpotApi.SharedApi;

        await api.GetSpotSymbolsAsync(new GetSymbolsRequest(), TestContext.Current.CancellationToken);

        var catalog = api.SpotSymbolCatalog.ShouldNotBeNull();
        catalog.Exchange.ShouldBe("Bitvavo");
        catalog.Symbols.ContainsKey("BTC-EUR").ShouldBeTrue();
        catalog.Symbols.ContainsKey("BTC-USDC").ShouldBeTrue();
        catalog.Assets["BTC"].Type.ShouldBe(SharedAssetType.Crypto);
        catalog.Assets["EUR"].Type.ShouldBe(SharedAssetType.Fiat);
        catalog.Assets["USDC"].SubType.ShouldBe(SharedAssetSubType.StableCoin);
    }

    // ── spot symbols: the symbol cache behind the legacy lookups ─────────────────────────

    [Fact]
    public async Task Spot_symbols_for_a_base_asset_come_from_the_symbol_cache_that_the_first_call_fills()
    {
        var handler = new StubHttpMessageHandler(Markets(BtcEurMarket, MarketJson("BTC", "USDC"), MarketJson("ETH", "EUR")));
        var legacy = ClientWithItsOwnSymbolCache(handler).SpotApi.SharedClient;

        var first = await legacy.GetSpotSymbolsForBaseAssetAsync("btc");
        var second = await legacy.GetSpotSymbolsForBaseAssetAsync("BTC");

        first.Success.ShouldBeTrue();
        second.Success.ShouldBeTrue();
        first.Data.Select(x => x.SymbolName).ShouldBe(new[] { "BTC-EUR", "BTC-USDC" }, ignoreOrder: true);
        second.Data.Select(x => x.SymbolName).ShouldBe(new[] { "BTC-EUR", "BTC-USDC" }, ignoreOrder: true);
        handler.Requests.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task A_spot_symbol_is_supported_when_the_markets_list_has_it_by_symbol_or_by_name()
    {
        var legacy = ClientWithItsOwnSymbolCache(new StubHttpMessageHandler(Markets(BtcEurMarket))).SpotApi.SharedClient;

        var knownSymbol = await legacy.SupportsSpotSymbolAsync(new SharedSymbol(TradingMode.Spot, "BTC", "EUR"));
        var unknownSymbol = await legacy.SupportsSpotSymbolAsync(new SharedSymbol(TradingMode.Spot, "BTC", "GBP"));
        var knownName = await legacy.SupportsSpotSymbolAsync("BTC-EUR");
        var unknownName = await legacy.SupportsSpotSymbolAsync("BTC-GBP");

        knownSymbol.Data.ShouldBe(true);
        unknownSymbol.Data.ShouldBe(false);
        knownName.Data.ShouldBe(true);
        unknownName.Data.ShouldBe(false);
    }

    [Fact]
    public async Task Only_a_spot_symbol_can_be_asked_about()
    {
        var handler = new StubHttpMessageHandler(Markets(BtcEurMarket));
        var legacy = ClientWithItsOwnSymbolCache(handler).SpotApi.SharedClient;

        await Should.ThrowAsync<ArgumentException>(() => legacy.SupportsSpotSymbolAsync(new SharedSymbol(TradingMode.PerpetualLinear, "BTC", "EUR")));

        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task When_the_markets_cannot_be_read_the_cache_backed_members_fail_with_the_server_error()
    {
        var handler = new StubHttpMessageHandler(InvalidParameterBody, HttpStatusCode.BadRequest);
        var legacy = ClientWithItsOwnSymbolCache(handler).SpotApi.SharedClient;

        var forBaseAsset = await legacy.GetSpotSymbolsForBaseAssetAsync("BTC");
        var bySymbol = await legacy.SupportsSpotSymbolAsync(new SharedSymbol(TradingMode.Spot, "BTC", "EUR"));
        var byName = await legacy.SupportsSpotSymbolAsync("BTC-EUR");

        forBaseAsset.Success.ShouldBeFalse();
        forBaseAsset.Error.ShouldBeOfType<ServerError>().ErrorType.ShouldBe(ErrorType.InvalidParameter);
        bySymbol.Error.ShouldBeOfType<ServerError>().ErrorType.ShouldBe(ErrorType.InvalidParameter);
        byName.Error.ShouldBeOfType<ServerError>().ErrorType.ShouldBe(ErrorType.InvalidParameter);
    }

    // ── quantity step (pure mapping) ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(0, "1")]
    [InlineData(2, "0.01")]
    [InlineData(8, "0.00000001")]
    [InlineData(28, "0.0000000000000000000000000001")]
    public void Quantity_decimals_give_the_smallest_quantity_increment(int quantityDecimals, string quantityStep)
    {
        ((int?)quantityDecimals).ToQuantityStep().ShouldBe(decimal.Parse(quantityStep, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(29)]
    public void Quantity_decimals_a_decimal_cannot_hold_give_no_quantity_step(int quantityDecimals)
    {
        ((int?)quantityDecimals).ToQuantityStep().ShouldBeNull();
    }

    [Fact]
    public void Unknown_quantity_decimals_give_no_quantity_step()
    {
        ((int?)null).ToQuantityStep().ShouldBeNull();
    }

    // ── tickers ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task One_ticker_is_mapped_and_only_its_market_is_asked_for()
    {
        var handler = new StubHttpMessageHandler("[" + EthEurTicker + "]");
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.GetTickerAsync(new GetTickerRequest(EthEur), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Symbol.ShouldBe("ETH-EUR");
        result.Data.SharedSymbol.ShouldBe(EthEur);
        result.Data.LastPrice.ShouldBe(3060m);
        result.Data.HighPrice.ShouldBe(3100m);
        result.Data.LowPrice.ShouldBe(2900m);
        result.Data.Volumes.QuantityInBaseAsset.ShouldBe(500m);
        result.Data.Volumes.QuantityInQuoteAsset.ShouldBe(1500000m);
        var request = handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Get);
        request.RequestUri!.AbsolutePath.ShouldBe("/v2/ticker/24h");
        request.RequestUri.Query.ShouldContain("market=ETH-EUR");
    }

    [Theory]
    [InlineData("3000", "3060", "2")]
    [InlineData("3000", "2940", "-2")]
    [InlineData("3000", "3000", "0")]
    [InlineData("3", "3.1", "3.333333")]
    public async Task The_change_percentage_is_last_against_open_in_percent_rounded_to_six_decimals(string open, string last, string changePercentage)
    {
        var ticker = $$"""[{"market":"ETH-EUR","open":"{{open}}","high":"4000","low":"1","last":"{{last}}","volume":"1","volumeQuote":"1"}]""";
        var api = new StubHttpMessageHandler(ticker).RestClient().SpotApi.SharedApi;

        var result = await api.GetTickerAsync(new GetTickerRequest(EthEur), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.ChangePercentage.ShouldBe(decimal.Parse(changePercentage, CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("""{"market":"ETH-EUR","open":"0","last":"3060"}""")]
    [InlineData("""{"market":"ETH-EUR","open":null,"last":"3060"}""")]
    [InlineData("""{"market":"ETH-EUR","last":"3060"}""")]
    [InlineData("""{"market":"ETH-EUR","open":"3000"}""")]
    public async Task Without_a_positive_open_price_or_a_last_price_there_is_no_change_percentage(string ticker)
    {
        var api = new StubHttpMessageHandler("[" + ticker + "]").RestClient().SpotApi.SharedApi;

        var result = await api.GetTickerAsync(new GetTickerRequest(EthEur), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.ChangePercentage.ShouldBeNull();
    }

    [Fact]
    public async Task An_unknown_ticker_market_is_an_UnknownSymbol_error()
    {
        var api = new StubHttpMessageHandler("[]").RestClient().SpotApi.SharedApi;

        var result = await api.GetTickerAsync(new GetTickerRequest(EthEur), TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.Error!.ErrorType.ShouldBe(ErrorType.UnknownSymbol);
    }

    [Fact]
    public async Task When_the_server_answers_with_more_markets_than_asked_the_requested_one_is_picked()
    {
        var api = new StubHttpMessageHandler("[" + BtcEurTicker + "," + EthEurTicker + "]").RestClient().SpotApi.SharedApi;

        var result = await api.GetTickerAsync(new GetTickerRequest(EthEur), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Symbol.ShouldBe("ETH-EUR");
        result.Data.LastPrice.ShouldBe(3060m);
    }

    [Fact]
    public async Task All_tickers_are_mapped_with_their_symbols_and_no_market_filter_is_sent()
    {
        var handler = new StubHttpMessageHandler("[" + EthEurTicker + "," + BtcEurTicker + "]");
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.GetAllTickersAsync(new GetTickersRequest(TradingMode.Spot), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Length.ShouldBe(2);
        result.Data[0].Symbol.ShouldBe("ETH-EUR");
        var btc = result.Data[1];
        btc.SharedSymbol.ShouldBe(new SharedSymbol(TradingMode.Spot, "BTC", "EUR") { SymbolName = "BTC-EUR" });
        btc.LastPrice.ShouldBe(60500m);
        btc.Volumes.QuantityInBaseAsset.ShouldBe(30m);
        btc.Volumes.QuantityInQuoteAsset.ShouldBeNull();
        var request = handler.Requests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/v2/ticker/24h");
        request.RequestUri.Query.ShouldNotContain("market");
    }

    [Fact]
    public async Task The_legacy_interface_serves_one_ticker_and_all_tickers_as_spot_tickers()
    {
        var handler = new StubHttpMessageHandler("[" + EthEurTicker + "," + BtcEurTicker + "]");
        var legacy = handler.RestClient().SpotApi.SharedClient;

        var one = await legacy.GetSpotTickerAsync(new GetTickerRequest(EthEur), TestContext.Current.CancellationToken);
        var all = await legacy.GetSpotTickersAsync(new GetTickersRequest(TradingMode.Spot), TestContext.Current.CancellationToken);

        one.Success.ShouldBeTrue();
        one.Data.ShouldBeOfType<SharedSpotTicker>();
        one.Data.LastPrice.ShouldBe(3060m);
        one.Data.HighPrice.ShouldBe(3100m);
        one.Data.Volumes.QuantityInBaseAsset.ShouldBe(500m);
        one.Data.ChangePercentage.ShouldBe(2m);
        all.Success.ShouldBeTrue();
        all.Data.Length.ShouldBe(2);
        all.Data[1].SharedSymbol!.BaseAsset.ShouldBe("BTC");
    }

    // ── order book ───────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_order_book_is_mapped_with_its_sequence_number_and_the_depth_is_sent()
    {
        var handler = new StubHttpMessageHandler(EthEurOrderBook);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.GetOrderBookAsync(new GetOrderBookRequest(EthEur, 100), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.QuantityType.ShouldBe(SharedQuantityType.BaseAsset);
        result.Data.SequenceNumber.ShouldBe(42L);
        result.Data.Bids.Length.ShouldBe(2);
        result.Data.Bids[0].Price.ShouldBe(2990m);
        result.Data.Bids[0].Quantity.ShouldBe(1.0m);
        result.Data.Bids[1].Price.ShouldBe(2989.5m);
        result.Data.Bids[1].Quantity.ShouldBe(2m);
        result.Data.Asks.Length.ShouldBe(2);
        result.Data.Asks[0].Price.ShouldBe(3010m);
        result.Data.Asks[0].Quantity.ShouldBe(2.0m);
        result.Data.Asks[1].Price.ShouldBe(3011m);
        result.Data.Asks[1].Quantity.ShouldBe(0.5m);
        var request = handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Get);
        request.RequestUri!.AbsolutePath.ShouldBe("/v2/ETH-EUR/book");
        request.RequestUri.Query.ShouldContain("depth=100");
    }

    [Fact]
    public async Task Without_a_limit_no_depth_is_sent()
    {
        var handler = new StubHttpMessageHandler(EthEurOrderBook);
        var api = handler.RestClient().SpotApi.SharedApi;

        await api.GetOrderBookAsync(new GetOrderBookRequest(EthEur), TestContext.Current.CancellationToken);

        handler.Requests.ShouldHaveSingleItem().RequestUri!.Query.ShouldNotContain("depth");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(1000)]
    public async Task The_depths_at_the_edges_of_the_range_are_sent(int depth)
    {
        var handler = new StubHttpMessageHandler(EthEurOrderBook);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.GetOrderBookAsync(new GetOrderBookRequest(EthEur, depth), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        handler.Requests.ShouldHaveSingleItem().RequestUri!.Query.ShouldContain("depth=" + depth.ToString(CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1001)]
    public async Task Depths_outside_1_to_1000_are_rejected_before_anything_is_sent(int depth)
    {
        var handler = new StubHttpMessageHandler(EthEurOrderBook);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.GetOrderBookAsync(new GetOrderBookRequest(EthEur, depth), TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.Error.ShouldBeOfType<ArgumentError>();
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_unlisted_market_is_an_UnknownSymbol_error_with_the_server_code()
    {
        var handler = new StubHttpMessageHandler("""{"errorCode":219,"error":"This market is no longer listed on Bitvavo."}""", HttpStatusCode.BadRequest);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.GetOrderBookAsync(new GetOrderBookRequest(new SharedSymbol(TradingMode.Spot, "NOPE", "EUR")), TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        var error = result.Error.ShouldBeOfType<ServerError>();
        error.ErrorType.ShouldBe(ErrorType.UnknownSymbol);
        error.Code.ShouldBe(219);
    }

    [Fact]
    public async Task The_legacy_interface_serves_the_order_book()
    {
        var legacy = new StubHttpMessageHandler("""{"market":"ETH-EUR","nonce":42,"bids":[["2990","1.0"]],"asks":[["3010","2.0"]]}""").RestClient().SpotApi.SharedClient;

        var result = await legacy.GetOrderBookAsync(new GetOrderBookRequest(EthEur, 100), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Bids.ShouldHaveSingleItem().Price.ShouldBe(2990m);
        result.Data.Asks.ShouldHaveSingleItem().Price.ShouldBe(3010m);
        result.Data.Asks[0].Quantity.ShouldBe(2.0m);
    }

    // ── book ticker ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_book_ticker_is_mapped_and_only_its_market_is_asked_for()
    {
        var handler = new StubHttpMessageHandler("[" + EthEurBookTicker + "]");
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.GetBookTickerAsync(new GetBookTickerRequest(EthEur), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Symbol.ShouldBe("ETH-EUR");
        result.Data.SharedSymbol.ShouldBe(EthEur);
        result.Data.BestBidPrice.ShouldBe(2999m);
        result.Data.BestBidQuantities.QuantityInBaseAsset.ShouldBe(1.5m);
        result.Data.BestAskPrice.ShouldBe(3001m);
        result.Data.BestAskQuantities.QuantityInBaseAsset.ShouldBe(2.5m);
        var request = handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Get);
        request.RequestUri!.AbsolutePath.ShouldBe("/v2/ticker/book");
        request.RequestUri.Query.ShouldContain("market=ETH-EUR");
    }

    [Fact]
    public async Task An_unknown_book_ticker_market_is_an_UnknownSymbol_error()
    {
        var api = new StubHttpMessageHandler("[]").RestClient().SpotApi.SharedApi;

        var result = await api.GetBookTickerAsync(new GetBookTickerRequest(EthEur), TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.Error!.ErrorType.ShouldBe(ErrorType.UnknownSymbol);
    }

    [Fact]
    public async Task When_the_server_answers_with_more_book_tickers_than_asked_the_requested_one_is_picked()
    {
        var api = new StubHttpMessageHandler("""[{"market":"BTC-EUR","bid":"1","bidSize":"1","ask":"2","askSize":"1"},""" + EthEurBookTicker + "]").RestClient().SpotApi.SharedApi;

        var result = await api.GetBookTickerAsync(new GetBookTickerRequest(EthEur), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Symbol.ShouldBe("ETH-EUR");
        result.Data.BestBidPrice.ShouldBe(2999m);
    }

    [Fact]
    public async Task An_ask_side_without_orders_has_price_zero_and_no_quantity()
    {
        var api = new StubHttpMessageHandler("""[{"market":"ETH-EUR","bid":"2999","bidSize":"1.5","ask":null,"askSize":null}]""").RestClient().SpotApi.SharedApi;

        var result = await api.GetBookTickerAsync(new GetBookTickerRequest(EthEur), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.BestBidPrice.ShouldBe(2999m);
        result.Data.BestAskPrice.ShouldBe(0m);
        result.Data.BestAskQuantities.QuantityInBaseAsset.ShouldBeNull();
    }

    [Fact]
    public async Task A_bid_side_without_orders_has_price_zero_and_no_quantity()
    {
        var api = new StubHttpMessageHandler("""[{"market":"ETH-EUR","bid":null,"bidSize":null,"ask":"3001","askSize":"2.5"}]""").RestClient().SpotApi.SharedApi;

        var result = await api.GetBookTickerAsync(new GetBookTickerRequest(EthEur), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.BestAskPrice.ShouldBe(3001m);
        result.Data.BestBidPrice.ShouldBe(0m);
        result.Data.BestBidQuantities.QuantityInBaseAsset.ShouldBeNull();
    }

    [Fact]
    public async Task The_legacy_interface_serves_the_book_ticker()
    {
        var legacy = new StubHttpMessageHandler("[" + EthEurBookTicker + "]").RestClient().SpotApi.SharedClient;

        var result = await legacy.GetBookTickerAsync(new GetBookTickerRequest(EthEur), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.BestBidPrice.ShouldBe(2999m);
        result.Data.BestAskPrice.ShouldBe(3001m);
        result.Data.BestAskQuantities.QuantityInBaseAsset.ShouldBe(2.5m);
    }

    // ── recent trades ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Recent_trades_are_mapped_newest_first_with_their_side_and_the_limit_is_sent()
    {
        var handler = new StubHttpMessageHandler(EthEurTrades);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.GetRecentTradesAsync(new GetRecentTradesRequest(EthEur, 10), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Length.ShouldBe(2);
        var newest = result.Data[0];
        newest.Symbol.ShouldBe("ETH-EUR");
        newest.SharedSymbol.ShouldBe(EthEur);
        newest.Price.ShouldBe(3000m);
        newest.Quantities.QuantityInBaseAsset.ShouldBe(0.5m);
        newest.Side.ShouldBe(SharedOrderSide.Buy);
        newest.Timestamp.ShouldBe(new DateTime(2024, 4, 26, 12, 0, 0, DateTimeKind.Utc));
        var older = result.Data[1];
        older.Price.ShouldBe(2999m);
        older.Quantities.QuantityInBaseAsset.ShouldBe(1.2m);
        older.Side.ShouldBe(SharedOrderSide.Sell);
        older.Timestamp.ShouldBe(new DateTime(2024, 4, 26, 11, 59, 50, DateTimeKind.Utc));
        var request = handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Get);
        request.RequestUri!.AbsolutePath.ShouldBe("/v2/ETH-EUR/trades");
        request.RequestUri.Query.ShouldContain("limit=10");
    }

    [Fact]
    public async Task A_trade_without_a_price_or_an_amount_has_price_zero_and_no_quantity()
    {
        var api = new StubHttpMessageHandler("""[{"id":"t1","timestamp":1714132800000,"side":"sell"}]""").RestClient().SpotApi.SharedApi;

        var result = await api.GetRecentTradesAsync(new GetRecentTradesRequest(EthEur), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        var trade = result.Data.ShouldHaveSingleItem();
        trade.Price.ShouldBe(0m);
        trade.Quantities.QuantityInBaseAsset.ShouldBeNull();
        trade.Side.ShouldBe(SharedOrderSide.Sell);
    }

    [Fact]
    public async Task Without_a_limit_none_is_sent()
    {
        var handler = new StubHttpMessageHandler(EthEurTrades);
        var api = handler.RestClient().SpotApi.SharedApi;

        await api.GetRecentTradesAsync(new GetRecentTradesRequest(EthEur), TestContext.Current.CancellationToken);

        handler.Requests.ShouldHaveSingleItem().RequestUri!.Query.ShouldNotContain("limit");
    }

    [Fact]
    public async Task A_limit_of_1000_is_sent()
    {
        var handler = new StubHttpMessageHandler(EthEurTrades);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.GetRecentTradesAsync(new GetRecentTradesRequest(EthEur, 1000), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        handler.Requests.ShouldHaveSingleItem().RequestUri!.Query.ShouldContain("limit=1000");
    }

    [Fact]
    public async Task A_limit_above_1000_is_rejected_before_anything_is_sent()
    {
        var handler = new StubHttpMessageHandler(EthEurTrades);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.GetRecentTradesAsync(new GetRecentTradesRequest(EthEur, 1001), TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.Error.ShouldBeOfType<ArgumentError>();
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_legacy_interface_serves_recent_trades()
    {
        var legacy = new StubHttpMessageHandler("""[{"id":"t1","timestamp":1714132800000,"amount":"0.5","price":"3000","side":"buy"}]""").RestClient().SpotApi.SharedClient;

        var result = await legacy.GetRecentTradesAsync(new GetRecentTradesRequest(EthEur, 10), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        var trade = result.Data.ShouldHaveSingleItem();
        trade.Price.ShouldBe(3000m);
        trade.Quantities.QuantityInBaseAsset.ShouldBe(0.5m);
        trade.Side.ShouldBe(SharedOrderSide.Buy);
    }

    // ── what the six capabilities have in common ─────────────────────────────────────────

    [Fact]
    public async Task Requests_for_another_trading_mode_are_rejected_before_anything_is_sent()
    {
        var handler = new StubHttpMessageHandler("[]");
        var api = handler.RestClient().SpotApi.SharedApi;
        var perpetual = new SharedSymbol(TradingMode.PerpetualLinear, "BTC", "EUR");

        var symbols = await api.GetSpotSymbolsAsync(new GetSymbolsRequest(TradingMode.PerpetualLinear), TestContext.Current.CancellationToken);
        var ticker = await api.GetTickerAsync(new GetTickerRequest(perpetual), TestContext.Current.CancellationToken);
        var tickers = await api.GetAllTickersAsync(new GetTickersRequest(TradingMode.PerpetualLinear), TestContext.Current.CancellationToken);
        var orderBook = await api.GetOrderBookAsync(new GetOrderBookRequest(perpetual), TestContext.Current.CancellationToken);
        var bookTicker = await api.GetBookTickerAsync(new GetBookTickerRequest(perpetual), TestContext.Current.CancellationToken);
        var trades = await api.GetRecentTradesAsync(new GetRecentTradesRequest(perpetual), TestContext.Current.CancellationToken);

        symbols.Error.ShouldBeOfType<ArgumentError>();
        ticker.Error.ShouldBeOfType<ArgumentError>();
        tickers.Error.ShouldBeOfType<ArgumentError>();
        orderBook.Error.ShouldBeOfType<ArgumentError>();
        bookTicker.Error.ShouldBeOfType<ArgumentError>();
        trades.Error.ShouldBeOfType<ArgumentError>();
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_server_error_passes_through_every_capability_with_its_mapped_type()
    {
        var handler = new StubHttpMessageHandler(InvalidParameterBody, HttpStatusCode.BadRequest);
        var api = ClientWithItsOwnSymbolCache(handler).SpotApi.SharedApi;

        var errors = new Error?[]
        {
            (await api.GetSpotSymbolsAsync(new GetSymbolsRequest(), TestContext.Current.CancellationToken)).Error,
            (await api.GetTickerAsync(new GetTickerRequest(EthEur), TestContext.Current.CancellationToken)).Error,
            (await api.GetAllTickersAsync(new GetTickersRequest(TradingMode.Spot), TestContext.Current.CancellationToken)).Error,
            (await api.GetOrderBookAsync(new GetOrderBookRequest(EthEur), TestContext.Current.CancellationToken)).Error,
            (await api.GetBookTickerAsync(new GetBookTickerRequest(EthEur), TestContext.Current.CancellationToken)).Error,
            (await api.GetRecentTradesAsync(new GetRecentTradesRequest(EthEur), TestContext.Current.CancellationToken)).Error,
        };

        foreach (var error in errors)
        {
            var serverError = error.ShouldBeOfType<ServerError>();
            serverError.ErrorType.ShouldBe(ErrorType.InvalidParameter);
            serverError.Code.ShouldBe(205);
        }
    }

    [Fact]
    public async Task All_six_capabilities_work_without_credentials()
    {
        var handler = new StubHttpMessageHandler(PublicAnswer);
        var api = ClientWithItsOwnSymbolCache(handler, withCredentials: false).SpotApi.SharedApi;

        var symbols = await api.GetSpotSymbolsAsync(new GetSymbolsRequest(), TestContext.Current.CancellationToken);
        var ticker = await api.GetTickerAsync(new GetTickerRequest(EthEur), TestContext.Current.CancellationToken);
        var tickers = await api.GetAllTickersAsync(new GetTickersRequest(TradingMode.Spot), TestContext.Current.CancellationToken);
        var orderBook = await api.GetOrderBookAsync(new GetOrderBookRequest(EthEur), TestContext.Current.CancellationToken);
        var bookTicker = await api.GetBookTickerAsync(new GetBookTickerRequest(EthEur), TestContext.Current.CancellationToken);
        var trades = await api.GetRecentTradesAsync(new GetRecentTradesRequest(EthEur), TestContext.Current.CancellationToken);

        symbols.Success.ShouldBeTrue();
        ticker.Success.ShouldBeTrue();
        tickers.Success.ShouldBeTrue();
        orderBook.Success.ShouldBeTrue();
        bookTicker.Success.ShouldBeTrue();
        trades.Success.ShouldBeTrue();
        handler.Requests.Count.ShouldBe(6);
        handler.Requests.Any(request => request.Headers.Contains("Bitvavo-Access-Signature")).ShouldBeFalse();
    }

    [Fact]
    public async Task The_transport_agnostic_capabilities_answer_as_the_REST_ones_do()
    {
        var handler = new StubHttpMessageHandler(PublicAnswer);
        var api = ClientWithItsOwnSymbolCache(handler).SpotApi.SharedApi;

        var symbols = await ((IGetSpotSymbols)api).GetSpotSymbolsAsync(new GetSymbolsRequest(), TestContext.Current.CancellationToken);
        var ticker = await ((IGetTicker)api).GetTickerAsync(new GetTickerRequest(EthEur), TestContext.Current.CancellationToken);
        var tickers = await ((IGetAllTickers)api).GetAllTickersAsync(new GetTickersRequest(TradingMode.Spot), TestContext.Current.CancellationToken);
        var orderBook = await ((IGetOrderBook)api).GetOrderBookAsync(new GetOrderBookRequest(EthEur), TestContext.Current.CancellationToken);
        var bookTicker = await ((IGetBookTicker)api).GetBookTickerAsync(new GetBookTickerRequest(EthEur), TestContext.Current.CancellationToken);
        var trades = await ((IGetRecentTrades)api).GetRecentTradesAsync(new GetRecentTradesRequest(EthEur), TestContext.Current.CancellationToken);

        symbols.Success.ShouldBeTrue();
        symbols.Data.ShouldHaveSingleItem().Name.ShouldBe("BTC-EUR");
        ticker.Success.ShouldBeTrue();
        ticker.Data.LastPrice.ShouldBe(3060m);
        tickers.Success.ShouldBeTrue();
        tickers.Data.ShouldHaveSingleItem().Symbol.ShouldBe("ETH-EUR");
        orderBook.Success.ShouldBeTrue();
        orderBook.Data.SequenceNumber.ShouldBe(42L);
        bookTicker.Success.ShouldBeTrue();
        bookTicker.Data.BestAskPrice.ShouldBe(3001m);
        trades.Success.ShouldBeTrue();
        trades.Data.Length.ShouldBe(2);
        symbols.Exchange.ShouldBe("Bitvavo");
    }

    [Fact]
    public void The_options_describe_what_Bitvavo_supports()
    {
        var api = new StubHttpMessageHandler("[]").RestClient().SpotApi.SharedApi;

        api.GetOrderBookOptions.MinLimit.ShouldBe(1);
        api.GetOrderBookOptions.MaxLimit.ShouldBe(1000);
        api.GetRecentTradesOptions.MaxLimit.ShouldBe(1000);
        api.GetTickerOptions.TickerType.ShouldBe(SharedTickerType.Day24H);
        api.GetAllTickersOptions.TickerType.ShouldBe(SharedTickerType.Day24H);
        foreach (var options in new CapabilityOptions[]
        {
            api.GetSpotSymbolsOptions,
            api.GetTickerOptions,
            api.GetAllTickersOptions,
            api.GetOrderBookOptions,
            api.GetBookTickerOptions,
            api.GetRecentTradesOptions,
        })
        {
            options.NeedsAuthentication.ShouldBeFalse(options.OperationName);
            options.SupportedTradingModes.SequenceEqual(new[] { TradingMode.Spot }).ShouldBeTrue(options.OperationName);
        }
    }

    [Fact]
    public void Every_option_of_the_group_is_registered_as_a_capability()
    {
        var api = new StubHttpMessageHandler("[]").RestClient().SpotApi.SharedApi;

        api.Capabilities.ShouldContain(api.GetSpotSymbolsOptions);
        api.Capabilities.ShouldContain(api.GetTickerOptions);
        api.Capabilities.ShouldContain(api.GetAllTickersOptions);
        api.Capabilities.ShouldContain(api.GetOrderBookOptions);
        api.Capabilities.ShouldContain(api.GetBookTickerOptions);
        api.Capabilities.ShouldContain(api.GetRecentTradesOptions);
    }

    // ── [V1] the legacy options are the V2 options ───────────────────────────────────────

    [Fact]
    public void The_legacy_options_are_the_V2_options()
    {
        var client = new StubHttpMessageHandler("[]").RestClient();
        var legacy = client.SpotApi.SharedClient;
        var api = client.SpotApi.SharedApi;

        legacy.GetSpotSymbolsOptions.ShouldBeSameAs(api.GetSpotSymbolsOptions);
        legacy.GetSpotTickerOptions.ShouldBeSameAs(api.GetTickerOptions);
        legacy.GetSpotTickersOptions.ShouldBeSameAs(api.GetAllTickersOptions);
        legacy.GetOrderBookOptions.ShouldBeSameAs(api.GetOrderBookOptions);
        legacy.GetBookTickerOptions.ShouldBeSameAs(api.GetBookTickerOptions);
        legacy.GetRecentTradesOptions.ShouldBeSameAs(api.GetRecentTradesOptions);
    }

    [Fact]
    public async Task The_legacy_interface_serves_the_spot_symbols()
    {
        var legacy = ClientWithItsOwnSymbolCache(new StubHttpMessageHandler(Markets(MarketJson("ETH", "EUR")))).SpotApi.SharedClient;

        var result = await legacy.GetSpotSymbolsAsync(new GetSymbolsRequest(TradingMode.Spot), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        var symbol = result.Data.ShouldHaveSingleItem();
        symbol.BaseAsset.ShouldBe("ETH");
        symbol.QuoteAsset.ShouldBe("EUR");
        symbol.Name.ShouldBe("ETH-EUR");
        symbol.Trading.ShouldBeTrue();
        symbol.MinTradeQuantity.ShouldBe(0.001m);
    }
}
