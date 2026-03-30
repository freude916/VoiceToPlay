using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;

namespace VoiceToPlay.Commands.Potion.Patches;

/// <summary>
///     NRun 生命周期补丁：绑定 Godot 原生信号管理药水词表
/// </summary>
[HarmonyPatch(typeof(NRun), MethodType.Constructor)]
internal static class NRunConstructorPatch
{
    private static void Postfix(NRun __instance)
    {
        __instance.Ready += () =>
        {
            MainFile.Logger.Info("NRun.Ready, refreshing Potion vocabulary");
            PotionCommand.RefreshVocabulary();
        };

        __instance.TreeExited += () =>
        {
            MainFile.Logger.Info("NRun.TreeExited, clearing Potion vocabulary");
            PotionCommand.RefreshVocabulary();
        };
    }
}
