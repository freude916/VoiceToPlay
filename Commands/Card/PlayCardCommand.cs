using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using VoiceToPlay.Commands.Combat;
using VoiceToPlay.Commands.DeckView;
using VoiceToPlay.Voice;
using VoiceToPlay.Voice.Core;

namespace VoiceToPlay.Commands.Card;

/// <summary>
///     出牌命令。支持按卡名和数字索引出牌（如 "防御一", "防御二"）。
///     不带数字的基础词（如 "防御"）会实时搜索首个可用的同名卡。
/// </summary>
public sealed class PlayCardCommand : IVoiceCommand
{
    /// <summary>
    ///     所有可用的卡牌名（用于不带数字词的实时搜索）
    /// </summary>
    private readonly HashSet<string> _cardNames = new(StringComparer.Ordinal);

    /// <summary>
    ///     带数字后缀的词 → CardModel（如 "打击一" → CardModel）
    /// </summary>
    private readonly Dictionary<string, CardModel> _indexedWordToCard = new();

    /// <summary>
    ///     缓存的词表，SupportedWords getter 直接返回此缓存
    /// </summary>
    private HashSet<string> _cachedWords = new(StringComparer.Ordinal);

    public PlayCardCommand()
    {
        Instance = this;
    }

    public static PlayCardCommand? Instance { get; private set; }

    /// <summary>
    ///     只返回缓存，不做任何计算
    /// </summary>
    public IEnumerable<string> SupportedWords => _cachedWords;

    public CommandResult Execute(string word)
    {
        // Pass：overlay 打开或手牌选牌模式或牌组视图时给其他命令让路
        if (NOverlayStack.Instance?.Peek() != null) return CommandResult.Pass;
        if (NPlayerHand.Instance?.IsInCardSelection == true) return CommandResult.Pass;
        if (DeckViewCommand.IsOpen) return CommandResult.Pass;

        var combatState = CombatManager.Instance?.DebugOnlyGetState();
        if (combatState == null)
        {
            MainFile.Logger.Warn("PlayCardCommand: combat not active");
            return CommandResult.Failed;
        }

        var player = LocalContext.GetMe(combatState);
        var hand = player?.PlayerCombatState?.Hand?.Cards;
        if (hand == null)
        {
            MainFile.Logger.Warn("PlayCardCommand: hand is null");
            return CommandResult.Failed;
        }

        CardModel? matchedCard = null;

        // 尝试匹配带数字后缀的词
        if (_indexedWordToCard.TryGetValue(word, out var cachedCard))
        {
            // 验证卡牌仍在手牌中
            if (!hand.Contains(cachedCard))
            {
                MainFile.Logger.Warn($"PlayCardCommand: cached card for '{word}' no longer in hand");
                return CommandResult.Failed;
            }

            matchedCard = cachedCard;
        }
        // 尝试匹配不带数字的基础词（实时搜索首个可用的同名卡）
        else if (_cardNames.Contains(word))
        {
            matchedCard = FindFirstAvailableCard(word, hand);
            if (matchedCard == null)
            {
                MainFile.Logger.Info($"PlayCardCommand: no available card for '{word}'");
                return CommandResult.Failed;
            }
        }
        else
        {
            MainFile.Logger.Warn($"PlayCardCommand: word '{word}' not found");
            return CommandResult.Failed;
        }

        if (!matchedCard.CanPlay(out _, out _))
        {
            MainFile.Logger.Info("PlayCardCommand: card cannot be played now");
            return CommandResult.Failed;
        }

        // 解析目标
        var target = ResolveTarget(matchedCard, combatState);

        // 无目标牌不需要验证目标
        var needsTarget = matchedCard.TargetType is TargetType.AnyEnemy or TargetType.AnyAlly;
        if (needsTarget && !matchedCard.IsValidTarget(target))
        {
            MainFile.Logger.Info("PlayCardCommand: invalid target");
            return CommandResult.Failed;
        }

        var queued = matchedCard.TryManualPlay(target);
        MainFile.Logger.Debug($"PlayCardCommand: '{word}' -> queued={queued}");

        // 不在这里刷新词表，由 Patch 在手牌真正变化时刷新

        return queued ? CommandResult.Success : CommandResult.Failed;
    }

