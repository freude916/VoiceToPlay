using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;

namespace VoiceToPlay.Voice.Patches;

/// <summary>
///     NGame._ExitTree 前置补丁：清理 VoiceEntryNode
/// </summary>
[HarmonyPatch(typeof(NGame), nameof(NGame._ExitTree))]
internal static class NGameVoiceCleanupPatch
{
    [HarmonyPrefix]
    private static void Prefix(NGame __instance)
    {
        var node = __instance.GetNodeOrNull<VoiceEntryNode>("VoiceToPlayEntry");
        node?.DisposeServiceAndQueueFree();
    }
}