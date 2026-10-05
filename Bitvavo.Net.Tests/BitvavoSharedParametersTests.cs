// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using Bitvavo.Net.Clients.SpotApi;
using CryptoExchange.Net.SharedApis;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests;

/// <summary>
/// The exchange parameters a Shared request carries for Bitvavo. Reading one back is only valid after the request has been
/// validated against options that require it; an unvalidated request fails loudly instead of sending an order without an operator.
/// </summary>
public class BitvavoSharedParametersTests
{
    private static readonly SharedSymbol EthEur = new(TradingMode.Spot, "ETH", "EUR");

    [Fact]
    public void The_operator_id_is_read_back_from_the_exchange_parameters()
    {
        var request = new GetOpenOrdersRequest(EthEur, new ExchangeParameters(new ExchangeParameter("Bitvavo", "OperatorId", 42L)));

        request.GetOperatorId().ShouldBe(42L);
    }

    [Fact]
    public void The_markets_are_read_back_from_the_exchange_parameters()
    {
        var request = new GetOpenOrdersRequest(EthEur, new ExchangeParameters(new ExchangeParameter("Bitvavo", "Markets", new[] { "ETH-EUR", "BTC-EUR" })));

        request.GetMarkets().ShouldBe(["ETH-EUR", "BTC-EUR"]);
    }

    [Fact]
    public void A_request_without_an_operator_id_cannot_be_read_as_one()
    {
        var error = Should.Throw<InvalidOperationException>(() => new GetOpenOrdersRequest(EthEur).GetOperatorId());

        error.Message.ShouldContain("OperatorId");
    }

    [Fact]
    public void A_request_without_markets_cannot_be_read_as_one()
    {
        var error = Should.Throw<InvalidOperationException>(() => new GetOpenOrdersRequest(EthEur).GetMarkets());

        error.Message.ShouldContain("Markets");
    }
}