    public event Action<IVoiceCommand>? VocabularyChanged;

    /// <summary>
    ///     查找首个可用的同名卡
    /// </summary>
    private static CardModel? FindFirstAvailableCard(string normalizedCardName, IEnumerable<CardModel> hand)
    {
        foreach (var card in hand)
        {
            var name = VoiceText.Normalize(VoiceText.GetCardCommandName(card));
            if (name == normalizedCardName)
                return card;
        }

        return null;
    }

    /// <summary>
    ///     刷新词表缓存，由 Patch 调用
    /// </summary>
    public static void RefreshVocabulary()
    {
        var instance = Instance;
        if (instance == null) return;

        var newWords = instance.ComputeSupportedWords();
        if (!newWords.SetEquals(instance._cachedWords))
        {
            instance._cachedWords = newWords;
            instance.VocabularyChanged?.Invoke(instance);
        }
    }

    /// <summary>
    ///     计算当前支持的词表（只在 RefreshVocabulary 中调用）
    /// </summary>
    private HashSet<string> ComputeSupportedWords()
    {
        _indexedWordToCard.Clear();
        _cardNames.Clear();

        // 如果在手牌选牌模式（消耗/升级等），禁用出牌命令
        if (NPlayerHand.Instance?.IsInCardSelection == true) return [];

        // 如果有 overlay 打开（选牌屏幕等），禁用出牌命令
        if (NOverlayStack.Instance?.Peek() != null) return [];

        var combatState = CombatManager.Instance?.DebugOnlyGetState();
        if (combatState == null) return [];

        var player = LocalContext.GetMe(combatState);
        var hand = player?.PlayerCombatState?.Hand?.Cards;
        if (hand == null) return [];

        // 使用 CardIndexedCommandCatalog 生成带数字后缀的词表
        foreach (var kvp in CardIndexedCommandCatalog.BuildWordToCard(hand))
            _indexedWordToCard[kvp.Key] = kvp.Value;

        // 收集所有卡牌名（用于不带数字词）
        foreach (var card in hand)
        {
            var normalizedName = VoiceText.Normalize(VoiceText.GetCardCommandName(card));
            if (!string.IsNullOrEmpty(normalizedName))
                _cardNames.Add(normalizedName);
        }

        // 合并词表：带数字后缀的词 + 基础卡牌名
        var allWords = new HashSet<string>(StringComparer.Ordinal);
        foreach (var word in _indexedWordToCard.Keys)
            allWords.Add(word);
        foreach (var name in _cardNames)
            allWords.Add(name);

        return allWords;
    }

    private static Creature? ResolveTarget(CardModel card, CombatState combatState)
    {
        return card.TargetType switch
        {
            TargetType.AnyEnemy => ResolveAnyEnemy(combatState),
            TargetType.AnyAlly => ResolveAnyAlly(card, combatState),
            _ => null // 无目标牌不需要 target
        };
    }

    private static Creature? ResolveAnyEnemy(CombatState combatState)
    {
        // 先尝试用瞄准状态
        var preferred = CombatTargetState.ResolvePreferredEnemy();
        if (preferred != null) return preferred;

        // 没有瞄准时，默认选第一个活着的敌人
        return combatState.Enemies.FirstOrDefault(c => c.IsAlive);
    }

    private static Creature? ResolveAnyAlly(CardModel card, CombatState combatState)
    {
        if (card.Owner?.Creature is { IsAlive: true } owner)
            return owner;
        return combatState.PlayerCreatures.FirstOrDefault(c => c.IsAlive);
    }
}