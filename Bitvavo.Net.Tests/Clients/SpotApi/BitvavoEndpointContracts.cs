// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Bitvavo.Net.Enums;
using Bitvavo.Net.Interfaces.Clients.SpotApi;
using Bitvavo.Net.Objects.Models.Spot;

namespace Bitvavo.Net.Tests.Clients.SpotApi;

/// <summary>
/// What one REST endpoint of the library must put on the wire and cost against Bitvavo's rate limit. The numbers come from the
/// vendor's documentation (docs.bitvavo.com, API v2.10.0), never from the code under test.
/// </summary>
/// <param name="Name"><c>Group.Method#n</c>: the sub-client, the method and which call shape (with / without optional filters).</param>
/// <param name="Call">The call, made through the public client surface.</param>
/// <param name="Method">HTTP verb.</param>
/// <param name="PathAndQuery">Path and query string exactly as sent (keys ordered ordinally).</param>
/// <param name="Body">JSON body exactly as sent; empty when the request has none.</param>
/// <param name="Signed">Whether the request is authenticated (signed) — which also decides the rate-limit budget it counts against.</param>
/// <param name="Weight">Rate-limit weight points the documentation allocates to this call shape; 0 = deliberately not counted.</param>
internal sealed record EndpointContract(
    string Name,
    Func<IBitvavoRestClientSpotApi, Task> Call,
    string Method,
    string PathAndQuery,
    string Body,
    bool Signed,
    int Weight);

