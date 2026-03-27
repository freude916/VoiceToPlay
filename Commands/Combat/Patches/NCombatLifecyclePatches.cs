using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace VoiceToPlay.Commands.Combat.Patches;

/// <summary>
///     NCombatRoom 生命周期补丁：管理敌人选择状态
/// </summary>
[HarmonyPatch(typeof(NCombatRoom), MethodType.Constructor)]
internal static class NCombatRoomConstructorPatch
{
    private static void Postfix(NCombatRoom __instance)
    {
        __instance.TreeExited += () =>
        {
            MainFile.Logger.Info("NCombatRoom.TreeExited, clearing target state");
            CombatTargetState.Clear();
        };
    }
}