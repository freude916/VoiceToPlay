using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens;

namespace VoiceToPlay.Commands.DeckView.Patches;

/// <summary>
///     NDeckViewScreen 生命周期补丁：管理牌组视图词表
/// </summary>
[HarmonyPatch(typeof(NDeckViewScreen), "_Ready")]
internal static class NDeckViewScreenReadyPatch
{
    private static void Postfix(NDeckViewScreen __instance)
    {
        MainFile.Logger.Debug("NDeckViewScreen._Ready, refreshing vocabulary");
        DeckViewCommand.RefreshVocabulary();
    }
}

[HarmonyPatch(typeof(NDeckViewScreen), "DisplayCards")]
internal static class NDeckViewScreenDisplayCardsPatch
{
    private static void Postfix(NDeckViewScreen __instance)
    {
        MainFile.Logger.Debug("NDeckViewScreen.DisplayCards, refreshing vocabulary");
        DeckViewCommand.RefreshVocabulary();
    }
}

[HarmonyPatch(typeof(NDeckViewScreen), "_ExitTree")]
internal static class NDeckViewScreenClosedPatch
{
    private static void Postfix()
    {
        MainFile.Logger.Debug("NDeckViewScreen._ExitTree, clearing vocabulary");
        DeckViewCommand.RefreshVocabulary();
    }
}
