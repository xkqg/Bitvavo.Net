# Changelog

All notable changes to **Bitvavo.Net** are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to
[Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
