# Copilot Instructions for Bitvavo.Net

This repository is **Bitvavo.Net** - a strongly typed C#/.NET client library for the Bitvavo spot REST and WebSocket APIs. It is built on CryptoExchange.Net 13.1.0, targets net8.0, net9.0 and net10.0, and has no futures, no margin and no testnet.

Two jobs: generating code that consumes Bitvavo.Net, and editing this library. Follow the matching section.

## Consumer code

### Use Bitvavo.Net, not raw HTTP

Never generate raw `HttpClient` or `ClientWebSocket` calls to Bitvavo endpoints. Always use `BitvavoRestClient` or `BitvavoSocketClient` (or the injected `IBitvavoRestClient` and `IBitvavoSocketClient`), so request signing, rate limiting, reconnection and typed errors stay consistent.

### Client setup

```csharp
using Bitvavo.Net;
using Bitvavo.Net.Clients;

using var restClient = new BitvavoRestClient(options =>
    options.ApiCredentials = new BitvavoCredentials(apiKey, apiSecret));
```

Public market data needs no credentials: `new BitvavoRestClient()`.
DI: `services.AddBitvavo()`, `services.AddBitvavo(configuration.GetSection("Bitvavo"))` or `services.AddBitvavo((BitvavoOptions o) => ...)` (namespace `Bitvavo.Net.Extensions`).
An untyped lambda `AddBitvavo(o => o.ApiCredentials = ...)` configures only the REST client at language version C# 13 or later (the default of net9.0+ projects; at C# 12, the net8.0 default, the library-options overload is picked and both clients get the credentials); type the parameter, `(BitvavoOptions o) => ...`, to configure both clients at every language version.
Never hardcode or commit API keys.

### Result handling

REST methods return `Task<HttpResult<T>>`. WebSocket subscriptions return `Task<WebSocketResult<UpdateSubscription>>`. Always check `.Success` before reading `.Data`; error details are on `.Error`. `.Error.ErrorType` is the mapped CryptoExchange.Net `ErrorType` (Bitvavo's 90 documented error codes are mapped).

### API structure

- `restClient.SpotApi.ExchangeData` - markets, candles, server time, assets, ticker prices, top of book, 24 h ticker, order book, public trades
- `restClient.SpotApi.Account` - account info, balances, fees, staking balance, transaction history, cancel-on-disconnect
- `restClient.SpotApi.Trading` - place, update, get, cancel, cancel all, atomic cancel, open orders, order history, own trades
- `restClient.SpotApi.Funding` - deposit address and history, withdrawal history, withdraw, crypto withdrawal
- `restClient.SpotApi.Report` - MiCA trade and order-book reports
- `restClient.SpotApi.Institutional` - subaccounts, transfers, per-subaccount queries and cancels
- `socketClient.SpotApi.ExchangeData` - candle and trade subscriptions
- `socketClient.SpotApi.Account` - private order and fill events (authenticated, per market)
- `SpotApi.SharedApi` on REST and socket - CryptoExchange.Net Shared API v2. `SpotApi.SharedClient` is the legacy v1 view of the same object
- `IBitvavoSharedApiClient` - both transports as one client: `GetCapability<T>()`, `GetCapabilities<T>()`, `Discover()`

### Bitvavo specifics

- Markets are `BASE-QUOTE`, uppercase, dash: `ETH-EUR`. Same on REST and WebSocket.
- Every order operation needs an operator id (`long`). Shared requests carry it as the `OperatorId` exchange parameter.
- `clientOrderId` must be a UUID.
- Only `BitvavoEnvironment.Live` exists. No testnet and no validate-only order flag: every order call is live.
- Shared private socket subscriptions take the markets as the `Markets` exchange parameter.
- `BitvavoMarket.PricePrecision` is null on live markets; use `TickSize`.
- Error 109 (`ErrorType.Timeout`, not transient): the operation may have happened. Read the order back before retrying.
- Rate limiting is one static limiter per process, `BitvavoExchange.RateLimiter` (namespace `Bitvavo.Net.Objects.Internal`), with separate budgets for signed and public requests (1000 weight per minute each).
- A WebSocket subscribe counts as successful once it is sent. Close with `await sub.Data.CloseAsync()`, `UnsubscribeAsync`, or by cancelling the token passed to the subscribe call.

### Avoid

- Raw HTTP or raw sockets against Bitvavo.
- Reading `.Data` without checking `.Success`.
- Blocking async calls with `.Result` or `.Wait()`.
- Creating clients per request in production; reuse them or use DI.
- Omitting the operator id, or sending a `clientOrderId` that is not a UUID.
- Assuming a testnet or a dry-run flag.
- Hand-written market names (ETHEUR, ETH/EUR) or interval strings; use `KlineInterval`.
- Spelling result types as WebCallResult or CallResult; this library returns `HttpResult<T>` and `WebSocketResult<UpdateSubscription>`.
- Inventing method names; inspect `Bitvavo.Net/Interfaces/Clients/**`.

### Not implemented

Public WebSocket ticker, ticker24h and book channels, local order book, trackers, multi-user client provider, WebSocket request/response actions, balance subscription, batch orders. Bitvavo is spot only.

## Editing this library

- TDD: failing test first, confirm RED, then implement. A bug fix starts with a failing repro test.
- 0 warnings and 0 errors on net8.0, net9.0 and net10.0. CI builds Release with `-warnaserror`.
- Always braces on `if`, `else`, `for`, `foreach`, `while`.
- No static `*Helper` / `*Util` classes. Use extension methods (`this T value`) or generics.
- No tuples. A multi-value result is a named `public readonly record struct`.
- XML docs on every public type and member. English only: code, comments, docs, commit messages.
- Public REST methods return `Task<HttpResult<T>>`, subscriptions `Task<WebSocketResult<UpdateSubscription>>`.
- Typed models, no loose dictionaries on the public surface.
- Extend the Shared API with one partial file per capability (`BitvavoRestClientSpotSharedApi.<Capability>.cs`), its interface in the aggregate interface, its options in `SetCapabilities`. `BitvavoSharedApiRegistrationTests` fails when the three disagree.
- A new REST endpoint needs an interface member, an implementation, and a row in `BitvavoEndpointContracts.All`.
- Tests are xunit v3 with Shouldly. The only seam is the transport: `StubHttpMessageHandler`, CryptoExchange.Net `TestSocket`, `RecordingSocketFactory`. No mocks of the library's own types; rate limiting stays on.
- One version number: `<Version>` in `Bitvavo.Net/Bitvavo.Net.csproj`.
- Commands: `dotnet build Bitvavo.Net.slnx` and `dotnet run --project Bitvavo.Net.Tests -f net10.0` (also net8.0 and net9.0). `dotnet test` runs 0 tests on SDK 10.

## Reference

For detailed patterns, contributor checklists and pitfalls see `AGENTS.md`, `llms.txt` and `llms-full.txt` in the repository root, `docs/ai-api-map.md` for the member, verb, path and weight of every call, `ARCHITECTURE.md` for how the library is built, and `Bitvavo.Net/Examples/ReadmeSamples/ReadmeSamples.cs` for compilable samples.