/// <summary>The contract of every REST endpoint — the single table the wire goldens and the rate-limit tests both read.</summary>
internal static class BitvavoEndpointContracts
{
    private static readonly DateTime T1 = new(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
    private static readonly DateTime T2 = new(2026, 1, 3, 3, 4, 5, DateTimeKind.Utc);

    public static readonly EndpointContract[] All =
    [
        new("ExchangeData.GetMarketsAsync#1", api => api.ExchangeData.GetMarketsAsync(), "GET", "/v2/markets", "", false, 1),
        new("ExchangeData.GetKlinesAsync#1", api => api.ExchangeData.GetKlinesAsync("BTC-EUR", KlineInterval.OneHour, 5, T1, T2), "GET", "/v2/BTC-EUR/candles?end=1767409445000&interval=1h&limit=5&start=1767323045000", "", false, 1),
        new("ExchangeData.GetKlinesAsync#2", api => api.ExchangeData.GetKlinesAsync("BTC-EUR", KlineInterval.OneHour), "GET", "/v2/BTC-EUR/candles?interval=1h", "", false, 1),
        new("ExchangeData.GetServerTimeAsync#1", api => api.ExchangeData.GetServerTimeAsync(), "GET", "/v2/time", "", false, 1),
        new("ExchangeData.GetAssetsAsync#1", api => api.ExchangeData.GetAssetsAsync("BTC"), "GET", "/v2/assets?symbol=BTC", "", false, 1),
        new("ExchangeData.GetTickerPricesAsync#1", api => api.ExchangeData.GetTickerPricesAsync("BTC-EUR"), "GET", "/v2/ticker/price?market=BTC-EUR", "", false, 1),
        new("ExchangeData.GetTickerBookAsync#1", api => api.ExchangeData.GetTickerBookAsync("BTC-EUR"), "GET", "/v2/ticker/book?market=BTC-EUR", "", false, 1),
        new("ExchangeData.GetTicker24hAsync#1", api => api.ExchangeData.GetTicker24hAsync("BTC-EUR"), "GET", "/v2/ticker/24h?market=BTC-EUR", "", false, 1),
        new("ExchangeData.GetTicker24hAsync#2", api => api.ExchangeData.GetTicker24hAsync(), "GET", "/v2/ticker/24h", "", false, 25),
        new("ExchangeData.GetOrderBookAsync#1", api => api.ExchangeData.GetOrderBookAsync("BTC-EUR", 10), "GET", "/v2/BTC-EUR/book?depth=10", "", false, 1),
        new("ExchangeData.GetPublicTradesAsync#1", api => api.ExchangeData.GetPublicTradesAsync("BTC-EUR", 5, T1, T2, "tf", "tt"), "GET", "/v2/BTC-EUR/trades?end=1767409445000&limit=5&start=1767323045000&tradeIdFrom=tf&tradeIdTo=tt", "", false, 5),
        new("Trading.GetOpenOrdersAsync#1", api => api.Trading.GetOpenOrdersAsync("BTC-EUR", "BTC"), "GET", "/v2/ordersOpen?base=BTC&market=BTC-EUR", "", true, 5),
        new("Trading.GetOpenOrdersAsync#2", api => api.Trading.GetOpenOrdersAsync(), "GET", "/v2/ordersOpen", "", true, 100),
        new("Trading.GetOrderHistoryAsync#1", api => api.Trading.GetOrderHistoryAsync("BTC-EUR", 10, T1, T2, "of", "ot"), "GET", "/v2/orders?end=1767409445000&limit=10&market=BTC-EUR&orderIdFrom=of&orderIdTo=ot&start=1767323045000", "", true, 5),
        new("Trading.GetUserTradesAsync#1", api => api.Trading.GetUserTradesAsync("BTC-EUR", 10, T1, T2, "tf", "tt"), "GET", "/v2/trades?end=1767409445000&limit=10&market=BTC-EUR&start=1767323045000&tradeIdFrom=tf&tradeIdTo=tt", "", true, 5),
        new("Trading.GetOrderAsync#1", api => api.Trading.GetOrderAsync("BTC-EUR", orderId: "abc"), "GET", "/v2/order?market=BTC-EUR&orderId=abc", "", true, 1),
        new("Trading.GetOrderAsync#2", api => api.Trading.GetOrderAsync("BTC-EUR", clientOrderId: "cli"), "GET", "/v2/order?clientOrderId=cli&market=BTC-EUR", "", true, 1),
        new("Account.GetAccountInfoAsync#1", api => api.Account.GetAccountInfoAsync(), "GET", "/v2/account", "", true, 1),
        new("Account.GetBalancesAsync#1", api => api.Account.GetBalancesAsync("BTC"), "GET", "/v2/balance?symbol=BTC", "", true, 5),
        new("Account.GetTradingFeesAsync#1", api => api.Account.GetTradingFeesAsync("BTC-EUR"), "GET", "/v2/account/fees?market=BTC-EUR", "", true, 1),
        new("Account.GetTransactionHistoryAsync#1", api => api.Account.GetTransactionHistoryAsync(T1, T2, 2, 50, "buy"), "GET", "/v2/account/history?fromDate=1767323045000&maxItems=50&page=2&toDate=1767409445000&type=buy", "", true, 1),
        new("Funding.GetDepositAddressAsync#1", api => api.Funding.GetDepositAddressAsync("BTC"), "GET", "/v2/deposit?symbol=BTC", "", true, 1),
        new("Funding.GetDepositHistoryAsync#1", api => api.Funding.GetDepositHistoryAsync("BTC", 10, T1, T2), "GET", "/v2/depositHistory?end=1767409445000&limit=10&start=1767323045000&symbol=BTC", "", true, 5),
        new("Funding.GetWithdrawalHistoryAsync#1", api => api.Funding.GetWithdrawalHistoryAsync("BTC", 10, T1, T2), "GET", "/v2/withdrawalHistory?end=1767409445000&limit=10&start=1767323045000&symbol=BTC", "", true, 5),
        new("Report.GetTradesReportAsync#1", api => api.Report.GetTradesReportAsync("BTC-EUR", 5, T1, T2, "tf", "tt"), "GET", "/v2/report/BTC-EUR/trades?end=1767409445000&limit=5&start=1767323045000&tradeIdFrom=tf&tradeIdTo=tt", "", false, 5),
        new("Report.GetBookReportAsync#1", api => api.Report.GetBookReportAsync("BTC-EUR", 10), "GET", "/v2/report/BTC-EUR/book?depth=10", "", false, 1),
        new("Institutional.GetSubaccountsAsync#1", api => api.Institutional.GetSubaccountsAsync(1, 10), "GET", "/v2/subaccounts?maxItems=10&page=1", "", true, 5),
        new("Institutional.GetTransfersAsync#1", api => api.Institutional.GetTransfersAsync("sub-1", "req-1", T1, T2, 10), "GET", "/v2/subaccounts/transfers?clientRequestId=req-1&end=1767409445000&limit=10&start=1767323045000&subaccountId=sub-1", "", true, 5),
        new("Institutional.GetTransferAsync#1", api => api.Institutional.GetTransferAsync("tr-1"), "GET", "/v2/subaccounts/transfers/tr-1", "", true, 5),
        new("Institutional.GetSubaccountBalancesAsync#1", api => api.Institutional.GetSubaccountBalancesAsync("sub-1", "BTC"), "GET", "/v2/institutional/subaccounts/balance?subaccountId=sub-1&symbol=BTC", "", true, 5),
        new("Institutional.GetSubaccountTransactionHistoryAsync#1", api => api.Institutional.GetSubaccountTransactionHistoryAsync("sub-1", T1, T2, 1, 10, "buy"), "GET", "/v2/institutional/subaccounts/history?fromDate=1767323045000&maxItems=10&page=1&subaccountId=sub-1&toDate=1767409445000&type=buy", "", true, 5),
        new("Institutional.GetSubaccountOpenOrdersAsync#1", api => api.Institutional.GetSubaccountOpenOrdersAsync("sub-1", "BTC-EUR", "BTC"), "GET", "/v2/institutional/subaccounts/orders/open?base=BTC&market=BTC-EUR&subaccountId=sub-1", "", true, 5),
        new("Institutional.GetSubaccountOpenOrdersAsync#2", api => api.Institutional.GetSubaccountOpenOrdersAsync("sub-1"), "GET", "/v2/institutional/subaccounts/orders/open?subaccountId=sub-1", "", true, 100),
        new("Trading.CancelOrderAsync#1", api => api.Trading.CancelOrderAsync("BTC-EUR", 7, orderId: "abc"), "DELETE", "/v2/order?market=BTC-EUR&operatorId=7&orderId=abc", "", true, 1),
        new("Trading.CancelOrderAsync#2", api => api.Trading.CancelOrderAsync("BTC-EUR", 7, clientOrderId: "cli"), "DELETE", "/v2/order?clientOrderId=cli&market=BTC-EUR&operatorId=7", "", true, 1),
        new("Trading.CancelOrdersAsync#1", api => api.Trading.CancelOrdersAsync(7, "BTC-EUR"), "DELETE", "/v2/orders?market=BTC-EUR&operatorId=7", "", true, 25),
        new("Trading.CancelOrdersAsync#2", api => api.Trading.CancelOrdersAsync(7), "DELETE", "/v2/orders?operatorId=7", "", true, 100),
        new("Trading.PlaceOrderAsync#1", api => api.Trading.PlaceOrderAsync(new BitvavoPlaceOrderRequest("BTC-EUR", OrderSide.Buy, OrderType.Limit, 7, Amount: 0.001m, Price: 50000.5m, TimeInForce: TimeInForce.GoodTillCanceled, PostOnly: true, ClientOrderId: "11111111-1111-1111-1111-111111111111")), "POST", "/v2/order", "{\"amount\":\"0.001\",\"clientOrderId\":\"11111111-1111-1111-1111-111111111111\",\"market\":\"BTC-EUR\",\"operatorId\":7,\"orderType\":\"limit\",\"postOnly\":true,\"price\":\"50000.5\",\"side\":\"buy\",\"timeInForce\":\"GTC\"}", true, 1),
        new("Trading.PlaceOrderAsync#2", api => api.Trading.PlaceOrderAsync(new BitvavoPlaceOrderRequest("BTC-EUR", OrderSide.Buy, OrderType.Market, 7, AmountQuote: 12.5m, ResponseRequired: false)), "POST", "/v2/order", "{\"amountQuote\":\"12.5\",\"market\":\"BTC-EUR\",\"operatorId\":7,\"orderType\":\"market\",\"responseRequired\":false,\"side\":\"buy\"}", true, 1),
        new("Trading.UpdateOrderAsync#1", api => api.Trading.UpdateOrderAsync(new BitvavoUpdateOrderRequest("BTC-EUR", 7, OrderId: "abc", Amount: 0.002m, Price: 51000m, PostOnly: false)), "PUT", "/v2/order", "{\"amount\":\"0.002\",\"market\":\"BTC-EUR\",\"operatorId\":7,\"orderId\":\"abc\",\"postOnly\":false,\"price\":\"51000\"}", true, 1),
        new("Account.ResetCancelOnDisconnectAsync#1", api => api.Account.ResetCancelOnDisconnectAsync(1, 30), "POST", "/v2/cancelOrdersAfter", "{\"codGroupId\":1,\"expiryAfterSeconds\":30}", true, 0),
        new("Funding.WithdrawAsync#1", api => api.Funding.WithdrawAsync(new BitvavoWithdrawRequest("BTC", 0.001m, "bc1qxyz", PaymentId: "memo", AddWithdrawalFee: true, Internal: false)), "POST", "/v2/withdrawal", "{\"address\":\"bc1qxyz\",\"addWithdrawalFee\":true,\"amount\":\"0.001\",\"internal\":false,\"paymentId\":\"memo\",\"symbol\":\"BTC\"}", true, 1),
        new("Institutional.CreateSubaccountAsync#1", api => api.Institutional.CreateSubaccountAsync("lbl"), "POST", "/v2/subaccounts", "{\"label\":\"lbl\"}", true, 5),
        new("Institutional.CreateTransferAsync#1", api => api.Institutional.CreateTransferAsync(new BitvavoCreateTransferRequest("sub-1", Bitvavo.Net.Enums.SubaccountTransferDirection.MasterToSub, "EUR", 10.5m, "req-1")), "POST", "/v2/subaccounts/transfers", "{\"amount\":\"10.5\",\"clientRequestId\":\"req-1\",\"direction\":\"masterToSub\",\"subaccountId\":\"sub-1\",\"symbol\":\"EUR\"}", true, 5),
        new("Institutional.CancelSubaccountOrderAsync#1", api => api.Institutional.CancelSubaccountOrderAsync(new BitvavoSubaccountCancelOrderRequest("BTC-EUR", "abc", 7, "sub-1")), "DELETE", "/v2/institutional/subaccounts/order", "{\"market\":\"BTC-EUR\",\"operatorId\":7,\"orderId\":\"abc\",\"subaccountId\":\"sub-1\"}", true, 1),
        new("Institutional.CancelSubaccountOrdersAsync#1", api => api.Institutional.CancelSubaccountOrdersAsync(7, "sub-1", "BTC-EUR"), "DELETE", "/v2/institutional/subaccounts/orders", "{\"market\":\"BTC-EUR\",\"operatorId\":7,\"subaccountId\":\"sub-1\"}", true, 25),
        new("Institutional.CancelSubaccountOrdersAsync#2", api => api.Institutional.CancelSubaccountOrdersAsync(7, "sub-1"), "DELETE", "/v2/institutional/subaccounts/orders", "{\"operatorId\":7,\"subaccountId\":\"sub-1\"}", true, 100),
        // Added in the v2.10.0 conformance step (docs.bitvavo.com/api-specs/exchange-rest-api.yaml): atomic cancel, crypto withdrawal,
        // staking balance, market-fee quote filter, trade-id filter, disableMarketProtection, amountRemaining.
        new("Trading.CancelOrdersAtomicAsync#1", api => api.Trading.CancelOrdersAtomicAsync("BTC-EUR", OrderSide.Buy, 7), "DELETE", "/v2/atomic/orders", "{\"market\":\"BTC-EUR\",\"operatorId\":7,\"side\":\"buy\"}", true, 100),
        new("Trading.PlaceOrderAsync#3", api => api.Trading.PlaceOrderAsync(new BitvavoPlaceOrderRequest("BTC-EUR", OrderSide.Buy, OrderType.Limit, 7, Amount: 0.001m, Price: 50000m, DisableMarketProtection: true)), "POST", "/v2/order", "{\"amount\":\"0.001\",\"disableMarketProtection\":true,\"market\":\"BTC-EUR\",\"operatorId\":7,\"orderType\":\"limit\",\"price\":\"50000\",\"side\":\"buy\"}", true, 1),
        new("Trading.UpdateOrderAsync#2", api => api.Trading.UpdateOrderAsync(new BitvavoUpdateOrderRequest("BTC-EUR", 7, OrderId: "abc", AmountRemaining: 0.0005m)), "PUT", "/v2/order", "{\"amountRemaining\":\"0.0005\",\"market\":\"BTC-EUR\",\"operatorId\":7,\"orderId\":\"abc\"}", true, 1),
        new("Trading.GetUserTradesAsync#2", api => api.Trading.GetUserTradesAsync("BTC-EUR", tradeId: "t-1"), "GET", "/v2/trades?market=BTC-EUR&tradeId=t-1", "", true, 5),
        new("Account.GetTradingFeesAsync#2", api => api.Account.GetTradingFeesAsync(quote: "EUR"), "GET", "/v2/account/fees?quote=EUR", "", true, 1),
        new("Account.GetStakingBalanceAsync#1", api => api.Account.GetStakingBalanceAsync("ADA"), "GET", "/v2/stakingBalance?symbol=ADA", "", true, 5),
        new("Account.GetStakingBalanceAsync#2", api => api.Account.GetStakingBalanceAsync(), "GET", "/v2/stakingBalance", "", true, 5),
        new("Funding.WithdrawCryptoAsync#1", api => api.Funding.WithdrawCryptoAsync(new BitvavoCryptoWithdrawRequest("BTC", "Bitcoin", "bc1qxyz", 0.001m, DeductFeeFromAmount: true, IdempotencyKey: "idem-1", Memo: "memo")), "POST", "/v2/crypto/withdrawal", "{\"address\":\"bc1qxyz\",\"amount\":\"0.001\",\"asset\":\"BTC\",\"deductFeeFromAmount\":true,\"idempotencyKey\":\"idem-1\",\"memo\":\"memo\",\"network\":\"Bitcoin\"}", true, 25),
    ];
}
