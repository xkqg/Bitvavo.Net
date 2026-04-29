// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Text.Json.Serialization;
using CryptoExchange.Net.Attributes;
using CryptoExchange.Net.Converters.SystemTextJson;

namespace Bitvavo.Net.Enums;

/// <summary>Direction of an order — buy or sell.</summary>
[JsonConverter(typeof(EnumConverter<OrderSide>))]
public enum OrderSide
{
    /// <summary>Buy the base asset (e.g. acquire ETH paying EUR).</summary>
    [Map("buy")] Buy,
    /// <summary>Sell the base asset (e.g. dispose of ETH for EUR).</summary>
    [Map("sell")] Sell,
}
