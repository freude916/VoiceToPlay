using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using VoiceToPlay.Commands.Combat;
using VoiceToPlay.Voice;
using VoiceToPlay.Voice.Core;

namespace VoiceToPlay.Commands.Card;

/// <summary>
///     出牌命令。支持按卡名和数字索引出牌（如 "防御", "防御3"）。
/// </summary>
public sealed class PlayCardCommand : IVoiceCommand
{
    // 词 → (卡牌名, occurrence)
    private readonly Dictionary<string, (string NormalizedName, int Occurrence)> _wordToCard = new();

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

    public void Execute(string word)
    {
        // 防御性检查：overlay 打开或手牌选牌模式时忽略
        if (NOverlayStack.Instance?.Peek() != null) return;
        if (NPlayerHand.Instance?.IsInCardSelection == true) return;

        if (!_wordToCard.TryGetValue(word, out var info))
        {
            MainFile.Logger.Warn($"PlayCardCommand: word '{word}' not found");
            return;
        }

        var (normalizedCardName, occurrence) = info;
        var combatState = CombatManager.Instance?.DebugOnlyGetState();
        if (combatState == null)
        {
            MainFile.Logger.Warn("PlayCardCommand: combat not active");
            return;
        }

        var player = LocalContext.GetMe(combatState);
        var hand = player?.PlayerCombatState?.Hand?.Cards;
        if (hand == null)
        {
            MainFile.Logger.Warn("PlayCardCommand: hand is null");
            return;
        }

        // 查找第 occurrence 张同名卡
        CardModel? matchedCard = null;
        var matchedCount = 0;
        foreach (var card in hand)
        {
            var name = VoiceText.Normalize(VoiceText.GetCardCommandName(card));
            if (name != normalizedCardName) continue;

            matchedCount++;
            if (matchedCount == occurrence)
            {
                matchedCard = card;
                break;
            }
        }

        if (matchedCard == null)
        {
            MainFile.Logger.Warn($"PlayCardCommand: card '{normalizedCardName}' occurrence {occurrence} not found");
            return;
        }

        if (!matchedCard.CanPlay(out _, out _))
        {
            MainFile.Logger.Info($"PlayCardCommand: card '{normalizedCardName}' cannot be played now");
            return;
        }

        // 解析目标
        var target = ResolveTarget(matchedCard, combatState);

        // 无目标牌不需要验证目标
        var needsTarget = matchedCard.TargetType is TargetType.AnyEnemy or TargetType.AnyAlly;
        if (needsTarget && !matchedCard.IsValidTarget(target))
        {
            MainFile.Logger.Info($"PlayCardCommand: invalid target for '{normalizedCardName}'");
            return;
        }

        var queued = matchedCard.TryManualPlay(target);
        MainFile.Logger.Info($"PlayCardCommand: '{word}' -> {normalizedCardName}#{occurrence}, queued={queued}");
        
        // 立即刷新词表，避免同一张卡被重复使用
        if (queued)
            RefreshVocabulary();
    }

    public event Action<IVoiceCommand>? VocabularyChanged;

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
        _wordToCard.Clear();

        // 如果在手牌选牌模式（消耗/升级等），禁用出牌命令
        if (NPlayerHand.Instance?.IsInCardSelection == true) return [];

        // 如果有 overlay 打开（选牌屏幕等），禁用出牌命令
        if (NOverlayStack.Instance?.Peek() != null) return [];

        var combatState = CombatManager.Instance?.DebugOnlyGetState();
        if (combatState == null) return [];

        var player = LocalContext.GetMe(combatState);
        var hand = player?.PlayerCombatState?.Hand?.Cards;
        if (hand == null) return [];

        // 统计每张卡的出现次数
        var cardCountByName = new Dictionary<string, int>(StringComparer.Ordinal);
        var cardsByName = new Dictionary<string, List<CardModel>>(StringComparer.Ordinal);

        foreach (var card in hand)
        {
            var normalizedName = VoiceText.Normalize(VoiceText.GetCardCommandName(card));
            if (string.IsNullOrEmpty(normalizedName)) continue;

            if (!cardsByName.TryGetValue(normalizedName, out var list))
                cardsByName[normalizedName] = list = [];
            list.Add(card);

            cardCountByName.TryGetValue(normalizedName, out var count);
            cardCountByName[normalizedName] = count + 1;
        }

        // 按卡名长度降序（优先匹配长名）
        var sortedNames = cardsByName.Keys.OrderByDescending(n => n).ToList();

        foreach (var normalizedName in sortedNames)
        {
            var count = cardCountByName[normalizedName];

            // 使用 CardIndexedCommandCatalog 生成带中文数字后缀的词表
            foreach (var word in CardIndexedCommandCatalog.EnumerateIndexedCommands(normalizedName))
                if (CardIndexedCommandCatalog.TryParseOccurrence(word, normalizedName, out var occurrence))
                    // 只生成实际存在的数量
                    if (occurrence <= count)
                        _wordToCard[word] = (normalizedName, occurrence);
        }

        return new HashSet<string>(_wordToCard.Keys, StringComparer.Ordinal);
    }

    private static Creature? ResolveTarget(CardModel card, CombatState combatState)
    {
        return card.TargetType switch
        {
            TargetType.AnyEnemy => ResolveAnyEnemy(combatState),
            TargetType.AnyAlly => ResolveAnyAlly(card, combatState),
            _ => null  // 无目标牌不需要 target
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
