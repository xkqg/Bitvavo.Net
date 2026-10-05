# Changelog

All notable changes to **Bitvavo.Net** are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.5.0] — 2026-10-05

Moves the library from CryptoExchange.Net 11.1.1 to **13.1.0** (the structure of Kraken.Net 8.6.0, so both libraries load in one process), brings the Bitvavo API coverage up to the documentation v2.10.0, and replaces the rate limiter.

### Breaking
- **Result types** (CryptoExchange.Net 13): REST methods return `HttpResult<T>` (was `WebCallResult<T>`), subscriptions `WebSocketResult<UpdateSubscription>` (was `CallResult<UpdateSubscription>`). `Success`, `Data` and `Error` are unchanged; code that spells the result type out must change the name.
- `IBitvavoRestClientSpotApiAccount.GetTradingFeesAsync(market, quote, ct)`: the `CancellationToken` moved to the third position (the new `quote` parameter takes the second).
- `IBitvavoRestClient` and `IBitvavoSocketClient` now extend `IRestClient<BitvavoCredentials>` and `ISocketClient<BitvavoCredentials>`, as `IKrakenRestClient` does: the credentials can be changed through the interface. A class that implements these interfaces (a test double) must implement the new members.
- `BitvavoMarket`: `Status` is a `BitvavoMarketStatus?` (was a string; an unknown or absent value is null, never "trading"), `PricePrecision` an `int?` (was a string; Bitvavo reports it as null on every market — use `TickSize`).
- The Shared API is reworked on the CryptoExchange.Net 13 model. `SharedClient` still returns the legacy (V1) interfaces, but it is now the Shared API object, no longer the API client itself; the new V2 capabilities are on `SharedApi`. Behaviour of the V1 members changed where Bitvavo's API demanded it:
  - `WithdrawAsync` uses `POST /v2/crypto/withdrawal`: the network is required, the fee is charged on top of the quantity, and the returned id is the one Bitvavo issues (it was the asset symbol).
  - `GetDepositAddressesAsync` rejects a request that names a network (Bitvavo returns one address per asset).
  - The history options no longer advertise ascending order, `PriceSignificantFigures` is never set (its source is null live; the price grid is `PriceStep`/`PriceDecimals` from `tickSize`), `SupportsSpotSymbolAsync` matches exactly.
- Signed-request rules: the report endpoints (`/report/{market}/trades`, `/book`) are public and no longer signed; the institutional DELETE endpoints are signed over their JSON body.
- A lambda passed positionally to `AddBitvavo` keeps binding to the `Action<BitvavoRestOptions>` overload on language version C# 13 or later (the default of net9.0 and later projects). The priority attribute that decides it is ignored below C# 13 — a net8.0 project defaults to C# 12, whichever compiler builds it — where a lambda over a member both option types carry (`ApiCredentials`, `Environment`) binds the library-options overload instead and `AddBitvavo(null)` is ambiguous. Set `<LangVersion>13.0</LangVersion>` or type the parameter (`(BitvavoRestOptions o) => …`).

