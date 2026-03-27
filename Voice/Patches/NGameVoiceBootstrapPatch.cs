using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;

namespace VoiceToPlay.Voice.Patches;

/// <summary>
///     NGame._Ready 后置补丁：注入 VoiceEntryNode
/// </summary>
[HarmonyPatch(typeof(NGame), nameof(NGame._Ready))]
internal static class NGameVoiceBootstrapPatch
{
    [HarmonyPostfix]
    private static void Postfix(NGame __instance)
    {
        ArgumentNullException.ThrowIfNull(__instance);

        // 避免重复注入
        if (__instance.GetNodeOrNull<VoiceEntryNode>("VoiceToPlayEntry") != null) return;

        var node = new VoiceEntryNode { Name = "VoiceToPlayEntry" };
        __instance.AddChild(node);
    }
}