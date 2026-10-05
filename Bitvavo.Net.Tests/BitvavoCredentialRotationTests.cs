// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bitvavo.Net.Clients.SpotApi;
using Bitvavo.Net.Interfaces.Clients;
using Bitvavo.Net.Objects.Internal;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests;

/// <summary>
/// <c>SetApiCredentials</c> on live clients. REST signs each request with the key the client holds when it sends. A WebSocket
/// connection is authenticated once, with one key, and the framework reuses an authenticated connection for the next private
/// subscription without asking which key authenticated it: after a key change a new private subscription must not ride the
/// old key's connection, or it would stream the old account's events.
/// </summary>
public class BitvavoCredentialRotationTests
{
    /// <summary>The credentials are changed through the interface a container hands out, as on every CryptoExchange.Net client.</summary>
    [Fact]
    public async Task The_rest_client_interface_changes_the_signing_key()
    {
        var handler = new StubHttpMessageHandler("[]");
        IBitvavoRestClient client = handler.RestClient(withCredentials: false);

        client.SetApiCredentials(new BitvavoCredentials("key-i", "secret-i"));
        await client.SpotApi.Account.GetBalancesAsync(ct: TestContext.Current.CancellationToken);

        handler.Requests.ShouldHaveSingleItem().Headers.GetValues("Bitvavo-Access-Key").ShouldBe(["key-i"]);
    }

    [Fact]
    public async Task The_socket_client_interface_changes_the_key_of_new_private_subscriptions()
    {
        var (client, factory) = RecordingSocketFactory.Create("key-a");
        IBitvavoSocketClient socketClient = client;

        await socketClient.SpotApi.Account.SubscribeToOrderUpdatesAsync(["BTC-EUR"], _ => { }, TestContext.Current.CancellationToken);
        socketClient.SetApiCredentials(new BitvavoCredentials("key-b", "secret-b"));
        await socketClient.SpotApi.Account.SubscribeToOrderUpdatesAsync(["ETH-EUR"], _ => { }, TestContext.Current.CancellationToken);

        factory.Connections.Select(c => c.AuthenticatedKeys.Single()).ShouldBe(["key-a", "key-b"]);
    }

    [Fact]
    public async Task SetApiCredentials_signs_the_next_rest_request_with_the_new_key()
    {
        var handler = new StubHttpMessageHandler("[]");
        var client = handler.RestClient();

        await client.SpotApi.Account.GetBalancesAsync(ct: TestContext.Current.CancellationToken);
        client.SetApiCredentials(new BitvavoCredentials("key-b", "secret-b"));
        await client.SpotApi.Account.GetBalancesAsync(ct: TestContext.Current.CancellationToken);

        handler.Requests.Select(r => r.Headers.GetValues("Bitvavo-Access-Key").Single()).ShouldBe(["test-key", "key-b"]);
    }

    [Fact]
    public async Task SetApiCredentials_new_private_subscription_authenticates_with_the_new_key()
    {
        var (client, factory) = RecordingSocketFactory.Create("key-a");

        var first = await client.SpotApi.Account.SubscribeToOrderUpdatesAsync(["BTC-EUR"], _ => { }, TestContext.Current.CancellationToken);
        client.SetApiCredentials(new BitvavoCredentials("key-b", "secret-b"));
        var second = await client.SpotApi.Account.SubscribeToOrderUpdatesAsync(["ETH-EUR"], _ => { }, TestContext.Current.CancellationToken);

        first.Success.ShouldBeTrue();
        second.Success.ShouldBeTrue();
        factory.Connections.Count.ShouldBe(2, "the new key needs a connection of its own");
        factory.Connections[0].AuthenticatedKeys.ShouldBe(["key-a"]);
        factory.Connections[1].AuthenticatedKeys.ShouldBe(["key-b"]);
    }

