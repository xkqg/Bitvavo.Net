// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;

namespace Bitvavo.Net.Objects.Internal;

/// <summary>
/// Which part of a channel nobody needs any more. Bitvavo's server holds one flat set of (channel, market, interval) entries
/// per connection and an <c>unsubscribe</c> removes entries whoever asked for them, so closing a subscription may only remove the
/// entries no other live subscription on the connection still needs. The framework cannot decide this: it compares the routes
/// of two subscriptions as wholes, which is wrong for partial overlap (a subscription covered by a larger one sends no frame and
/// would lose its feed when the larger one closes) and for the private <c>account</c> channel, which the order and the fill
/// subscription share under different routes.
/// </summary>
internal static class BitvavoChannelDemand
{
    /// <summary>One entry of the server's set: a market of a channel, with the candle interval where the channel has one.</summary>
    private readonly record struct ChannelKey(string Market, string? Interval);

    /// <summary>
    /// The channels to unsubscribe when <paramref name="closing"/> closes: its entries minus those of every channel in
    /// <paramref name="stillNeeded"/> that has the same name, grouped back into one channel per interval. Empty when nothing is left,
    /// in which case no frame may be sent at all.
    /// </summary>
    /// <param name="closing">The channel of the subscription that closes.</param>
    /// <param name="stillNeeded">The channels of the other live subscriptions on the same connection.</param>
    internal static BitvavoSocketChannel[] Unneeded(BitvavoSocketChannel closing, IEnumerable<BitvavoSocketChannel> stillNeeded)
    {
        var needed = stillNeeded
            .Where(channel => channel.Name == closing.Name)
            .SelectMany(Keys)
            .ToHashSet();

        return Keys(closing)
            .Where(key => !needed.Contains(key))
            .GroupBy(key => key.Interval)
            .Select(group => new BitvavoSocketChannel
            {
                Name = closing.Name,
                Interval = group.Key is null ? null : [group.Key],
                Markets = group.Select(key => key.Market).Distinct().ToArray(),
            })
            .ToArray();
    }

    private static IEnumerable<ChannelKey> Keys(BitvavoSocketChannel channel)
    {
        string?[] intervals = channel.Interval is { Length: > 0 } ? channel.Interval : new string?[] { null };
        return channel.Markets.SelectMany(market => intervals.Select(interval => new ChannelKey(market, interval)));
    }
}
