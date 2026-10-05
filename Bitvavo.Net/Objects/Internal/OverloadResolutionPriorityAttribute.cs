// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

#if !NET9_0_OR_GREATER
using System;

namespace System.Runtime.CompilerServices;

/// <summary>
/// The C# 13 attribute for net8.0, where the base class library does not carry it yet. The compiler honours the attribute by
/// name — also on a consumer's compilation, through the metadata of this assembly — so a polyfill has the same effect as the
/// framework's own type, which net9.0 and later bring with them. It takes effect on a compilation at language version C# 13 or
/// later and is ignored below it, whichever compiler builds the project (a net8.0 project defaults to C# 12).
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
internal sealed class OverloadResolutionPriorityAttribute(int priority) : Attribute
{
    /// <summary>The priority: among the applicable overloads of one type, the highest wins before any other tie-break.</summary>
    public int Priority { get; } = priority;
}
#endif
