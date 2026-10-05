// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests;

/// <summary>
/// The environment lookup behind configuration binding. A <c>"Live"</c> read from an appsettings file must find the same
/// environment as <c>"live"</c>: a bound <see cref="BitvavoEnvironment"/> that is not looked up again carries a name and no URLs.
/// </summary>
public class BitvavoEnvironmentTests
{
    [Theory]
    [InlineData("live")]
    [InlineData("Live")]
    [InlineData("LIVE")]
    [InlineData("")]
    [InlineData(null)]
    public void GetEnvironmentByName_resolves_live_whatever_the_casing(string? name)
    {
        BitvavoEnvironment.GetEnvironmentByName(name).ShouldBeSameAs(BitvavoEnvironment.Live);
    }

    [Theory]
    [InlineData("staging")]
    [InlineData("testnet")]
    public void GetEnvironmentByName_returns_null_for_a_name_that_is_no_environment(string name)
    {
        BitvavoEnvironment.GetEnvironmentByName(name).ShouldBeNull();
    }

    [Fact]
    public void All_lists_the_known_environment_names()
    {
        BitvavoEnvironment.All.ShouldBe([BitvavoEnvironment.Live.Name]);
    }
}
