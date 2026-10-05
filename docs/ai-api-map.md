# Bitvavo.Net 0.5.0 AI API map — routes an intent to the exact client member, HTTP verb + path, auth need and rate-limit weight.

Rule: member, parameter or type not listed here → read `Bitvavo.Net/Interfaces/Clients/**` before generating code. Never invent endpoints. Never call Bitvavo over raw `HttpClient`, never sign by hand.

Facts: NuGet `Bitvavo.Net` 0.5.0 · net8.0, net9.0, net10.0 · built on CryptoExchange.Net 13.1.0 · Bitvavo is spot only · matches Bitvavo REST API docs v2.10.0 · REST base `https://api.bitvavo.com` (every path below sits under `/v2/`) · WebSocket `wss://ws.bitvavo.com/v2/` · no testnet, `BitvavoEnvironment.Live` is the only environment.

## Namespaces

| Namespace | Types |
|---|---|
| `Bitvavo.Net` | `BitvavoCredentials`, `BitvavoEnvironment` |
| `Bitvavo.Net.Clients` | `BitvavoRestClient`, `BitvavoSocketClient`, `BitvavoSharedApiClient` |
| `Bitvavo.Net.Interfaces.Clients` | `IBitvavoRestClient`, `IBitvavoSocketClient`, `IBitvavoSharedApiClient` |
| `Bitvavo.Net.Interfaces.Clients.SpotApi` | `IBitvavoRestClientSpotApi`, `IBitvavoRestClientSpotApiExchangeData`, `IBitvavoRestClientSpotApiAccount`, `IBitvavoRestClientSpotApiTrading`, `IBitvavoRestClientSpotApiFunding`, `IBitvavoRestClientSpotApiReport`, `IBitvavoRestClientSpotApiInstitutional`, `IBitvavoSocketClientSpotApi`, `IBitvavoSocketClientSpotApiExchangeData`, `IBitvavoSocketClientSpotApiAccount`, Shared aggregates `IBitvavoRestClientSpotSharedApi` (V2), `IBitvavoRestClientSpotApiShared` (V1), `IBitvavoSocketClientSpotSharedApi` (V2), `IBitvavoSocketClientSpotApiShared` (V1) |
| `Bitvavo.Net.Enums` | `KlineInterval`, `OrderSide`, `OrderType`, `OrderStatus`, `TimeInForce`, `SelfTradePrevention`, `TriggerType`, `TriggerReference`, `BitvavoMarketStatus`, `SubaccountTransferDirection` |
| `Bitvavo.Net.Objects.Models.Spot` | REST models and request records |
| `Bitvavo.Net.Objects.Models.Spot.Streams` | `BitvavoStreamCandleEvent`, `BitvavoStreamTrade`, `BitvavoStreamOrderUpdate`, `BitvavoStreamFillEvent` |
| `Bitvavo.Net.Objects.Options` | `BitvavoOptions`, `BitvavoRestOptions`, `BitvavoSocketOptions` |
| `Bitvavo.Net.Objects.Internal` | public types `BitvavoExchange`, `BitvavoRateLimiters` (the namespace is called Internal, the types are public) |
| `Bitvavo.Net.Extensions` | `BitvavoServiceCollectionExtensions` → `AddBitvavo` |
| `CryptoExchange.Net.Objects` | `HttpResult<T>`, `WebSocketResult<T>`, `Error`, `RateLimitingBehaviour`, `ClientRateLimitError`, `ServerRateLimitError` |
| `CryptoExchange.Net.Objects.Errors` | `ErrorType` |
| `CryptoExchange.Net.Objects.Sockets` | `UpdateSubscription`, `DataEvent<T>` |
| `CryptoExchange.Net.RateLimiting` | `RateLimitAdmission`, `RateLimitEvent`, `RateLimitUpdateEvent` |
| `CryptoExchange.Net.SharedApis` | Shared API types (`SharedSymbol`, requests, capability interfaces, `ExchangeParameters`) |
| `CryptoExchange.Net.Authentication` | `HMACCredential` |

## Client roots

| Intent | Use |
|---|---|
| REST calls | `new BitvavoRestClient()` → `client.SpotApi` |
| WebSocket streams | `new BitvavoSocketClient()` → `socketClient.SpotApi` |
| Sign requests | `options.ApiCredentials = new BitvavoCredentials(apiKey, apiSecret)` (also `new BitvavoCredentials(new HMACCredential(key, secret))`) |
| Environment | `BitvavoEnvironment.Live` (`SpotRestBaseAddress`, `SpotSocketPublicAddress`); lookup `BitvavoEnvironment.GetEnvironmentByName(name)`; no factory for custom environments |
| Change key at runtime | `client.SetApiCredentials(new BitvavoCredentials(key, secret))` (on `IBitvavoRestClient`, `IBitvavoSocketClient`, `BitvavoRestClient`, `BitvavoSocketClient`) |
| Dependency injection | `services.AddBitvavo()` · `services.AddBitvavo(IConfiguration)` · `services.AddBitvavo((BitvavoOptions o) => { ... })` |
| REST sub-clients | `SpotApi.ExchangeData`, `SpotApi.Account`, `SpotApi.Trading`, `SpotApi.Funding`, `SpotApi.Report`, `SpotApi.Institutional` |
| Socket sub-clients | `SpotApi.ExchangeData` (public), `SpotApi.Account` (private) |
| Exchange-agnostic Shared API V2 | `client.SpotApi.SharedApi`, `socketClient.SpotApi.SharedApi`, or `IBitvavoSharedApiClient` (`SpotRest`, `SpotSocket`, `GetCapability<T>()`) |
| Shared API V1 (legacy, kept) | `client.SpotApi.SharedClient`, `socketClient.SpotApi.SharedClient` |
| Exchange constants | `BitvavoExchange.ExchangeName` ("Bitvavo"), `BitvavoExchange.WeightPerMinute` (1000), `BitvavoExchange.RateLimiter`, `BitvavoExchange.Metadata`, `BitvavoExchange.FormatSymbol(baseAsset, quoteAsset, tradingMode)` → `BASE-QUOTE` upper case |

Every REST and subscribe method ends with `CancellationToken ct = default`. It is omitted from the signatures below. `?` after a parameter = optional (`= null`).

## REST — `client.SpotApi.ExchangeData` (public, never signed)

| Intent | Member | Verb + path | Auth | Weight | `Data` |
|---|---|---|---|---|---|
| All markets, tick sizes, limits, status | `GetMarketsAsync()` | GET `/v2/markets` | no | 1 | `IEnumerable<BitvavoMarket>` |
| Candles / OHLCV history | `GetKlinesAsync(market, interval, limit?, startTime?, endTime?)` | GET `/v2/{market}/candles` | no | 1 | `IEnumerable<BitvavoKline>` |
| Server time / clock offset | `GetServerTimeAsync()` | GET `/v2/time` | no | 1 | `BitvavoServerTime` |
| Assets, networks, deposit/withdraw status | `GetAssetsAsync(symbol?)` | GET `/v2/assets` | no | 1 | `IEnumerable<BitvavoAsset>` |
| Last traded price | `GetTickerPricesAsync(market?)` | GET `/v2/ticker/price` | no | 1 | `IEnumerable<BitvavoTickerPrice>` |
| Best bid and ask (top of book) | `GetTickerBookAsync(market?)` | GET `/v2/ticker/book` | no | 1 | `IEnumerable<BitvavoTickerBook>` |
| 24 h OHLCV + best quote | `GetTicker24hAsync(market?)` | GET `/v2/ticker/24h` | no | 25 without `market`, 1 with | `IEnumerable<BitvavoTicker24h>` |
| Order book snapshot | `GetOrderBookAsync(market, depth?)` | GET `/v2/{market}/book` | no | 1 | `BitvavoOrderBook` |
| Public trade tape | `GetPublicTradesAsync(market, limit?, startTime?, endTime?, tradeIdFrom?, tradeIdTo?)` | GET `/v2/{market}/trades` | no | 5 | `IEnumerable<BitvavoPublicTrade>` |