### Added
- **Shared API V2**: `restClient.SpotApi.SharedApi` (32 capabilities — assets, klines, symbols, tickers, order book, book ticker, recent trades, balances, fees, ledger, deposit addresses and history, withdrawal history, withdraw, spot orders: place / get / by client id / open / closed / cancel / cancel by client id / order trades / user trades, cancel all (global and per symbol), edit (by id and by client id), spot trigger orders: place / get / cancel) and `socketClient.SpotApi.SharedApi` (klines, trades, spot orders, user trades), with `Discover()`. `IBitvavoSharedApiClient` / `BitvavoSharedApiClient` combine both transports; the container hands out every capability interface.
- `BitvavoOptions` (`SharedApi`, the shared environment and credentials), `AddBitvavo(IConfiguration)` and `AddBitvavo(Action<BitvavoOptions>)`. An environment name is looked up case-insensitively and an unknown name throws.
- `BitvavoRateLimiters` and the static, settable `BitvavoExchange.RateLimiter` (events, headroom, allocated limit); `BitvavoExchange.Metadata`.
- Endpoints and parameters of the Bitvavo API v2.10.0: atomic cancel (`CancelOrdersAtomicAsync`), crypto withdrawal (`WithdrawCryptoAsync`), staking balance (`GetStakingBalanceAsync`), fee tier by quote asset, `DisableMarketProtection` on place, `AmountRemaining` on update, `tradeId` on the own-trade history (Bitvavo deprecates it and reads it as `tradeIdTo`).
- Model fields: market `QuantityDecimals`, `NotionalDecimals`, `TickSize`, `MaxOpenOrders`, `FeeCategory` and the statuses `AuctionMatching` and `CancelOnly`; order `DisableMarketProtection`, `RestatementReason`, `CreatedNs`, `UpdatedNs`; `OperatorId` on fills and stream events; nanosecond timestamps on trades, fills and the order book.
- Error mapping: the 90 documented error codes map onto CryptoExchange.Net's `ErrorType` (`BitvavoErrors`); an HTTP 429 keeps the server's code (105 or 112) and message.
- `ARCHITECTURE.md`, the AI-assistant instruction files (`AGENTS.md`, `llms.txt`, `llms-full.txt`, `docs/ai-api-map.md`, Cursor and Copilot instructions) and the `Examples/ReadmeSamples` project that compiles the README samples with the solution.

