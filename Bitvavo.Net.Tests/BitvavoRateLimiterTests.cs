// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Bitvavo.Net.Clients;
using Bitvavo.Net.Clients.SpotApi;
using Bitvavo.Net.Objects.Internal;
using Bitvavo.Net.Objects.Options;
using Bitvavo.Net.Tests.Clients.SpotApi;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.RateLimiting;
using CryptoExchange.Net.RateLimiting.Filters;
using CryptoExchange.Net.RateLimiting.Guards;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests;

/// <summary>
/// Client-side rate limiting, end to end through the real CryptoExchange.Net request pipeline (only the HTTP transport is
/// replaced). Every test starts with a fresh limiter (<see cref="FreshRateLimiterAttribute"/>), so the counters read back
/// are exactly what the test itself spent. Oracle: the weights and budgets Bitvavo documents (docs.bitvavo.com, "Rate limits").
/// </summary>
public class BitvavoRateLimiterTests
{
    private readonly record struct ApiUnderTest(BitvavoRestClientSpotApi Api, StubHttpMessageHandler Handler);

    private static ApiUnderTest CreateApi(
        RateLimitingBehaviour behaviour = RateLimitingBehaviour.Wait,
        Func<RequestDefinition, int, RateLimitAdmission>? admission = null,
        string response = "[]")
    {
        var handler = new StubHttpMessageHandler(response);
        var options = new BitvavoRestOptions
        {
            ApiCredentials = new BitvavoCredentials("test-key", "test-secret"),
            RateLimitingBehaviour = behaviour,
            RateLimitAdmission = admission,
        };
        var client = new BitvavoRestClient(new HttpClient(handler), null, Options.Create(options));
        return new ApiUnderTest((BitvavoRestClientSpotApi)client.SpotApi, handler);
    }

    private static List<RateLimitUpdateEvent> RecordUpdates()
    {
        var updates = new List<RateLimitUpdateEvent>();
        BitvavoExchange.RateLimiter.RateLimitUpdated += updates.Add;
        return updates;
    }

    public static IEnumerable<object[]> ContractNames() => BitvavoEndpointContracts.All.Select(c => new object[] { c.Name });

    // ── weights ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(ContractNames))]
    public async Task Endpoint_counts_its_documented_weight(string name)
    {
        var contract = BitvavoEndpointContracts.All.Single(c => c.Name == name);
        var updates = RecordUpdates();
        var api = CreateApi();

        await contract.Call(api.Api);

        if (contract.Weight == 0)
        {
            updates.ShouldBeEmpty();
            return;
        }

        var update = updates.ShouldHaveSingleItem();
        update.Current.ShouldBe(contract.Weight);
        update.Limit.ShouldBe(BitvavoExchange.WeightPerMinute);
    }

    [Fact]
    public void Every_rest_endpoint_has_a_contract_row()
    {
        var endpoints = typeof(Bitvavo.Net.Interfaces.Clients.SpotApi.IBitvavoRestClientSpotApi).GetProperties()
            .Where(p => p.PropertyType.IsInterface
                && p.PropertyType.Name.StartsWith("IBitvavoRestClientSpotApi", StringComparison.Ordinal)
                && !p.PropertyType.Name.Contains("Shared", StringComparison.Ordinal))
            .SelectMany(p => p.PropertyType.GetMethods().Select(m => $"{p.Name}.{m.Name}"))
            .ToHashSet();
        var covered = BitvavoEndpointContracts.All.Select(c => c.Name[..c.Name.IndexOf('#')]).ToHashSet();

        covered.ShouldBe(endpoints, ignoreOrder: true);
    }

    [Fact]
    public async Task Open_orders_without_market_counts_weight_100_after_a_market_call()
    {
        var updates = RecordUpdates();
        var api = CreateApi();

        await api.Api.Trading.GetOpenOrdersAsync("BTC-EUR", ct: TestContext.Current.CancellationToken);
        await api.Api.Trading.GetOpenOrdersAsync(ct: TestContext.Current.CancellationToken);

        updates.Select(u => u.Current).ShouldBe([5, 105]);
    }

