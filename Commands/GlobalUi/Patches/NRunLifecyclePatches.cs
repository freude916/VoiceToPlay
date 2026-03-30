using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;

namespace VoiceToPlay.Commands.GlobalUi.Patches;

/// <summary>
///     NRun 生命周期补丁：绑定 Godot 原生信号管理词表
/// </summary>
[HarmonyPatch(typeof(NRun), MethodType.Constructor)]
internal static class NRunConstructorPatch
{
    private static void Postfix(NRun __instance)
    {
        __instance.Ready += () =>
        {
            MainFile.Logger.Info("NRun.Ready (via Signal), refreshing GlobalUi vocabulary");
            GlobalUiCommand.RefreshVocabulary();
        };

        __instance.TreeExited += () =>
        {
            MainFile.Logger.Info("NRun.TreeExited (via Signal), clearing GlobalUi vocabulary");
            GlobalUiCommand.RefreshVocabulary();
        };
    }
}