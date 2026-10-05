// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Text.Json;
using Bitvavo.Net.Clients.MessageHandlers;
using Bitvavo.Net.Enums;
using Bitvavo.Net.Objects.Internal;
using Bitvavo.Net.Objects.Models.Spot;
using Bitvavo.Net.Objects.Models.Spot.Streams;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests;

/// <summary>
/// The response models against Bitvavo's v2.10.0 specification (docs.bitvavo.com REST + WebSocket API specs) and against what
/// the live API returns today. Payloads are read with the very options the REST handler deserializes with in production.
/// </summary>
public class BitvavoModelConformanceTests
{
    private static JsonSerializerOptions Options { get; } = new BitvavoRestSpotMessageHandler(BitvavoErrors.SpotMapping).Options;

    private static T Read<T>(string json) => JsonSerializer.Deserialize<T>(json, Options)!;

    // ── GET /v2/markets ──────────────────────────────────────────────────────────────────

    /// <summary>The BTC-EUR entry as the live API returns it (2026-10-05): <c>pricePrecision</c> is null on every market, the tick size is the price grid.</summary>
    [Fact]
    public void Market_reads_the_live_payload()
    {
        var market = Read<BitvavoMarket>("""
            {"market":"BTC-EUR","status":"trading","base":"BTC","quote":"EUR","pricePrecision":null,
             "minOrderInBaseAsset":"0.00006607","minOrderInQuoteAsset":"5.00","maxOrderInBaseAsset":"1000000000","maxOrderInQuoteAsset":"1000000000",
             "quantityDecimals":8,"notionalDecimals":2,"tickSize":"1.00","maxOpenOrders":400,"feeCategory":"A",
             "orderTypes":["market","limit","stopLoss","stopLossLimit","takeProfit","takeProfitLimit"]}
            """);

        market.Status.ShouldBe(BitvavoMarketStatus.Trading);
        market.PricePrecision.ShouldBeNull();
        market.QuantityDecimals.ShouldBe(8);
        market.NotionalDecimals.ShouldBe(2);
        market.TickSize.ShouldBe(1.00m);
        market.MaxOpenOrders.ShouldBe(400);
        market.FeeCategory.ShouldBe("A");
        market.OrderTypes.Count.ShouldBe(6);
    }

    [Theory]
    [InlineData("trading", BitvavoMarketStatus.Trading)]
    [InlineData("halted", BitvavoMarketStatus.Halted)]
    [InlineData("auction", BitvavoMarketStatus.Auction)]
    [InlineData("auctionMatching", BitvavoMarketStatus.AuctionMatching)]
    [InlineData("cancelOnly", BitvavoMarketStatus.CancelOnly)]
    public void Market_reads_every_documented_status(string wire, BitvavoMarketStatus expected)
    {
        Read<BitvavoMarket>("{\"market\":\"BTC-EUR\",\"status\":\"" + wire + "\"}").Status.ShouldBe(expected);
    }

    /// <summary>The spec says <c>pricePrecision</c> is an integer and <c>maxOpenOrders</c> a number; live they are null on some markets and the fee category has more than A–C. None of it may break the read.</summary>
    [Fact]
    public void Market_stays_readable_when_optional_fields_are_null_or_new_values_appear()
    {
        var market = Read<BitvavoMarket>("""{"market":"XYZ-EUR","status":"trading","pricePrecision":5,"maxOpenOrders":null,"feeCategory":"D","tickSize":"0.00001"}""");

        market.PricePrecision.ShouldBe(5);
        market.MaxOpenOrders.ShouldBeNull();
        market.FeeCategory.ShouldBe("D");
        market.TickSize.ShouldBe(0.00001m);
    }

    // ── orders and fills ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Order_reads_market_protection_restatement_reason_and_nanosecond_stamps()
    {
        var order = Read<BitvavoOrder>("""
            {"orderId":"11111111-1111-1111-1111-111111111111","market":"BTC-EUR","created":1700000000000,"updated":1700000000001,
             "status":"canceled","side":"buy","orderType":"limit","amount":"0.1","price":"50000","operatorId":7,
             "disableMarketProtection":true,"restatementReason":"cancelOnMaintenance","createdNs":1700000000000123456,"updatedNs":1700000000001654321}
            """);

        order.DisableMarketProtection.ShouldBe(true);
        order.RestatementReason.ShouldBe("cancelOnMaintenance");
        order.CreatedNs.ShouldBe(1700000000000123456L);
        order.UpdatedNs.ShouldBe(1700000000001654321L);
    }

