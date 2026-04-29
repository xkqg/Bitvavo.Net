// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Net.Http;
using Bitvavo.Net.Clients.SpotApi;
using Bitvavo.Net.Interfaces.Clients;
using Bitvavo.Net.Interfaces.Clients.SpotApi;
using Bitvavo.Net.Objects.Options;
using CryptoExchange.Net.Clients;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bitvavo.Net.Clients;

/// <inheritdoc cref="IBitvavoRestClient" />
public class BitvavoRestClient : BaseRestClient<BitvavoEnvironment, BitvavoCredentials>, IBitvavoRestClient
{
    /// <inheritdoc />
    public IBitvavoRestClientSpotApi SpotApi { get; }

    /// <summary>
    /// Construct a new BitvavoRestClient. Mirrors <c>KrakenRestClient(Action&lt;...&gt;)</c> — pass an
    /// optional delegate to customise the <see cref="BitvavoRestOptions"/>.
    /// </summary>
    public BitvavoRestClient(Action<BitvavoRestOptions>? optionsDelegate = null)
        : this(null, null, Microsoft.Extensions.Options.Options.Create(ApplyOptionsDelegate(optionsDelegate)))
    {
    }

    /// <summary>DI-friendly ctor — pooled HttpClient + IOptions binding.</summary>
    public BitvavoRestClient(HttpClient? httpClient, ILoggerFactory? loggerFactory, IOptions<BitvavoRestOptions> options)
        : base(loggerFactory, "Bitvavo")
    {
        Initialize(options.Value);
        SpotApi = AddApiClient(new BitvavoRestClientSpotApi(_logger, httpClient, options.Value));
    }

    /// <summary>Set the default options used by every new <see cref="BitvavoRestClient"/>.</summary>
    public static void SetDefaultOptions(Action<BitvavoRestOptions> optionsDelegate)
    {
        BitvavoRestOptions.Default = ApplyOptionsDelegate(optionsDelegate);
    }
}
