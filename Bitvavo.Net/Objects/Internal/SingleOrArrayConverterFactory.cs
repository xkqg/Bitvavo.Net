// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bitvavo.Net.Objects.Internal;

/// <summary>
/// Lets every <see cref="IEnumerable{T}"/> read what Bitvavo sends for a query that names one item: a single JSON object where the
/// same query without a name returns an array. Recorded from the live API: <c>GET /v2/ticker/24h?market=BTC-EUR</c> (also
/// <c>ticker/book</c>, <c>ticker/price</c> and <c>markets</c>) answers an object although the OpenAPI specification shows an array.
/// An array is read as before; a single object becomes a collection of one.
/// </summary>
internal sealed class SingleOrArrayConverterFactory : JsonConverterFactory
{
    /// <summary>Only the interface itself: a <see cref="List{T}"/> or an array property keeps the framework's own reading.</summary>
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(IEnumerable<>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        => (JsonConverter)Activator.CreateInstance(typeof(SingleOrArrayConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()[0]))!;

    private sealed class SingleOrArrayConverter<T> : JsonConverter<IEnumerable<T>>
    {
        // The element and the list are read through List<T> and T, never through IEnumerable<T> again: that would re-enter this converter.
        public override IEnumerable<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => reader.TokenType switch
            {
                JsonTokenType.StartObject => [JsonSerializer.Deserialize<T>(ref reader, options)!],
                _ => JsonSerializer.Deserialize<List<T>>(ref reader, options),
            };

        public override void Write(Utf8JsonWriter writer, IEnumerable<T> value, JsonSerializerOptions options)
            => JsonSerializer.Serialize(writer, value.ToList(), options);
    }
}
