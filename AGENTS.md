---
name: bitvavo-net
description: Use Bitvavo.Net when generating C#/.NET code for the Bitvavo cryptocurrency exchange (spot only) - public market data, signed REST for account, trading, funding, MiCA reports and institutional subaccounts, WebSocket candles and trades, the private order and fill stream, dependency injection, or CryptoExchange.Net Shared APIs. Also holds the rules for contributing to this repository. Triggers on Bitvavo integration requests in C#, .NET, dotnet, F# or VB.NET.
---

# Bitvavo.Net

.NET client for the Bitvavo spot REST and WebSocket APIs. Built on CryptoExchange.Net 13.1.0.
Package id: `Bitvavo.Net`. Repository version: 0.5.0 (pre-1.0, the API may change in a minor release).
Targets: net8.0, net9.0, net10.0.
Bitvavo is spot only. No futures, no margin, no testnet.

Part 1 = using the library. Part 2 = contributing to it.

---

# Part 1 - Using the library

## 1. Quick decision

- Bitvavo from C#/.NET -> use Bitvavo.Net.
- Never call Bitvavo with raw `HttpClient` or `ClientWebSocket`. That loses HMAC signing, client-side rate limiting, reconnection and typed errors.
- Exchange-agnostic code -> CryptoExchange.Net Shared API (section 10).
- Never invent member names. Public surface = `Bitvavo.Net/Interfaces/Clients/**`.
- Intent -> exact member, HTTP verb and path, auth, rate-limit weight: `docs/ai-api-map.md`.
- Compilable samples: `Bitvavo.Net/Examples/ReadmeSamples/ReadmeSamples.cs`, `Bitvavo.Net/Examples/QuickStart/Program.cs`.

## 2. Install

```bash
dotnet add package Bitvavo.Net --prerelease
```

The README uses this form: the package is pre-1.0.

## 3. Namespaces

| Namespace | Holds |
|--|--|
| `Bitvavo.Net` | `BitvavoCredentials`, `BitvavoEnvironment` |
| `Bitvavo.Net.Clients` | `BitvavoRestClient`, `BitvavoSocketClient`, `BitvavoSharedApiClient` |
| `Bitvavo.Net.Interfaces.Clients` | `IBitvavoRestClient`, `IBitvavoSocketClient`, `IBitvavoSharedApiClient` |
| `Bitvavo.Net.Interfaces.Clients.SpotApi` | sub-client interfaces, for example `IBitvavoRestClientSpotApiTrading` |
| `Bitvavo.Net.Extensions` | `AddBitvavo` |
| `Bitvavo.Net.Enums` | `OrderSide`, `OrderType`, `TimeInForce`, `KlineInterval`, `OrderStatus`, `TriggerReference` |
| `Bitvavo.Net.Objects.Models.Spot` | REST models and request records, for example `BitvavoPlaceOrderRequest` |
| `Bitvavo.Net.Objects.Models.Spot.Streams` | WebSocket event models |
| `Bitvavo.Net.Objects.Options` | `BitvavoOptions`, `BitvavoRestOptions`, `BitvavoSocketOptions` |
| `Bitvavo.Net.Objects.Internal` | `BitvavoExchange`, `BitvavoRateLimiters` (public, despite the namespace) |
| `CryptoExchange.Net.Authentication` | `HMACCredential` |

## 4. Client setup

Public data, no credentials:

```csharp
using Bitvavo.Net.Clients;

using var client = new BitvavoRestClient();
```

Signed:

```csharp
using Bitvavo.Net;
using Bitvavo.Net.Clients;

using var client = new BitvavoRestClient(options =>
    options.ApiCredentials = new BitvavoCredentials(apiKey, apiSecret));
```

