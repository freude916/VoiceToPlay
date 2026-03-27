using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic;

namespace VoiceToPlay.Commands.Treasure.Patches;

/// <summary>
///     宝箱遗物房间生命周期补丁：管理遗物选择词表
/// </summary>
[HarmonyPatch(typeof(NTreasureRoom), "_EnterTree")]
internal static class NTreasureRoomEnterPatch
{
    private static void Postfix()
    {
        MainFile.Logger.Info("NTreasureRoom._EnterTree, refreshing vocabulary");
        TreasureCommand.RefreshVocabulary();
    }
}

[HarmonyPatch(typeof(NTreasureRoomRelicCollection), "InitializeRelics")]
internal static class NTreasureRoomRelicCollectionInitPatch
{
    private static void Postfix()
    {
        MainFile.Logger.Info("NTreasureRoomRelicCollection.InitializeRelics, refreshing vocabulary");
        TreasureCommand.RefreshVocabulary();
    }
}

/// <summary>
///     宝箱房间退出时清除词表
///     注：HoverTip 由 NHoverTipSet 自动通过 TreeExiting 信号清理
/// </summary>
[HarmonyPatch(typeof(NTreasureRoom), "_ExitTree")]
internal static class NTreasureRoomExitPatch
{
    private static void Postfix()
    {
        MainFile.Logger.Info("NTreasureRoom._ExitTree, clearing vocabulary");
        TreasureCommand.RefreshVocabulary();
    }
}
