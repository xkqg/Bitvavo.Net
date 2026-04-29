// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Text.Json.Serialization;
using CryptoExchange.Net.Attributes;
using CryptoExchange.Net.Converters.SystemTextJson;

namespace Bitvavo.Net.Enums;

/// <summary>How Bitvavo resolves a situation where one of your orders would match against another of your own orders.</summary>
[JsonConverter(typeof(EnumConverter<SelfTradePrevention>))]
public enum SelfTradePrevention
{
    /// <summary>Decrement both quantities and cancel both orders.</summary>
    [Map("decrementAndCancel")] DecrementAndCancel,
    /// <summary>Cancel the older of the two orders, let the newer fill.</summary>
    [Map("cancelOldest")] CancelOldest,
    /// <summary>Cancel the newer of the two orders, let the older remain.</summary>
    [Map("cancelNewest")] CancelNewest,
    /// <summary>Cancel both orders entirely.</summary>
    [Map("cancelBoth")] CancelBoth,
}
