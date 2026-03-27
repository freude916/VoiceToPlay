using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Combat;

namespace VoiceToPlay.Commands.Card.Patches;

/// <summary>
///     NPlayerHand 生命周期补丁。订阅 ModeChanged 信号以刷新词表。
/// </summary>
[HarmonyPatch(typeof(NPlayerHand), MethodType.Constructor)]
internal static class NPlayerHandLifecyclePatches
{
    private static void Postfix(NPlayerHand __instance)
    {
        __instance.ModeChanged += OnModeChanged;
    }

    private static void OnModeChanged()
    {
        MainFile.Logger.Info("NPlayerHand.ModeChanged, refreshing vocabulary");
        HandCardSelectionCommand.RefreshVocabulary();
        PlayCardCommand.RefreshVocabulary();
    }
}
