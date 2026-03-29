using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace VoiceToPlay.Commands.Ui.Patches;

/// <summary>
///     NBackButton 生命周期补丁：管理"返回"词表
/// </summary>
[HarmonyPatch(typeof(NBackButton), "OnEnable")]
internal static class NBackButtonEnablePatch
{
    private static void Postfix(NBackButton __instance)
    {
        BackCommand.RegisterButton(__instance);
    }
}

[HarmonyPatch(typeof(NBackButton), "OnDisable")]
internal static class NBackButtonDisablePatch
{
    private static void Postfix()
    {
        BackCommand.UnregisterButton();
    }
}
