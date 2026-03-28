using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace VoiceToPlay.Commands.Map.Patches;

/// <summary>
///     NMapScreen 生命周期补丁：管理地图词表
/// </summary>
[HarmonyPatch(typeof(NMapScreen), MethodType.Constructor)]
internal static class NMapScreenLifecyclePatches
{
    private static void Postfix(NMapScreen __instance)
    {
        __instance.Opened += () =>
        {
            MainFile.Logger.Info("NMapScreen.Opened, refreshing vocabulary");
            MapCommand.RefreshVocabulary();
        };

        __instance.Closed += () =>
        {
            MainFile.Logger.Info("NMapScreen.Closed, clearing vocabulary");
            MapCommand.RefreshVocabulary();
        };
    }
}
