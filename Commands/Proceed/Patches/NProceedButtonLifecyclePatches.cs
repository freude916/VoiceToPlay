using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace VoiceToPlay.Commands.Proceed.Patches;

/// <summary>
///     NProceedButton 生命周期补丁：管理"前进"词表
/// </summary>
[HarmonyPatch(typeof(NProceedButton), "OnEnable")]
internal static class NProceedButtonEnablePatch
{
    private static void Postfix()
    {
        MainFile.Logger.Info("NProceedButton.OnEnable, refreshing vocabulary");
        ProceedCommand.RefreshVocabulary();
    }
}

[HarmonyPatch(typeof(NProceedButton), "OnDisable")]
internal static class NProceedButtonDisablePatch
{
    private static void Postfix()
    {
        MainFile.Logger.Info("NProceedButton.OnDisable, clearing vocabulary");
        ProceedCommand.RefreshVocabulary();
    }
}
