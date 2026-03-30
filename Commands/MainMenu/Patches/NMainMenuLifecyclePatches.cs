using Godot;
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
            MainFile.Logger.Debug("NMainMenu.Ready (via Signal), refreshing vocabulary");
            MainMenuCommand.RefreshVocabulary();

            // 监听子菜单栈变化，在子菜单打开/关闭时刷新词表
            var submenuStack = __instance.SubmenuStack;
            if (submenuStack != null)
                submenuStack.Connect(NSubmenuStack.SignalName.StackModified, Callable.From(() =>
                {
                    MainFile.Logger.Debug("NMainMenuSubmenuStack.StackModified, refreshing vocabulary");
                    MainMenuCommand.RefreshVocabulary();
                }));
        };

        __instance.TreeExited += () =>
        {
            MainFile.Logger.Debug("NMainMenu.TreeExited (via Signal), clearing vocabulary");
            MainMenuCommand.RefreshVocabulary();
        };
    }
}

/// <summary>
///     NMainMenu.RefreshButtons 补丁：在按钮状态变化时刷新词表
/// </summary>
[HarmonyPatch(typeof(NMainMenu), nameof(NMainMenu.RefreshButtons))]
internal static class NMainMenuRefreshButtonsPatch
{
    private static void Postfix()
    {
        MainFile.Logger.Debug("NMainMenu.RefreshButtons, refreshing vocabulary");
        MainMenuCommand.RefreshVocabulary();
    }
}