// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Net.Http;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.RateLimiting.Interfaces;

namespace Bitvavo.Net.Clients.SpotApi;

/// <summary>
/// Bitvavo-internal extensions on <see cref="RequestDefinitionCache"/>. Centralises the
/// "InUri-DELETE" pattern Bitvavo uses for <c>DELETE /v2/order</c> and <c>DELETE /v2/orders</c>
/// — the framework's default for <see cref="HttpMethod.Delete"/> is in-body, but Bitvavo's
/// HMAC signing covers path + query (never body for non-POST/PUT), so the parameters must
/// land in the URI.
/// </summary>
internal static class RequestDefinitionCacheExtensions
{
    /// <summary>
    /// Get-or-create a rate-limit-gated cached <see cref="RequestDefinition"/> with
    /// <see cref="HttpMethodParameterPosition.InUri"/> applied. Mirrors the 5-arg
    /// <c>RequestDefinitionCache.GetOrCreate(method, path, gate, weight, authenticated)</c>
    /// overload — it threads the per-host <paramref name="gate"/> and per-endpoint
    /// <paramref name="weight"/> through so InUri DELETEs are rate-limit-scheduled exactly like
    /// every other endpoint, then unconditionally sets the InUri parameter position. Idempotent:
    /// the cache returns the same instance for repeated (method, path, authenticated) keys, and
    /// the position is set on every call so concurrent first-callers can't observe a half-built
    /// definition.
    /// </summary>
    /// <param name="cache">The per-sub-client request-definition cache.</param>
    /// <param name="method">The HTTP method (always <see cref="HttpMethod.Delete"/> for Bitvavo InUri use).</param>
    /// <param name="path">The endpoint path relative to the Spot REST base address.</param>
    /// <param name="gate">The per-host rate-limit gate the request is scheduled against.</param>
    /// <param name="weight">The Bitvavo weight this endpoint consumes from the per-minute budget.</param>
    /// <param name="authenticated">Whether the request must be HMAC-signed.</param>
    /// <returns>The cached, InUri-positioned, gate-bound <see cref="RequestDefinition"/>.</returns>
    public static RequestDefinition GetOrCreateInUri(
        this RequestDefinitionCache cache,
        HttpMethod method,
        string path,
        IRateLimitGate gate,
        int weight,
        bool authenticated)
    {
        var def = cache.GetOrCreate(method, path, gate, weight, authenticated);
        def.ParameterPosition = HttpMethodParameterPosition.InUri;
        return def;
    }
}
