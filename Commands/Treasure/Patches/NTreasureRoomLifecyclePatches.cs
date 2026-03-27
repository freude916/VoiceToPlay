using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
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
///     宝箱房间退出时清除词表和 HoverTip
/// </summary>
[HarmonyPatch(typeof(NTreasureRoom), "_ExitTree")]
internal static class NTreasureRoomExitPatch
{
    private static void Prefix(NTreasureRoom __instance)
    {
        MainFile.Logger.Info("NTreasureRoom._ExitTree, clearing vocabulary and hovertips");
        
        // 清理所有遗物 holder 的 hovertips
        var stack = new Stack<Node>();
        stack.Push(__instance);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is NTreasureRoomRelicHolder holder)
                NHoverTipSet.Remove(holder);
            
            foreach (var child in node.GetChildren())
                if (child is Node childNode)
                    stack.Push(childNode);
        }
        
        TreasureCommand.RefreshVocabulary();
    }
}
