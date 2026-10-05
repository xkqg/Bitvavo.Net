// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Bitvavo.Net.Clients;
using Bitvavo.Net.Enums;
using Bitvavo.Net.Objects.Internal;
using Bitvavo.Net.Objects.Models.Spot;
using Bitvavo.Net.Objects.Options;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Errors;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests;

/// <summary>
/// Bitvavo's error catalog (docs.bitvavo.com, "Handle errors": 90 error codes over HTTP 400/403/404/409/429/500/503) mapped onto
/// CryptoExchange.Net's <see cref="ErrorType"/>, so a caller can branch on what went wrong (insufficient balance, unknown order,
/// invalid price, …) and on whether waiting helps, without parsing the server's message.
/// </summary>
public class BitvavoErrorMappingTests
{
    private readonly record struct CatalogEntry(int Code, ErrorType Type, bool Transient);

    /// <summary>Every code of the vendor's catalog, with the type it must map to; <c>Transient</c> = the situation clears by itself, so retrying later can succeed.</summary>
    private static readonly CatalogEntry[] Catalog =
    [
        // 400 request format
        new(102, ErrorType.InvalidParameter, false), new(200, ErrorType.InvalidParameter, false), new(201, ErrorType.InvalidParameter, false),
        new(202, ErrorType.RejectedOrderConfiguration, false), new(203, ErrorType.MissingParameter, false), new(204, ErrorType.InvalidParameter, false),
        new(205, ErrorType.InvalidParameter, false), new(206, ErrorType.InvalidParameter, false),
        // 400 order and trading
        new(210, ErrorType.InvalidQuantity, false), new(211, ErrorType.InvalidPrice, false), new(212, ErrorType.InvalidQuantity, false),
        new(213, ErrorType.InvalidPrice, false), new(215, ErrorType.InvalidPrice, false), new(216, ErrorType.InsufficientBalance, false),
        new(217, ErrorType.InvalidQuantity, false), new(219, ErrorType.UnknownSymbol, false), new(220, ErrorType.DuplicateClientOrderId, false),
        new(231, ErrorType.RejectedOrderConfiguration, false), new(232, ErrorType.MissingParameter, false), new(234, ErrorType.RejectedOrderConfiguration, false),
        new(235, ErrorType.RateLimitOrder, false), new(236, ErrorType.InvalidParameter, false), new(237, ErrorType.InvalidStopParameters, false),
        new(238, ErrorType.InvalidStopParameters, false), new(239, ErrorType.InvalidParameter, false), new(422, ErrorType.InvalidPrice, false),
        new(429, ErrorType.InvalidParameter, false),
        // 400 market status
        new(423, ErrorType.UnavailableSymbol, true), new(424, ErrorType.UnavailableSymbol, true), new(425, ErrorType.UnavailableSymbol, true),
        new(426, ErrorType.UnavailableSymbol, true),
        // 400 deposits and withdrawals
        new(401, ErrorType.InvalidOperation, false), new(402, ErrorType.Unauthorized, false), new(403, ErrorType.Unauthorized, false),
        new(404, ErrorType.SystemError, true), new(405, ErrorType.InvalidOperation, true), new(406, ErrorType.InvalidQuantity, false),
        new(407, ErrorType.InvalidOperation, false), new(408, ErrorType.InsufficientBalance, false), new(409, ErrorType.Unauthorized, false),
        new(410, ErrorType.InvalidOperation, false), new(411, ErrorType.InvalidOperation, false), new(412, ErrorType.InvalidOperation, false),
        new(413, ErrorType.InvalidOperation, false), new(414, ErrorType.InvalidOperation, true), new(434, ErrorType.RiskError, false),
        new(435, ErrorType.RiskError, false), new(436, ErrorType.RiskError, false), new(437, ErrorType.RiskError, false),
        new(438, ErrorType.RiskError, false), new(439, ErrorType.InvalidParameter, false),
        // 403 authentication
        new(300, ErrorType.MissingCredentials, false), new(301, ErrorType.Unauthorized, false), new(302, ErrorType.InvalidTimestamp, false),
        new(303, ErrorType.InvalidTimestamp, false), new(304, ErrorType.InvalidTimestamp, false), new(305, ErrorType.Unauthorized, false),
        new(306, ErrorType.Unauthorized, false), new(307, ErrorType.Unauthorized, false), new(308, ErrorType.Unauthorized, false),
        new(309, ErrorType.Unauthorized, false),
        // 403 permissions
        new(310, ErrorType.Unauthorized, false), new(311, ErrorType.Unauthorized, false), new(312, ErrorType.Unauthorized, false),
        new(313, ErrorType.Unauthorized, false), new(314, ErrorType.Unauthorized, false), new(316, ErrorType.Unauthorized, false),
        new(317, ErrorType.Unauthorized, false), new(318, ErrorType.Unauthorized, false), new(319, ErrorType.Unauthorized, false),
        new(320, ErrorType.Unauthorized, false), new(322, ErrorType.Unauthorized, false), new(511, ErrorType.Unauthorized, false),
        new(512, ErrorType.Unauthorized, false), new(513, ErrorType.Unauthorized, false), new(514, ErrorType.InvalidOperation, false),
        // 404
        new(240, ErrorType.UnknownOrder, false), new(415, ErrorType.InvalidOperation, false), new(510, ErrorType.InvalidParameter, false),
        // 409
        new(431, ErrorType.UnavailableSymbol, true),
        // 429
        new(105, ErrorType.RateLimitRequest, true), new(112, ErrorType.RateLimitRequest, true),
        // 500
        new(101, ErrorType.SystemError, true), new(400, ErrorType.SystemError, true),
        // 503 — 109: the operation may or may not have happened, so it is not "safe to retry"
        new(107, ErrorType.SystemError, true), new(108, ErrorType.SystemError, true), new(109, ErrorType.Timeout, false),
        new(111, ErrorType.SystemError, true), new(419, ErrorType.SystemError, true), new(430, ErrorType.Timeout, true),
    ];

