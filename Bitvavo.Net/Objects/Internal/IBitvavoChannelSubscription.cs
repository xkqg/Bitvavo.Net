// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

namespace Bitvavo.Net.Objects.Internal;

/// <summary>A subscription that holds one Bitvavo channel, so the other subscriptions on its connection can tell what it still needs.</summary>
internal interface IBitvavoChannelSubscription
{
    /// <summary>The channel this subscription subscribes to and unsubscribes from.</summary>
    BitvavoSocketChannel Channel { get; }
}
