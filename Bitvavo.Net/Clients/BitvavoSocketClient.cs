// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using Bitvavo.Net.Clients.SpotApi;
using Bitvavo.Net.Interfaces.Clients;
using Bitvavo.Net.Interfaces.Clients.SpotApi;
using Bitvavo.Net.Objects.Options;
using CryptoExchange.Net.Clients;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bitvavo.Net.Clients;

/// <inheritdoc cref="IBitvavoSocketClient" />
public sealed class BitvavoSocketClient : BaseSocketClient<BitvavoEnvironment, BitvavoCredentials>, IBitvavoSocketClient
{
    /// <inheritdoc />
    public IBitvavoSocketClientSpotApi SpotApi { get; }

    /// <summary>Construct with an optional options-customisation delegate.</summary>
    public BitvavoSocketClient(Action<BitvavoSocketOptions>? optionsDelegate = null)
        : this(null, Microsoft.Extensions.Options.Options.Create(ApplyOptionsDelegate(optionsDelegate)))
    {
    }

    /// <summary>DI-friendly ctor.</summary>
    public BitvavoSocketClient(ILoggerFactory? loggerFactory, IOptions<BitvavoSocketOptions> options)
        : base(loggerFactory, "Bitvavo")
    {
        Initialize(options.Value);
        SpotApi = AddApiClient(new BitvavoSocketClientSpotApi(_logger, options.Value));
    }

    /// <summary>Set the default options used by every new <see cref="BitvavoSocketClient"/>.</summary>
    public static void SetDefaultOptions(Action<BitvavoSocketOptions> optionsDelegate)
    {
        BitvavoSocketOptions.Default = ApplyOptionsDelegate(optionsDelegate);
    }
}