    [Fact]
    public async Task Setting_the_same_key_again_keeps_the_authenticated_connection()
    {
        var (client, factory) = RecordingSocketFactory.Create("key-a");

        await client.SpotApi.Account.SubscribeToOrderUpdatesAsync(["BTC-EUR"], _ => { }, TestContext.Current.CancellationToken);
        client.SetApiCredentials(new BitvavoCredentials("key-a", "secret-key-a"));
        var second = await client.SpotApi.Account.SubscribeToOrderUpdatesAsync(["ETH-EUR"], _ => { }, TestContext.Current.CancellationToken);

        second.Success.ShouldBeTrue();
        factory.Connections.Count.ShouldBe(1);
        factory.Connections[0].AuthenticatedKeys.ShouldBe(["key-a"]);
    }

    [Fact]
    public async Task A_public_subscription_after_a_key_change_reuses_the_open_connection()
    {
        var (client, factory) = RecordingSocketFactory.Create("key-a");

        await client.SpotApi.Account.SubscribeToOrderUpdatesAsync(["BTC-EUR"], _ => { }, TestContext.Current.CancellationToken);
        client.SetApiCredentials(new BitvavoCredentials("key-b", "secret-b"));
        var publicSubscription = await client.SpotApi.ExchangeData.SubscribeToTradeUpdatesAsync(["ETH-EUR"], _ => { }, TestContext.Current.CancellationToken);

        publicSubscription.Success.ShouldBeTrue();
        factory.Connections.Count.ShouldBe(1);
    }

    /// <summary>What a connection can carry, asked directly: everything unauthenticated, an authenticated request only for the key it holds.</summary>
    [Fact]
    public async Task A_connection_serves_an_authenticated_request_only_for_the_key_it_holds()
    {
        var (client, factory) = RecordingSocketFactory.Create("key-a");
        await client.SpotApi.Account.SubscribeToOrderUpdatesAsync(["BTC-EUR"], _ => { }, TestContext.Current.CancellationToken);
        var api = (BitvavoSocketClientSpotApi)client.SpotApi;
        var connection = factory.Connections.ShouldHaveSingleItem().Socket.Connection.ShouldNotBeNull();

        api.Serves(connection, authenticated: true).ShouldBeTrue();
        api.Serves(connection, authenticated: false).ShouldBeTrue();

        client.SetApiCredentials(new BitvavoCredentials("key-b", "secret-b"));

        api.Serves(connection, authenticated: true).ShouldBeFalse();
        api.Serves(connection, authenticated: false).ShouldBeTrue();
    }

    [Fact]
    public async Task Without_credentials_no_connection_serves_an_authenticated_request()
    {
        var (client, factory) = RecordingSocketFactory.Create();
        await client.SpotApi.ExchangeData.SubscribeToTradeUpdatesAsync(["ETH-EUR"], _ => { }, TestContext.Current.CancellationToken);
        var api = (BitvavoSocketClientSpotApi)client.SpotApi;
        var connection = factory.Connections.ShouldHaveSingleItem().Socket.Connection.ShouldNotBeNull();

        api.Serves(connection, authenticated: true).ShouldBeFalse();
        api.Serves(connection, authenticated: false).ShouldBeTrue();
    }

    /// <summary>A connection that was never authenticated has no key: a private subscription cannot ride it, whichever key is set.</summary>
    [Fact]
    public async Task A_private_subscription_does_not_ride_a_public_connection()
    {
        var (client, factory) = RecordingSocketFactory.Create("key-a");

        await client.SpotApi.ExchangeData.SubscribeToTradeUpdatesAsync(["ETH-EUR"], _ => { }, TestContext.Current.CancellationToken);
        var privateSubscription = await client.SpotApi.Account.SubscribeToOrderUpdatesAsync(["BTC-EUR"], _ => { }, TestContext.Current.CancellationToken);

        privateSubscription.Success.ShouldBeTrue();
        factory.Connections.Count.ShouldBe(2);
        factory.Connections[0].AuthenticatedKeys.ShouldBeEmpty();
        factory.Connections[1].AuthenticatedKeys.ShouldBe(["key-a"]);
    }

