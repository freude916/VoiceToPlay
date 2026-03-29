using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace VoiceToPlay.Commands.Ui.Patches;

/// <summary>
///     NProceedButton 生命周期补丁：管理"前进"词表
/// </summary>
[HarmonyPatch(typeof(NProceedButton), "OnEnable")]
internal static class NProceedButtonEnablePatch
{
    private static void Postfix(NProceedButton __instance)
    {
        ProceedCommand.RegisterButton(__instance);
    }
}

[HarmonyPatch(typeof(NProceedButton), "OnDisable")]
internal static class NProceedButtonDisablePatch
{
    private static void Postfix()
    {
        ProceedCommand.UnregisterButton();
    }
}