### Changed
- **Rate limiting is rebuilt.** Bitvavo tracks 1000 weight points per minute per account for signed requests and per IP address for public ones; the limiter keeps the two budgets apart, uses sliding windows (the host clock measured 480–529 ms ahead of Bitvavo's, which breaks a clock-aligned window) and keeps 10 % of each budget free (`MaxUtilization`, default 0.9, combined with the CryptoExchange.Net `RateLimitAdmission`). Weights follow the documentation per endpoint (`account/history` 1, `cancelOrdersAfter` 5, atomic cancel 100, crypto withdrawal 25, staking balance 5; variable weights are passed per call). WebSocket frames are not counted. The cancel-on-disconnect heartbeat stays ungated on purpose: a refused heartbeat lets the timer expire and Bitvavo cancels the group's orders, and its weight fits in the free headroom.
- **Correction of the 0.4.0 entry above:** it says every endpoint passed through the rate-limit gate. The gate did not match any request, so nothing was counted. 0.5.0 is the first release whose rate limiter counts.
- The DI registration feeds the options pipeline for every overload, so an application's `PostConfigure` wins; what a registration sets beats the process-wide defaults of `SetDefaultOptions`, the lowest layer. The REST client is created on a typed `HttpClient` whose timeout is `RequestTimeout` (it was the factory default of 100 s) with CryptoExchange.Net's handler and a handler lifetime that never rotates. Clients register as in 0.4.0 (`TryAdd`: a client registered earlier stays the one that resolves, and a second `AddBitvavo` adds its options but no second set of clients); each REST and socket client gets a copy of the options, so `SetOptions` on one client does not reach the container or the next client.
- `AutoTimestamp` works for REST: the offset comes from the public `GET /v2/time`.
- The WebSocket `authenticate` timestamp comes from the same time source as REST signing.
- `SetApiCredentials` on a socket client: the next private subscription authenticates with the new key on a connection of its own instead of riding the connection of the old key. A connection that reconnects authenticates again with the key held then (right for rotating the key of one account; another account needs a client of its own).
- Changing the key (`SetApiCredentials`, `SetOptions` with credentials) is atomic for requests running at the same time, on the REST and the socket client. CryptoExchange.Net 13.1.0 replaces the signing provider in several steps; a request reading it in between can leave a socket client on the old key until the next change (after a revocation every authentication then fails) or find no provider. Measured against the unguarded base class: 15 of 100,000 changes under a concurrent reader.
- A WebSocket subscribe counts as successful once it is sent (Bitvavo acknowledges subscribes only in an aggregated event); cancelling the token passed to the subscribe call closes the subscription.
- The body of every request whose parameters travel in the body is signed (decided by where the parameters travel, not by the HTTP verb).
- Fee rates stay fractions in the REST models; the Shared fee is a percentage.
- Test suite: the only test seam is the network transport; rate limiting is no longer switched off in tests; the suite runs per target framework (net8.0, net9.0, net10.0).
- Dependencies: `CryptoExchange.Net` 11.1.1 → 13.1.0; SourceLink 10.0.401 (clears the advisory on the older package).

### Fixed
- A ticker (`GetTicker24hAsync`, `GetTickerBookAsync`, `GetTickerPricesAsync`) or asset (`GetAssetsAsync`) query that names ONE market or asset failed with a deserialization error on the live API (also in 0.4.0): Bitvavo answers it with a single JSON object where the query without a name returns an array, and the OpenAPI specification shows an array for both. Every collection now reads either shape.
- The institutional and atomic-cancel DELETE requests were signed without their body (see Changed).
- Live-only tests of the public surface (`BitvavoLivePublicTests`, skipped unless `BITVAVO_LIVE_PUBLIC=1`) now check the mapping against what Bitvavo actually answers — REST, the Shared API and a public WebSocket subscription.

### Not implemented
Public WebSocket `ticker`, `ticker24h` and `book` channels, `SymbolOrderBook`, trackers, a multi-user client provider, WebSocket request/response actions, WebSocket `error` event routing — see [ARCHITECTURE.md](ARCHITECTURE.md#9-not-implemented). Behaviour that cannot be verified without live credentials is listed there under "Not measured".

## [0.4.0] — 2026-05-23

### Added — P1 endpoint completeness
- `IBitvavoRestClientSpotApiAccount.GetTransactionHistoryAsync` — `GET /v2/account/history`
  (account ledger; Bitvavo API v2.5.0). Page-number pagination via the new
  `BitvavoTransactionHistory` / `BitvavoTransactionHistoryEntry` records.
- New `Report` sub-client (`IBitvavoRestClientSpotApiReport`) — MiCA regulatory reporting
  (Bitvavo API v2.9.0): `GetTradesReportAsync` (`GET /v2/report/{market}/trades`) and
  `GetBookReportAsync` (`GET /v2/report/{market}/book`). New records `BitvavoTradesReport`,
  `BitvavoBookReport`, `BitvavoBookReportEntry`.
- New `Institutional` sub-client (`IBitvavoRestClientSpotApiInstitutional`) — all 10
  institutional endpoints (Bitvavo API 2026-03-23): create / list subaccounts, create /
  get / list transfers, per-subaccount balance / transaction history / open orders, and
  subaccount single / bulk order cancellation. New records `BitvavoSubaccount`,
  `BitvavoSubaccountList`, `BitvavoSubaccountTransfer`, `BitvavoSubaccountTransferList`,
  `BitvavoSubaccountBalances`, `BitvavoCreateTransferRequest`,
  `BitvavoSubaccountCancelOrderRequest`; new enum `SubaccountTransferDirection`.
- 27 new test cases across `BitvavoRestClientSpotApiAccountTests`,
  `BitvavoRestClientSpotApiReportTests`, `BitvavoRestClientSpotApiInstitutionalTests`.

### Changed — P1-bis rate-limit gate coverage
- Every authenticated + public REST endpoint now passes through the per-host
  `RateLimitGate` (900 weight/min sliding, undercutting Bitvavo's 1000/min by a
  100-weight safety margin) with the Bitvavo-documented per-endpoint weight
  (most = 1; `balance` / `trades` / `*History` / report-trades = 5; `cancelOrders`
  & `ordersOpen` without a market filter = 100). Previously only 4 of 38 endpoints
  were gated.
- `RequestDefinitionCacheExtensions.GetOrCreateInUri` gained a gate-aware overload
  so the in-URI `DELETE` endpoints (`cancel-order`, `cancel-orders`) are gated too.
- `ResetCancelOnDisconnectAsync` (`POST /v2/cancelOrdersAfter`) is intentionally
  **left ungated** — the cancel-on-disconnect heartbeat must never be client-side
  rate-limited; its weight is absorbed by the safety margin.
- New package-icon: the official Bitvavo rounded-square mark (`icon.png`, 128×128).

### Added — P2 CryptoExchange.Net Shared-interface layer
- `BitvavoRestClientSpotApi` now implements the CryptoExchange.Net Shared REST
  interfaces — 14 in total: `IAssetsRestClient`, `IKlineRestClient`,
  `IRecentTradeRestClient`, `IOrderBookRestClient`, `ISpotSymbolRestClient`,
  `ISpotTickerRestClient`, `IBookTickerRestClient`, `IBalanceRestClient`,
  `ISpotOrderRestClient`, `ISpotOrderClientIdRestClient`, `IFeeRestClient`,
  `IDepositRestClient`, `IWithdrawalRestClient`, `IWithdrawRestClient`. New
  `IBitvavoRestClientSpotApiShared` facade interface and partial implementation
  `BitvavoRestClientSpotApi.Shared.cs`.
- `BitvavoSocketClientSpotApi` now implements the CryptoExchange.Net Shared socket
  interfaces — 4 in total: `IKlineSocketClient`, `ITradeSocketClient`,
  `ISpotOrderSocketClient`, `IUserTradeSocketClient`. New
  `IBitvavoSocketClientSpotApiShared` facade interface and partial implementation
  `BitvavoSocketClientSpotApi.Shared.cs`. The account-channel market set is supplied
  through the `ExchangeParameters` `Markets` escape hatch, since the Shared
  `SubscribeSpotOrderRequest` / `SubscribeUserTradeRequest` types carry no symbol set.
- The Shared layer lets Bitvavo be driven through the same exchange-agnostic
  abstractions as every other CryptoExchange.Net client (`Binance.Net`, `Kraken.Net`,
  etc.) without exchange-specific code at the call site.

### Changed — P1 endpoint completeness
- **Breaking:** `IBitvavoRestClientSpotApiTrading.CancelOrdersAsync` now takes a required
  leading `long operatorId` parameter — Bitvavo made `operatorId` mandatory on bulk-cancel
  (API v2.9.0). New signature: `CancelOrdersAsync(long operatorId, string? market = null,
  CancellationToken ct = default)`.
- **Breaking:** `codGroupId` is now typed as `int` (was `string`) — Bitvavo's API models it
  as a number. Affects `BitvavoCancelOrdersAfter.CodGroupId` and the
  `IBitvavoRestClientSpotApiAccount.ResetCancelOnDisconnectAsync` parameter.

### Added
- `services.AddBitvavo()` DI extension (transient `IBitvavoRestClient`, singleton
  `IBitvavoSocketClient`, optional `Action<BitvavoRestOptions>` /
  `Action<BitvavoSocketOptions>` configurators).
- `KlineInterval.OneWeek` (`"1W"`) and `KlineInterval.OneMonth` (`"1M"`) — Bitvavo wire
  tokens; capital letters distinguish these from minute (`"1m"`) and the initial erroneous
  `"1w"` that shipped with the first preview.
- `BitvavoWithdrawRequest.Internal` — opt-in flag for inter-account Bitvavo transfers
  (no on-chain / fiat-rail movement, no fee).
- `BitvavoDepositAddress.Description` — fiat (SEPA) deposit description, distinct from
  the memo-style `PaymentReference` used by memo-required cryptos.
- Per-host rate-limit gate on `BitvavoRestClientSpotApi` honouring
  `BitvavoExchange.WeightPerMinute` (1000/min). Wired on hot-path endpoints
  (`GetMarketsAsync`, `GetKlinesAsync`, `GetTicker24hAsync`, `PlaceOrderAsync`); other
  endpoints will be wired with documented per-call weights in v0.4.0.
- `RequestDefinitionCacheExtensions.GetOrCreateInUri` — centralised the `DELETE` +
  query-string pattern used by `CancelOrderAsync` and `CancelOrdersAsync`.
- 5 new test files: `BitvavoCredentialsTests`, `BitvavoServiceCollectionExtensionsTests`,
  `BitvavoSocketAccountFilterTests`, `BitvavoErrorParsingTests`, `KlineIntervalTests`
  (44 added test cases; 115 total).

### Changed
- `BitvavoCredentials.Copy()` is now a true deep copy — re-creates the inner
  `HMACCredential` from `(Key, Secret)` instead of sharing the reference.
- `KlineInterval` JSON converter swapped from `JsonStringEnumConverter` to
  `EnumConverter<KlineInterval>` so `[Map]` attributes drive both serialization and
  deserialization symmetrically.
- `BitvavoSocketSpotMessageHandler` topic mapping for `BitvavoStreamOrderUpdate` and
  `BitvavoStreamFillEvent` now uses `x => x.Market` (was `_ => string.Empty`), and
  `BitvavoAccountSubscription<T>` uses `MessageRouter.CreateWithTopicFilters` with the
  per-subscription market set. Two account subscriptions on disjoint markets no longer
  cross-leak each other's events.
- All test projects updated to xUnit v3 cancellation-token guidance — every async call
  now passes `TestContext.Current.CancellationToken` (or, where the underlying API uses
  a different parameter name, `cancellationToken:`). Build is now 0 warnings.
- `CryptoExchange.Net` dependency bumped from `11.1.0` → `11.1.1` (WS rate-limiter
  reset-on-disconnect fix; foundation for the new rate-limit gate above).
- Doc URLs in `IBitvavoRestClientSpotApiExchangeData` normalised from anchor form
  (`docs.bitvavo.com/#tag/...`) to pretty-path (`docs.bitvavo.com/docs/rest-api/...`).
- XML doc clarified on `BitvavoErrors.SpotMapping` and on records using `ArrayConverter<T>`
  (`BitvavoKline`, `BitvavoOrderBookEntry`) explaining why mutable setters are required.

### Deferred
- Per-endpoint weight wiring for non-hot endpoints (account/balance, deposit history,
  trade history, etc.) — landing in v0.4.0 with the per-call weight table from Bitvavo's
  docs.
- Per-code `ErrorInfo` entries in `BitvavoErrors.SpotMapping` — landing before v1.0.
- `<PackageIcon>` 128×128 PNG — required before JKorf NuGet submission; placeholder
  comment in `Bitvavo.Net.csproj`.

## [0.3.0-preview] — 2026-04-26
### Added
- WebSocket subscriptions for the private `account` channel (order updates, fill events).
- Authenticated WebSocket flow (`BitvavoSocketAuthQuery` + `BitvavoSocketAuthResponse`).
- REST coverage for the remaining public read endpoints + `POST /v2/withdrawal`.

## [0.2.0-preview] — 2026-04-26
### Added
- Full signed REST surface across Account, Trading, and Funding sub-clients (place/update/
  cancel order, balances, fees, withdrawal history, deposit address, etc.).

## [0.1.0-preview] — 2026-04-26
### Added
- JKorf-conform package skeleton: `BitvavoRestClient`, `BitvavoSocketClient`,
  `BitvavoCredentials`, `BitvavoEnvironment`, `BitvavoExchange` (display name +
  symbol formatting + per-minute weight constant).
- First live REST endpoint: `IBitvavoRestClientSpotApiExchangeData.GetMarketsAsync`.
- Test project bootstrap on xUnit v3 + Shouldly + NSubstitute.