    /// <summary>
    /// A connection that reconnects authenticates again with the key the client holds then — the right outcome when the key of one
    /// account is rotated (the subscriptions it carries keep flowing); a different account needs a client of its own.
    /// </summary>
    [Fact]
    public async Task A_reconnect_authenticates_again_with_the_key_the_client_holds_then()
    {
        var (client, factory) = RecordingSocketFactory.Create("key-a");
        var api = (BitvavoSocketClientSpotApi)client.SpotApi;
        await client.SpotApi.Account.SubscribeToOrderUpdatesAsync(["BTC-EUR"], _ => { }, TestContext.Current.CancellationToken);
        var connection = factory.Connections.ShouldHaveSingleItem();

        client.SetApiCredentials(new BitvavoCredentials("key-b", "secret-b"));
        api.Serves(connection.Socket.Connection.ShouldNotBeNull(), authenticated: true).ShouldBeFalse("until it reconnects the connection still holds key-a");
        await connection.Socket.ReconnectAsync();
        await WaitUntilAsync(() => connection.AuthenticatedKeys.Count() == 2);

        connection.AuthenticatedKeys.ShouldBe(["key-a", "key-b"]);
        api.Serves(connection.Socket.Connection.ShouldNotBeNull(), authenticated: true).ShouldBeTrue("the table follows the key the connection really holds");
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        condition().ShouldBeTrue("the condition did not come true within 5 seconds");
    }

    private const int KeyChanges = 100_000;

    /// <summary>What a reader that polls the signing provider while the key changes saw: the provider of an earlier key, or none.</summary>
    private readonly record struct KeyChangeOutcome(int StaleProviders, int MissingProviders);

    /// <summary>
    /// Changes the key <see cref="KeyChanges"/> times while another thread reads the provider as fast as it can, and counts the
    /// reads that found no provider and the changes after which the provider was not the one of the new key.
    /// </summary>
    private static async Task<KeyChangeOutcome> ChangeTheKeyUnderLoadAsync(Func<BitvavoAuthenticationProvider?> readProvider, Action<BitvavoCredentials> change)
    {
        var ct = TestContext.Current.CancellationToken;
        var stale = 0;
        var missing = 0;
        for (var i = 1; i <= KeyChanges; i++)
        {
            using var stop = new CancellationTokenSource();
            using var reading = new ManualResetEventSlim();
            var reader = Task.Run(
                () =>
                {
                    while (!stop.IsCancellationRequested)
                    {
                        if (readProvider() == null)
                        {
                            Interlocked.Increment(ref missing);
                        }

                        reading.Set();
                    }
                },
                ct);
            reading.Wait(ct);

            change(new BitvavoCredentials($"key-{i}", "secret"));

            await stop.CancelAsync();
            await reader;
            if (readProvider()?.Key != $"key-{i}")
            {
                stale++;
            }
        }

        return new KeyChangeOutcome(stale, missing);
    }

    /// <summary>
    /// CryptoExchange.Net 13.1.0 replaces the key in several steps (drop the signing provider, mark it unbuilt, store the new
    /// credentials). A reader that gets in between can leave the client on the OLD key until the next change — after a revocation
    /// every request then fails — or find no provider at all. Here the change is atomic for every reader.
    /// </summary>
    [Fact]
    public async Task Changing_the_key_under_a_concurrent_reader_never_leaves_the_socket_api_on_an_old_or_missing_provider()
    {
        var (client, _) = RecordingSocketFactory.Create("key-0");
        var api = (BitvavoSocketClientSpotApi)client.SpotApi;

        var outcome = await ChangeTheKeyUnderLoadAsync(() => api.AuthenticationProvider, client.SetApiCredentials);

        outcome.ShouldBe(new KeyChangeOutcome(0, 0));
    }

    [Fact]
    public async Task Changing_the_key_under_a_concurrent_reader_never_leaves_the_rest_api_on_an_old_or_missing_provider()
    {
        var client = new StubHttpMessageHandler("[]").RestClient();
        var api = (BitvavoRestClientSpotApi)client.SpotApi;

        var outcome = await ChangeTheKeyUnderLoadAsync(() => api.AuthenticationProvider, client.SetApiCredentials);

        outcome.ShouldBe(new KeyChangeOutcome(0, 0));
    }
}
