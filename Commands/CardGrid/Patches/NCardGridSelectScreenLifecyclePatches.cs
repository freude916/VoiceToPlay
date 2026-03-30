using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;

namespace VoiceToPlay.Commands.CardGrid.Patches;

/// <summary>
///     NCardGridSelectionScreen 生命周期补丁：管理牌堆选牌词表
/// </summary>
[HarmonyPatch(typeof(NCardGridSelectionScreen), "AfterOverlayShown")]
internal static class NCardGridSelectionScreenShownPatch
{
    private static void Postfix(NCardGridSelectionScreen __instance)
    {
        MainFile.Logger.Debug("NCardGridSelectionScreen.AfterOverlayShown, refreshing vocabulary");
        CardGridSelectCommand.RefreshVocabulary();
    }
}

[HarmonyPatch(typeof(NCardGridSelectionScreen), "AfterOverlayClosed")]
internal static class NCardGridSelectionScreenClosedPatch
{
    private static void Postfix()
    {
        MainFile.Logger.Debug("NCardGridSelectionScreen.AfterOverlayClosed, clearing vocabulary");
        CardGridSelectCommand.ClearVocabulary();
    }
}