    // ── budgets ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Public_and_authenticated_requests_count_against_independent_budgets()
    {
        var updates = RecordUpdates();
        var api = CreateApi();

        await api.Api.ExchangeData.GetPublicTradesAsync("BTC-EUR", ct: TestContext.Current.CancellationToken);
        await api.Api.Trading.GetOpenOrdersAsync(ct: TestContext.Current.CancellationToken);
        await api.Api.ExchangeData.GetPublicTradesAsync("BTC-EUR", ct: TestContext.Current.CancellationToken);

        updates.Select(u => u.Current).ShouldBe([5, 100, 10]);
    }

    [Fact]
    public async Task Gate_counts_weight_and_refuses_with_ClientRateLimitError_under_Fail()
    {
        var api = CreateApi(RateLimitingBehaviour.Fail);

        for (var i = 0; i < 9; i++)
        {
            (await api.Api.Trading.GetOpenOrdersAsync(ct: TestContext.Current.CancellationToken)).Success.ShouldBeTrue();
        }

        var refused = await api.Api.Trading.GetOpenOrdersAsync(ct: TestContext.Current.CancellationToken);

        refused.Success.ShouldBeFalse();
        refused.Error.ShouldBeOfType<ClientRateLimitError>();
        api.Handler.Requests.Count.ShouldBe(9);
    }

    /// <summary>
    /// Bitvavo's counter resets on the clock minute, but this host's clock runs ahead of Bitvavo's (480–529 ms measured), so the
    /// guards slide over the trailing minute instead. The tell, with no clock stand-in: once the budget is spent, the wait
    /// handed to the next request is a full trailing minute whatever the second of the clock — a clock-aligned window would
    /// hand out whatever is left of the current minute.
    /// </summary>
    [Fact]
    public async Task Exhausted_budget_makes_waiters_wait_a_trailing_minute_not_the_clock_minute()
    {
        var api = CreateApi();
        using var cts = new CancellationTokenSource();
        var triggers = new List<RateLimitEvent>();
        BitvavoExchange.RateLimiter.RateLimitTriggered += trigger =>
        {
            triggers.Add(trigger);
            cts.Cancel();
        };
        for (var i = 0; i < 9; i++)
        {
            await api.Api.Trading.GetOpenOrdersAsync(ct: TestContext.Current.CancellationToken);
        }

        var blocked = await api.Api.Trading.GetOpenOrdersAsync(ct: cts.Token);

        blocked.Error.ShouldBeOfType<CancellationRequestedError>();
        triggers.ShouldHaveSingleItem().DelayTime!.Value.ShouldBeGreaterThan(TimeSpan.FromSeconds(59));
    }

    // ── headroom (admission) ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0.1)]
    [InlineData(0.9)]
    [InlineData(1.0)]
    public void Admission_ratio_that_admits_the_heaviest_request_is_accepted(double maxUtilization)
    {
        new BitvavoRateLimiters(maxUtilization).MaxUtilization.ShouldBe(maxUtilization);
    }

