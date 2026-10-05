// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.RateLimiting;
using CryptoExchange.Net.RateLimiting.Interfaces;
using Microsoft.Extensions.Logging;

namespace Bitvavo.Net.Objects.Internal;

/// <summary>
/// The one gate every Bitvavo REST request definition is bound to. It owns no state: each call goes to whatever
/// <see cref="BitvavoExchange.RateLimiter"/> is current.
/// </summary>
/// <remarks>
/// A request definition is cached on first use and keeps the gate it was created with. Binding the definitions to the limiter
/// itself would make <see cref="BitvavoExchange.RateLimiter"/> inert for every endpoint already called once; binding them to
/// this indirection keeps the setter effective at any time.
/// </remarks>
internal sealed class BitvavoRateLimitGate : IRateLimitGate
{
    internal static BitvavoRateLimitGate Instance { get; } = new();

    private BitvavoRateLimitGate()
    {
    }

    private static BitvavoRateLimiters Current => BitvavoExchange.RateLimiter;

    /// <inheritdoc />
    public event Action<RateLimitEvent> RateLimitTriggered
    {
        add => Current.RateLimitTriggered += value;
        remove => Current.RateLimitTriggered -= value;
    }

    /// <inheritdoc />
    public event Action<RateLimitUpdateEvent>? RateLimitUpdated
    {
        add => Current.RateLimitUpdated += value;
        remove => Current.RateLimitUpdated -= value;
    }

    /// <inheritdoc />
    public IRateLimitGate AddGuard(IRateLimitGuard guard)
    {
        Current.Gate.AddGuard(guard);
        return this;
    }

    /// <inheritdoc />
    public Task SetRetryAfterGuardAsync(DateTime retryAfter, RateLimitItemType type = RateLimitItemType.Request)
        => Current.Gate.SetRetryAfterGuardAsync(retryAfter, type);

    /// <inheritdoc />
    public Task<DateTime?> GetRetryAfterTime()
        => Current.Gate.GetRetryAfterTime();

    /// <inheritdoc />
    public ValueTask<CallResult> ProcessAsync(
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
        => Current.ProcessAsync(logger, itemId, type, definition, apiKey, requestWeight, behaviour, keySuffix, allowedRateRatio, ct);

    /// <inheritdoc />
    public ValueTask<CallResult> ProcessSingleAsync(
        ILogger logger,
        int itemId,
        IRateLimitGuard guard,
        RateLimitItemType type,
        RequestDefinition definition,
        string? apiKey,
        int requestWeight,
        RateLimitingBehaviour behaviour,
        string? keySuffix,
        double allowedRateRatio,
        CancellationToken ct)
        => Current.Gate.ProcessSingleAsync(logger, itemId, guard, type, definition, apiKey, requestWeight, behaviour, keySuffix, allowedRateRatio, ct);

    /// <inheritdoc />
    public Task ResetAsync(
        RateLimitItemType type,
        RequestDefinition definition,
        string? apiKey,
        string? keySuffix,
        int? amount,
        CancellationToken ct)
        => Current.Gate.ResetAsync(type, definition, apiKey, keySuffix, amount, ct);
}
