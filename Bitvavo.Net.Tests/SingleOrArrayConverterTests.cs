// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Bitvavo.Net.Objects.Internal;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests;

/// <summary>
/// The converter that lets a collection property read what Bitvavo sends for a query that names one item: a single object where
/// the same query without a name returns an array.
/// </summary>
public class SingleOrArrayConverterTests
{
    private sealed record Item(string Name, int Size);

    private sealed record Holder(IEnumerable<Item>? Items);

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new SingleOrArrayConverterFactory() },
    };

    [Fact]
    public void An_array_stays_a_collection()
    {
        var items = JsonSerializer.Deserialize<IEnumerable<Item>>("""[{"name":"a","size":1},{"name":"b","size":2}]""", Options);

        items.ShouldNotBeNull().Select(x => x.Name).ShouldBe(["a", "b"]);
    }

    [Fact]
    public void A_single_object_becomes_a_collection_of_one()
    {
        var items = JsonSerializer.Deserialize<IEnumerable<Item>>("""{"name":"a","size":1}""", Options);

        items.ShouldNotBeNull().ShouldHaveSingleItem().ShouldBe(new Item("a", 1));
    }

    [Fact]
    public void An_empty_array_stays_empty()
    {
        JsonSerializer.Deserialize<IEnumerable<Item>>("[]", Options).ShouldNotBeNull().ShouldBeEmpty();
    }

    [Fact]
    public void Null_stays_null()
    {
        JsonSerializer.Deserialize<IEnumerable<Item>>("null", Options).ShouldBeNull();
    }

    [Fact]
    public void A_collection_property_of_a_model_accepts_both_shapes()
    {
        var one = JsonSerializer.Deserialize<Holder>("""{"items":{"name":"a","size":1}}""", Options);
        var many = JsonSerializer.Deserialize<Holder>("""{"items":[{"name":"a","size":1},{"name":"b","size":2}]}""", Options);

        one!.Items.ShouldNotBeNull().Count().ShouldBe(1);
        many!.Items.ShouldNotBeNull().Count().ShouldBe(2);
    }

    [Fact]
    public void A_collection_is_written_as_an_array()
    {
        IEnumerable<Item> items = [new Item("a", 1), new Item("b", 2)];

        var json = JsonSerializer.Serialize(items, Options);

        json.ShouldBe("""[{"Name":"a","Size":1},{"Name":"b","Size":2}]""");
    }

    [Fact]
    public void Only_the_enumerable_interface_is_taken_over()
    {
        var factory = new SingleOrArrayConverterFactory();

        factory.CanConvert(typeof(IEnumerable<Item>)).ShouldBeTrue();
        factory.CanConvert(typeof(List<Item>)).ShouldBeFalse();
        factory.CanConvert(typeof(Item[])).ShouldBeFalse();
        factory.CanConvert(typeof(Item)).ShouldBeFalse();
    }
}
