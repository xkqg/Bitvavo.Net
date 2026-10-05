// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.SharedApis;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests.Clients.SpotApi.SharedApi;

/// <summary>
/// The Shared kline capability on the REST API ([V2] <c>IGetKlinesRest</c> and the legacy [V1] <c>IKlineRestClient</c> on the same
/// instance), through the real request pipeline. Bitvavo returns candles newest first and serves up to 1440 per request.
/// </summary>
public class BitvavoRestSharedKlinesTests
{
    private const string Candles = """[[1714136400000,"3050","3120","3040","3100","8.5"],[1714132800000,"3000","3100","2950","3050","12.5"]]""";

    private static SharedSymbol EthEur => new(TradingMode.Spot, "ETH", "EUR");

    [Fact]
    public async Task Klines_are_mapped_and_the_interval_limit_and_window_are_sent()
    {
        var handler = new StubHttpMessageHandler(Candles);
        var api = handler.RestClient().SpotApi.SharedApi;
        var start = new DateTime(2024, 4, 26, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2024, 4, 27, 0, 0, 0, DateTimeKind.Utc);

        var result = await api.GetKlinesAsync(
            new GetKlinesRequest(EthEur, SharedKlineInterval.OneHour, start, end, limit: 5),
            ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Length.ShouldBe(2);
        var newest = result.Data[0];
        newest.OpenTime.ShouldBe(new DateTime(2024, 4, 26, 13, 0, 0, DateTimeKind.Utc));
        newest.OpenPrice.ShouldBe(3050m);
        newest.HighPrice.ShouldBe(3120m);
        newest.LowPrice.ShouldBe(3040m);
        newest.ClosePrice.ShouldBe(3100m);
        newest.Volumes.QuantityInBaseAsset.ShouldBe(8.5m);
        newest.Symbol.ShouldBe("ETH-EUR");
        var request = handler.Requests.ShouldHaveSingleItem();
        request.RequestUri!.AbsolutePath.ShouldBe("/v2/ETH-EUR/candles");
        request.RequestUri.Query.ShouldContain("interval=1h");
        request.RequestUri.Query.ShouldContain("limit=5");
        request.RequestUri.Query.ShouldContain("start=" + new DateTimeOffset(start).ToUnixTimeMilliseconds());
        request.RequestUri.Query.ShouldContain("end=" + new DateTimeOffset(end).ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task A_request_without_an_end_time_does_not_invent_one()
    {
        var handler = new StubHttpMessageHandler(Candles);
        var api = handler.RestClient().SpotApi.SharedApi;

        await api.GetKlinesAsync(new GetKlinesRequest(EthEur, SharedKlineInterval.OneHour), ct: TestContext.Current.CancellationToken);

        var query = handler.Requests.ShouldHaveSingleItem().RequestUri!.Query;
        query.ShouldNotContain("end=");
        query.ShouldNotContain("start=");
    }

    [Fact]
    public async Task A_full_page_returns_the_request_for_the_next_older_page_and_the_token_continues_from_there()
    {
        var handler = new StubHttpMessageHandler(Candles);
        var api = handler.RestClient().SpotApi.SharedApi;

        var first = await api.GetKlinesAsync(
            new GetKlinesRequest(EthEur, SharedKlineInterval.OneHour, limit: 2),
            ct: TestContext.Current.CancellationToken);

        var oldest = new DateTime(2024, 4, 26, 12, 0, 0, DateTimeKind.Utc);
        first.NextPageRequest.ShouldNotBeNull();
        first.NextPageRequest!.EndTime.ShouldBe(oldest.AddHours(-1));

        await api.GetKlinesAsync(
            new GetKlinesRequest(EthEur, SharedKlineInterval.OneHour, limit: 2),
            first.NextPageRequest,
            TestContext.Current.CancellationToken);

        handler.Requests[1].RequestUri!.Query.ShouldContain("end=" + new DateTimeOffset(oldest.AddHours(-1)).ToUnixTimeMilliseconds());
    }

    [Fact]
    public async Task A_short_page_has_no_next_page()
    {
        var handler = new StubHttpMessageHandler(Candles);
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.GetKlinesAsync(
            new GetKlinesRequest(EthEur, SharedKlineInterval.OneHour, limit: 100),
            ct: TestContext.Current.CancellationToken);

        result.NextPageRequest.ShouldBeNull();
    }

    [Fact]
    public async Task Requests_the_options_forbid_are_rejected_before_anything_is_sent()
    {
        var handler = new StubHttpMessageHandler(Candles);
        var api = handler.RestClient().SpotApi.SharedApi;

        var threeMinutes = await api.GetKlinesAsync(new GetKlinesRequest(EthEur, SharedKlineInterval.ThreeMinutes), ct: TestContext.Current.CancellationToken);
        var ascending = await api.GetKlinesAsync(new GetKlinesRequest(EthEur, SharedKlineInterval.OneHour, direction: DataDirection.Ascending), ct: TestContext.Current.CancellationToken);
        var tooMany = await api.GetKlinesAsync(new GetKlinesRequest(EthEur, SharedKlineInterval.OneHour, limit: 1441), ct: TestContext.Current.CancellationToken);

        threeMinutes.Error.ShouldBeOfType<ArgumentError>();
        ascending.Error.ShouldBeOfType<ArgumentError>();
        tooMany.Error.ShouldBeOfType<ArgumentError>();
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public void The_options_describe_what_Bitvavo_supports()
    {
        var options = new StubHttpMessageHandler("[]").RestClient().SpotApi.SharedApi.GetKlinesOptions;

        options.SupportsAscending.ShouldBeFalse();
        options.SupportsDescending.ShouldBeTrue();
        options.MaxLimit.ShouldBe(1440);
        options.NeedsAuthentication.ShouldBeFalse();
        options.SupportIntervals.Length.ShouldBe(13);
        options.IsSupported(SharedKlineInterval.ThreeMinutes).ShouldBeFalse();
    }

    // ── [V1] the legacy interface on the same instance ───────────────────────────────────

    [Fact]
    public async Task The_legacy_interface_serves_klines_and_keeps_the_total_data_point_ceiling_the_consumer_reads()
    {
        var handler = new StubHttpMessageHandler(Candles);
        var legacy = handler.RestClient().SpotApi.SharedClient;

        var result = await legacy.GetKlinesAsync(new GetKlinesRequest(EthEur, SharedKlineInterval.OneHour), ct: TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Length.ShouldBe(2);
        legacy.GetKlinesOptions.MaxTotalDataPoints.ShouldBe(1440);
    }
}
