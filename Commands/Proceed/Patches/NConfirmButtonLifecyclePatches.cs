using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace VoiceToPlay.Commands.Proceed.Patches;

/// <summary>
///     NConfirmButton 生命周期补丁：管理"确定/确认"词表
/// </summary>
[HarmonyPatch(typeof(NConfirmButton), "OnEnable")]
internal static class NConfirmButtonEnablePatch
{
    private static void Postfix()
    {
        MainFile.Logger.Info("NConfirmButton.OnEnable, refreshing vocabulary");
        ConfirmCommand.RefreshVocabulary();
    }
}

[HarmonyPatch(typeof(NConfirmButton), "OnDisable")]
internal static class NConfirmButtonDisablePatch
{
    private static void Postfix()
    {
        MainFile.Logger.Info("NConfirmButton.OnDisable, clearing vocabulary");
        ConfirmCommand.RefreshVocabulary();
    }
}
