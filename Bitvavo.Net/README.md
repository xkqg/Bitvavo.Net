# Bitvavo.Net

A high-performance .NET client library for the [Bitvavo](https://bitvavo.com) REST and WebSocket APIs, built on the [CryptoExchange.Net](https://github.com/JKorf/CryptoExchange.Net) base library.

This package is a community contribution, intended for adoption into the JKorf exchange-clients ecosystem alongside `Binance.Net`, `Kraken.Net`, `Bybit.Net`, etc. — same API patterns, same `WebCallResult<T>` discipline, same options shape, same DI extension surface.

## Status

`v0.3.0-preview`:

- ✅ Public REST: `GetMarketsAsync`, `GetKlinesAsync`, `GetServerTimeAsync`, `GetAssetsAsync`, `GetTickerPricesAsync`, `GetTickerBookAsync`, `GetTicker24hAsync`, `GetOrderBookAsync`, `GetPublicTradesAsync`
- ✅ Public WebSocket: candles + trades subscriptions
- ✅ Signed REST (HMAC-SHA256) — Account (info / balances / fees), Trading (place / update / get / cancel / batch-cancel / open / history / fills), Funding (deposit address + history, withdrawal history, **withdraw POST**)
- ✅ Authenticated WebSocket: private `account` channel — subscribe to order-state + fill events

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
    Console.WriteLine($"{m.Market}  status={m.Status}  pricePrecision={m.PricePrecision}");
}
```

Output:
```
ETH-EUR  status=trading  pricePrecision=5
BTC-EUR  status=trading  pricePrecision=2
...
```

## Quick start — WebSocket

```csharp
using var socketClient = new BitvavoSocketClient();
var sub = await socketClient.SpotApi.ExchangeData.SubscribeToKlineUpdatesAsync(
    "ETH-EUR", KlineInterval.OneMinute,
    update => Console.WriteLine($"{update.Data.Market}  close={update.Data.ClosePrice}"));
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

// Place a limit buy order
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
    new[] { "ETH-EUR" },
    evt => Console.WriteLine($"order {evt.Data.OrderId} → {evt.Data.Status}"));
```

The framework runs Bitvavo's HMAC-SHA256 auth handshake automatically before
dispatching the first authenticated subscription on the connection.

## Smoke testing

The `BitvavoSmoke` exe (in `Application/BitvavoSmoke`) exercises every layer of the
package out-of-the-box:

```bash
dotnet run --project Application/BitvavoSmoke                    # REST (markets + candles)
dotnet run --project Application/BitvavoSmoke -- --socket        # + public WebSocket subs
dotnet run --project Application/BitvavoSmoke -- --signed        # + signed REST: account / balances / order history
dotnet run --project Application/BitvavoSmoke -- --signed-ws     # + signed WebSocket: ETH-EUR private order/fill stream
```

The `--signed` and `--signed-ws` paths expect `BITVAVO_API_KEY` +
`BITVAVO_API_SECRET` environment variables. The API key only needs the `view`
capability for the smoke; trading + withdraw scopes are NOT exercised. **Never
check credentials into git.**

## Conventions

This library follows JKorf's exchange-client conventions exactly:

- **Result type**: every public method returns `Task<WebCallResult<T>>` (or `Task<CallResult<UpdateSubscription>>` for sockets) — never throws on protocol errors.
- **Options**: configure via `new BitvavoRestClient(opts => { opts.RequestTimeout = ...; })`.
- **DI**: register via `services.AddBitvavo()` — pooled `HttpClient`, transient REST client, singleton WebSocket client. Optional `Action<BitvavoRestOptions>` / `Action<BitvavoSocketOptions>` configurators.
- **Naming**: `Bitvavo*RestClient*SpotApi*ExchangeData/Trading/Account` mirrors `Binance.Net` etc.

## Bitvavo API

- Bitvavo REST docs: https://docs.bitvavo.com/
- Bitvavo WebSocket docs: https://docs.bitvavo.com/#tag/WebSocket-Channels
- Public REST base URL: `https://api.bitvavo.com`
- Public WebSocket: `wss://ws.bitvavo.com/v2/`
- Rate limit (public): 1000 weight / minute / IP

## Building

```bash
dotnet build Bitvavo.Net.csproj
dotnet test  Bitvavo.Net.Tests
```

## Contributing

Issues + PRs welcome. The eventual home for this package is `JKorf/Bitvavo.Net` — once the read-only surface is feature-complete and battle-tested, it will be submitted for adoption into JKorf's organisation.

## License

MIT
