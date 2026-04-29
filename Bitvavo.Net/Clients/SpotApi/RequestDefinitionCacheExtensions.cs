// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Net.Http;
using CryptoExchange.Net.Objects;

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
    /// Get-or-create a cached <see cref="RequestDefinition"/> with
    /// <see cref="HttpMethodParameterPosition.InUri"/> applied. Idempotent: the cache
    /// returns the same instance for repeated (method, path, authenticated) keys, and the
    /// position is unconditionally set so concurrent first-callers can't observe a half-built
    /// definition.
    /// </summary>
    public static RequestDefinition GetOrCreateInUri(
        this RequestDefinitionCache cache,
        HttpMethod method,
        string path,
        bool authenticated)
    {
        var def = cache.GetOrCreate(method, path, authenticated);
        def.ParameterPosition = HttpMethodParameterPosition.InUri;
        return def;
    }
}
