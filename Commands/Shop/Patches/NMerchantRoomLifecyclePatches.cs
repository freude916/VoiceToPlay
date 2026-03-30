using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;

namespace VoiceToPlay.Commands.Shop.Patches;

/// <summary>
///     商店房间生命周期补丁：管理商店词表和 HoverTip 清理
/// </summary>
[HarmonyPatch(typeof(NMerchantRoom), "_EnterTree")]
internal static class NMerchantRoomEnterPatch
{
    private static void Postfix()
    {
        MainFile.Logger.Debug("NMerchantRoom._EnterTree, opening inventory and refreshing vocabulary");

        // 自动打开商店界面（商店默认关闭，需要手动打开才能获取物品列表）
        var merchantRoom = NMerchantRoom.Instance;
        if (merchantRoom?.Inventory != null && !merchantRoom.Inventory.IsOpen)
            merchantRoom.OpenInventory();

        ShopCommand.RefreshVocabulary();
    }
}

/// <summary>
///     商店房间退出时清除词表和 HoverTip
/// </summary>
[HarmonyPatch(typeof(NMerchantRoom), "_ExitTree")]
internal static class NMerchantRoomExitPatch
{
    private static void Postfix()
    {
        MainFile.Logger.Debug("NMerchantRoom._ExitTree, clearing vocabulary and hover tips");
        ShopCommand.ClearHoverTips();
        ShopCommand.RefreshVocabulary();
    }
}

/// <summary>
///     商店打开时刷新词表
/// </summary>
[HarmonyPatch(typeof(NMerchantInventory), "Open")]
internal static class NMerchantInventoryOpenPatch
{
    private static void Postfix()
    {
        MainFile.Logger.Debug("NMerchantInventory.Open, refreshing vocabulary");
        ShopCommand.RefreshVocabulary();
    }
}

/// <summary>
///     商店物品购买后刷新词表
/// </summary>
[HarmonyPatch(typeof(MerchantEntry), "InvokePurchaseCompleted")]
internal static class MerchantEntryPurchaseCompletedPatch
{
    private static void Postfix()
    {
        MainFile.Logger.Debug("MerchantEntry.InvokePurchaseCompleted, refreshing vocabulary");
        ShopCommand.RefreshVocabulary();
    }
}