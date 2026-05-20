// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Text.Json.Serialization;
using CryptoExchange.Net.Attributes;
using CryptoExchange.Net.Converters.SystemTextJson;

namespace Bitvavo.Net.Enums;

/// <summary>Direction of an institutional subaccount asset transfer.</summary>
[JsonConverter(typeof(EnumConverter<SubaccountTransferDirection>))]
public enum SubaccountTransferDirection
{
    /// <summary>Move assets from the main account to the subaccount.</summary>
    [Map("masterToSub")] MasterToSub = 0,
    /// <summary>Move assets from the subaccount to the main account.</summary>
    [Map("subToMaster")] SubToMaster = 1,
}
