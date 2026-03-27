using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;
using VoiceToPlay.Voice.Core;
using Logger = MegaCrit.Sts2.Core.Logging.Logger;

namespace VoiceToPlay;

[ModInitializer(nameof(Initialize))]
public partial class MainFile : Node
{
    private const string ModId = "VoiceToPlay"; //At the moment, this is used only for the Logger and harmony names.

    public static Logger Logger { get; } =
        new(ModId, LogType.Generic);

    public static void Initialize()
    {
        ModAssemblyResolver.Initialize();

        // 预加载外部依赖，确保 JIT 编译时程序集可用
        ModAssemblyResolver.EnsureLoaded("Vosk");
        ModAssemblyResolver.EnsureLoaded("JiebaNet.Segmenter");

        Harmony harmony = new(ModId);
        harmony.PatchAll();
    }
}