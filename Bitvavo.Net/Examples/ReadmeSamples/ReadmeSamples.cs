// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.
//
// The code samples of README.md, compiled (never run) so that the documentation cannot drift from the API.
// A sample that changes in the README changes here first; the build then checks it.

using Bitvavo.Net.Clients;
using Bitvavo.Net.Enums;
using Bitvavo.Net.Extensions;
using Bitvavo.Net.Interfaces.Clients;
using Bitvavo.Net.Interfaces.Clients.SpotApi;
using Bitvavo.Net.Objects.Internal;
using Bitvavo.Net.Objects.Models.Spot;
using Bitvavo.Net.Objects.Options;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.SharedApis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bitvavo.Net.Examples.ReadmeSamples;

internal static class ReadmeSamples
{
    public static async Task PublicRest()
    {
        using var client = new BitvavoRestClient();

        var markets = await client.SpotApi.ExchangeData.GetMarketsAsync();
        if (!markets.Success)
        {
            Console.WriteLine($"Failed: {markets.Error}");
            return;
        }

        foreach (var m in markets.Data.Take(5))
        {
            Console.WriteLine($"{m.Market}  status={m.Status}  tickSize={m.TickSize}");
        }
    }

    public static async Task PublicWebSocket()
    {
        using var socketClient = new BitvavoSocketClient();

        var sub = await socketClient.SpotApi.ExchangeData.SubscribeToKlineUpdatesAsync(
            "ETH-EUR",
            KlineInterval.OneMinute,
            update =>
            {
                var latest = update.Data.Candle.LastOrDefault();
                Console.WriteLine($"{update.Data.Market}  close={latest?.ClosePrice}");
            });
    }

    public static async Task SignedRest(string apiKey, string apiSecret)
    {
        using var client = new BitvavoRestClient(opts =>
            opts.ApiCredentials = new BitvavoCredentials(apiKey, apiSecret));

        // Account info + balances
        var info = await client.SpotApi.Account.GetAccountInfoAsync();
        var balances = await client.SpotApi.Account.GetBalancesAsync();

        // Place a limit buy order — Bitvavo requires an operator id on every order operation
        var order = await client.SpotApi.Trading.PlaceOrderAsync(
            new BitvavoPlaceOrderRequest(
                "ETH-EUR", OrderSide.Buy, OrderType.Limit, OperatorId: 1,
                Amount: 0.5m, Price: 1500m, TimeInForce: TimeInForce.GoodTillCanceled));
        if (!order.Success)
        {
            Console.WriteLine($"Failed: {order.Error}");
        }
    }

    public static async Task AuthenticatedWebSocket(string apiKey, string apiSecret)
    {
        using var client = new BitvavoSocketClient(opts =>
            opts.ApiCredentials = new BitvavoCredentials(apiKey, apiSecret));

        var sub = await client.SpotApi.Account.SubscribeToOrderUpdatesAsync(
            ["ETH-EUR"],
            evt => Console.WriteLine($"order {evt.Data.OrderId} → {evt.Data.Status}"));
    }

    public static void DependencyInjection(IServiceCollection services, IConfiguration configuration)
    {
        // Defaults: REST client per resolution on a typed HttpClient, WebSocket client as a singleton
        services.AddBitvavo();

        // From configuration, for example an appsettings.json section "Bitvavo"
        services.AddBitvavo(configuration.GetSection("Bitvavo"));

        // From code, for the whole library at once
        services.AddBitvavo((BitvavoOptions o) =>
        {
            o.ApiCredentials = new BitvavoCredentials("apiKey", "apiSecret");
            o.Rest.RequestTimeout = TimeSpan.FromSeconds(10);
            o.SharedApi.PreferredTransport = SharedTransport.Socket;
        });

        // The application's own PostConfigure runs after all of them and wins
        services.PostConfigure<BitvavoRestOptions>(o => o.RateLimitingBehaviour = RateLimitingBehaviour.Fail);
    }

    public static async Task Injected(IBitvavoRestClient rest, IBitvavoSocketClient socket)
    {
        var time = await rest.SpotApi.ExchangeData.GetServerTimeAsync();
        var sub = await socket.SpotApi.ExchangeData.SubscribeToTradeUpdatesAsync(
            ["BTC-EUR", "ETH-EUR"],
            update => Console.WriteLine($"{update.Data.Market} {update.Data.Price}"));
    }

    public static void RateLimiter()
    {
        // One budget per account (signed requests) and one per IP address (public requests); the limiter keeps 10 % of each free
        BitvavoExchange.RateLimiter = new BitvavoRateLimiters(maxUtilization: 0.8);
        BitvavoExchange.RateLimiter.RateLimitTriggered += trigger => Console.WriteLine(trigger);
    }

    public static async Task SharedApi(IBitvavoSharedApiClient shared)
    {
        // What does Bitvavo offer, over which transport?
        var info = shared.Discover();

        // Ask for a capability; the transport preference decides when REST and the WebSocket could both answer
        var klines = shared.GetCapability<IGetKlines>();
        if (klines is null)
        {
            return;
        }

        var result = await klines.Capability.GetKlinesAsync(
            new GetKlinesRequest(new SharedSymbol(TradingMode.Spot, "ETH", "EUR"), SharedKlineInterval.OneHour, limit: 24));
        if (result.Success)
        {
            foreach (var kline in result.Data)
            {
                Console.WriteLine($"{kline.OpenTime:g}  close={kline.ClosePrice}");
            }
        }
    }

    public static async Task SharedOrder(IBitvavoRestClientSpotSharedApi api)
    {
        // The Shared request has no field for Bitvavo's operator id: it travels as an exchange parameter
        var result = await api.PlaceSpotOrderAsync(new PlaceSpotOrderRequest(
            new SharedSymbol(TradingMode.Spot, "ETH", "EUR"),
            SharedOrderSide.Buy,
            SharedOrderType.Limit,
            SharedQuantity.Base(0.5m),
            price: 1500m,
            timeInForce: SharedTimeInForce.GoodTillCanceled,
            clientOrderId: api.GenerateClientOrderId(),
            exchangeParameters: new ExchangeParameters(new ExchangeParameter("Bitvavo", "OperatorId", 1L))));
    }

    public static void ChangeCredentials(IBitvavoSocketClient socket)
    {
        // Subscriptions made after this authenticate with the new key, on a connection of their own
        socket.SetApiCredentials(new BitvavoCredentials("otherKey", "otherSecret"));
    }
}
