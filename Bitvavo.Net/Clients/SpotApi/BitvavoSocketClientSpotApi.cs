// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Bitvavo.Net.Clients.MessageHandlers;
using Bitvavo.Net.Interfaces.Clients.SpotApi;
using Bitvavo.Net.Objects.Internal;
using Bitvavo.Net.Objects.Options;
using CryptoExchange.Net.Clients;
using CryptoExchange.Net.Converters.MessageParsing.DynamicConverters;
using CryptoExchange.Net.Converters.SystemTextJson;
using CryptoExchange.Net.Interfaces;
using CryptoExchange.Net.Objects.Errors;
using CryptoExchange.Net.Objects.Options;
using CryptoExchange.Net.SharedApis;
using CryptoExchange.Net.Sockets;
using CryptoExchange.Net.Sockets.Default;
using Microsoft.Extensions.Logging;

namespace Bitvavo.Net.Clients.SpotApi;

/// <inheritdoc cref="IBitvavoSocketClientSpotApi" />
/// <remarks>
/// The CryptoExchange.Net Shared API (V1 and V2 on one instance) lives in <see cref="BitvavoSocketClientSpotSharedApi"/>, which
/// this client creates and exposes as <see cref="SharedClient"/> and <see cref="SharedApi"/>.
/// </remarks>
internal sealed class BitvavoSocketClientSpotApi : SocketApiClient<BitvavoEnvironment, BitvavoAuthenticationProvider, BitvavoCredentials>, IBitvavoSocketClientSpotApi
{
    private readonly BitvavoSocketClientSpotSharedApi _sharedApi;

    /// <inheritdoc />
    public new BitvavoSocketOptions ClientOptions => (BitvavoSocketOptions)base.ClientOptions;

    /// <inheritdoc />
    protected override ErrorMapping ErrorMapping => BitvavoErrors.SpotMapping;

    /// <inheritdoc />
    public IBitvavoSocketClientSpotApiExchangeData ExchangeData { get; }

    /// <inheritdoc />
    public IBitvavoSocketClientSpotApiAccount Account { get; }

    /// <inheritdoc />
    public IBitvavoSocketClientSpotApiShared SharedClient => _sharedApi;

    /// <inheritdoc />
    public IBitvavoSocketClientSpotSharedApi SharedApi => _sharedApi;

    internal BitvavoSocketClientSpotApi(ILoggerFactory? loggerFactory, BitvavoSocketOptions options)
        : base(loggerFactory, BitvavoExchange.ExchangeName, options.Environment.SpotSocketPublicAddress, options, options.SpotOptions)
    {
        var logger = (loggerFactory ?? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance).CreateLogger(BitvavoExchange.ExchangeName);
        ExchangeData = new BitvavoSocketClientSpotApiExchangeData(logger, this);
        Account = new BitvavoSocketClientSpotApiAccount(logger, this);
        _sharedApi = new BitvavoSocketClientSpotSharedApi(this);
    }

    /// <inheritdoc />
    protected override IMessageSerializer CreateSerializer() =>
        new SystemTextJsonMessageSerializer(new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true });

    /// <inheritdoc />
    public override ISocketMessageHandler CreateMessageConverter(WebSocketMessageType messageType) =>
        new BitvavoSocketSpotMessageHandler();

    /// <inheritdoc />
    public override string FormatSymbol(string baseAsset, string quoteAsset, TradingMode tradingMode, System.DateTime? deliverDate = null)
        => BitvavoExchange.FormatSymbol(baseAsset, quoteAsset, tradingMode, deliverDate);

    /// <inheritdoc />
    protected override BitvavoAuthenticationProvider CreateAuthenticationProvider(BitvavoCredentials credentials)
        => new(credentials, ClientOptions.ReceiveWindowMs);

    private readonly object _credentialsGate = new();

    /// <summary>
    /// The signing provider, read under the lock that <see cref="SetApiCredentials"/> and <see cref="SetOptions"/> hold while they
    /// replace the key. CryptoExchange.Net 13.1.0 replaces the provider in several steps; a reader in between can leave the client
    /// on the OLD key until the next change (after a revocation every authentication then fails) or find no provider at all.
    /// </summary>
    public override BitvavoAuthenticationProvider? AuthenticationProvider
    {
        get
        {
            lock (_credentialsGate)
            {
                return base.AuthenticationProvider;
            }
        }
    }

    /// <inheritdoc />
    public override void SetApiCredentials(BitvavoCredentials credentials)
    {
        lock (_credentialsGate)
        {
            base.SetApiCredentials(credentials);
        }
    }

    /// <inheritdoc />
    public override void SetOptions(UpdateOptions<BitvavoCredentials> options)
    {
        lock (_credentialsGate)
        {
            base.SetOptions(options);
        }
    }

    /// <summary>
    /// The API key each connection was last authenticated with. The framework reuses an authenticated connection for the next
    /// private subscription and only checks that it is authenticated, not whom it authenticated as; this table lets
    /// <see cref="ConnectionCanBeUsedFor"/> ask. Weak, so a closed connection takes its entry with it.
    /// </summary>
    private readonly ConditionalWeakTable<SocketConnection, string> _authenticatedWithKey = new();

    /// <inheritdoc />
    protected override Task<Query?> GetAuthenticationRequestAsync(SocketConnection connection)
    {
        var provider = (BitvavoAuthenticationProvider)AuthenticationProvider!;
        var payload = provider.BuildSocketAuth(this);

        // Recorded as the frame is built: the connection then either authenticates with exactly this key or is closed by the
        // framework. A reconnect authenticates again through here, so the entry follows the key the connection really holds.
        _authenticatedWithKey.AddOrUpdate(connection, provider.Key);

        return Task.FromResult<Query?>(new BitvavoSocketAuthQuery(payload));
    }

    /// <summary>
    /// A private subscription may only ride a connection that was authenticated with the API key the client holds now. After
    /// <c>SetApiCredentials</c> with another key, the old key's connection stays open for the subscriptions it already carries
    /// and the next private subscription gets a connection of its own, authenticated with the new key. Public subscriptions
    /// are unaffected. A connection that reconnects authenticates again with the key the client holds then: right when the key of
    /// one account is rotated (its subscriptions keep flowing), so another account needs a client of its own.
    /// </summary>
    protected override bool ConnectionCanBeUsedFor(SocketConnection connection, string address, bool authenticated, string? topic = null)
        => base.ConnectionCanBeUsedFor(connection, address, authenticated, topic) && Serves(connection, authenticated);

    /// <summary>
    /// Whether <paramref name="connection"/> can carry a request: any connection carries one that needs no authentication; one
    /// that does needs a connection authenticated with the API key the client holds now (no key held, no connection serves it).
    /// </summary>
    internal bool Serves(SocketConnection connection, bool authenticated)
        => !authenticated || (AuthenticationProvider?.Key is { } current && _authenticatedWithKey.TryGetValue(connection, out var key) && key == current);

    /// <summary>
    /// Internal wrapper for the inherited protected
    /// <c>SocketApiClient.SubscribeAsync(string, Subscription, CancellationToken)</c> so peer
    /// classes (the *Data partials) can subscribe without being derived from this class.
    /// </summary>
    internal System.Threading.Tasks.Task<CryptoExchange.Net.Objects.WebSocketResult<CryptoExchange.Net.Objects.Sockets.UpdateSubscription>> SubscribeInternalAsync(
        string url,
        Subscription subscription,
        CancellationToken ct)
        => SubscribeAsync(url, subscription, ct);
}