    [Fact]
    public void Fill_reads_the_operator_id()
    {
        var fill = Read<BitvavoFill>("""{"id":"f-1","orderId":"o-1","operatorId":7,"timestamp":1700000000000,"market":"BTC-EUR","side":"buy","amount":"0.1","price":"50000","taker":true}""");

        fill.OperatorId.ShouldBe(7L);
    }

    [Fact]
    public void Order_book_reads_the_nanosecond_timestamp()
    {
        var book = Read<BitvavoOrderBook>("""{"market":"BTC-EUR","nonce":42,"bids":[["49999","1"]],"asks":[["50001","2"]],"timestamp":1700000000000123456}""");

        book.TimestampNs.ShouldBe(1700000000000123456L);
    }

    // ── WebSocket events ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Account_order_event_reads_operator_id_and_nanosecond_stamps()
    {
        var update = Read<BitvavoStreamOrderUpdate>("""
            {"event":"order","orderId":"11111111-1111-1111-1111-111111111111","operatorId":7,"market":"BTC-EUR","created":1700000000000,"updated":1700000000001,
             "status":"new","side":"buy","orderType":"limit","createdNs":1700000000000123456,"updatedNs":1700000000001654321}
            """);

        update.OperatorId.ShouldBe(7L);
        update.CreatedNs.ShouldBe(1700000000000123456L);
        update.UpdatedNs.ShouldBe(1700000000001654321L);
    }

    [Fact]
    public void Account_fill_event_reads_operator_id_and_nanosecond_timestamp()
    {
        var fill = Read<BitvavoStreamFillEvent>("""
            {"event":"fill","market":"BTC-EUR","orderId":"11111111-1111-1111-1111-111111111111","operatorId":7,"fillId":"f-1",
             "timestamp":1700000000002,"amount":"0.001","side":"buy","price":"50000","taker":true,"fee":"0.125","feeCurrency":"EUR","timestampNs":1700000000002123456}
            """);

        fill.OperatorId.ShouldBe(7L);
        fill.TimestampNs.ShouldBe(1700000000002123456L);
    }

    [Fact]
    public void Public_trade_event_reads_the_nanosecond_timestamp()
    {
        var trade = Read<BitvavoStreamTrade>("""{"event":"trade","timestamp":1548685870299,"market":"BTC-EUR","id":"616bfa4e-b3ff-4b3f-a394-1538a49eb9bc","amount":"1","price":"2996","side":"buy","timestampNs":1548685870299123456}""");

        trade.TimestampNs.ShouldBe(1548685870299123456L);
    }

    // ── POST /v2/crypto/withdrawal, GET /v2/stakingBalance, GET /v2/account/fees ─────────

    [Fact]
    public void Crypto_withdrawal_reads_the_201_response()
    {
        var withdrawal = Read<BitvavoCryptoWithdrawal>("""
            {"id":"wd-1","asset":"BTC","network":"Bitcoin","address":"bc1qxyz","amount":"0.001","fee":"0.00005","createdAt":"2026-10-05T12:34:56.789Z"}
            """);

        withdrawal.Id.ShouldBe("wd-1");
        withdrawal.Asset.ShouldBe("BTC");
        withdrawal.Network.ShouldBe("Bitcoin");
        withdrawal.Address.ShouldBe("bc1qxyz");
        withdrawal.Amount.ShouldBe(0.001m);
        withdrawal.Fee.ShouldBe(0.00005m);
        withdrawal.CreatedAt.ShouldBe(new DateTime(2026, 10, 5, 12, 34, 56, 789, DateTimeKind.Utc));
    }

    [Fact]
    public void Staking_balance_reads_symbol_and_amount()
    {
        var balances = Read<BitvavoStakingBalance[]>("""[{"symbol":"ADA","amount":"12.5"}]""");

        balances.ShouldHaveSingleItem().Symbol.ShouldBe("ADA");
        balances[0].Amount.ShouldBe(12.5m);
    }
}