- `BitvavoCredentials(apiKey, apiSecret)` or `BitvavoCredentials(new HMACCredential(key, secret))`. One HMAC credential (`BitvavoCredentials.Spot`), because Bitvavo is spot only.
- Same shape for sockets: `new BitvavoSocketClient(options => ...)` with `BitvavoSocketOptions`.
- Change the key later: `SetApiCredentials(new BitvavoCredentials(...))` on a client or its interface.
- Process-wide defaults: `BitvavoRestClient.SetDefaultOptions(...)`, `BitvavoSocketClient.SetDefaultOptions(...)`.
- Read credentials from a secret store or environment. Never hardcode, never commit.
- API key capabilities are separate: `view` (reads), trading, withdraw. Use the least privilege the code needs.
- Environment: `BitvavoEnvironment.Live` only. REST `https://api.bitvavo.com`, WebSocket `wss://ws.bitvavo.com/v2/`.
- Options worth knowing: `RequestTimeout`, `ReceiveWindowMs` (signed-request window, default 10000, Bitvavo maximum 60000), `AutoTimestamp` (clock offset from public `GET /v2/time`), `RateLimitingBehaviour` (`Wait` or `Fail`), `RateLimitAdmission`.
- Clock drift beyond the window fails with `ErrorType.InvalidTimestamp`: raise `ReceiveWindowMs` or enable `AutoTimestamp`.
- Reuse clients. Dispose standalone clients (`using var`). With DI the container owns them.

## 5. Results and errors

REST returns `Task<HttpResult<T>>` (or `HttpResult`). Subscriptions return `Task<WebSocketResult<UpdateSubscription>>`.
Check `.Success` before `.Data`. `.Error` carries `ErrorType`, `ErrorCode`, `Message`, `IsTransient`.
Protocol errors are results, never exceptions.

```csharp
var prices = await client.SpotApi.ExchangeData.GetTickerPricesAsync("ETH-EUR");
if (!prices.Success)
{
    Console.WriteLine($"Failed: {prices.Error}");
    return;
}

var price = prices.Data.First().Price;
```

- Bitvavo's 90 documented error codes map onto CryptoExchange.Net `ErrorType` (`InsufficientBalance`, `UnknownOrder`, `InvalidPrice`, `InvalidQuantity`, `DuplicateClientOrderId`, `UnavailableSymbol`, `InvalidTimestamp`, `RateLimitRequest`, ...). Branch on `ErrorType`, not on message text. The raw Bitvavo code is `ErrorCode`.
- Error 109 (503, request timed out) maps to `ErrorType.Timeout` with `IsTransient == false`: the operation may or may not have happened. Read the order back by `clientOrderId` before retrying.
- Error 429 is an HTTP 400 about too many decimal places. It is not a rate limit. Rate limits are 105 and 112 (`ErrorType.RateLimitRequest`).
- HTTP 429 -> `ServerRateLimitError`. A request refused by the client-side limiter (`RateLimitingBehaviour.Fail`) -> `ClientRateLimitError`.

## 6. API surface

```csharp
client.SpotApi.ExchangeData   // GetMarketsAsync, GetKlinesAsync, GetServerTimeAsync, GetAssetsAsync, GetTickerPricesAsync,
                              // GetTickerBookAsync, GetTicker24hAsync, GetOrderBookAsync, GetPublicTradesAsync   (public)
client.SpotApi.Account        // GetAccountInfoAsync, GetBalancesAsync, GetTradingFeesAsync, GetStakingBalanceAsync,
                              // GetTransactionHistoryAsync, ResetCancelOnDisconnectAsync
client.SpotApi.Trading        // PlaceOrderAsync, UpdateOrderAsync, GetOrderAsync, CancelOrderAsync, CancelOrdersAsync,
                              // CancelOrdersAtomicAsync, GetOpenOrdersAsync, GetOrderHistoryAsync, GetUserTradesAsync
client.SpotApi.Funding        // GetDepositAddressAsync, GetDepositHistoryAsync, GetWithdrawalHistoryAsync,
                              // WithdrawAsync, WithdrawCryptoAsync
client.SpotApi.Report         // GetTradesReportAsync, GetBookReportAsync   (MiCA reports, public, sent unsigned)
client.SpotApi.Institutional  // subaccounts, transfers, per-subaccount balances/history/open orders/cancels
client.SpotApi.SharedApi      // CryptoExchange.Net Shared API v2 (REST)
client.SpotApi.SharedClient   // legacy v1 Shared interfaces, same object

socketClient.SpotApi.ExchangeData  // SubscribeToKlineUpdatesAsync, SubscribeToTradeUpdatesAsync (one market or many)
socketClient.SpotApi.Account       // SubscribeToOrderUpdatesAsync, SubscribeToFillUpdatesAsync   (authenticated)
socketClient.SpotApi.SharedApi     // Shared API v2 (WebSocket)
```