## REST — `client.SpotApi.Account` (signed)

| Intent | Member | Verb + path | Auth | Weight | `Data` |
|---|---|---|---|---|---|
| Fee tier (category A) and 30-day volume | `GetAccountInfoAsync()` | GET `/v2/account` | signed | 1 | `BitvavoAccountInfo` |
| Balances (all assets above zero, or one) | `GetBalancesAsync(symbol?)` | GET `/v2/balance` | signed | 5 | `IEnumerable<BitvavoBalance>` |
| Fee tier of a market or quote asset | `GetTradingFeesAsync(market?, quote?)` | GET `/v2/account/fees` | signed | 1 | `BitvavoMarketFee` |
| Fixed-staking balance | `GetStakingBalanceAsync(symbol?)` | GET `/v2/stakingBalance` | signed | 5 | `IEnumerable<BitvavoStakingBalance>` |
| Cancel-on-disconnect: create, refresh (heartbeat) or remove a group | `ResetCancelOnDisconnectAsync(codGroupId, expiryAfterSeconds)` | POST `/v2/cancelOrdersAfter` (body) | signed | documented 5, deliberately NOT counted by the client limiter | `BitvavoCancelOrdersAfter` |
| Account ledger (all transactions) | `GetTransactionHistoryAsync(fromDate?, toDate?, page?, maxItems?, type?)` | GET `/v2/account/history` | signed | 1 | `BitvavoTransactionHistory` |

## REST — `client.SpotApi.Trading` (signed)

| Intent | Member | Verb + path | Auth | Weight | `Data` |
|---|---|---|---|---|---|
| Place order | `PlaceOrderAsync(BitvavoPlaceOrderRequest request)` | POST `/v2/order` (body) | signed | 1 | `BitvavoOrder` |
| Update limit or untriggered trigger order | `UpdateOrderAsync(BitvavoUpdateOrderRequest request)` | PUT `/v2/order` (body) | signed | 1 | `BitvavoOrder` |
| Get one order | `GetOrderAsync(market, orderId?, clientOrderId?)` | GET `/v2/order` | signed | 1 | `BitvavoOrder` |
| Cancel one order | `CancelOrderAsync(market, operatorId, orderId?, clientOrderId?)` | DELETE `/v2/order` (query) | signed | 1 | `BitvavoOrderId` |
| Cancel all open orders (account or one market) | `CancelOrdersAsync(operatorId, market?)` | DELETE `/v2/orders` (query) | signed | 100 without `market`, 25 with | `IEnumerable<BitvavoOrderId>` |
| Cancel all buys or all sells of a market atomically | `CancelOrdersAtomicAsync(market, side, operatorId)` | DELETE `/v2/atomic/orders` (body) | signed | 100 | `IEnumerable<BitvavoOrderId>` |
| Open orders | `GetOpenOrdersAsync(market?, baseAsset?)` | GET `/v2/ordersOpen` | signed | 100 without `market`, 5 with | `IEnumerable<BitvavoOrder>` |
| Order history of a market | `GetOrderHistoryAsync(market, limit?, startTime?, endTime?, orderIdFrom?, orderIdTo?)` | GET `/v2/orders` | signed | 5 | `IEnumerable<BitvavoOrder>` |
| Own trades / fills of a market | `GetUserTradesAsync(market, limit?, startTime?, endTime?, tradeIdFrom?, tradeIdTo?, tradeId?)` | GET `/v2/trades` | signed | 5 | `IEnumerable<BitvavoFill>` |

`operatorId` is `long` and required on place, update, cancel, cancel-all, atomic cancel. Reads need none. Trigger orders (stop loss, take profit) are ordinary orders: `PlaceOrderAsync` with `OrderType.StopLoss`, `StopLossLimit`, `TakeProfit` or `TakeProfitLimit` + `TriggerAmount` (+ `TriggerType`, `TriggerReference`).

## REST — `client.SpotApi.Funding` (signed)

| Intent | Member | Verb + path | Auth | Weight | `Data` |
|---|---|---|---|---|---|
| Deposit address or bank details | `GetDepositAddressAsync(symbol)` | GET `/v2/deposit` | signed | 1 | `BitvavoDepositAddress` |
| Deposit history | `GetDepositHistoryAsync(symbol?, limit?, startTime?, endTime?)` | GET `/v2/depositHistory` | signed | 5 | `IEnumerable<BitvavoDepositHistoryEntry>` |
| Withdrawal history | `GetWithdrawalHistoryAsync(symbol?, limit?, startTime?, endTime?)` | GET `/v2/withdrawalHistory` | signed | 5 | `IEnumerable<BitvavoWithdrawalHistoryEntry>` |
| Withdraw to address or IBAN (also internal transfer) | `WithdrawAsync(BitvavoWithdrawRequest request)` | POST `/v2/withdrawal` (body) | signed | 1 | `BitvavoWithdrawalResult` |
| Withdraw crypto over a named network | `WithdrawCryptoAsync(BitvavoCryptoWithdrawRequest request)` | POST `/v2/crypto/withdrawal` (body) | signed | 25 | `BitvavoCryptoWithdrawal` |

Both withdraw calls move funds. Bitvavo disables 2FA and the e-mail address confirmation for API withdrawals: the `withdraw` permission of the API key is the only gate.

## REST — `client.SpotApi.Report` (MiCA, public, never signed)

| Intent | Member | Verb + path | Auth | Weight | `Data` |
|---|---|---|---|---|---|
| MiCA trade report | `GetTradesReportAsync(market, limit?, startTime?, endTime?, tradeIdFrom?, tradeIdTo?)` | GET `/v2/report/{market}/trades` | no | 5 | `IEnumerable<BitvavoTradesReport>` |
| MiCA order-book report | `GetBookReportAsync(market, depth?)` | GET `/v2/report/{market}/book` | no | 1 | `BitvavoBookReport` |

## REST — `client.SpotApi.Institutional` (signed; main-account key with `Include all subaccounts`, `Internal Transfer`, `Administrative`)

