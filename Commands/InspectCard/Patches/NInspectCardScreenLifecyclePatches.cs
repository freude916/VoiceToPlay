using HarmonyLib;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens;

namespace VoiceToPlay.Commands.InspectCard.Patches;

/// <summary>
///     NInspectCardScreen 生命周期补丁：管理卡牌详情词表
/// </summary>
[HarmonyPatch(typeof(NInspectCardScreen), nameof(NInspectCardScreen.Open))]
internal static class NInspectCardScreenOpenedPatch
{
    private static void Postfix(NInspectCardScreen __instance, List<CardModel> cards, int index,
        bool viewAllUpgraded = false)
    {
        MainFile.Logger.Debug("NInspectCardScreen.Open, refreshing vocabulary");
        InspectCardCommand.RefreshVocabulary();
    }
}

[HarmonyPatch(typeof(NInspectCardScreen), nameof(NInspectCardScreen.Close))]
internal static class NInspectCardScreenClosedPatch
{
    private static void Postfix(NInspectCardScreen __instance)
    {
        MainFile.Logger.Debug("NInspectCardScreen.Close, clearing vocabulary");
        InspectCardCommand.RefreshVocabulary();
    }
}