- Institutional: main-account key with the permissions "Include all subaccounts", "Internal Transfer", "Administrative".
- Cancel-on-disconnect: tag orders with `BitvavoPlaceOrderRequest.CodGroupId` (1-1000), then keep calling `Account.ResetCancelOnDisconnectAsync(codGroupId, expiryAfterSeconds)` before the deadline. Minimum 10 s, 0 removes the group. The client-side limiter never counts this call.
- Public trade and report windows are limited to 24 hours.

## 7. Markets and formats

- Market = `BASE-QUOTE`, uppercase, dash: `ETH-EUR`, `BTC-EUR`. Same on REST and WebSocket.
- Do not write ETHEUR, ETH/EUR or eth-eur. Build the name as `BASE-QUOTE` in uppercase, or with `BitvavoExchange.FormatSymbol`.
- `BitvavoExchange.FormatSymbol("eth", "eur", TradingMode.Spot)` -> `ETH-EUR`.
- Intervals: `KlineInterval` (`OneMinute` ... `OneWeek` = "1W", `OneMonth` = "1M"). Never hand-write interval strings.
- Amounts and prices are `decimal`. They travel as JSON strings; the models parse them.
- Times are UTC `DateTime`.
- `BitvavoMarket.PricePrecision` is null on live markets. The price grid is `BitvavoMarket.TickSize`.
- Fee rates in the REST models are fractions (`0.0015` = 0.15 %). The Shared fee is a percentage.

## 8. Orders

- Every order operation needs an operator id: `long operatorId`, account-scoped, any stable value (for example 1).
  Place, update, cancel, cancel all, atomic cancel, and the institutional cancels.
- `clientOrderId` is optional and must be a UUID: `Guid.NewGuid().ToString()`.
- No testnet and no validate-only flag. Every order call hits the live account.
- Trigger orders (`OrderType.StopLoss`, `StopLossLimit`, `TakeProfit`, `TakeProfitLimit`) wait in status `AwaitingTrigger`. `TriggerAmount` is the trigger price, `TriggerType` is `TriggerType.Price`, `TriggerReference` picks the reference price.

```csharp
using Bitvavo.Net;
using Bitvavo.Net.Clients;
using Bitvavo.Net.Enums;
using Bitvavo.Net.Objects.Models.Spot;

using var client = new BitvavoRestClient(options =>
    options.ApiCredentials = new BitvavoCredentials(apiKey, apiSecret));

var order = await client.SpotApi.Trading.PlaceOrderAsync(
    new BitvavoPlaceOrderRequest(
        "ETH-EUR", OrderSide.Buy, OrderType.Limit, OperatorId: 1,
        Amount: 0.5m, Price: 1500m,
        TimeInForce: TimeInForce.GoodTillCanceled,
        ClientOrderId: Guid.NewGuid().ToString()));
if (!order.Success)
{
    Console.WriteLine($"Failed: {order.Error}");
    return;
}

var cancelled = await client.SpotApi.Trading.CancelOrderAsync("ETH-EUR", operatorId: 1, orderId: order.Data.OrderId);
```

- Market buy sized in quote currency: `AmountQuote` instead of `Amount`. Never both.
- `WithdrawAsync` and `WithdrawCryptoAsync` move funds. `CancelOrdersAsync` without a market cancels every open order (weight 100). Do not put them in examples unless the user asked.
- Withdrawals: 2FA and the e-mail confirmation of the address are off for API withdrawals. The key's withdraw permission is the only gate.

## 9. WebSocket

```csharp
using Bitvavo.Net.Clients;
using Bitvavo.Net.Enums;

using var socketClient = new BitvavoSocketClient();

var sub = await socketClient.SpotApi.ExchangeData.SubscribeToKlineUpdatesAsync(
    "ETH-EUR",
    KlineInterval.OneMinute,
    update =>
    {
        foreach (var candle in update.Data.Candle)
        {
            Console.WriteLine($"{update.Data.Market} close={candle.ClosePrice}");
        }
    });
if (!sub.Success)
{
    Console.WriteLine($"Failed: {sub.Error}");
    return;
}

await sub.Data.CloseAsync();
```

Private (order and fill events of the `account` channel, subscribed per market):

