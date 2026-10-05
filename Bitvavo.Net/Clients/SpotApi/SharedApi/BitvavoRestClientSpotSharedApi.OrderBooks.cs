// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bitvavo.Net.Objects.Models.Spot;
using CryptoExchange.Net.Interfaces;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Errors;
using CryptoExchange.Net.SharedApis;

namespace Bitvavo.Net.Clients.SpotApi;

/// <summary>
/// Order book capabilities: [V2] <see cref="IGetOrderBookRest"/> and <see cref="IGetBookTickerRest"/> and the legacy [V1]
/// <see cref="IOrderBookRestClient"/> and <see cref="IBookTickerRestClient"/>, whose members all carry the same names as the V2
/// ones: each V1 interface is implemented by the very members V2 uses.
/// </summary>
internal sealed partial class BitvavoRestClientSpotSharedApi : IGetOrderBookRest, IGetBookTickerRest, IOrderBookRestClient, IBookTickerRestClient
{
    private const int _orderBookMaxDepth = 1000;

    // ── order book ───────────────────────────────────────────────────────────────────────

    async Task<IExchangeCallResult<SharedOrderBook>> IGetOrderBook.GetOrderBookAsync(GetOrderBookRequest request, CancellationToken ct)
        => await GetOrderBookAsync(request, ct).ConfigureAwait(false);

    /// <summary>
    /// Bitvavo serves 1 to 1000 levels per side. Its order book <c>nonce</c>, which increases with every change of the book, is
    /// the Shared sequence number.
    /// </summary>
    public GetOrderBookOptions GetOrderBookOptions { get; } = new GetOrderBookOptions(_exchangeName, 1, _orderBookMaxDepth, false);

    /// <inheritdoc />
    public async Task<HttpResult<SharedOrderBook>> GetOrderBookAsync(GetOrderBookRequest request, CancellationToken ct = default)
    {
        var validationError = GetOrderBookOptions.ValidateRequest(request, this);
        if (validationError != null)
        {
            return HttpResult.Fail<SharedOrderBook>(Exchange, validationError);
        }

        var result = await _api.ExchangeData.GetOrderBookAsync(request.Symbol!.GetSymbol(FormatSymbol), request.Limit, ct).ConfigureAwait(false);
        if (!result.Success)
        {
            return HttpResult.Fail<SharedOrderBook>(result);
        }

        var book = result.Data;
        return HttpResult.Ok(
            result,
            new SharedOrderBook(
                SharedQuantityType.BaseAsset,
                book.Nonce,
                book.Asks.Select(x => (ISymbolOrderBookEntry)new BitvavoSharedOrderBookEntry { Price = x.Price, Quantity = x.Size }).ToArray(),
                book.Bids.Select(x => (ISymbolOrderBookEntry)new BitvavoSharedOrderBookEntry { Price = x.Price, Quantity = x.Size }).ToArray()));
    }

    // ── book ticker ──────────────────────────────────────────────────────────────────────

    async Task<IExchangeCallResult<SharedBookTicker>> IGetBookTicker.GetBookTickerAsync(GetBookTickerRequest request, CancellationToken ct)
        => await GetBookTickerAsync(request, ct).ConfigureAwait(false);

    /// <inheritdoc />
    public GetBookTickerOptions GetBookTickerOptions { get; } = new GetBookTickerOptions(_exchangeName, false);

    /// <summary>
    /// A side of the book without orders has no price: Bitvavo leaves it out and the Shared book ticker, whose prices cannot be
    /// null, shows 0 with no quantity.
    /// </summary>
    public async Task<HttpResult<SharedBookTicker>> GetBookTickerAsync(GetBookTickerRequest request, CancellationToken ct = default)
    {
        var validationError = GetBookTickerOptions.ValidateRequest(request, this);
        if (validationError != null)
        {
            return HttpResult.Fail<SharedBookTicker>(Exchange, validationError);
        }

        var sharedSymbol = request.Symbol!;
        var market = sharedSymbol.GetSymbol(FormatSymbol);
        var result = await _api.ExchangeData.GetTickerBookAsync(market, ct).ConfigureAwait(false);
        if (!result.Success)
        {
            return HttpResult.Fail<SharedBookTicker>(result);
        }

        // The market filter is the server's job; matching here as well means a longer list can never pick the wrong market.
        var book = result.Data.FirstOrDefault(x => string.Equals(x.Market, market, StringComparison.OrdinalIgnoreCase));
        if (book == null)
        {
            return HttpResult.Fail<SharedBookTicker>(result, new ServerError(new ErrorInfo(ErrorType.UnknownSymbol, "Book ticker not found")));
        }

        return HttpResult.Ok(
            result,
            new SharedBookTicker(
                sharedSymbol,
                book.Market,
                book.Ask ?? 0m,
                new SharedOrderQuantity(book.AskSize),
                book.Bid ?? 0m,
                new SharedOrderQuantity(book.BidSize)));
    }
}
