// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Text.Json.Serialization;
using CryptoExchange.Net.Attributes;
using CryptoExchange.Net.Converters.SystemTextJson;

namespace Bitvavo.Net.Enums;

/// <summary>How long an order remains active before it expires or is canceled.</summary>
[JsonConverter(typeof(EnumConverter<TimeInForce>))]
public enum TimeInForce
{
    /// <summary>Good-till-canceled — rests on the book until filled or explicitly canceled. Default.</summary>
    [Map("GTC")] GoodTillCanceled,
    /// <summary>Immediate-or-cancel — fills what it can immediately, cancels the remainder.</summary>
    [Map("IOC")] ImmediateOrCancel,
    /// <summary>Fill-or-kill — must fill in full immediately, otherwise canceled entirely.</summary>
    [Map("FOK")] FillOrKill,
}
