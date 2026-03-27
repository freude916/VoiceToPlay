using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;

namespace VoiceToPlay.Commands.CardRow.Patches;

/// <summary>
///     NCardRewardSelectionScreen 生命周期补丁：管理选牌词表
/// </summary>
[HarmonyPatch(typeof(NCardRewardSelectionScreen), "AfterOverlayShown")]
internal static class NCardRewardSelectionScreenShownPatch
{
    private static void Postfix(NCardRewardSelectionScreen __instance)
    {
        MainFile.Logger.Info("NCardRewardSelectionScreen.AfterOverlayShown, refreshing vocabulary");
        CardRowSelectCommand.RefreshVocabulary();
    }
}

[HarmonyPatch(typeof(NCardRewardSelectionScreen), "AfterOverlayClosed")]
internal static class NCardRewardSelectionScreenClosedPatch
{
    private static void Postfix()
    {
        MainFile.Logger.Info("NCardRewardSelectionScreen.AfterOverlayClosed, clearing vocabulary");
        CardRowSelectCommand.ClearVocabulary();
    }
}
