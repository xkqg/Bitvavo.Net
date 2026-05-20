// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Net.WebSockets;
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
using CryptoExchange.Net.SharedApis;
using CryptoExchange.Net.Sockets;
using CryptoExchange.Net.Sockets.Default;
using Microsoft.Extensions.Logging;

namespace Bitvavo.Net.Clients.SpotApi;

/// <inheritdoc cref="IBitvavoSocketClientSpotApi" />
internal sealed partial class BitvavoSocketClientSpotApi : SocketApiClient<BitvavoEnvironment, BitvavoAuthenticationProvider, BitvavoCredentials>, IBitvavoSocketClientSpotApi
{
    /// <inheritdoc />
    public new BitvavoSocketOptions ClientOptions => (BitvavoSocketOptions)base.ClientOptions;

    /// <inheritdoc />
    protected override ErrorMapping ErrorMapping => BitvavoErrors.SpotMapping;

    /// <inheritdoc />
    public IBitvavoSocketClientSpotApiExchangeData ExchangeData { get; }

    /// <inheritdoc />
    public IBitvavoSocketClientSpotApiAccount Account { get; }

    /// <inheritdoc />
    /// <remarks>
    /// The Shared-API surface is implemented directly on this class (see the
    /// <c>BitvavoSocketClientSpotApi.Shared.cs</c> facade-hosted partial), so the accessor
    /// returns <c>this</c>.
    /// </remarks>
    public IBitvavoSocketClientSpotApiShared SharedClient => this;

    internal BitvavoSocketClientSpotApi(ILogger logger, BitvavoSocketOptions options)
        : base(logger, options.Environment.SpotSocketPublicAddress, options, options.SpotOptions)
    {
        ExchangeData = new BitvavoSocketClientSpotApiExchangeData(logger, this);
        Account = new BitvavoSocketClientSpotApiAccount(logger, this);
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

    /// <inheritdoc />
    protected override Task<Query?> GetAuthenticationRequestAsync(SocketConnection connection)
    {
        var provider = (BitvavoAuthenticationProvider)AuthenticationProvider!;
        var payload = provider.BuildSocketAuth();
        return Task.FromResult<Query?>(new BitvavoSocketAuthQuery(payload));
    }

    /// <summary>
    /// Internal wrapper for the inherited protected
    /// <c>SocketApiClient.SubscribeAsync(string, Subscription, CancellationToken)</c> so peer
    /// classes (the *Data partials) can subscribe without being derived from this class.
    /// </summary>
    internal System.Threading.Tasks.Task<CryptoExchange.Net.Objects.CallResult<CryptoExchange.Net.Objects.Sockets.UpdateSubscription>> SubscribeInternalAsync(
        string url,
        Subscription subscription,
        CancellationToken ct)
        => SubscribeAsync(url, subscription, ct);
}
