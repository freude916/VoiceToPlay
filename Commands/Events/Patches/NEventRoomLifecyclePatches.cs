using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace VoiceToPlay.Commands.Events.Patches;

/// <summary>
///     NEventRoom 生命周期补丁：管理事件选项词表
/// </summary>
[HarmonyPatch(typeof(NEventRoom), "SetOptions")]
internal static class NEventRoomSetOptionsPatch
{
    private static void Postfix()
    {
        MainFile.Logger.Debug("NEventRoom.SetOptions, refreshing vocabulary");
        EventsCommand.RefreshVocabulary();
    }
}

/// <summary>
///     选项被禁用时清除词表
/// </summary>
[HarmonyPatch(typeof(NEventLayout), "DisableEventOptions")]
internal static class NEventLayoutDisableOptionsPatch
{
    private static void Postfix()
    {
        MainFile.Logger.Debug("NEventLayout.DisableEventOptions, clearing vocabulary");
        EventsCommand.RefreshVocabulary();
    }
}