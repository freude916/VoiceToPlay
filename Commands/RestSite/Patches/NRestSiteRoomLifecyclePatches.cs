using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace VoiceToPlay.Commands.RestSite.Patches;

/// <summary>
///     NRestSiteRoom 生命周期补丁：管理火堆选项词表
///     注：Patch _Ready 而非 _EnterTree，因为 options 在 _Ready 的 UpdateRestSiteOptions 中创建
/// </summary>
[HarmonyPatch(typeof(NRestSiteRoom), "_Ready")]
internal static class NRestSiteRoomReadyPatch
{
    private static void Postfix(NRestSiteRoom __instance)
    {
        MainFile.Logger.Info("NRestSiteRoom._Ready, refreshing vocabulary");
        RestSiteCommand.RefreshVocabulary();
    }
}

[HarmonyPatch(typeof(NRestSiteRoom), "_ExitTree")]
internal static class NRestSiteRoomExitPatch
{
    private static void Postfix()
    {
        MainFile.Logger.Info("NRestSiteRoom._ExitTree, clearing vocabulary");
        RestSiteCommand.ClearVocabulary();
    }
}
