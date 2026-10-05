// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using Bitvavo.Net.Objects.Internal;
using CryptoExchange.Net.SharedApis;

namespace Bitvavo.Net.Clients.SpotApi;

/// <summary>
/// The Bitvavo-specific parameters a Shared request carries in its <see cref="ExchangeParameters"/> because the Shared contract has
/// no slot for them. Each rule is declared exactly once, here, in the form the capabilities' options publish
/// (<c>ExchangeParameterRules</c>); the accessors read the value back after request validation has proved it is there.
/// </summary>
internal static class BitvavoSharedParameters
{
    /// <summary>Name of the <c>OperatorId</c> exchange parameter.</summary>
    public const string OperatorId = "OperatorId";

    /// <summary>Name of the <c>Markets</c> exchange parameter.</summary>
    public const string Markets = "Markets";

    /// <summary>
    /// Every order operation on Bitvavo (place, update, cancel) carries an <c>operatorId</c>: an account-scoped integer naming the
    /// originator of the request. The Shared order requests have no field for it.
    /// </summary>
    public static ExchangeParameterDescription OperatorIdRule { get; } = ExchangeParameterRule.Required<long>(
        OperatorId,
        description: "Account-scoped integer identifying the originator of the order request; Bitvavo requires it on every order operation",
        exampleValue: 1L);

    /// <summary>
    /// Bitvavo's private <c>account</c> channel is subscribed per market, and the Shared subscription requests carry no symbol set:
    /// the markets travel in this parameter.
    /// </summary>
    public static ExchangeParameterDescription MarketsRule { get; } = ExchangeParameterRule.Required<string[]>(
        Markets,
        description: "The markets the account-channel subscription covers, as Bitvavo market names",
        exampleValue: new[] { "ETH-EUR" });

    /// <summary>The <c>OperatorId</c> of a request that passed validation against options carrying <see cref="OperatorIdRule"/>.</summary>
    /// <exception cref="InvalidOperationException">The request has no operator id: it was not validated first.</exception>
    public static long GetOperatorId(this SharedRequest request)
        => request.GetParamValue<long?>(BitvavoExchange.ExchangeName, OperatorId)
            ?? throw new InvalidOperationException($"The '{OperatorId}' exchange parameter is missing; the request must be validated against its options first");

    /// <summary>The <c>Markets</c> of a request that passed validation against options carrying <see cref="MarketsRule"/>.</summary>
    /// <exception cref="InvalidOperationException">The request has no markets: it was not validated first.</exception>
    public static string[] GetMarkets(this SharedRequest request)
        => request.GetParamValue<string[]>(BitvavoExchange.ExchangeName, Markets)
            ?? throw new InvalidOperationException($"The '{Markets}' exchange parameter is missing; the request must be validated against its options first");
}
