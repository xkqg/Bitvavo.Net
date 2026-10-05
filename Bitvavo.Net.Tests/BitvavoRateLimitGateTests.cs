// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Bitvavo.Net.Objects.Internal;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.RateLimiting;
using CryptoExchange.Net.RateLimiting.Filters;
using CryptoExchange.Net.RateLimiting.Guards;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests;

/// <summary>
/// The one gate every request definition is created with. It owns no state — every call goes to whatever
/// <see cref="BitvavoExchange.RateLimiter"/> is current — so these tests drive the gate itself and read the effect on the limiter.
/// </summary>
public class BitvavoRateLimitGateTests
{
    private static readonly RequestDefinition Definition = new("https://api.bitvavo.com", "v2/time", HttpMethod.Get) { Authenticated = false };

    private static ValueTask<CallResult> Process(int weight, RateLimitingBehaviour behaviour = RateLimitingBehaviour.Fail)
        => BitvavoRateLimitGate.Instance.ProcessAsync(
            NullLogger.Instance, 1, RateLimitItemType.Request, Definition, null, weight, behaviour, null, 1.0, TestContext.Current.CancellationToken);

    [Fact]
    public async Task A_counter_listener_added_through_the_gate_hears_the_limiter_until_it_is_removed()
    {
        var updates = new List<RateLimitUpdateEvent>();
        void Listener(RateLimitUpdateEvent update) => updates.Add(update);
        BitvavoRateLimitGate.Instance.RateLimitUpdated += Listener;

        await Process(1);
        BitvavoRateLimitGate.Instance.RateLimitUpdated -= Listener;
        await Process(1);

        updates.ShouldHaveSingleItem().Current.ShouldBe(1);
    }

    [Fact]
    public async Task A_trigger_listener_added_through_the_gate_hears_a_refused_request_until_it_is_removed()
    {
        // 10 % of 1000 points is exactly the weight of the heaviest request: one such request fills the budget.
        BitvavoExchange.RateLimiter = new BitvavoRateLimiters(maxUtilization: 0.1);
        var triggers = new List<RateLimitEvent>();
        void Listener(RateLimitEvent trigger) => triggers.Add(trigger);
        BitvavoRateLimitGate.Instance.RateLimitTriggered += Listener;

        (await Process(100)).Success.ShouldBeTrue();
        (await Process(100)).Success.ShouldBeFalse();
        BitvavoRateLimitGate.Instance.RateLimitTriggered -= Listener;
        (await Process(100)).Success.ShouldBeFalse();

        triggers.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task A_single_guard_can_be_processed_through_the_gate()
    {
        var guard = new RateLimitGuard(RateLimitGuard.PerHost, new AuthenticatedEndpointFilter(false), 10, TimeSpan.FromMinutes(1), RateLimitWindowType.Sliding);

        var first = await BitvavoRateLimitGate.Instance.ProcessSingleAsync(
            NullLogger.Instance, 1, guard, RateLimitItemType.Request, Definition, null, 6, RateLimitingBehaviour.Fail, null, 1.0, TestContext.Current.CancellationToken);
        var second = await BitvavoRateLimitGate.Instance.ProcessSingleAsync(
            NullLogger.Instance, 2, guard, RateLimitItemType.Request, Definition, null, 6, RateLimitingBehaviour.Fail, null, 1.0, TestContext.Current.CancellationToken);

        first.Success.ShouldBeTrue();
        second.Success.ShouldBeFalse();
    }
}