```csharp
using var socketClient = new BitvavoSocketClient(options =>
    options.ApiCredentials = new BitvavoCredentials(apiKey, apiSecret));

var orders = await socketClient.SpotApi.Account.SubscribeToOrderUpdatesAsync(
    ["ETH-EUR"],
    update => Console.WriteLine($"{update.Data.OrderId} -> {update.Data.Status}"));
var fills = await socketClient.SpotApi.Account.SubscribeToFillUpdatesAsync(
    ["ETH-EUR"],
    update => Console.WriteLine($"{update.Data.FillId} {update.Data.Amount} @ {update.Data.Price}"));
```

- Public candles and trades only. Candle event = `BitvavoStreamCandleEvent` with a `Candle` array of `BitvavoKline`.
- A subscribe counts as successful once it is sent. Bitvavo acknowledges subscribes only in an aggregated event, and `error` events are not routed to the caller.
- Close: `await sub.Data.CloseAsync()`, `await socketClient.UnsubscribeAsync(sub.Data)`, `await socketClient.UnsubscribeAllAsync()`, or cancel the `CancellationToken` passed to the subscribe call.
- Private subscriptions authenticate their connection first (HMAC-SHA256 `authenticate` action). After `SetApiCredentials`, the next private subscription uses a connection of its own with the new key.
- Reconnection is automatic. WebSocket frames do not count against the client-side rate limiter.

## 10. Shared API (exchange-agnostic)

- `SpotApi.SharedApi` = v2 capabilities (`IBitvavoRestClientSpotSharedApi`, `IBitvavoSocketClientSpotSharedApi`). `SpotApi.SharedClient` = legacy v1 interfaces, same object. Use v2 in new code.
- `IBitvavoSharedApiClient` (from the container) holds both transports. `GetCapability<T>()` / `GetCapabilities<T>()` look a capability up at runtime, `Discover()` lists everything, the `SharedApi.PreferredTransport` option of `BitvavoOptions` decides when REST and WebSocket could both answer (default REST). A missing capability comes back null: check it.
- `SpotApi.SharedApi` when the transport is known. Authoritative capability lists: `IBitvavoRestClientSpotSharedApi`, `IBitvavoSocketClientSpotSharedApi`.
- Results: REST capability interfaces return `HttpResult<T>`, socket ones `WebSocketResult<UpdateSubscription>`, the transport-agnostic capability interfaces (`IGetKlines`, `IPlaceSpotOrder`, ...) `IExchangeCallResult<T>`.
- Every capability has an options object (`GetKlinesOptions`, `PlaceSpotOrderOptions`, ...) with limits, `ExchangeParameterRules` and `RequestNotes`. A request is validated against it first. A rejected request sends nothing and returns an `ArgumentError`.
- Exchange parameters (the Shared requests have no field for them), exchange name `Bitvavo`:
  - `OperatorId` (`long`, required): place, cancel, cancel all, edit, trigger place and cancel.
  - `Markets` (`string[]`, required): private socket subscriptions (orders, user trades).
  - `TriggerReference` (`TriggerReference`, optional): trigger orders. Default last trade.
  - A default for all requests of one API: `SharedApi.SetDefaultExchangeParameter("OperatorId", 1L)`, undo with `ResetDefaultExchangeParameters()`.
- Order types: Limit, Market, LimitMaker (post-only limit). Time in force: GTC, IOC, FOK. Stop-loss and take-profit go through the trigger-order capabilities (`IPlaceSpotTriggerOrderRest`, `IGetSpotTriggerOrderRest`, `ICancelSpotTriggerOrderRest`).
- Cancel-all capabilities return no order ids. Shared withdraw needs the `Network` and charges the fee on top of the quantity.
- No batch-order capability: Bitvavo has no such endpoint.
- Client order ids: `SharedApi.GenerateClientOrderId()` returns a UUID.

```csharp
using CryptoExchange.Net.SharedApis;

var shared = restClient.SpotApi.SharedApi;
var symbol = new SharedSymbol(TradingMode.Spot, "ETH", "EUR");

var klines = await shared.GetKlinesAsync(new GetKlinesRequest(symbol, SharedKlineInterval.OneHour, limit: 24));

var placed = await shared.PlaceSpotOrderAsync(new PlaceSpotOrderRequest(
    symbol,
    SharedOrderSide.Buy,
    SharedOrderType.Limit,
    SharedQuantity.Base(0.5m),
    price: 1500m,
    clientOrderId: shared.GenerateClientOrderId(),
    exchangeParameters: new ExchangeParameters(new ExchangeParameter("Bitvavo", "OperatorId", 1L))));
```