    public static IEnumerable<object[]> Codes() => Catalog.Select(e => new object[] { e.Code });

    private static BitvavoRestClient CreateClient(HttpStatusCode status, string body)
    {
        var options = new BitvavoRestOptions { ApiCredentials = new BitvavoCredentials("test-key", "test-secret") };
        return new BitvavoRestClient(new HttpClient(new StubHttpMessageHandler(body, status)), null, Options.Create(options));
    }

    [Fact]
    public void The_catalog_lists_90_distinct_codes()
    {
        Catalog.Length.ShouldBe(90);
        Catalog.Select(e => e.Code).Distinct().Count().ShouldBe(90);
    }

    [Theory]
    [MemberData(nameof(Codes))]
    public void Documented_error_code_maps_to_its_error_type(int code)
    {
        var expected = Catalog.Single(e => e.Code == code);

        var info = BitvavoErrors.SpotMapping.GetErrorInfo(code.ToString(), "server message");

        info.ErrorType.ShouldBe(expected.Type);
        info.IsTransient.ShouldBe(expected.Transient);
        info.Message.ShouldBe("server message");
    }

    [Fact]
    public void Code_outside_the_catalog_maps_to_Unknown()
    {
        BitvavoErrors.SpotMapping.GetErrorInfo("9999", "who knows").ErrorType.ShouldBe(ErrorType.Unknown);
    }

    /// <summary>A caller holding only a code (from a log, from a Shared result) asks the API client what it means: REST and WebSocket share the catalog.</summary>
    [Fact]
    public void Both_api_clients_resolve_a_code_from_the_catalog()
    {
        var rest = (Bitvavo.Net.Clients.SpotApi.BitvavoRestClientSpotApi)CreateClient(HttpStatusCode.OK, "{}").SpotApi;
        var socket = (Bitvavo.Net.Clients.SpotApi.BitvavoSocketClientSpotApi)new BitvavoSocketClient().SpotApi;

        rest.GetErrorInfo("216", "no money").ErrorType.ShouldBe(ErrorType.InsufficientBalance);
        socket.GetErrorInfo("240").ErrorType.ShouldBe(ErrorType.UnknownOrder);
    }

    [Fact]
    public async Task Server_error_reaches_the_caller_with_its_mapped_type_code_and_message()
    {
        var client = CreateClient(HttpStatusCode.BadRequest, """{"errorCode":216,"error":"Your balance is insufficient."}""");

        var result = await client.SpotApi.Trading.PlaceOrderAsync(
            new BitvavoPlaceOrderRequest("BTC-EUR", OrderSide.Buy, OrderType.Market, 7, AmountQuote: 12.5m), TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        var error = result.Error.ShouldBeOfType<ServerError>();
        error.ErrorType.ShouldBe(ErrorType.InsufficientBalance);
        error.Code.ShouldBe(216);
        error.Message.ShouldBe("Your balance is insufficient.");
    }

    /// <summary>
    /// HTTP 429 takes the framework's rate-limit path, which would otherwise discard the body. The server's code (105: the
    /// budget is spent and the caller is blocked; 112: too many WebSocket messages per second) and message stay readable.
    /// </summary>
    [Fact]
    public async Task Rate_limit_response_keeps_the_server_code_and_message()
    {
        var client = CreateClient(HttpStatusCode.TooManyRequests, """{"errorCode":105,"error":"You exceeded the rate limit."}""");

        var result = await client.SpotApi.Trading.GetOpenOrdersAsync(ct: TestContext.Current.CancellationToken);

        var error = result.Error.ShouldBeOfType<ServerRateLimitError>();
        error.ErrorType.ShouldBe(ErrorType.RateLimitRequest);
        error.ErrorCode.ShouldBe("105");
        error.Message.ShouldNotBeNull().ShouldContain("You exceeded the rate limit.");
    }

    [Fact]
    public async Task Rate_limit_response_without_a_readable_body_is_still_a_rate_limit_error()
    {
        var client = CreateClient(HttpStatusCode.TooManyRequests, "<html>slow down</html>");

        var result = await client.SpotApi.Trading.GetOpenOrdersAsync(ct: TestContext.Current.CancellationToken);

        var error = result.Error.ShouldBeOfType<ServerRateLimitError>();
        error.ErrorType.ShouldBe(ErrorType.RateLimitRequest);
        error.ErrorCode.ShouldBeNull();
    }
}
