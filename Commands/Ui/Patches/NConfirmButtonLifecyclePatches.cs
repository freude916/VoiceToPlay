using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace VoiceToPlay.Commands.Ui.Patches;

/// <summary>
///     NConfirmButton 生命周期补丁：管理"确定/确认"词表
/// </summary>
[HarmonyPatch(typeof(NConfirmButton), "OnEnable")]
internal static class NConfirmButtonEnablePatch
{
    private static void Postfix(NConfirmButton __instance)
    {
        ConfirmCommand.RegisterButton(__instance);
    }
}

[HarmonyPatch(typeof(NConfirmButton), "OnDisable")]
internal static class NConfirmButtonDisablePatch
{
    private static void Postfix()
    {
        ConfirmCommand.UnregisterButton();
    }
}