Private socket subscription (the `Markets` parameter):

```csharp
var markets = new ExchangeParameters(new ExchangeParameter("Bitvavo", "Markets", new[] { "ETH-EUR" }));
var sub = await socketClient.SpotApi.SharedApi.SubscribeToSpotOrderUpdatesAsync(
    new SubscribeSpotOrderRequest(markets),
    update => Console.WriteLine(update.Data.Length));
```

## 11. Dependency injection

```csharp
using Bitvavo.Net;
using Bitvavo.Net.Extensions;
using Bitvavo.Net.Objects.Options;

services.AddBitvavo();
services.AddBitvavo(configuration.GetSection("Bitvavo"));
services.AddBitvavo((BitvavoOptions o) =>
{
    o.ApiCredentials = new BitvavoCredentials(apiKey, apiSecret);
    o.Rest.RequestTimeout = TimeSpan.FromSeconds(10);
});
```

- Inject `IBitvavoRestClient`, `IBitvavoSocketClient`, `IBitvavoSharedApiClient`, the spot API interfaces and every Shared capability interface.
- Lifetimes: REST client per resolution on a typed `HttpClient` (timeout = `RequestTimeout`). Socket client singleton unless `SocketClientLifeTime` says otherwise.
- Configuration keys: `Environment:Name`, `ApiCredentials:Spot:Key`, `ApiCredentials:Spot:Secret`, `Rest:*`, `Socket:*`, `SharedApi:*`. An unknown environment name throws at registration.
- An untyped lambda such as `AddBitvavo(o => o.ApiCredentials = ...)` binds the `Action<BitvavoRestOptions>` overload at language version C# 13 or later (the default of net9.0+ projects): the REST client gets the credentials, the socket client does not. At C# 12 (the net8.0 default, whichever compiler builds it) the library-options overload is picked instead, both clients get the credentials and `AddBitvavo(null)` is ambiguous. Type the parameter (`(BitvavoOptions o) => ...`) or use configuration: both clients get the credentials at every language version.
- `services.PostConfigure<BitvavoRestOptions>(...)` runs last and wins for every overload.

## 12. Rate limiting

- One static, settable limiter for the whole process: `BitvavoExchange.RateLimiter` (`BitvavoRateLimiters`). Bitvavo's budget belongs to the account or IP address, not to a client instance.
- Two independent budgets of 1000 weight points per minute: signed requests (one tracker per process, whichever key signs) and public requests (per host). Both are sliding one-minute windows.
- `MaxUtilization` default 0.9 keeps 10 % free for other processes. Overrunning blocks the caller for 1 minute (signed) or 15 minutes (IP).
- Weight 1 for most endpoints (account history too). 5 for balances, own trades, order, deposit and withdrawal histories, report trades. 25 for public 24 h ticker without market, cancel orders with market, crypto withdrawal. 100 for open orders or cancel orders without market, atomic cancel. Exact table: `BitvavoEndpointContracts`.
- WebSocket frames are not counted. `ResetCancelOnDisconnectAsync` is never held back.

```csharp
using Bitvavo.Net.Objects.Internal;

BitvavoExchange.RateLimiter = new BitvavoRateLimiters(maxUtilization: 0.8);
BitvavoExchange.RateLimiter.RateLimitTriggered += trigger => Console.WriteLine(trigger);
```

Set it at start-up. A replacement starts counting from zero. Events: `RateLimitTriggered`, `RateLimitUpdated`.

## 13. Common pitfalls - AVOID

- Raw HTTP or raw sockets against Bitvavo.
- Reading `.Data` without checking `.Success`.
- Blocking async calls with `.Result` or `.Wait()`.
- Creating a client per request.
- Forgetting to close subscriptions or dispose standalone clients.
- Treating a successful subscribe as confirmation from Bitvavo. It only means the frame was sent.
- Retrying an order call blindly after `ErrorType.Timeout` (error 109).
- Omitting the operator id. Sending a `clientOrderId` that is not a UUID.
- `AddBitvavo(o => o.ApiCredentials = ...)` with an untyped lambda and expecting the socket client to be authenticated (section 11). Type the lambda parameter.
- Assuming a testnet or a dry-run flag.
- Hand-written market names or interval strings (section 7).
- Using `PricePrecision` for rounding. Use `TickSize`.
- Spelling the result type as WebCallResult or CallResult (older CryptoExchange.Net names). This library returns `HttpResult<T>` and `WebSocketResult<UpdateSubscription>`.
- Committing API keys.

