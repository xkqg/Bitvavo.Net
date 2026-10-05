// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Bitvavo.Net.Tests;

/// <summary>
/// A network-transport seam that never answers: the request waits until the caller's timeout or cancellation ends it. Proves a
/// timeout is enforced by the HTTP client the library actually sends through, without a real server and without waiting for one.
/// </summary>
internal sealed class HangingHttpMessageHandler : HttpMessageHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await Task.Delay(Timeout.Infinite, cancellationToken).ConfigureAwait(false);
        return new HttpResponseMessage();
    }
}