    [Theory]
    [InlineData(0.09)]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(1.01)]
    public void Admission_ratio_below_weight_over_limit_is_rejected_at_configuration(double maxUtilization)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new BitvavoRateLimiters(maxUtilization));
    }

    [Fact]
    public void Limit_below_the_heaviest_request_is_rejected_at_configuration()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => new BitvavoRateLimiters(weightPerMinute: 99));
    }

    [Fact]
    public void Default_headroom_is_90_percent_of_the_documented_1000_points()
    {
        BitvavoExchange.RateLimiter.MaxUtilization.ShouldBe(0.9);
        BitvavoExchange.RateLimiter.WeightPerMinute.ShouldBe(1000);
    }

    [Fact]
    public async Task Request_admission_that_can_never_fit_is_refused_not_thrown()
    {
        var api = CreateApi(admission: (_, _) => RateLimitAdmission.WithMaxUtilizationRatio(0.05));

        var refused = await api.Api.Trading.GetOpenOrdersAsync(ct: TestContext.Current.CancellationToken);

        refused.Error.ShouldBeOfType<ClientRateLimitError>();
        api.Handler.Requests.ShouldBeEmpty();
    }

    // ── the settable limiter ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Replaced_RateLimiter_is_used_by_already_cached_definitions()
    {
        var api = CreateApi();
        await api.Api.Trading.GetOpenOrdersAsync(ct: TestContext.Current.CancellationToken);
        var replacement = new BitvavoRateLimiters();
        BitvavoExchange.RateLimiter = replacement;
        var updates = new List<RateLimitUpdateEvent>();
        replacement.RateLimitUpdated += updates.Add;

        await api.Api.Trading.GetOpenOrdersAsync(ct: TestContext.Current.CancellationToken);

        updates.ShouldHaveSingleItem().Current.ShouldBe(100);
    }

    [Fact]
    public void Setting_a_null_RateLimiter_is_rejected()
    {
        Should.Throw<ArgumentNullException>(() => BitvavoExchange.RateLimiter = null!);
    }

    [Fact]
    public async Task Stable_gate_forwards_retry_after_to_the_current_limiter()
    {
        var gate = BitvavoRestClientSpotApi.RateLimitGate;
        var retryAfter = DateTime.UtcNow.AddSeconds(30);

        await gate.SetRetryAfterGuardAsync(retryAfter);

        (await gate.GetRetryAfterTime()).ShouldBe(retryAfter);
        BitvavoExchange.RateLimiter = new BitvavoRateLimiters();
        (await gate.GetRetryAfterTime()).ShouldBeNull();
    }

    [Fact]
    public async Task Stable_gate_forwards_added_guards_and_events_to_the_current_limiter()
    {
        var gate = BitvavoRestClientSpotApi.RateLimitGate;
        var triggers = new List<RateLimitEvent>();
        gate.RateLimitTriggered += triggers.Add;
        // Limit 10 under the 90 % headroom admits exactly one balance request (weight 5): (5 + 5) / 10 is over 0.9.
        var returned = gate.AddGuard(new RateLimitGuard(
            RateLimitGuard.PerHost, new AuthenticatedEndpointFilter(true), 10, TimeSpan.FromMinutes(1), RateLimitWindowType.Sliding));
        var api = CreateApi(RateLimitingBehaviour.Fail);

        (await api.Api.Account.GetBalancesAsync(ct: TestContext.Current.CancellationToken)).Success.ShouldBeTrue();
        var refused = await api.Api.Account.GetBalancesAsync(ct: TestContext.Current.CancellationToken);

        returned.ShouldBeSameAs(gate);
        refused.Error.ShouldBeOfType<ClientRateLimitError>();
        triggers.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Stable_gate_forwards_a_reset_to_the_current_limiter()
    {
        var updates = RecordUpdates();
        var api = CreateApi();
        await api.Api.Trading.GetOpenOrdersAsync(ct: TestContext.Current.CancellationToken);
        var definition = new RequestDefinition("https://api.bitvavo.com", "v2/ordersOpen", HttpMethod.Get) { Authenticated = true };

        await BitvavoRestClientSpotApi.RateLimitGate.ResetAsync(RateLimitItemType.Request, definition, "test-key", null, null, TestContext.Current.CancellationToken);
        await api.Api.Trading.GetOpenOrdersAsync(ct: TestContext.Current.CancellationToken);

        updates.Select(u => u.Current).ShouldBe([100, 100]);
    }

    // ── the heartbeat ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <c>cancelOrdersAfter</c> is documented at weight 5 and deliberately never counted: a refused heartbeat would let the
    /// cancel-on-disconnect timer expire and Bitvavo would cancel the group's orders.
    /// </summary>
    [Fact]
    public async Task Cancel_on_disconnect_heartbeat_is_never_refused()
    {
        var api = CreateApi(RateLimitingBehaviour.Fail);
        for (var i = 0; i < 9; i++)
        {
            await api.Api.Trading.GetOpenOrdersAsync(ct: TestContext.Current.CancellationToken);
        }
        (await api.Api.Trading.GetOpenOrdersAsync(ct: TestContext.Current.CancellationToken)).Error.ShouldBeOfType<ClientRateLimitError>();
        var heartbeatApi = CreateApi(RateLimitingBehaviour.Fail, response: """{"codGroupId":1,"timeOfExpirySeconds":1900000000}""");

        var heartbeat = await heartbeatApi.Api.Account.ResetCancelOnDisconnectAsync(1, 30, TestContext.Current.CancellationToken);

        heartbeat.Success.ShouldBeTrue();
        heartbeatApi.Handler.Requests.ShouldHaveSingleItem();
    }
}
