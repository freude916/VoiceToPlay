using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;

namespace VoiceToPlay.Commands.CharacterSelect.Patches;

/// <summary>
///     NCharacterSelectScreen 构造函数补丁：绑定生命周期刷新词表
/// </summary>
[HarmonyPatch(typeof(NCharacterSelectScreen), MethodType.Constructor)]
internal static class NCharacterSelectScreenConstructorPatch
{
    private static void Postfix(NCharacterSelectScreen __instance)
    {
        __instance.Ready += () =>
        {
            MainFile.Logger.Info("NCharacterSelectScreen.Ready, refreshing vocabulary");
            CharacterSelectCommand.RefreshVocabulary();
        };

        __instance.TreeExited += () =>
        {
            MainFile.Logger.Info("NCharacterSelectScreen.TreeExited, clearing vocabulary");
            CharacterSelectCommand.RefreshVocabulary();
        };
    }
}

/// <summary>
///     NCharacterSelectScreen.OnSubmenuOpened 补丁：子菜单打开时刷新词表
/// </summary>
[HarmonyPatch(typeof(NCharacterSelectScreen), nameof(NCharacterSelectScreen.OnSubmenuOpened))]
internal static class NCharacterSelectScreenOpenedPatch
{
    private static void Postfix()
    {
        MainFile.Logger.Info("NCharacterSelectScreen.OnSubmenuOpened, refreshing vocabulary");
        CharacterSelectCommand.RefreshVocabulary();
    }
}