| Intent | Member | Verb + path | Auth | Weight | `Data` |
|---|---|---|---|---|---|
| Create subaccount | `CreateSubaccountAsync(label?)` | POST `/v2/subaccounts` (body) | signed | 5 | `BitvavoSubaccount` |
| List subaccounts | `GetSubaccountsAsync(page?, maxItems?)` | GET `/v2/subaccounts` | signed | 5 | `BitvavoSubaccountList` |
| Transfer main ↔ sub | `CreateTransferAsync(BitvavoCreateTransferRequest request)` | POST `/v2/subaccounts/transfers` (body) | signed | 5 | `BitvavoSubaccountTransfer` |
| One transfer | `GetTransferAsync(transferId)` | GET `/v2/subaccounts/transfers/{transferId}` | signed | 5 | `BitvavoSubaccountTransfer` |
| Transfers of a subaccount | `GetTransfersAsync(subaccountId, clientRequestId?, startTime?, endTime?, limit?)` | GET `/v2/subaccounts/transfers` | signed | 5 | `BitvavoSubaccountTransferList` |
| Balances of a subaccount (null = main account) | `GetSubaccountBalancesAsync(subaccountId?, symbol?)` | GET `/v2/institutional/subaccounts/balance` | signed | 5 | `BitvavoSubaccountBalances` |
| Ledger of a subaccount | `GetSubaccountTransactionHistoryAsync(subaccountId?, fromDate?, toDate?, page?, maxItems?, type?)` | GET `/v2/institutional/subaccounts/history` | signed | 5 | `BitvavoTransactionHistory` |
| Open orders of a subaccount | `GetSubaccountOpenOrdersAsync(subaccountId?, market?, baseAsset?)` | GET `/v2/institutional/subaccounts/orders/open` | signed | 100 without `market`, 5 with | `IEnumerable<BitvavoOrder>` |
| Cancel one order of a subaccount | `CancelSubaccountOrderAsync(BitvavoSubaccountCancelOrderRequest request)` | DELETE `/v2/institutional/subaccounts/order` (body) | signed | 1 | `BitvavoOrderId` |
| Cancel all orders of a subaccount | `CancelSubaccountOrdersAsync(operatorId, subaccountId?, market?)` | DELETE `/v2/institutional/subaccounts/orders` (body) | signed | 100 without `market`, 25 with | `IEnumerable<BitvavoOrderId>` |

## REST wire conventions

- Wire name = C# name in lower camel case (`tradeIdFrom`, `orderIdTo`, `fromDate`, `maxItems`, `operatorId`, `orderType`, ...), except `startTime` → `start`, `endTime` → `end`, `baseAsset` → `base`.
- `DateTime` travels as unix milliseconds. `decimal` travels as a JSON string (`"amount":"0.5"`). Enums travel as their wire strings (see Enums). Response numbers arrive as strings and parse into `decimal`.
- Query strings and JSON bodies are sorted ordinally and case-insensitively; the HMAC signs exactly those bytes.
- Signature: HMAC-SHA256, lower-case hex, over `timestamp + METHOD + "/v2/path[?query]" + body`. Headers `Bitvavo-Access-Key`, `Bitvavo-Access-Signature`, `Bitvavo-Access-Timestamp`, `Bitvavo-Access-Window` (`ReceiveWindowMs`, default 10000, Bitvavo max 60000).
- Only signed endpoints carry the headers. Public endpoints stay unsigned even when credentials are set, and count against the per-IP budget.
- No automatic pagination in the typed API: page with `startTime` / `endTime`, `tradeIdFrom` / `tradeIdTo`, `orderIdFrom` / `orderIdTo`, or `page` / `maxItems` (ledger, subaccounts). Bitvavo limits: trades and own trades window at most 24 h; candles `limit` 1–1440; order book depth 1–1000; histories 1–1000 (default 500); ledger `maxItems` 1–100.
- Candle `startTime` / `endTime` must align with the interval boundary (Bitvavo).
- A ticker query that names one market (`GetTicker24hAsync`, `GetTickerBookAsync`, `GetTickerPricesAsync`) is answered by Bitvavo with a single JSON object where the unfiltered query returns an array. The library reads both into `IEnumerable<T>`: with a market filter the collection holds one item.

## WebSocket — `socketClient.SpotApi.ExchangeData` (public)

All return `Task<WebSocketResult<UpdateSubscription>>`; handler is `Action<DataEvent<T>>`; `ct` cancels (closes) the subscription. Weight: a subscribe frame is weight 1 at Bitvavo (the same for every channel and number of markets); the client limiter does not count WebSocket frames.

| Intent | Member | Channel | Auth | Event `T` (`update.Data`) |
|---|---|---|---|---|
| Candle updates of one market and interval | `SubscribeToKlineUpdatesAsync(market, interval, onMessage)` | `candles` | no | `BitvavoStreamCandleEvent` (`Candle` is an array of `BitvavoKline`) |
| Public trades of one market | `SubscribeToTradeUpdatesAsync(string market, onMessage)` | `trades` | no | `BitvavoStreamTrade` |
| Public trades of several markets on one subscription | `SubscribeToTradeUpdatesAsync(IEnumerable<string> markets, onMessage)` | `trades` | no | `BitvavoStreamTrade` (dispatch on `Market`) |

## WebSocket — `socketClient.SpotApi.Account` (private `account` channel, authenticated per connection)

| Intent | Member | Event | Auth | Event `T` |
|---|---|---|---|---|
| Own order state changes | `SubscribeToOrderUpdatesAsync(string[] markets, onMessage)` | `order` | signed | `BitvavoStreamOrderUpdate` |
| Own fills / trades | `SubscribeToFillUpdatesAsync(string[] markets, onMessage)` | `fill` | signed | `BitvavoStreamFillEvent` |

- Subscribed per market; `markets` is required. Both members use the same Bitvavo channel (`account`) and differ only by the event type they route.
- The framework sends the `authenticate` action (key, signature, timestamp, window) before the first private subscribe on a connection. Missing credentials → `NoApiCredentialsError`.
- A subscribe counts as successful once it is sent: Bitvavo acknowledges subscribes only in an aggregated event. A wrong market name is therefore not reported by `Success`.
- No ordering guarantee between `order` and `fill` events (Bitvavo).
- Lifecycle: `socketClient.UnsubscribeAsync(sub.Data)`, `socketClient.UnsubscribeAllAsync()`, `sub.Data.CloseAsync()`, `socketClient.CurrentSubscriptions`, `socketClient.CurrentConnections`. `UpdateSubscription` events: `ConnectionLost`, `ConnectionRestored`, `ConnectionClosed`, `ResubscribingFailed`, `Exception`, `SubscriptionStatusChanged`.
- `DataEvent<T>`: `Data`, `ReceiveTime`, `Symbol`, `StreamId`, `OriginalData`.

## Shared API V2 — REST capabilities (`IBitvavoRestClientSpotSharedApi`, 32)

Entry points: `client.SpotApi.SharedApi`, `IBitvavoSharedApiClient.SpotRest`, `IBitvavoSharedApiClient.GetCapability<T>()`. A capability interface whose name ends in `Rest` returns `HttpResult<T>`; its transport-neutral twin (same name without `Rest`, for example `IGetKlines`) returns `IExchangeCallResult<T>`. Symbols: `new SharedSymbol(TradingMode.Spot, "ETH", "EUR")` → market `ETH-EUR`.

`IBitvavoSharedApiClient` (from `ISharedApiClientBase`): `GetCapability<T>(SharedTransport? transport = null)` → lookup result `SharedCapabilityResolution<T>` (`Capability`, `Transport`, `Exchange`, `Options`) or null when no registered capability matches; `GetCapabilities<T>()`, `Discover()`, `SharedApis`, `PreferredTransport` (`BitvavoOptions.SharedApi.PreferredTransport`, default REST), `Exchange`, `UnsubscribeAllAsync()`.

