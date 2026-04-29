// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.
//
// Bitvavo.Net QuickStart — demonstrates the canonical wiring:
//   1. services.AddBitvavo() registration
//   2. one public REST call (markets list)
//   3. one public WebSocket subscription (BTC-EUR 1h candles)
//
// Run: dotnet run --project Examples/QuickStart
// Live API only — no credentials required for these endpoints.

using Bitvavo.Net.Enums;
using Bitvavo.Net.Extensions;
using Bitvavo.Net.Interfaces.Clients;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var services = new ServiceCollection();
services.AddLogging(b => b.AddSimpleConsole(o => { o.SingleLine = true; o.TimestampFormat = "HH:mm:ss "; }));
services.AddBitvavo();

await using var sp = services.BuildServiceProvider();

var rest = sp.GetRequiredService<IBitvavoRestClient>();
var sock = sp.GetRequiredService<IBitvavoSocketClient>();

// --- 1) Public REST: list markets and print the first three ---
var marketsResult = await rest.SpotApi.ExchangeData.GetMarketsAsync();
if (!marketsResult.Success)
{
    Console.WriteLine($"GetMarketsAsync failed: {marketsResult.Error}");
    return 1;
}
Console.WriteLine($"Loaded {marketsResult.Data.Count()} markets; first three:");
foreach (var m in marketsResult.Data.Take(3))
    Console.WriteLine($"  {m.Market} (status={m.Status}, base={m.BaseAsset}, quote={m.QuoteAsset})");

// --- 2) Public WebSocket: subscribe to one BTC-EUR 1h candle update, exit on first ---
var seen = new TaskCompletionSource<DateTime>();
var sub = await sock.SpotApi.ExchangeData.SubscribeToKlineUpdatesAsync(
    market: "BTC-EUR",
    interval: KlineInterval.OneHour,
    onMessage: ev =>
    {
        var candleEvent = ev.Data;
        var latest = candleEvent.Candle.LastOrDefault();
        if (latest is not null)
            Console.WriteLine($"  [WS] {candleEvent.Market} {candleEvent.Interval} O={latest.OpenPrice} C={latest.ClosePrice}");
        seen.TrySetResult(DateTime.UtcNow);
    });

if (!sub.Success)
{
    Console.WriteLine($"Subscribe failed: {sub.Error}");
    return 1;
}

Console.WriteLine("Subscribed — waiting up to 90s for one candle update...");
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
cts.Token.Register(() => seen.TrySetCanceled());

try
{
    await seen.Task;
    Console.WriteLine("Got a candle update — done.");
}
catch (TaskCanceledException)
{
    Console.WriteLine("Timed out waiting for a candle update (network down or market quiet).");
}

await sub.Data.CloseAsync();
return 0;
