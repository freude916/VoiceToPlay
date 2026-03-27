using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens;

namespace VoiceToPlay.Commands.Rewards.Patches;

/// <summary>
///     NRewardsScreen 生命周期补丁：管理奖励词表
/// </summary>
[HarmonyPatch(typeof(NRewardsScreen), "SetRewards")]
internal static class NRewardsScreenSetRewardsPatch
{
    private static void Postfix(NRewardsScreen __instance)
    {
        MainFile.Logger.Info("NRewardsScreen.SetRewards, refreshing vocabulary");
        RewardsCommand.RefreshVocabulary();
    }
}

[HarmonyPatch(typeof(NRewardsScreen), "AfterOverlayShown")]
internal static class NRewardsScreenShownPatch
{
    private static void Postfix()
    {
        MainFile.Logger.Info("NRewardsScreen.AfterOverlayShown, refreshing vocabulary (forced)");
        RewardsCommand.RefreshVocabulary(force: true);
    }
}

[HarmonyPatch(typeof(NRewardsScreen), "AfterOverlayClosed")]
internal static class NRewardsScreenClosedPatch
{
    private static void Postfix()
    {
        MainFile.Logger.Info("NRewardsScreen.AfterOverlayClosed, clearing vocabulary");
        RewardsCommand.RefreshVocabulary();
    }
}