| Capability | Method | Typed call behind it | Auth | Exchange parameter | Weight |
|---|---|---|---|---|---|
| `IGetAssetRest` | `GetAssetAsync(GetAssetRequest)` | `ExchangeData.GetAssetsAsync(symbol)` | no | — | 1 |
| `IGetAllAssetsRest` | `GetAllAssetsAsync(GetAssetsRequest)` | `ExchangeData.GetAssetsAsync()` | no | — | 1 |
| `IGetKlinesRest` | `GetKlinesAsync(GetKlinesRequest, PageRequest?)` | `ExchangeData.GetKlinesAsync` | no | — | 1 |
| `IGetBalancesRest` | `GetBalancesAsync(GetBalancesRequest)` | `Account.GetBalancesAsync()` | signed | — | 5 |
| `IGetFeesRest` | `GetFeesAsync(GetFeeRequest)` | `Account.GetTradingFeesAsync(market)` | signed | — | 1 |
| `IGetLedgerRest` | `GetLedgerAsync(GetLedgerRequest, PageRequest?)` | `Account.GetTransactionHistoryAsync` | signed | — | 1 |
| `IGetDepositAddressesRest` | `GetDepositAddressesAsync(GetDepositAddressesRequest)` | `Funding.GetDepositAddressAsync` | signed | — | 1 |
| `IGetDepositHistoryRest` | `GetDepositHistoryAsync(GetDepositsRequest, PageRequest?)` | `Funding.GetDepositHistoryAsync` | signed | — | 5 |
| `IGetWithdrawalHistoryRest` | `GetWithdrawalHistoryAsync(GetWithdrawalsRequest, PageRequest?)` | `Funding.GetWithdrawalHistoryAsync` | signed | — | 5 |
| `IWithdrawRest` | `WithdrawAsync(WithdrawRequest)` | `Funding.WithdrawCryptoAsync` | signed | — (request `Network` required) | 25 |
| `IPlaceSpotOrderRest` | `PlaceSpotOrderAsync(PlaceSpotOrderRequest)`, `GenerateClientOrderId()` | `Trading.PlaceOrderAsync` | signed | `OperatorId` (`long`, required) | 1 |
| `IGetSpotOrderRest` | `GetSpotOrderAsync(GetOrderRequest)` | `Trading.GetOrderAsync(market, orderId:)` | signed | — | 1 |
| `IGetSpotOrderByClientOrderIdRest` | `GetSpotOrderByClientOrderIdAsync(GetOrderRequest)` | `Trading.GetOrderAsync(market, clientOrderId:)` | signed | — | 1 |
| `IGetOpenSpotOrdersRest` | `GetOpenSpotOrdersAsync(GetOpenOrdersRequest)` | `Trading.GetOpenOrdersAsync(market?)` | signed | — | 100 without symbol, 5 with |
| `IGetClosedSpotOrdersRest` | `GetClosedSpotOrdersAsync(GetClosedOrdersRequest, PageRequest?)` | `Trading.GetOrderHistoryAsync` | signed | — | 5 |
| `IGetSpotOrderTradesRest` | `GetSpotOrderTradesAsync(GetOrderTradesRequest)` | `Trading.GetOrderAsync` (embedded `Fills`) | signed | — | 1 |
| `IGetSpotUserTradeHistoryRest` | `GetSpotUserTradeHistoryAsync(GetUserTradesRequest, PageRequest?)` | `Trading.GetUserTradesAsync` | signed | — | 5 |
| `ICancelSpotOrderRest` | `CancelSpotOrderAsync(CancelOrderRequest)` | `Trading.CancelOrderAsync(market, operatorId, orderId:)` | signed | `OperatorId` (required) | 1 |
| `ICancelSpotOrderByClientOrderIdRest` | `CancelSpotOrderByClientOrderIdAsync(CancelOrderRequest)` | `Trading.CancelOrderAsync(market, operatorId, clientOrderId:)` | signed | `OperatorId` (required) | 1 |
| `IGetSpotSymbolsRest` | `GetSpotSymbolsAsync(GetSymbolsRequest)`, `SpotSymbolCatalog` | `ExchangeData.GetMarketsAsync` | no | — | 1 |
| `IGetTickerRest` | `GetTickerAsync(GetTickerRequest)` | `ExchangeData.GetTicker24hAsync(market)` | no | — | 1 |
| `IGetAllTickersRest` | `GetAllTickersAsync(GetTickersRequest)` | `ExchangeData.GetTicker24hAsync()` | no | — | 25 |
| `IGetOrderBookRest` | `GetOrderBookAsync(GetOrderBookRequest)` | `ExchangeData.GetOrderBookAsync` | no | — | 1 |
| `IGetBookTickerRest` | `GetBookTickerAsync(GetBookTickerRequest)` | `ExchangeData.GetTickerBookAsync(market)` | no | — | 1 |
| `IGetRecentTradesRest` | `GetRecentTradesAsync(GetRecentTradesRequest)` | `ExchangeData.GetPublicTradesAsync` | no | — | 5 |
| `ICancelAllSpotOrdersRest` | `CancelAllSpotOrdersAsync(CancelAllOrdersRequest)` | `Trading.CancelOrdersAsync(operatorId)` | signed | `OperatorId` (required) | 100 |
| `ICancelAllSpotSymbolOrdersRest` | `CancelAllSpotSymbolOrdersAsync(CancelAllSymbolOrdersRequest)` | `Trading.CancelOrdersAsync(operatorId, market)` | signed | `OperatorId` (required) | 25 |
| `IEditSpotOrderRest` | `EditSpotOrderAsync(EditOrderRequest)` | `Trading.UpdateOrderAsync` | signed | `OperatorId` (required) | 1 |
| `IEditSpotOrderByClientOrderIdRest` | `EditSpotOrderByClientOrderIdAsync(EditOrderRequest)` | `Trading.UpdateOrderAsync` | signed | `OperatorId` (required) | 1 |
| `IPlaceSpotTriggerOrderRest` | `PlaceSpotTriggerOrderAsync(PlaceSpotTriggerOrderRequest)` | `Trading.PlaceOrderAsync` (stop / take-profit type) | signed | `OperatorId` (required), `TriggerReference` (optional) | 1 |
| `IGetSpotTriggerOrderRest` | `GetSpotTriggerOrderAsync(GetOrderRequest)` | `Trading.GetOrderAsync` | signed | — | 1 |
| `ICancelSpotTriggerOrderRest` | `CancelSpotTriggerOrderAsync(CancelOrderRequest)` | `Trading.CancelOrderAsync(market, operatorId, orderId:)` | signed | `OperatorId` (required) | 1 |

