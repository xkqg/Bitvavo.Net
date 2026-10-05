// Copyright (c) Bitvavo.Net contributors. Licensed under the MIT License.

using System.Threading.Tasks;
using CryptoExchange.Net.Objects;
using CryptoExchange.Net.Objects.Errors;
using CryptoExchange.Net.SharedApis;
using Shouldly;
using Xunit;

namespace Bitvavo.Net.Tests.Clients.SpotApi.SharedApi;

/// <summary>
/// The Shared asset capabilities on the REST API: [V2] <c>IGetAssetRest</c> / <c>IGetAllAssetsRest</c> and the legacy [V1]
/// <c>IAssetsRestClient</c> (whose <c>GetAssetsAsync</c> is the V2 <c>GetAllAssetsAsync</c> under its old name), on one instance.
/// </summary>
public class BitvavoRestSharedAssetsTests
{
    private const string Btc = """{"symbol":"BTC","name":"Bitcoin","decimals":8,"depositFee":"0","depositConfirmations":2,"depositStatus":"OK","withdrawalFee":"0.0002","withdrawalMinAmount":"0.001","withdrawalStatus":"OK","networks":["Mainnet"]}""";
    private const string Eth = """{"symbol":"ETH","name":"Ethereum","decimals":18,"depositConfirmations":12,"depositStatus":"MAINTENANCE","withdrawalStatus":"DELISTED","networks":["Mainnet","Arbitrum"]}""";

    [Fact]
    public async Task One_asset_is_mapped_with_its_networks_fees_and_status()
    {
        var handler = new StubHttpMessageHandler("[" + Btc + "]");
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.GetAssetAsync(new GetAssetRequest("BTC"), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Name.ShouldBe("BTC");
        result.Data.FullName.ShouldBe("Bitcoin");
        var network = result.Data.Networks.ShouldNotBeNull().ShouldHaveSingleItem();
        network.Name.ShouldBe("Mainnet");
        network.WithdrawFee.ShouldBe(0.0002m);
        network.MinWithdrawQuantity.ShouldBe(0.001m);
        network.WithdrawEnabled.ShouldBe(true);
        network.DepositEnabled.ShouldBe(true);
        network.MinConfirmations.ShouldBe(2);
        handler.Requests.ShouldHaveSingleItem().RequestUri!.PathAndQuery.ShouldBe("/v2/assets?symbol=BTC");
    }

    [Fact]
    public async Task An_asset_in_maintenance_or_delisted_is_not_enabled_for_deposits_or_withdrawals()
    {
        var api = new StubHttpMessageHandler("[" + Eth + "]").RestClient().SpotApi.SharedApi;

        var result = await api.GetAssetAsync(new GetAssetRequest("ETH"), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        var networks = result.Data.Networks.ShouldNotBeNull();
        networks.Length.ShouldBe(2);
        networks[0].DepositEnabled.ShouldBe(false);
        networks[0].WithdrawEnabled.ShouldBe(false);
    }

    [Fact]
    public async Task When_the_server_answers_with_the_full_list_the_requested_asset_is_picked()
    {
        var api = new StubHttpMessageHandler("[" + Btc + "," + Eth + "]").RestClient().SpotApi.SharedApi;

        var result = await api.GetAssetAsync(new GetAssetRequest("eth"), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Name.ShouldBe("ETH");
    }

    [Fact]
    public async Task An_unknown_asset_is_an_UnknownAsset_error()
    {
        var api = new StubHttpMessageHandler("[]").RestClient().SpotApi.SharedApi;

        var result = await api.GetAssetAsync(new GetAssetRequest("NOPE"), TestContext.Current.CancellationToken);

        result.Success.ShouldBeFalse();
        result.Error!.ErrorType.ShouldBe(ErrorType.UnknownAsset);
    }

    [Fact]
    public async Task All_assets_are_mapped()
    {
        var handler = new StubHttpMessageHandler("[" + Btc + "," + Eth + "]");
        var api = handler.RestClient().SpotApi.SharedApi;

        var result = await api.GetAllAssetsAsync(new GetAssetsRequest(), TestContext.Current.CancellationToken);

        result.Success.ShouldBeTrue();
        result.Data.Length.ShouldBe(2);
        result.Data[0].Name.ShouldBe("BTC");
        result.Data[1].Name.ShouldBe("ETH");
        handler.Requests.ShouldHaveSingleItem().RequestUri!.PathAndQuery.ShouldBe("/v2/assets");
    }

    [Fact]
    public void Both_capabilities_need_no_credentials()
    {
        var api = new StubHttpMessageHandler("[]").RestClient(withCredentials: false).SpotApi.SharedApi;

        api.GetAssetOptions.NeedsAuthentication.ShouldBeFalse();
        api.GetAllAssetsOptions.NeedsAuthentication.ShouldBeFalse();
    }

    // ── [V1] ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_legacy_interface_serves_one_asset_and_the_asset_list_under_its_old_names()
    {
        var handler = new StubHttpMessageHandler("[" + Btc + "," + Eth + "]");
        var client = handler.RestClient();
        var legacy = client.SpotApi.SharedClient;

        var one = await legacy.GetAssetAsync(new GetAssetRequest("BTC"), TestContext.Current.CancellationToken);
        var all = await legacy.GetAssetsAsync(new GetAssetsRequest(), TestContext.Current.CancellationToken);

        one.Success.ShouldBeTrue();
        all.Success.ShouldBeTrue();
        all.Data.Length.ShouldBe(2);
        legacy.GetAssetsOptions.ShouldBeSameAs(client.SpotApi.SharedApi.GetAllAssetsOptions);
    }
}
