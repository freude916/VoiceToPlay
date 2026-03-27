using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Nodes;

namespace VoiceToPlay.Commands.Card.Patches;

/// <summary>
///     NRun 生命周期补丁：管理战斗中的手牌词表订阅
/// </summary>
[HarmonyPatch(typeof(NRun), MethodType.Constructor)]
internal static class NRunConstructorPatch
{
    private static void Postfix(NRun __instance)
    {
        __instance.Ready += () =>
        {
            MainFile.Logger.Info("NRun.Ready, subscribing to CombatManager.TurnStarted");
            SubscribeToCombatEvents();
        };

        __instance.TreeExited += () =>
        {
            MainFile.Logger.Info("NRun.TreeExited, unsubscribing from combat events");
            UnsubscribeFromCombatEvents();
            UnsubscribeFromHandChanges();
            PlayCardCommand.RefreshVocabulary();
        };
    }

    private static void OnTurnStarted(CombatState state)
    {
        if (state.CurrentSide != CombatSide.Player) return;

        MainFile.Logger.Info("Player turn started, subscribing to hand changes");
        SubscribeToHandChanges();
        PlayCardCommand.RefreshVocabulary();
    }

    private static void OnHandContentsChanged()
    {
        MainFile.Logger.Info("Hand contents changed, refreshing vocabulary");
        PlayCardCommand.RefreshVocabulary();
    }

    private static void SubscribeToCombatEvents()
    {
        var combatManager = CombatManager.Instance;
        if (combatManager == null)
        {
            MainFile.Logger.Warn("SubscribeToCombatEvents: CombatManager.Instance is null");
            return;
        }

        combatManager.TurnStarted += OnTurnStarted;
        MainFile.Logger.Info("Subscribed to CombatManager.TurnStarted");
    }

    private static void UnsubscribeFromCombatEvents()
    {
        var combatManager = CombatManager.Instance;
        if (combatManager == null) return;

        combatManager.TurnStarted -= OnTurnStarted;
        MainFile.Logger.Info("Unsubscribed from CombatManager.TurnStarted");
    }

    private static void SubscribeToHandChanges()
    {
        var combatState = CombatManager.Instance?.DebugOnlyGetState();
        if (combatState == null)
        {
            MainFile.Logger.Warn("SubscribeToHandChanges: combatState is null");
            return;
        }

        var player = LocalContext.GetMe(combatState);
        if (player == null)
        {
            MainFile.Logger.Warn("SubscribeToHandChanges: player is null");
            return;
        }

        var hand = player.PlayerCombatState?.Hand;
        if (hand == null)
        {
            MainFile.Logger.Warn("SubscribeToHandChanges: hand is null");
            return;
        }

        hand.ContentsChanged += OnHandContentsChanged;
        MainFile.Logger.Info("Subscribed to hand.ContentsChanged");
    }

    private static void UnsubscribeFromHandChanges()
    {
        var combatState = CombatManager.Instance?.DebugOnlyGetState();
        if (combatState == null) return;

        var player = LocalContext.GetMe(combatState);
        var hand = player?.PlayerCombatState?.Hand;
        if (hand == null) return;

        hand.ContentsChanged -= OnHandContentsChanged;
        MainFile.Logger.Info("Unsubscribed from hand.ContentsChanged");
    }
}