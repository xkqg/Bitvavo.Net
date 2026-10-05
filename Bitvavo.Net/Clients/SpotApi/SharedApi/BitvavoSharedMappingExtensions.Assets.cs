// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System;
using System.Linq;
using Bitvavo.Net.Objects.Models.Spot;
using CryptoExchange.Net.SharedApis;

namespace Bitvavo.Net.Clients.SpotApi;

internal static partial class BitvavoSharedMappingExtensions
{
    /// <summary>
    /// A Bitvavo asset as a Shared asset. Bitvavo's asset record carries one fee, minimum and status per asset and lists its network
    /// names, so every network inherits them; an asset is enabled for deposits or withdrawals while its status is <c>OK</c>
    /// (<c>MAINTENANCE</c> and <c>DELISTED</c> are not).
    /// </summary>
    public static SharedAsset ToSharedAsset(this BitvavoAsset asset) => new(asset.Symbol)
    {
        FullName = asset.Name,
        Networks = asset.Networks
            .Select(network => new SharedAssetNetwork(network)
            {
                WithdrawFee = asset.WithdrawalFee,
                MinWithdrawQuantity = asset.WithdrawalMinAmount,
                WithdrawEnabled = string.Equals(asset.WithdrawalStatus, "OK", StringComparison.OrdinalIgnoreCase),
                DepositEnabled = string.Equals(asset.DepositStatus, "OK", StringComparison.OrdinalIgnoreCase),
                MinConfirmations = asset.DepositConfirmations,
            })
            .ToArray(),
    };
}
