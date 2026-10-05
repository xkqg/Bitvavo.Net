// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Bitvavo.Net.Tests;

/// <summary>
/// The network-transport seam of the test suite: an <see cref="HttpMessageHandler"/> that feeds canned JSON responses to the
/// CryptoExchange.Net request pipeline and records every request, so DTO mapping, signing and rate limiting are verified
/// through the real pipeline without hitting api.bitvavo.com. Nothing of the library itself is replaced.
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = new();

    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public StubHttpMessageHandler(string responseJson, HttpStatusCode status = HttpStatusCode.OK)
        : this(_ => new HttpResponseMessage(status) { Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json") })
    { }

    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(_responder(request));
    }
}