## 14. Not implemented

- Public WebSocket ticker, ticker24h and book channels.
- Local order book (CryptoExchange.Net `SymbolOrderBook`), and the CryptoExchange.Net trackers (`KlineTracker`, `TradeTracker`).
- Multi-user client provider.
- WebSocket request/response actions (create/cancel order over the socket).
- WebSocket `error` event routing.
- Balance subscription (the `account` channel carries only order and fill events).
- Batch orders (Bitvavo has no endpoint).
- Futures, margin, testnet (Bitvavo is spot only).

---

# Part 2 - Contributing

## 15. Hard rules

1. TDD. Failing test first, see it RED, then implement. A bug fix starts with a failing repro test.
2. 0 warnings and 0 errors on every target framework (net8.0, net9.0, net10.0). CI builds Release with `-warnaserror`; that includes NuGet advisories (NU19xx).
3. Always braces on control flow: `if`, `else`, `for`, `foreach`, `while`. One-liners too.
4. No static `*Helper` / `*Util` / `*Utility` classes. Shared logic = extension method (`this T value`) or a generic. Static classes only for named domain things (`BitvavoExchange`, `BitvavoErrors`, `BitvavoSharedParameters`) or for extension methods (`BitvavoSharedMappingExtensions`).
5. No tuples. A multi-value result is a named `public readonly record struct` (deconstructs like a tuple, keeps names in IntelliSense and stack traces).
6. XML docs on every public type and member. `GenerateDocumentationFile` is on, so a missing doc is CS1591 and `-warnaserror` fails the build.
7. English only: code, comments, XML docs, docs, commit messages.
8. Public REST methods return `Task<HttpResult<T>>`, subscriptions `Task<WebSocketResult<UpdateSubscription>>`. Never WebCallResult or CallResult. Protocol errors are results, never exceptions.
9. Typed request and response models. No loose dictionaries on the public surface.
10. Extend the Shared API through the partial-per-capability pattern (section 20): open for extension, closed for modification.
11. One version number: `<Version>` in `Bitvavo.Net/Bitvavo.Net.csproj`. Do not duplicate it in code or build files.
12. Every source file starts with `// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.`
13. Notable changes go under `## [Unreleased]` in `CHANGELOG.md`.
14. Never commit credentials. Tests use stub credentials (for example `test-key` / `test-secret`).

## 16. Commands

```bash
dotnet restore Bitvavo.Net.slnx
dotnet build Bitvavo.Net.slnx                                         # library, tests, examples
dotnet build Bitvavo.Net.slnx --configuration Release -warnaserror    # what CI runs
dotnet run --project Bitvavo.Net.Tests -f net10.0                     # also net8.0 and net9.0
```

- The test project is an xunit v3 executable (`OutputType` Exe): `dotnet run` runs the tests. `dotnet test` on SDK 10 runs 0 tests and still exits 0 (see `.github/workflows/ci.yml`). A run with 0 tests is a failure.
- Install .NET 8, 9 and 10 SDKs for the full matrix.

## 17. Layout

```text
Bitvavo.Net.slnx                          library, tests, two example projects
Directory.Build.props                     deterministic build, SourceLink, embedded symbols
Bitvavo.Net/Bitvavo.Net.csproj            the library (<Version> lives here)
Bitvavo.Net/Interfaces/Clients/           public interfaces (the public surface)
Bitvavo.Net/Clients/                      BitvavoRestClient, BitvavoSocketClient, BitvavoSharedApiClient
Bitvavo.Net/Clients/SpotApi/              internal API clients + one sub-client class per group
Bitvavo.Net/Clients/SpotApi/SharedApi/    Shared API: one class per transport, one partial file per capability
Bitvavo.Net/Clients/MessageHandlers/      REST and socket message handlers
Bitvavo.Net/Extensions/                   AddBitvavo
Bitvavo.Net/Enums, Objects/Models, Objects/Options, Objects/Internal
Bitvavo.Net/Examples/                     QuickStart (runnable), ReadmeSamples (README code, compiled never run)
Bitvavo.Net.Tests/                        xunit v3 test executable
ARCHITECTURE.md                           how it is built and why
```

