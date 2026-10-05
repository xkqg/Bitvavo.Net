// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Net.Http;
using Bitvavo.Net.Clients;
using Bitvavo.Net.Objects.Options;
using Microsoft.Extensions.Options;

namespace Bitvavo.Net.Tests;

/// <summary>Builds the real REST client on top of the test transport.</summary>
internal static class StubHttpMessageHandlerExtensions
{
    /// <summary>
    /// A <see cref="BitvavoRestClient"/> whose every request goes to <paramref name="handler"/>. By default it holds API
    /// credentials (<c>test-key</c> / <c>test-secret</c>), so signed endpoints can be called.
    /// </summary>
    /// <param name="handler">The transport every request is sent through.</param>
    /// <param name="withCredentials">Whether the client holds API credentials.</param>
    /// <param name="configure">Optional options customisation, applied after the defaults and the credentials.</param>
    public static BitvavoRestClient RestClient(this StubHttpMessageHandler handler, bool withCredentials = true, Action<BitvavoRestOptions>? configure = null)
    {
        var options = new BitvavoRestOptions();
        if (withCredentials)
        {
            options.ApiCredentials = new BitvavoCredentials("test-key", "test-secret");
        }

        configure?.Invoke(options);

        return new BitvavoRestClient(new HttpClient(handler), null, Options.Create(options));
    }
}
