// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.RateLimiting;
using CryptoExchange.Net.RateLimiting.Filters;
using CryptoExchange.Net.RateLimiting.Guards;
using CryptoExchange.Net.RateLimiting.Interfaces;
using Microsoft.Extensions.Logging;

namespace Bitvavo.Net.Objects.Internal;

/// <summary>
/// Client-side rate limiting for the Bitvavo REST API — what <see cref="BitvavoExchange.RateLimiter"/> holds.
/// </summary>
/// <remarks>
/// <para>Bitvavo allocates a weight budget per minute (default 1000, see <see cref="WeightPerMinute"/>) and tracks it
/// independently for authenticated requests (across all of the user's keys and accounts) and for unauthenticated requests
/// (per IP address). Going over it blocks the caller for 1 minute (authenticated) or 15 minutes (IP): the limiter therefore
/// keeps both budgets separately, and keeps <see cref="MaxUtilization"/> of each free as headroom.</para>
/// <para>Both budgets slide over the trailing minute. Bitvavo itself resets its counter on the clock minute, but a local clock
/// that runs ahead of Bitvavo's (480–529 ms measured) would let a clock-aligned window admit a full budget twice around
/// the boundary; a sliding window is immune to that skew.</para>
/// <para>The weight of every request is counted when it is sent; WebSocket frames (also weight 1 each) are not counted.
/// The authenticated budget is one tracker per process, whichever key signs: that is the conservative reading of "all of your
/// accounts".</para>
/// </remarks>
public sealed class BitvavoRateLimiters
{
    /// <summary>The share of the budget a client may use unless configured otherwise: 90 %, leaving 10 % headroom for other processes on the same account or IP.</summary>
    public const double DefaultMaxUtilization = 0.9;

    private static readonly Func<RequestDefinition, string?, string> PerProcess = static (_, _) => "authenticated";

    private readonly RateLimitGate _gate;

    /// <summary>Event for when a request has to wait for, or is refused by, the rate limit.</summary>
    public event Action<RateLimitEvent>? RateLimitTriggered;

    /// <summary>Event for when the counter changes. It only changes when a request is sent; there are no updates while the usage slides out of the window.</summary>
    public event Action<RateLimitUpdateEvent>? RateLimitUpdated;

    /// <summary>The share of each budget a request may use, between <c>heaviest request / <see cref="WeightPerMinute"/></c> and 1.</summary>
    public double MaxUtilization { get; }

    /// <summary>The weight points per minute Bitvavo allocates to the account or IP address (<c>bitvavo-ratelimit-limit</c>); 1000 unless an agreement says otherwise.</summary>
    public int WeightPerMinute { get; }

    /// <summary>Create the rate limiter.</summary>
    /// <param name="maxUtilization">The share of each budget a request may use; the default is <see cref="DefaultMaxUtilization"/>.
    /// It must admit the heaviest request (weight 100), so with the default limit it is at least 0.1.</param>
    /// <param name="weightPerMinute">The weight points per minute allocated to the account or IP address; at least the weight of the heaviest request (100).</param>
    /// <exception cref="ArgumentOutOfRangeException">The limit or the utilization cannot admit the heaviest request.</exception>
    public BitvavoRateLimiters(double maxUtilization = DefaultMaxUtilization, int weightPerMinute = BitvavoExchange.WeightPerMinute)
    {
        if (weightPerMinute < BitvavoExchange.MaxRequestWeight)
        {
            throw new ArgumentOutOfRangeException(nameof(weightPerMinute), weightPerMinute,
                $"The limit must be at least {BitvavoExchange.MaxRequestWeight}, the weight of the heaviest request; a request that heavy could never be sent.");
        }

        var minimumUtilization = BitvavoExchange.MaxRequestWeight / (double)weightPerMinute;
        if (!(maxUtilization >= minimumUtilization && maxUtilization <= 1))
        {
            throw new ArgumentOutOfRangeException(nameof(maxUtilization), maxUtilization,
                $"The utilization must be between {minimumUtilization} and 1: the heaviest request ({BitvavoExchange.MaxRequestWeight} points) would otherwise never fit in {weightPerMinute} points per minute.");
        }

        MaxUtilization = maxUtilization;
        WeightPerMinute = weightPerMinute;

        _gate = new RateLimitGate("Bitvavo Spot Rest");
        _gate.AddGuard(new RateLimitGuard(RateLimitGuard.PerHost, new AuthenticatedEndpointFilter(false), weightPerMinute, TimeSpan.FromMinutes(1), RateLimitWindowType.Sliding));
        _gate.AddGuard(new RateLimitGuard(PerProcess, new AuthenticatedEndpointFilter(true), weightPerMinute, TimeSpan.FromMinutes(1), RateLimitWindowType.Sliding));
        _gate.RateLimitTriggered += trigger => RateLimitTriggered?.Invoke(trigger);
        _gate.RateLimitUpdated += update => RateLimitUpdated?.Invoke(update);
    }

    /// <summary>The CryptoExchange.Net gate that holds the two guards; reached only through <see cref="BitvavoRateLimitGate"/>.</summary>
    internal IRateLimitGate Gate => _gate;

    /// <summary>
    /// Admit or refuse one request. The utilization that applies is the stricter of the limiter's own and the one the caller asked
    /// for (the <c>RateLimitAdmission</c> client option); a request whose weight can never fit under it is refused with a
    /// <see cref="ClientRateLimitError"/> — the framework's tracker would throw for it.
    /// </summary>
    internal ValueTask<CallResult> ProcessAsync(
        ILogger logger,
        int itemId,
        RateLimitItemType type,
        RequestDefinition definition,
        string? apiKey,
        int requestWeight,
        RateLimitingBehaviour behaviour,
        string? keySuffix,
        double allowedRateRatio,
        CancellationToken ct)
    {
        var ratio = Math.Min(allowedRateRatio, MaxUtilization);
        if (requestWeight / (double)WeightPerMinute > ratio)
        {
            return new ValueTask<CallResult>(CallResult.Fail(new ClientRateLimitError(
                $"A request of weight {requestWeight} can never be admitted: it is {requestWeight / (double)WeightPerMinute} of the {WeightPerMinute} points per minute, " +
                $"but at most {ratio} of the budget may be used. Raise the admission ratio of the client or the limiter's {nameof(MaxUtilization)}.")));
        }

        return _gate.ProcessAsync(logger, itemId, type, definition, apiKey, requestWeight, behaviour, keySuffix, ratio, ct);
    }
}