- `BitvavoRestClientSpotApi` and `BitvavoSocketClientSpotApi` are `internal sealed`. Callers only see the interfaces.
- Examples are in the solution: CI compiles them, so an API change must keep them building.

## 18. Add a REST endpoint

1. RED first. Add the member to `Interfaces/Clients/SpotApi/IBitvavoRestClientSpotApi<Group>.cs` (XML docs with a link to the Bitvavo docs page). Add its row to `BitvavoEndpointContracts.All`: name `Group.Method#n`, verb, path and query exactly as sent, JSON body, signed, weight from the vendor documentation. `BitvavoWireGoldenTests` and `BitvavoRateLimiterTests` run per row. `Every_rest_endpoint_has_a_contract_row` fails without a row.
2. Implement in `Clients/SpotApi/BitvavoRestClientSpotApi<Group>.cs`:
   - `new Parameters(BitvavoExchange.ParameterSerializationSettings)`, then `Add(name, value)`. Null values are left out.
   - `_definitions.GetOrCreate(HttpMethod.X, _baseClient.BaseAddress, "v2/<path>", BitvavoRestClientSpotApi.RateLimitGate, weight: n, authenticated: bool)`. `_definitions` is a static `RequestDefinitionCache` per sub-client.
   - Call `_baseClient.SendAsync<T>(...)`. Body endpoints use the overload with `bodyParameters`.
   - A definition is cached on first use. When the weight varies per call, pass `weight` to `SendAsync` as well.
   - DELETE with query parameters: `parameterPosition: HttpMethodParameterPosition.InUri`.
   - Always bind to `BitvavoRestClientSpotApi.RateLimitGate`, never to a limiter instance (it keeps `BitvavoExchange.RateLimiter` replaceable).
3. Models: `public record` in `Objects/Models/Spot/`, XML docs, `decimal` for amounts (Bitvavo sends strings, the handler reads them with `AllowReadingFromString`). Enums: `[JsonConverter(typeof(EnumConverter<T>))]` with `[Map("wire")]`. Array-shaped payloads use `ArrayConverter<T>` and need setters (`BitvavoKline`).
4. Model test in `BitvavoModelConformanceTests` with a payload shaped like the vendor specification.
5. Expected values come from the vendor documentation (docs.bitvavo.com), never from the code under test.
6. If the endpoint backs a Shared capability, add it (section 20).

Request path: sub-client -> `RequestDefinitionCache.GetOrCreate` -> `RestApiClient` -> `BitvavoAuthenticationProvider.ProcessRequest` (HMAC-SHA256 hex of `timestamp + METHOD + path[?query] + body`, the body is signed whenever the parameters travel in it) -> HTTP -> `BitvavoRestSpotMessageHandler` (errors via `BitvavoErrors.SpotMapping`) -> `HttpResult<T>`.

## 19. Add a socket subscription

1. Test first, through the framework's test transport: `TestHelpers.ConfigureSocketClient(client, "wss://ws.bitvavo.com/v2/")` returns a `TestSocket`; collect the frames the client sends with `socket.OnMessageSend`, feed Bitvavo-shaped frames with `socket.InvokeMessage(...)` (see `BitvavoSocketSubscriptionTests`, `BitvavoSocketAccountDispatchTests`). A private subscription needs the `authenticate` reply: `{"event":"authenticate","authenticated":true}`.
2. Member on `IBitvavoSocketClientSpotApiExchangeData` or `IBitvavoSocketClientSpotApiAccount`. Event model in `Objects/Models/Spot/Streams/`.
3. Implement in `BitvavoSocketClientSpotApiExchangeData` / `BitvavoSocketClientSpotApiAccount` with `BitvavoSubscription<T>` (public) or `BitvavoAccountSubscription<T>` (private, authenticated). Subscribe through `SubscribeInternalAsync`.
4. Register the topic key in `BitvavoSocketSpotMessageHandler` with `AddTopicMapping<T>(...)`. The event type comes from the `event` field of each message.
5. Subscribe and unsubscribe are fire-and-forget (`BitvavoSubscribeQuery`): Bitvavo sends no per-request acknowledgment.