Capability notes:
- Exchange parameters travel in the request's `ExchangeParameters`: `new ExchangeParameters(new ExchangeParameter(BitvavoExchange.ExchangeName, "OperatorId", 1L))`. The value type is `long` (write `1L`). A request without `OperatorId` is rejected with `ArgumentError` before anything is sent. Default for all requests: `api.SetDefaultExchangeParameter("OperatorId", 1L)`, undo with `api.ResetDefaultExchangeParameters()`.
- `TriggerReference` takes a `TriggerReference` enum value; default `LastTrade`. Trigger type follows side + `SharedTriggerPriceDirection` + limit price: sell + `PriceBelow` = stop loss, sell + `PriceAbove` = take profit, buy + `PriceAbove` = stop loss, buy + `PriceBelow` = take profit; with `OrderPrice` it is the `...Limit` variant. Quantity in the quote asset only without a limit price. The firing direction of stop loss and take profit is the usual meaning, assumed, not measured against the live API.
- Order placing: `SharedOrderType.Limit`, `Market`, `LimitMaker` (limit + post-only); `SharedTimeInForce.GoodTillCanceled`, `ImmediateOrCancel`, `FillOrKill`; limit quantity in base asset; market quantity base or quote (`SharedQuantity.Base(x)`, `SharedQuantity.Quote(x)`). `SharedOrderType.Other` is never placed (stop/take-profit go through the trigger capabilities). `GenerateClientOrderId()` returns a UUID.
- By-client-id capabilities carry the client order id in the request's `OrderId` field.
- Edit: new total `Quantity` and/or new `Price`, at least one; market orders cannot be edited.
- Cancel-all capabilities return `HttpResult` without order ids.
- Klines: 13 intervals (`SharedKlineInterval` OneMinute … OneMonth, no ThreeMinutes), max 1440 per request, newest first. Order book 1–1000 levels, `Nonce` is the sequence number. Fees are percentages (fraction × 100).
- Balances: spot account only (`SharedAccountType.Spot`); another account type is rejected. `SharedBalance.Total` = available + the amount in orders (`BitvavoBalance.InOrder`).
- Book ticker: a side without orders shows price 0 and no quantity.
- Ledger: max 100 transactions per page; a trade is two entries sharing `RelationId`; the fee is no entry of its own; the asset filter is applied per fetched page.
- Closed orders: Bitvavo's history also lists open orders; they are dropped, so a page can be shorter than the limit.
- Deposit addresses: a request naming a network is rejected; fiat assets answer an empty list.
- Withdraw: network required, fee charged on top, `AddressTag` → memo, returned id is Bitvavo's withdrawal id.
- Symbols: `GetSpotSymbolsAsync` fills the process-wide symbol cache behind `SpotSymbolCatalog`; price grid = `PriceStep` / `PriceDecimals` from `tickSize`; `PriceSignificantFigures` is never set.

## Shared API V2 — WebSocket capabilities (`IBitvavoSocketClientSpotSharedApi`, 4)

Entry points: `socketClient.SpotApi.SharedApi`, `IBitvavoSharedApiClient.SpotSocket`. All return `WebSocketResult<UpdateSubscription>`; handlers take `DataEvent<T>`.

| Capability | Method | Behind it | Auth | Exchange parameter | `T` |
|---|---|---|---|---|---|
| `ISubscribeKlinesSocket` | `SubscribeToKlineUpdatesAsync(SubscribeKlineRequest, handler)` | `ExchangeData.SubscribeToKlineUpdatesAsync` | no | — (one symbol only; `Symbols` not supported; each candle of an event is its own call) | `SharedKline` |
| `ISubscribeTradesSocket` | `SubscribeToTradeUpdatesAsync(SubscribeTradeRequest, handler)` | `ExchangeData.SubscribeToTradeUpdatesAsync` | no | — (several symbols allowed) | `SharedTrade[]` |
| `ISubscribeSpotOrdersSocket` | `SubscribeToSpotOrderUpdatesAsync(SubscribeSpotOrderRequest, handler)` | `Account.SubscribeToOrderUpdatesAsync` | signed | `Markets` (`string[]`, required) | `SharedSpotOrderUpdate[]` |
| `ISubscribeUserTradesSocket` | `SubscribeToUserTradeUpdatesAsync(SubscribeUserTradeRequest, handler)` | `Account.SubscribeToFillUpdatesAsync` | signed | `Markets` (`string[]`, required) | `SharedUserTrade[]` |

`Markets` example: `new ExchangeParameters(new ExchangeParameter(BitvavoExchange.ExchangeName, "Markets", new[] { "ETH-EUR" }))` passed as `new SubscribeSpotOrderRequest(markets)` or `new SubscribeUserTradeRequest(exchangeParameters: markets)`. `UnsubscribeAllAsync()` closes every subscription of the socket API.

## Shared API V1 (legacy aggregates, same instance as V2)

- REST `IBitvavoRestClientSpotApiShared` (14): `IAssetsRestClient`, `IKlineRestClient`, `IBalanceRestClient`, `IFeeRestClient`, `IDepositRestClient`, `IWithdrawalRestClient`, `IWithdrawRestClient`, `ISpotOrderRestClient`, `ISpotOrderClientIdRestClient`, `ISpotSymbolRestClient`, `ISpotTickerRestClient`, `IOrderBookRestClient`, `IBookTickerRestClient`, `IRecentTradeRestClient`.
- Socket `IBitvavoSocketClientSpotApiShared` (4): `IKlineSocketClient`, `ITradeSocketClient`, `ISpotOrderSocketClient`, `IUserTradeSocketClient`.
- V1 names that differ from V2: `GetAssetsAsync` = V2 `GetAllAssetsAsync`; `GetDepositsAsync` = `GetDepositHistoryAsync`; `GetWithdrawalsAsync` = `GetWithdrawalHistoryAsync`; `GetSpotUserTradesAsync` = `GetSpotUserTradeHistoryAsync`; `GetSpotTickerAsync` / `GetSpotTickersAsync` = `GetTickerAsync` / `GetAllTickersAsync` (answer `SharedSpotTicker`).
- V1-only helpers on `ISpotSymbolRestClient`: `GetSpotSymbolsForBaseAssetAsync(baseAsset)`, `SupportsSpotSymbolAsync(symbol)` → `ExchangeCallResult<T>`.
- No ledger, edit, cancel-all or trigger capability in V1. No balance subscription anywhere.

## Models index

Request records (positional; named arguments recommended):

| Record | Parameters in order |
|---|---|
| `BitvavoPlaceOrderRequest` | `Market`, `Side`, `OrderType`, `OperatorId`, `Amount?`, `AmountQuote?`, `Price?`, `TriggerAmount?`, `TriggerType?`, `TriggerReference?`, `TimeInForce?`, `PostOnly?`, `SelfTradePrevention?`, `ResponseRequired?`, `ClientOrderId?`, `CodGroupId?`, `DisableMarketProtection?` |
| `BitvavoUpdateOrderRequest` | `Market`, `OperatorId`, `OrderId?`, `ClientOrderId?`, `Amount?`, `AmountQuote?`, `Price?`, `TriggerAmount?`, `TimeInForce?`, `SelfTradePrevention?`, `PostOnly?`, `ResponseRequired?`, `AmountRemaining?` |
| `BitvavoWithdrawRequest` | `Symbol`, `Amount`, `Address`, `PaymentId?`, `AddWithdrawalFee?`, `Internal?` |
| `BitvavoCryptoWithdrawRequest` | `Asset`, `Network`, `Address`, `Amount`, `DeductFeeFromAmount?`, `IdempotencyKey?`, `Memo?` |
| `BitvavoCreateTransferRequest` | `SubaccountId`, `Direction`, `Symbol`, `Amount`, `ClientRequestId?` |
| `BitvavoSubaccountCancelOrderRequest` | `Market`, `OrderId`, `OperatorId`, `SubaccountId?`, `ClientOrderId?` |

Response models (`decimal?` unless noted; UTC `DateTime`):

