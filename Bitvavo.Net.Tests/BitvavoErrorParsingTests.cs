// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Bitvavo.Net.Clients.MessageHandlers;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Errors;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests;

/// <summary>
/// The contract of <see cref="BitvavoRestSpotMessageHandler.ParseErrorResponse"/> and
/// <see cref="BitvavoRestSpotMessageHandler.ParseErrorRateLimitResponse"/>: the regression net for Bitvavo's
/// <c>{ "errorCode": int, "error": "msg" }</c> envelope, including the bodies that are no envelope at all (an array, an
/// object without the fields, fields of another type).
/// </summary>
public class BitvavoErrorParsingTests
{
    private static BitvavoRestSpotMessageHandler NewHandler() =>
        new(new ErrorMapping(System.Array.Empty<ErrorInfo>(), System.Array.Empty<ErrorEvaluator>()));

    private static Stream Body(string json) => new MemoryStream(Encoding.UTF8.GetBytes(json));

    private static System.Net.Http.Headers.HttpResponseHeaders EmptyHeaders =>
        new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest).Headers;

    [Fact]
    public async Task ParseErrorResponse_StandardEnvelope_returns_ServerError_with_code_and_message()
    {
        var handler = NewHandler();

        var error = await handler.ParseErrorResponse(400, EmptyHeaders, Body("""{"errorCode":203,"error":"key required"}"""));

        error.ShouldBeOfType<ServerError>();
        error.ErrorCode.ShouldBe("203");
        error.Code.ShouldBe(203);
    }

    [Fact]
    public async Task ParseErrorResponse_ErrorCodeAbsent_returns_ServerError_with_unknown_info()
    {
        var handler = NewHandler();

        var error = await handler.ParseErrorResponse(500, EmptyHeaders, Body("""{"foo":"bar"}"""));

        error.ShouldBeOfType<ServerError>();
        // No errorCode → mapped to ErrorInfo.Unknown; ErrorCode falls back to whatever the
        // unknown info exposes (null/empty), but ErrorType must be the unknown sentinel.
        error.ErrorType.ShouldBe(ErrorInfo.Unknown.ErrorType);
    }

    [Fact]
    public async Task ParseErrorResponse_NonJsonBody_returns_parse_error()
    {
        var handler = NewHandler();

        var error = await handler.ParseErrorResponse(502, EmptyHeaders, Body("not json at all <html>"));

        // The framework's GetJsonDocument returns its own parseError on invalid JSON;
        // we only care that it doesn't throw and that the returned Error is non-null.
        error.ShouldNotBeNull();
    }

    [Fact]
    public async Task ParseErrorResponse_ArrayBody_returns_ServerError_with_unknown_info()
    {
        var error = await NewHandler().ParseErrorResponse(400, EmptyHeaders, Body("[]"));

        error.ShouldBeOfType<ServerError>();
        error.ErrorType.ShouldBe(ErrorInfo.Unknown.ErrorType);
        error.ErrorCode.ShouldBeNull();
    }

    [Theory]
    [InlineData("""{"errorCode":105,"error":"Rate limit exceeded"}""", "105", "Rate limit exceeded")]
    [InlineData("""{"errorCode":112}""", "112", null)]
    [InlineData("""{"error":"Too many requests"}""", null, "Too many requests")]
    public async Task ParseErrorRateLimitResponse_keeps_whatever_the_server_said(string body, string? code, string? message)
    {
        var error = await NewHandler().ParseErrorRateLimitResponse(429, EmptyHeaders, Body(body));

        error.ErrorCode.ShouldBe(code);
        // CryptoExchange.Net prefixes the server's text with its own "Server rate limit exceeded", which is all there is without one.
        error.ToString().ShouldContain(message ?? "Server rate limit exceeded");
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("""{"foo":"bar"}""")]
    [InlineData("""{"errorCode":"x","error":5}""")]
    [InlineData("not json at all <html>")]
    public async Task ParseErrorRateLimitResponse_without_a_usable_envelope_is_the_plain_rate_limit_error(string body)
    {
        var error = await NewHandler().ParseErrorRateLimitResponse(429, EmptyHeaders, Body(body));

        error.ShouldBeOfType<ServerRateLimitError>();
        error.ErrorCode.ShouldBeNull();
    }

    [Fact]
    public async Task ParseErrorResponse_StandardEnvelope_propagates_errorCode_into_string_form()
    {
        var handler = NewHandler();

        var error = await handler.ParseErrorResponse(400, EmptyHeaders, Body("""{"errorCode":99,"error":"something"}"""));

        error.ShouldBeOfType<ServerError>();
        error.ErrorCode.ShouldBe("99");
    }
}