## 20. Add a Shared API capability

A capability = three things, nothing else:

1. One partial file: `BitvavoRestClientSpotSharedApi.<Capability>.cs` (REST) or `BitvavoSocketClientSpotSharedApi.<Capability>.cs` (socket). It declares the CryptoExchange.Net capability interface(s), holds the options property and the call.
2. Its interface in the aggregate interface: `IBitvavoRestClientSpotSharedApi` / `IBitvavoSocketClientSpotSharedApi` (v2), plus the legacy aggregate (`IBitvavoRestClientSpotApiShared` / `IBitvavoSocketClientSpotApiShared`) when a legacy v1 interface is implemented too.
3. Its options object in the `SetCapabilities(...)` list of the class constructor.

- `BitvavoSharedApiRegistrationTests` fails when the three disagree. DI needs no change: `BitvavoSharedApiClientRegistrationTests` checks that every capability resolves.
- Every call: validate the request against its options first, call the typed sub-client, map the answer. Mappings are extension methods in a partial of `BitvavoSharedMappingExtensions` (`BitvavoSharedMappingExtensions.<Capability>.cs`).
- Mapping rules: data that came from Bitvavo never throws (unknown value -> `Unknown` or nothing). A value a caller put in a request that Bitvavo cannot express is rejected by validation first.
- Bitvavo specifics without a Shared field go in `ExchangeParameters`. Declare the rule once in `BitvavoSharedParameters` (`OperatorIdRule`, `MarketsRule`) and read it with its accessor (`GetOperatorId`, `GetMarkets`).
- Document quirks in the options' `RequestNotes`.
- Tests: one file per capability group in `Bitvavo.Net.Tests/Clients/SpotApi/SharedApi/`.

## 21. Tests

- xunit v3 and Shouldly. No mocking library.
- The only seam is the transport. REST: `StubHttpMessageHandler` (canned JSON, records requests) with the `RestClient()` extension that builds the real `BitvavoRestClient`. WebSocket: CryptoExchange.Net's `TestSocket` via `TestHelpers.ConfigureSocketClient`, or `RecordingSocketFactory` when more than one connection matters. `HangingHttpMessageHandler` for timeouts.
- The library's own types are never replaced by test doubles. Rate limiting is never switched off: the assembly attribute `FreshRateLimiter` gives every test a fresh `BitvavoRateLimiters`, and the assembly runs serially.
- Every awaited call passes `TestContext.Current.CancellationToken`.
- Test names are sentences with underscores. Test classes carry an XML summary saying what they pin.
- Payloads are raw string literals shaped like the vendor documentation.

## 22. CI, release, pull requests

- `.github/workflows/ci.yml` (push and pull request to master): ubuntu and windows, SDK 8, 9 and 10, restore, Release build with `-warnaserror`, tests on net8.0, net9.0, net10.0, pack validation (exactly one `.nupkg`, no `.snupkg`).
- `.github/workflows/publish.yml` (GitHub release published): same build and tests, pack, push to NuGet.
- Release: bump `<Version>` in `Bitvavo.Net/Bitvavo.Net.csproj`, move `[Unreleased]` entries in `CHANGELOG.md` under the new version, publish a GitHub release.
- Pull request: topic branch, test first, 0 warnings, all target frameworks green, describe change and motivation, reference the issue. See `CONTRIBUTING.md`.

## Reference

- API quick map (members, verbs, paths, weights, models, enums, error table): `docs/ai-api-map.md`
- Full LLM context (usings, quick starts, API surface, errors, rate limits, pitfalls): `llms-full.txt`
- LLM index: `llms.txt`
- Architecture: `ARCHITECTURE.md`
- README: `README.md`. Changelog: `CHANGELOG.md`. Contributing: `CONTRIBUTING.md`.
- Bitvavo API docs: https://docs.bitvavo.com/
- CryptoExchange.Net docs: https://cryptoexchange.jkorf.dev
- CryptoExchange.Net source: https://github.com/JKorf/CryptoExchange.Net
- Source: https://github.com/xkqg/Bitvavo.Net
- NuGet: https://www.nuget.org/packages/Bitvavo.Net