| Model | From | Members |
|---|---|---|
| `BitvavoMarket` | `GetMarketsAsync` | `Market`, `Status` (`BitvavoMarketStatus?`), `BaseAsset`, `QuoteAsset`, `PricePrecision` (`int?`, null on every live market), `MinOrderInBaseAsset`, `MinOrderInQuoteAsset`, `MaxOrderInBaseAsset`, `MaxOrderInQuoteAsset` (strings), `OrderTypes`, `QuantityDecimals`, `NotionalDecimals`, `TickSize`, `MaxOpenOrders`, `FeeCategory` |
| `BitvavoKline` | `GetKlinesAsync`, candle event | `OpenTime`, `OpenPrice`, `HighPrice`, `LowPrice`, `ClosePrice`, `Volume` (non-nullable `decimal`; wire is a positional array) |
| `BitvavoServerTime` | `GetServerTimeAsync` | `Time`, `TimeNs` |
| `BitvavoAsset` | `GetAssetsAsync` | `Symbol`, `Name`, `Decimals`, `DepositFee`, `DepositConfirmations`, `DepositStatus`, `WithdrawalFee`, `WithdrawalMinAmount`, `WithdrawalStatus`, `Networks`, `Message` |
| `BitvavoTickerPrice` | `GetTickerPricesAsync` | `Market`, `Price` |
| `BitvavoTickerBook` | `GetTickerBookAsync` | `Market`, `Bid`, `BidSize`, `Ask`, `AskSize` |
| `BitvavoTicker24h` | `GetTicker24hAsync` | `Market`, `StartTimestamp`, `Timestamp`, `Open`, `OpenTimestamp`, `High`, `Low`, `Last`, `CloseTimestamp`, `Bid`, `BidSize`, `Ask`, `AskSize`, `Volume`, `VolumeQuote` |
| `BitvavoOrderBook` / `BitvavoOrderBookEntry` | `GetOrderBookAsync` | `Market`, `Nonce`, `Bids`, `Asks`, `TimestampNs`; entry `Price`, `Size` (wire `[price, size]`) |
| `BitvavoPublicTrade` | `GetPublicTradesAsync` | `Id`, `Timestamp`, `Amount`, `Price`, `Side` |
| `BitvavoAccountInfo` / `BitvavoFeeTier` | `GetAccountInfoAsync` | `Fees` (`Taker`, `Maker`, `Volume`), `Capabilities` (not in the v2.10.0 spec response; empty list when absent) |
| `BitvavoBalance` | `GetBalancesAsync` | `Symbol`, `Available`, `InOrder` |
| `BitvavoMarketFee` | `GetTradingFeesAsync` | `Tier`, `Volume`, `Taker`, `Maker` (fractions, 0.0015 = 0.15 %) |
| `BitvavoStakingBalance` | `GetStakingBalanceAsync` | `Symbol`, `Amount` |
| `BitvavoCancelOrdersAfter` | `ResetCancelOnDisconnectAsync` | `CodGroupId` (`int`), `TimeOfExpirySeconds` (unix seconds) |
| `BitvavoTransactionHistory` / `BitvavoTransactionHistoryEntry` | ledger | `Items`, `CurrentPage`, `TotalPages`, `MaxItems`; entry `TransactionId`, `ExecutedAt`, `Type`, `PriceCurrency`, `PriceAmount`, `SentCurrency`, `SentAmount`, `ReceivedCurrency`, `ReceivedAmount`, `FeesCurrency`, `FeesAmount`, `Address` |
| `BitvavoOrder` | place, update, get, open, history | `OrderId`, `ClientOrderId`, `Market`, `Created`, `Updated`, `Status`, `Side`, `OrderType`, `Amount`, `AmountRemaining`, `Price`, `AmountQuote`, `AmountQuoteRemaining`, `FilledAmount`, `FilledAmountQuote`, `FeePaid`, `FeeCurrency`, `Fills`, `TimeInForce`, `PostOnly`, `SelfTradePrevention`, `Visible`, `TriggerAmount`, `TriggerPrice`, `TriggerType`, `TriggerReference`, `OperatorId`, `CodGroupId`, `OnHold`, `OnHoldCurrency`, `DisableMarketProtection`, `RestatementReason`, `CreatedNs`, `UpdatedNs` |
| `BitvavoFill` | `GetUserTradesAsync`, `BitvavoOrder.Fills` | `Id`, `OrderId`, `ClientOrderId`, `Timestamp`, `Market`, `Side`, `Amount`, `Price`, `Taker`, `Fee`, `FeeCurrency`, `Settled`, `OperatorId` (`OrderId`, `Market`, `Side` only on the standalone form) |
| `BitvavoOrderId` | cancel calls | `OrderId`, `Market` |
| `BitvavoDepositAddress` | `GetDepositAddressAsync` | `Address`, `Iban`, `Bic`, `PaymentReference`, `Description` |
| `BitvavoDepositHistoryEntry` | deposit history | `Timestamp`, `Symbol`, `Amount`, `Address`, `PaymentReference`, `TxId`, `Fee`, `Status` |
| `BitvavoWithdrawalHistoryEntry` | withdrawal history | `Timestamp`, `Symbol`, `Amount`, `Address`, `PaymentReference`, `TxId`, `Fee`, `Status` |
| `BitvavoWithdrawalResult` | `WithdrawAsync` | `Success`, `Symbol`, `Amount` |
| `BitvavoCryptoWithdrawal` | `WithdrawCryptoAsync` | `Id`, `Asset`, `Network`, `Address`, `Amount`, `Fee`, `CreatedAt` |
| `BitvavoTradesReport` | `GetTradesReportAsync` | `TradeId`, `TransactTimestamp`, `AssetCode`, `AssetName`, `Price`, `MissingPrice`, `PriceNotation`, `PriceCurrency`, `Quantity`, `QuantityCurrency`, `QuantityNotation`, `Venue`, `PublicationTimestamp`, `PublicationVenue` |
| `BitvavoBookReport` / `BitvavoBookReportEntry` | `GetBookReportAsync` | `SubmissionTimestamp`, `AssetCode`, `AssetName`, `PriceCurrency`, `PriceNotation`, `QuantityCurrency`, `QuantityNotation`, `Venue`, `TradingSystem`, `PublicationTimestamp`, `Bids`, `Asks`; entry `Side`, `Price`, `Quantity`, `NumOrders` |
| `BitvavoSubaccount` / `BitvavoSubaccountList` | subaccount calls | `Id`, `Type`, `Status`, `Label`; list `Items`, `CurrentPage`, `TotalPages`, `MaxItems` |
| `BitvavoSubaccountTransfer` / `BitvavoSubaccountTransferList` | transfer calls | `TransferId`, `ClientRequestId`, `SubaccountId`, `Direction`, `Symbol`, `Amount`, `Status`, `CreatedAt`; list `Items`, `Start`, `End`, `Limit` |
| `BitvavoSubaccountBalances` | `GetSubaccountBalancesAsync` | `Balances` (`BitvavoBalance` list) |
| `BitvavoStreamCandleEvent` | candle stream | `Event`, `Market`, `Interval`, `Candle` |
| `BitvavoStreamTrade` | trade stream | `Event`, `Timestamp`, `Market`, `Id`, `Amount`, `Price`, `Side`, `TimestampNs` |
| `BitvavoStreamOrderUpdate` | order stream | `Event`, `OrderId`, `ClientOrderId`, `Market`, `Created`, `Updated`, `Status`, `Side`, `OrderType`, `Amount`, `AmountRemaining`, `Price`, `OnHold`, `OnHoldCurrency`, `TriggerAmount`, `TriggerPrice`, `TriggerType`, `TriggerReference`, `TimeInForce`, `PostOnly`, `SelfTradePrevention`, `Visible`, `FilledAmount`, `FilledAmountQuote`, `ExecutionType`, `RestatementReason`, `CodGroupId`, `OperatorId`, `CreatedNs`, `UpdatedNs` |
| `BitvavoStreamFillEvent` | fill stream | `Event`, `Market`, `OrderId`, `ClientOrderId`, `FillId`, `Timestamp`, `Amount`, `Side`, `Price`, `Taker`, `Fee`, `FeeCurrency`, `OperatorId`, `TimestampNs` |
| `BitvavoSharedOrderBookEntry` | Shared order book | `Price`, `Quantity` (implements `ISymbolOrderBookEntry`) |

