# Bitvavo.Net

[![NuGet version](https://img.shields.io/nuget/v/Bitvavo.Net?style=for-the-badge)](https://www.nuget.org/packages/Bitvavo.Net) [![NuGet downloads](https://img.shields.io/nuget/dt/Bitvavo.Net?style=for-the-badge)](https://www.nuget.org/packages/Bitvavo.Net) ![License](https://img.shields.io/badge/license-MIT-blue?style=for-the-badge)

> The NuGet badges show "not found" until the first package publish — that is expected for this pre-release.

A .NET client library for the [Bitvavo](https://bitvavo.com) REST and WebSocket APIs, built on the [CryptoExchange.Net](https://github.com/JKorf/CryptoExchange.Net) base library.

This package is a community contribution, intended for adoption into the JKorf exchange-clients ecosystem alongside `Binance.Net`, `Kraken.Net`, `Bybit.Net`, etc. — same API patterns, same `HttpResult<T>` result discipline, same options shape, same DI extension surface, same Shared API. It loads next to the other CryptoExchange.Net clients in one process.

The package targets `net8.0;net9.0;net10.0` and is built on `CryptoExchange.Net 13.1.0`.

For information on the clients, dependency injection, response processing, and the shared interfaces, see the [CryptoExchange.Net documentation](https://cryptoexchange.jkorf.dev). How this library is put together is described in [ARCHITECTURE.md](ARCHITECTURE.md).

## Status

`v0.5.0` (pre-1.0: the API can still change between minor versions — see the [CHANGELOG](CHANGELOG.md)):

- ✅ Public REST: markets, candles, server time, assets, ticker prices, top of book, 24 h ticker, order book, public trades, and the MiCA report endpoints
- ✅ Public WebSocket: candle and trade subscriptions
- ✅ Signed REST (HMAC-SHA256) — Account (info, balances, fees, staking balance, transaction history, cancel-on-disconnect), Trading (place / update / get / cancel / atomic cancel / bulk cancel / open orders / history / own trades), Funding (deposit address and history, withdrawal history, withdraw, crypto withdrawal), Institutional (sub-accounts)
- ✅ Authenticated WebSocket: the private `account` channel — order-state and fill events
- ✅ The CryptoExchange.Net Shared API, version 2 (32 REST capabilities, 4 WebSocket) and the legacy version 1 interfaces
- ✅ Client-side rate limiting that follows Bitvavo's two weight budgets

## Supported features

### Spot REST
|API|Supported|Location|
|--|--:|--|
|Public market data|✅|`restClient.SpotApi.ExchangeData`|
|Account|✅|`restClient.SpotApi.Account`|
|Trading|✅|`restClient.SpotApi.Trading`|
|Funding|✅|`restClient.SpotApi.Funding`|
|Report (MiCA)|✅|`restClient.SpotApi.Report`|
|Institutional / subaccounts|✅|`restClient.SpotApi.Institutional`|

### Spot WebSocket
|API|Supported|Location|
|--|--:|--|
|Public streams (candles, trades)|✅|`socketClient.SpotApi.ExchangeData`|
|Private account channel (orders, fills)|✅|`socketClient.SpotApi.Account`|

### CryptoExchange.Net Shared API
|API|Supported|Location|
|--|--:|--|
|Shared API v2, REST|✅|`restClient.SpotApi.SharedApi`|
|Shared API v2, WebSocket|✅|`socketClient.SpotApi.SharedApi`|
|Shared API v1 (legacy interfaces)|✅|`restClient.SpotApi.SharedClient`, `socketClient.SpotApi.SharedClient`|
|Both transports as one client|✅|`IBitvavoSharedApiClient` (from the container)|

Bitvavo is driven through the same exchange-agnostic capabilities as every other CryptoExchange.Net client: assets, klines, symbols, tickers, order book, recent trades, balances, fees, ledger, deposits, withdrawals, spot orders (place, get, open, closed, cancel, edit, cancel all), the trades of an order, the user's trade history, and trigger orders (stop loss, take profit). `Discover()` lists what is registered. Bitvavo has no batch-order endpoint, so that capability is absent.

## Install

```bash
dotnet add package Bitvavo.Net --prerelease
```

## Quick start — REST

```csharp
using Bitvavo.Net.Clients;

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
```

## Quick start — WebSocket

```csharp
using var socketClient = new BitvavoSocketClient();

var sub = await socketClient.SpotApi.ExchangeData.SubscribeToKlineUpdatesAsync(
    "ETH-EUR",
    KlineInterval.OneMinute,
    update =>
    {
        var latest = update.Data.Candle.LastOrDefault();
        Console.WriteLine($"{update.Data.Market}  close={latest?.ClosePrice}");
    });
```

## Quick start — signed REST

```csharp
using Bitvavo.Net;
using Bitvavo.Net.Clients;
using Bitvavo.Net.Enums;
using Bitvavo.Net.Objects.Models.Spot;

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
```

## Quick start — authenticated WebSocket

```csharp
using var client = new BitvavoSocketClient(opts =>
    opts.ApiCredentials = new BitvavoCredentials(apiKey, apiSecret));

var sub = await client.SpotApi.Account.SubscribeToOrderUpdatesAsync(
    ["ETH-EUR"],
    evt => Console.WriteLine($"order {evt.Data.OrderId} → {evt.Data.Status}"));
```

The framework runs Bitvavo's HMAC-SHA256 `authenticate` handshake before the first private subscription on a connection. Bitvavo does not acknowledge a subscribe individually, so a subscribe counts as successful once it is sent; cancelling the token you pass in closes the subscription. To switch to another API key, call `SetApiCredentials`: subscriptions made afterwards authenticate with the new key, on a connection of their own; a connection that reconnects authenticates again with the key held at that moment, so this is meant for rotating the key of one account (another account needs a client of its own).

## Quick start — dependency injection

```csharp
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
```

`AddBitvavo()` still accepts the optional `Action<BitvavoRestOptions>` and `Action<BitvavoSocketOptions>` configurators of earlier versions. After registration, `IBitvavoRestClient`, `IBitvavoSocketClient`, `IBitvavoSharedApiClient` and every Shared capability interface can be injected. The clients register once: a client you registered earlier stays the one that resolves, and a second `AddBitvavo` only adds its options.

## Quick start — Shared API

```csharp
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
```

The Shared requests have no field for some Bitvavo specifics, which travel as exchange parameters: `OperatorId` on every order operation, `Markets` on the private WebSocket subscriptions, and an optional `TriggerReference` on trigger orders:

```csharp
var result = await api.PlaceSpotOrderAsync(new PlaceSpotOrderRequest(
    new SharedSymbol(TradingMode.Spot, "ETH", "EUR"),
    SharedOrderSide.Buy,
    SharedOrderType.Limit,
    SharedQuantity.Base(0.5m),
    price: 1500m,
    timeInForce: SharedTimeInForce.GoodTillCanceled,
    clientOrderId: api.GenerateClientOrderId(),
    exchangeParameters: new ExchangeParameters(new ExchangeParameter("Bitvavo", "OperatorId", 1L))));
```

## Rate limiting

Bitvavo gives every account 1000 weight points per minute for signed requests and every IP address 1000 for public ones — two separate budgets — and blocks the caller for one minute (signed) or fifteen minutes (IP) when one is overrun. The client counts every request against both budgets and keeps 10 % of each free by default; a request that would overrun waits, or fails if `RateLimitingBehaviour` is `Fail`. The limiter is one process-wide object:

```csharp
BitvavoExchange.RateLimiter = new BitvavoRateLimiters(maxUtilization: 0.8);
BitvavoExchange.RateLimiter.RateLimitTriggered += trigger => Console.WriteLine(trigger);
```

Set it at start-up; a replacement starts counting from zero. The reasoning is in [ARCHITECTURE.md](ARCHITECTURE.md).

## Good to know

- Every order operation (place, update, cancel, cancel all) needs an `operatorId`.
- A `clientOrderId` must be a UUID.
- Bitvavo reports `pricePrecision` as null on every market; the price grid is `tickSize`.
- Fee rates are fractions (`0.0015` is 0.15 %); the Shared API reports percentages.
- Prices and amounts travel as JSON strings; `decimal` properties parse them.
- `AutoTimestamp` asks Bitvavo's public `GET /v2/time` for the clock offset before the first signed request.
- Error codes are mapped onto CryptoExchange.Net's `ErrorType`. Code 109 (a timed-out request) is not marked transient: the operation may have happened, so read the order back before retrying.

## Examples

- [`Bitvavo.Net/Examples/QuickStart`](Bitvavo.Net/Examples/QuickStart) — a runnable program: DI, one public REST call, one public WebSocket subscription.
- [`Bitvavo.Net/Examples/ReadmeSamples`](Bitvavo.Net/Examples/ReadmeSamples) — the code samples of this README, compiled with the solution so they cannot drift from the API.

## Conventions

This library follows JKorf's exchange-client conventions:

- **Result type**: every REST method returns `Task<HttpResult<T>>` and every subscription `Task<WebSocketResult<UpdateSubscription>>` — protocol errors are results, never exceptions.
- **Options**: configure via `new BitvavoRestClient(opts => { opts.RequestTimeout = ...; })`, or register with `services.AddBitvavo(...)`.
- **DI**: `services.AddBitvavo()` — typed `HttpClient`, transient REST client, singleton WebSocket client unless `SocketClientLifeTime` says otherwise.
- **Naming**: `Bitvavo*RestClient*SpotApi*ExchangeData/Trading/Account` mirrors `Binance.Net` etc.

## Bitvavo API

- Bitvavo REST docs: https://docs.bitvavo.com/
- Public REST base URL: `https://api.bitvavo.com`
- WebSocket: `wss://ws.bitvavo.com/v2/`
- Rate limit: 1000 weight points per minute, per account (signed requests) and per IP address (public requests)

## Building

```bash
dotnet build Bitvavo.Net.slnx
dotnet run --project Bitvavo.Net.Tests --framework net10.0   # also net8.0 and net9.0
```

The test project is an xunit v3 executable: `dotnet run` runs the tests, `dotnet test` does not.

## Contributing

Issues and PRs welcome — see [CONTRIBUTING.md](CONTRIBUTING.md). The eventual home for this package is `JKorf/Bitvavo.Net` — once the surface is feature-complete and battle-tested, it will be submitted for adoption into JKorf's organisation.

## License

MIT
