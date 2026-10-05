// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Reflection;
using Bitvavo.Net.Objects.Internal;
using Xunit;
using Xunit.v3;

[assembly: Bitvavo.Net.Tests.FreshRateLimiter]
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Bitvavo.Net.Tests;

/// <summary>
/// Gives every test an empty rate-limit budget. Bitvavo's budget is process-wide by design — <c>BitvavoExchange.RateLimiter</c>
/// is one static, settable object — so tests that send requests would otherwise spend each other's weight points (and, past
/// 900 points, wait out the minute). Applied to the whole assembly, together with serial test execution, which is what makes
/// the static safe to replace between tests. The real limiter is used; nothing about its behaviour is switched off.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
internal sealed class FreshRateLimiterAttribute : BeforeAfterTestAttribute
{
    public override void Before(MethodInfo methodUnderTest, IXunitTest test)
    {
        BitvavoExchange.RateLimiter = new BitvavoRateLimiters();
    }
}