## Enums (`Bitvavo.Net.Enums`) and wire values

A wire value is shown after the member where it is not simply the member name in lower camel case.

| Enum | Values → wire |
|---|---|
| `KlineInterval` | `OneMinute` 1m, `FiveMinutes` 5m, `FifteenMinutes` 15m, `ThirtyMinutes` 30m, `OneHour` 1h, `TwoHours` 2h, `FourHours` 4h, `SixHours` 6h, `EightHours` 8h, `TwelveHours` 12h, `OneDay` 1d, `OneWeek` 1W, `OneMonth` 1M (capitals: 1m is one minute, 1M is one month) |
| `OrderSide` | `Buy` buy, `Sell` sell |
| `OrderType` | `Market` market, `Limit` limit, `StopLoss` stopLoss, `StopLossLimit` stopLossLimit, `TakeProfit` takeProfit, `TakeProfitLimit` takeProfitLimit |
| `OrderStatus` | `New` new, `AwaitingTrigger` awaitingTrigger, `Canceled` canceled, `CanceledAuction`, `CanceledSelfTradePrevention`, `CanceledIoc` canceledIOC, `CanceledFok` canceledFOK, `CanceledMarketProtection`, `CanceledPostOnly`, `Filled` filled, `PartiallyFilled` partiallyFilled, `Expired` expired, `Rejected` rejected |
| `TimeInForce` | `GoodTillCanceled` GTC, `ImmediateOrCancel` IOC, `FillOrKill` FOK |
| `SelfTradePrevention` | `DecrementAndCancel`, `CancelOldest`, `CancelNewest`, `CancelBoth` |
| `TriggerType` | `Price` |
| `TriggerReference` | `LastTrade` lastTrade, `BestBid` bestBid, `BestAsk` bestAsk, `MidPrice` midPrice |
| `BitvavoMarketStatus` | `Trading`, `Halted`, `Auction`, `AuctionMatching`, `CancelOnly` (Bitvavo: `Trading` open; `Halted` no create, update or cancel; `Auction` no market orders and no matching; `AuctionMatching` opening price is calculated; `CancelOnly` cancels only) |
| `SubaccountTransferDirection` | `MasterToSub` masterToSub, `SubToMaster` subToMaster |

Order lifecycle: start `New` or `AwaitingTrigger` (trigger types); active `New`, `AwaitingTrigger`, `PartiallyFilled`; completed `Filled`, `Expired`, `Canceled*`. Reason of a cancel or reduce: `BitvavoOrder.RestatementReason` (string).

## Options, DI, rate limits

| Option | Where | Note |
|---|---|---|
| `ApiCredentials` | `BitvavoRestOptions`, `BitvavoSocketOptions`, `BitvavoOptions` | `BitvavoCredentials` |
| `Environment` | all three | `BitvavoEnvironment.Live` |
| `ReceiveWindowMs` | `BitvavoRestOptions`, `BitvavoSocketOptions` | default 10000; `Bitvavo-Access-Window` and the `window` of the socket `authenticate` action |
| `AutoTimestamp` | `BitvavoRestOptions` (CryptoExchange.Net) | offset from public `GET /v2/time` before the first signed request |
| `RequestTimeout`, `Proxy`, `OutputOriginalData` | `BitvavoRestOptions`, `BitvavoSocketOptions` (CryptoExchange.Net) | `OutputOriginalData` fills `OriginalData` of results and events |
| `RateLimitingBehaviour` | `BitvavoRestOptions` | `Wait` or `Fail` (`Fail` → `ClientRateLimitError`) |
| `RateLimiterEnabled`, `RateLimitAdmission` | `BitvavoRestOptions` (CryptoExchange.Net) | `RateLimitAdmission` is a `Func<RequestDefinition, int, RateLimitAdmission>`, built with `RateLimitAdmission.WithMaxUtilizationRatio(ratio)`; the stricter of that ratio and `MaxUtilization` applies |
| `SocketSubscriptionsCombineTarget` | `BitvavoSocketOptions` | default 10 subscriptions per connection |
| `SocketNoDataTimeout`, `ReconnectPolicy`, `ReconnectInterval`, `MaxSocketConnections` | `BitvavoSocketOptions` (CryptoExchange.Net) | |
| `SpotOptions` | `BitvavoRestOptions` (`RestApiOptions`), `BitvavoSocketOptions` (`SocketApiOptions`) | per-API overrides |
| `Rest`, `Socket`, `SharedApi`, `SocketClientLifeTime` | `BitvavoOptions` | `SharedApi.PreferredTransport` (`SharedTransport.Rest` default); `SocketClientLifeTime` is a `ServiceLifetime?` |

Defaults for new clients: `BitvavoRestClient.SetDefaultOptions(...)`, `BitvavoSocketClient.SetDefaultOptions(...)`.
Options factories: `BitvavoOptions.Create(Action<BitvavoOptions>?)`, `BitvavoOptions.CreateFromConfiguration(IConfiguration)`. Configuration keys relative to the bound section: `Environment:Name`, `ApiCredentials:Spot:Key`, `ApiCredentials:Spot:Secret`, `Rest:ReceiveWindowMs`, `Rest:RequestTimeout`, `Socket:ReceiveWindowMs`, `SharedApi:PreferredTransport`. Environment name lookup ignores case, an unknown name throws at registration.

