// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Text.Json.Serialization;
using CryptoExchange.Net.Attributes;
using CryptoExchange.Net.Converters.SystemTextJson;

namespace Bitvavo.Net.Enums;

/// <summary>Type of trigger event for stop-loss / take-profit orders. Bitvavo currently exposes only price-based triggers.</summary>
[JsonConverter(typeof(EnumConverter<TriggerType>))]
public enum TriggerType
{
    /// <summary>Trigger fires when the chosen price reference reaches the specified amount.</summary>
    [Map("price")] Price,
}
