using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace VoiceToPlay.Commands.MainMenu.Patches;

/// <summary>
///     NMainMenu 构造函数补丁：绑定 Godot 原生信号管理词表
/// </summary>
[HarmonyPatch(typeof(NMainMenu), MethodType.Constructor)]
internal static class NMainMenuConstructorPatch
{
    private static void Postfix(NMainMenu __instance)
    {
        __instance.Ready += () =>
        {
            MainFile.Logger.Info("NMainMenu.Ready (via Signal), refreshing vocabulary");
            MainMenuCommand.RefreshVocabulary();
        };

        __instance.TreeExited += () =>
        {
            MainFile.Logger.Info("NMainMenu.TreeExited (via Signal), clearing vocabulary");
            MainMenuCommand.RefreshVocabulary();
        };
    }
}