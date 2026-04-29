// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Bitvavo.Net.Clients.MessageHandlers;
using Bitvavo.Net.Objects.Models.Spot.Streams;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests;

/// <summary>
/// Drives Phase 2C — currently <see cref="BitvavoSocketSpotMessageHandler"/> registers
/// <c>AddTopicMapping&lt;BitvavoStreamOrderUpdate&gt;(_ =&gt; string.Empty)</c> and the
/// equivalent for fill events. Two account subscriptions on disjoint markets therefore
/// share the same routing key (empty), so any incoming order/fill is dispatched to BOTH
/// subscriptions — a privacy-class bug.
///
/// After the fix the topic selectors must be <c>x =&gt; x.Market</c> so each subscription
/// only receives events for the markets it asked for.
///
/// The tests reach into the framework's internal mapping store via reflection: a full
/// socket round-trip would require pinning the auth-WS HMAC signature (deferred), but the
/// topic-selector contract is what actually drives Bitvavo's per-subscription dispatch.
/// </summary>
public class BitvavoSocketAccountFilterTests
{
    private static Delegate? FindTopicMapping<T>(BitvavoSocketSpotMessageHandler handler)
    {
        for (var t = (Type?)handler.GetType(); t != null; t = t.BaseType)
        {
            foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (f.GetValue(handler) is not IDictionary dict) continue;
                foreach (DictionaryEntry entry in dict)
                {
                    if (entry.Key is Type k && k == typeof(T) && entry.Value is Delegate del)
                        return del;
                }
            }
        }
        return null;
    }

    private static string? InvokeTopic<T>(BitvavoSocketSpotMessageHandler handler, T sample)
    {
        var del = FindTopicMapping<T>(handler);
        if (del is Func<T, string> typed) return typed(sample);
        return del?.DynamicInvoke(sample) as string;
    }

    [Fact]
    public void OrderUpdate_TopicSelector_uses_Market_not_empty()
    {
        var handler = new BitvavoSocketSpotMessageHandler();
        var sample = new BitvavoStreamOrderUpdate { Market = "BTC-EUR" };

        var topic = InvokeTopic(handler, sample);

        // Phase 2C contract: per-market routing — never empty (would route to all).
        topic.ShouldBe("BTC-EUR");
    }

    [Fact]
    public void FillEvent_TopicSelector_uses_Market_not_empty()
    {
        var handler = new BitvavoSocketSpotMessageHandler();
        var sample = new BitvavoStreamFillEvent { Market = "ETH-EUR" };

        var topic = InvokeTopic(handler, sample);

        topic.ShouldBe("ETH-EUR");
    }

    [Fact]
    public void OrderUpdate_TopicSelector_disjoint_markets_yield_disjoint_topic_keys()
    {
        var handler = new BitvavoSocketSpotMessageHandler();

        var btc = InvokeTopic(handler, new BitvavoStreamOrderUpdate { Market = "BTC-EUR" });
        var eth = InvokeTopic(handler, new BitvavoStreamOrderUpdate { Market = "ETH-EUR" });

        btc.ShouldBe("BTC-EUR");
        eth.ShouldBe("ETH-EUR");
        btc.ShouldNotBe(eth);
    }

    [Fact]
    public void FillEvent_TopicSelector_disjoint_markets_yield_disjoint_topic_keys()
    {
        var handler = new BitvavoSocketSpotMessageHandler();

        var btc = InvokeTopic(handler, new BitvavoStreamFillEvent { Market = "BTC-EUR" });
        var eth = InvokeTopic(handler, new BitvavoStreamFillEvent { Market = "ETH-EUR" });

        btc.ShouldBe("BTC-EUR");
        eth.ShouldBe("ETH-EUR");
        btc.ShouldNotBe(eth);
    }

    [Fact]
    public void OrderUpdate_TopicMapping_isRegistered()
    {
        // Sanity: mapping exists. If the framework field name changes and reflection misses
        // the dictionary, the four behavioral tests above all silently no-op — this test
        // ensures the reflection plumbing itself can find the mapping.
        var handler = new BitvavoSocketSpotMessageHandler();
        FindTopicMapping<BitvavoStreamOrderUpdate>(handler).ShouldNotBeNull();
    }
}