DI, exactly:
- `services.AddBitvavo()` = legacy overload `AddBitvavo(Action<BitvavoRestOptions>? = null, Action<BitvavoSocketOptions>? = null)`; a lambda passed positionally binds here.
- `AddBitvavo(IConfiguration)` and `AddBitvavo(Action<BitvavoOptions>)` are the library-wide overloads. Use an explicitly typed lambda `(BitvavoOptions o) => ...` or a lambda that touches `Rest`, `Socket`, `SharedApi` or `SocketClientLifeTime`.
- Trap: `services.AddBitvavo(o => o.ApiCredentials = ...)` binds the REST overload (language version C# 13 and later; at C# 12, the net8.0 default, the library-wide overload is picked and both clients get the credentials): the socket client gets NO credentials. For both transports at every language version use `(BitvavoOptions o) => o.ApiCredentials = ...`.
- `IBitvavoRestClient` transient on a typed `HttpClient` (timeout = `RequestTimeout`); `IBitvavoSocketClient` singleton unless `SocketClientLifeTime` is set; `IBitvavoRestClientSpotApi` transient; `IBitvavoSocketClientSpotApi` follows the socket client lifetime.
- Every overload registers options through the options pipeline: an application `PostConfigure<BitvavoRestOptions>` or `PostConfigure<BitvavoSocketOptions>` runs last and wins.
- Also resolvable: `IBitvavoSharedApiClient`, `ISharedApiClientBase`, `IBitvavoRestClientSpotSharedApi`, `IBitvavoSocketClientSpotSharedApi`, every V1 and V2 Shared capability interface.

Rate limiting (REST only; WebSocket frames are not counted):
- Bitvavo budget: 1000 weight points per minute, per account for signed requests (all keys, all subaccounts) and per IP for public requests. Overrun → HTTP 429 code 105, blocked 1 minute (signed) or 15 minutes (IP).
- Client side: two sliding one-minute guards (signed, public), `BitvavoExchange.RateLimiter` = static settable `BitvavoRateLimiters`, default `MaxUtilization` 0.9 (90 % utilization: 900 usable points per budget). Constructor `BitvavoRateLimiters(double maxUtilization = 0.9, int weightPerMinute = 1000)`; `MaxUtilization` must be ≥ 100 / `weightPerMinute` and ≤ 1.
- Events: `RateLimitTriggered`, `RateLimitUpdated` (`Current`, `Limit`). Set the limiter at start-up; a replacement counts from zero.
- Per-call variable weights: `GetTicker24hAsync()` 25, `GetOpenOrdersAsync()` 100, `CancelOrdersAsync(operatorId)` 100, `GetSubaccountOpenOrdersAsync()` 100, `CancelSubaccountOrdersAsync(operatorId)` 100.
- `ResetCancelOnDisconnectAsync` is never counted or refused by the limiter (heartbeat).

## Errors

`result.Success` false → `result.Error` (`Error`): `ErrorType`, `ErrorCode` (string, Bitvavo `errorCode`), `Code`, `Message`, `IsTransient`, `Exception`. Mapping of the Bitvavo codes (an unlisted code → `ErrorType.Unknown`):

| `ErrorType` | Transient | Codes |
|---|---|---|
| `InvalidParameter` | no | 102, 200, 201, 204, 205, 206, 236, 239, 429, 439, 510 |
| `MissingParameter` | no | 203, 232 |
| `RejectedOrderConfiguration` | no | 202, 231, 234 |
| `InvalidQuantity` | no | 210, 212, 217, 406 |
| `InvalidPrice` | no | 211, 213, 215, 422 |
| `InsufficientBalance` | no | 216, 408 |
| `UnknownSymbol` | no | 219 |
| `DuplicateClientOrderId` | no | 220 |
| `RateLimitOrder` | no | 235 |
| `InvalidStopParameters` | no | 237, 238 |
| `UnknownOrder` | no | 240 |
| `UnavailableSymbol` | yes | 423, 424, 425, 426, 431 |
| `InvalidOperation` | no | 401, 407, 410, 411, 412, 413, 415, 514 |
| `InvalidOperation` | yes | 405, 414 |
| `Unauthorized` | no | 301, 305–314, 316–320, 322, 402, 403, 409, 511, 512, 513 |
| `RiskError` | no | 434, 435, 436, 437, 438 |
| `SystemError` | yes | 101, 107, 108, 111, 400, 404, 419 |
| `MissingCredentials` | no | 300 |
| `InvalidTimestamp` | no | 302, 303, 304 |
| `RateLimitRequest` | yes | 105, 112 |
| `Timeout` | no | 109 (the operation may or may not have happened: read it back before retrying) |
| `Timeout` | yes | 430 |

Code 429 is an HTTP 400 about decimal places, unrelated to HTTP 429. HTTP 429 keeps the server code (105 or 112) on a `ServerRateLimitError`; Bitvavo sends no `Retry-After`. Client-side refusals: `ClientRateLimitError`, `ArgumentError` (Shared request validation), `NoApiCredentialsError`, `CancellationRequestedError`.

## Conventions

- REST methods return `Task<HttpResult<T>>`, subscriptions `Task<WebSocketResult<UpdateSubscription>>`. Protocol errors are results, never exceptions. Check `Success` before reading `Data`.
- Misuse still throws (null limiter, invalid limiter bounds, unknown environment name, `SharedOrderType.Other` on place, unsupported Shared enum values).
- `ct` is the last parameter of every method; some Shared calls take `PageRequest? nextPageToken` before it. Pass `ct: token` by name.
- Reuse clients. Dispose with `using`. Do not create a client per request.
- `operatorId` (`long`) on every order operation; `clientOrderId` must be a UUID (`Guid.NewGuid().ToString()`), unique per market; amounts and prices are `decimal` in C# and strings on the wire; `Amount` (base asset) and `AmountQuote` (quote asset) are mutually exclusive; prices are multiples of `BitvavoMarket.TickSize`.
- Market names are `BASE-QUOTE` upper case (`ETH-EUR`). Assets are symbols (`BTC`, `EUR`).
- Fee rates in REST models are fractions; the Shared fee is a percentage.

## Not implemented

- Public WebSocket `ticker`, `ticker24h` and `book` channels (poll `GetTickerBookAsync`, `GetTicker24hAsync`, `GetOrderBookAsync`).
- `SymbolOrderBook`, trackers (`ITrackerFactory`), user-client-provider (multi-user).
- WebSocket request/response actions (`privateCreateOrder` etc.): orders go over REST.
- WebSocket `error` event routing.
- Balance subscription: Bitvavo's account channel emits only `order` and `fill` events. Read balances with `Account.GetBalancesAsync` (weight 5).
- Batch orders: Bitvavo has no endpoint. Futures and margin: Bitvavo is spot only.
- Custom or test environment: none. Dry-run or validate-only order flag: none; every order call hits the live account.
- Not measured without live credentials: whether the signed budget counts per API key or per account (the limiter assumes per account); the reply to a rejected socket `authenticate`; the stop-loss and take-profit firing direction (the usual meaning is assumed).

## Routing pitfalls

| Do not use | Use instead |
|---|---|
| the result type names of earlier CryptoExchange.Net versions, `WebCallResult<T>` and `CallResult<T>` (code written for Bitvavo.Net before 0.5.0) | `HttpResult<T>` for REST, `WebSocketResult<UpdateSubscription>` for subscriptions |
| raw `HttpClient`, manual HMAC | `BitvavoRestClient` / `BitvavoSocketClient` |
| generic `ApiCredentials` | `BitvavoCredentials` |
| `pricePrecision` for price rounding | `BitvavoMarket.TickSize` (price must be a multiple), `QuantityDecimals`, `NotionalDecimals` |
| `services.AddBitvavo(o => o.ApiCredentials = ...)` for REST and socket | `services.AddBitvavo((BitvavoOptions o) => o.ApiCredentials = ...)` |
| `SpotApi.Account` for deposits and withdrawals | `SpotApi.Funding` |
| `SpotApi.SharedClient` in new code | `SpotApi.SharedApi` or `IBitvavoSharedApiClient` |
| `CancelOrdersAsync(market)` | `CancelOrdersAsync(operatorId, market)` (operator id first) |
| `GetTradingFeesAsync(market, ct)` (pre-0.5.0 shape) | `GetTradingFeesAsync(market, quote, ct)`; pass the token by name (`ct: ct`) |
| WebSocket ticker or book subscription | REST polling (rate limit: weight per call above) |
| WebSocket balance subscription | `order` and `fill` events + `GetBalancesAsync` |
| `BitvavoErrors` (internal) | `result.Error.ErrorType`, `ErrorCode`, `IsTransient` |
| expecting report or market-data calls to be signed | they are public and sent unsigned; no credentials needed |
| `.Data` before `.Success` | check `Success` first |
