// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using CryptoExchange.Net.Objects.Options;
using CryptoExchange.Net.SharedApis;
using Microsoft.Extensions.Configuration;

namespace Bitvavo.Net.Objects.Options;

/// <summary>
/// Library-wide options of the DI registration — <c>AddBitvavo(Action&lt;BitvavoOptions&gt;)</c> and
/// <c>AddBitvavo(IConfiguration)</c>: the environment and the credentials both transports share, the REST and socket
/// sections, and the Shared API transport preference. Mirrors <c>KrakenOptions</c>.
/// </summary>
/// <remarks>
/// The environment and the credentials set at this level are handed to the REST and socket sections that did not set their
/// own; a section that gets neither starts from the process-wide defaults (<c>SetDefaultOptions</c>), the lowest layer. The values
/// the clients run with are the registered <c>IOptions&lt;BitvavoRestOptions&gt;</c> and
/// <c>IOptions&lt;BitvavoSocketOptions&gt;</c> — a <c>PostConfigure</c> on those applies after everything set here.
/// </remarks>
public class BitvavoOptions : LibraryOptions<BitvavoRestOptions, BitvavoSocketOptions, BitvavoCredentials, BitvavoEnvironment>
{
    /// <summary>
    /// Options of the Shared API client (<see cref="Interfaces.Clients.IBitvavoSharedApiClient"/>): which transport answers when
    /// a capability exists on both REST and the WebSocket.
    /// </summary>
    public SharedApiOptions SharedApi { get; set; } = new();

    /// <summary>Create the options: start from the defaults, apply <paramref name="configure"/>, then normalise.</summary>
    /// <param name="configure">Optional customisation.</param>
    public static BitvavoOptions Create(Action<BitvavoOptions>? configure = null)
    {
        var options = CreateUnconfigured();
        configure?.Invoke(options);
        return Normalize(options);
    }

    /// <summary>
    /// Create the options from a configuration section (for example <c>configuration.GetSection("Bitvavo")</c>), then normalise.
    /// An environment name that is no Bitvavo environment throws; the lookup ignores case.
    /// </summary>
    /// <param name="configuration">The configuration to bind.</param>
    /// <exception cref="ArgumentNullException"><paramref name="configuration"/> is null.</exception>
    /// <exception cref="InvalidOperationException">A value cannot be bound, or an environment name is unknown.</exception>
    public static BitvavoOptions CreateFromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var options = CreateUnconfigured();
        try
        {
            configuration.Bind(options);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidOperationException("Invalid Bitvavo configuration provided", ex);
        }

        if (options.Environment != null)
        {
            options.Environment = Resolve(options.Environment);
        }

        if (options.Rest.Environment != null)
        {
            options.Rest.Environment = Resolve(options.Rest.Environment);
        }

        if (options.Socket.Environment != null)
        {
            options.Socket.Environment = Resolve(options.Socket.Environment);
        }

        return Normalize(options);
    }

    /// <summary>
    /// Copy everything into <paramref name="target"/>: the shared environment and credentials, the whole Bitvavo-specific REST
    /// and socket sections (the inherited copy only knows the framework's part), and the Shared API options.
    /// </summary>
    internal void CopyTo(BitvavoOptions target)
    {
        Set(target);
        target.Rest = Rest.Set(target.Rest);
        target.Socket = Socket.Set(target.Socket);
        target.SharedApi = new SharedApiOptions { PreferredTransport = SharedApi.PreferredTransport };
    }

    /// <summary>
    /// Options with the environment and the credentials of the REST and socket sections cleared, so that what is set afterwards is
    /// what the caller set: their constructors would otherwise already have filled in the process-wide defaults, which
    /// <see cref="Normalize"/> must not mistake for a choice that beats the top level.
    /// </summary>
    private static BitvavoOptions CreateUnconfigured()
    {
        var options = new BitvavoOptions();
        options.Rest.Environment = null!;
        options.Rest.ApiCredentials = null;
        options.Socket.Environment = null!;
        options.Socket.ApiCredentials = null;
        return options;
    }

    /// <summary>
    /// Settles the environment and the credentials of each section, strongest first: what the section itself got, what the top
    /// level got, the process-wide default of the section (<c>SetDefaultOptions</c>) and, for the environment, the live one.
    /// </summary>
    private static BitvavoOptions Normalize(BitvavoOptions options)
    {
        if (options.Rest == null)
        {
            throw new ArgumentException("REST options cannot be null", nameof(options));
        }

        if (options.Socket == null)
        {
            throw new ArgumentException("Socket options cannot be null", nameof(options));
        }

        options.Rest.Environment ??= options.Environment ?? BitvavoRestOptions.Default.Environment ?? BitvavoEnvironment.Live;
        options.Rest.ApiCredentials ??= options.ApiCredentials ?? BitvavoRestOptions.Default.ApiCredentials;
        options.Socket.Environment ??= options.Environment ?? BitvavoSocketOptions.Default.Environment ?? BitvavoEnvironment.Live;
        options.Socket.ApiCredentials ??= options.ApiCredentials ?? BitvavoSocketOptions.Default.ApiCredentials;
        return options;
    }

    /// <summary>The environment a bound one stands for. A bound environment carries a name and no URLs, so an unknown name cannot be used.</summary>
    private static BitvavoEnvironment Resolve(BitvavoEnvironment bound)
        => BitvavoEnvironment.GetEnvironmentByName(bound.Name)
            ?? throw new InvalidOperationException(
                $"Unknown Bitvavo environment '{bound.Name}'. Known environments: {string.Join(", ", BitvavoEnvironment.All)}.");
}
