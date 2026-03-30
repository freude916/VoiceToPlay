using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Combat;
using VoiceToPlay.Voice;
using VoiceToPlay.Voice.Core;

namespace VoiceToPlay.Commands.Card;

/// <summary>
///     手牌选牌命令。用于"选择一张手牌消耗"等场景。
///     当 NPlayerHand.IsInCardSelection 为 true 时激活。
/// </summary>
public sealed class HandCardSelectionCommand : IVoiceCommand
{
    /// <summary>
    ///     卡牌名 → 首个 holder（用于不带数字词的实时搜索）
    /// </summary>
    private readonly Dictionary<string, NHandCardHolder> _cardNameToFirstHolder = new(StringComparer.Ordinal);

    /// <summary>
    ///     带数字后缀的词 → NHandCardHolder
    /// </summary>
    private readonly Dictionary<string, NHandCardHolder> _indexedWordToHolder = new();

    /// <summary>
    ///     缓存的词表，SupportedWords getter 直接返回此缓存
    /// </summary>
    private HashSet<string> _cachedWords = new(StringComparer.Ordinal);

    public HandCardSelectionCommand()
    {
        Instance = this;
    }

    public static HandCardSelectionCommand? Instance { get; private set; }

    /// <summary>
    ///     只返回缓存，不做任何计算
    /// </summary>
    public IEnumerable<string> SupportedWords => _cachedWords;

    public CommandResult Execute(string word)
    {
        NHandCardHolder? holder;

        // 尝试匹配带数字后缀的词
        if (_indexedWordToHolder.TryGetValue(word, out var cachedHolder))
        {
            // 验证 holder 仍然有效
            if (!GodotObject.IsInstanceValid(cachedHolder) || !cachedHolder.IsInsideTree())
            {
                MainFile.Logger.Warn("HandCardSelectionCommand: cached holder is invalid");
                return CommandResult.Failed;
            }

            holder = cachedHolder;
        }
        // 尝试匹配不带数字的基础词（实时搜索首个可用的同名卡）
        else if (_cardNameToFirstHolder.TryGetValue(word, out var firstHolder))
        {
            if (!GodotObject.IsInstanceValid(firstHolder) || !firstHolder.IsInsideTree())
            {
                MainFile.Logger.Warn("HandCardSelectionCommand: first holder is invalid");
                return CommandResult.Failed;
            }

            holder = firstHolder;
        }
        else
        {
            MainFile.Logger.Warn($"HandCardSelectionCommand: word '{word}' not found");
            return CommandResult.Failed;
        }

        holder.EmitSignal(NCardHolder.SignalName.Pressed, holder);
        MainFile.Logger.Debug($"HandCardSelectionCommand: '{word}' -> selected holder");
        return CommandResult.Success;
    }

    public event Action<IVoiceCommand>? VocabularyChanged;

    /// <summary>
    ///     计算当前支持的词表（只在 RefreshVocabulary 中调用）
    /// </summary>
    private HashSet<string> ComputeSupportedWords()
    {
        _indexedWordToHolder.Clear();
        _cardNameToFirstHolder.Clear();

        var hand = NPlayerHand.Instance;
        if (hand == null || !hand.IsInCardSelection) return [];

        var holders = hand.ActiveHolders;
        if (holders.Count == 0) return [];

        // 构建 cardModel -> holder 映射
        var cardToHolder = new Dictionary<CardModel, NHandCardHolder>();
        var cardModels = new List<CardModel>();
        foreach (var holder in holders)
        {
            var cardModel = holder.CardModel;
            if (cardModel == null) continue;
            cardToHolder[cardModel] = holder;
            cardModels.Add(cardModel);
        }

        // 使用 CardIndexedCommandCatalog 生成带数字后缀的词表
        var wordToCard = CardIndexedCommandCatalog.BuildWordToCard(cardModels);
        foreach (var (key, cardModel) in wordToCard)
            if (cardToHolder.TryGetValue(cardModel, out var holder))
                _indexedWordToHolder[key] = holder;

        // 收集基础卡牌名 → 首个 holder
        foreach (var cardModel in cardModels)
        {
            var normalizedName = VoiceText.Normalize(VoiceText.GetCardCommandName(cardModel));
            if (string.IsNullOrEmpty(normalizedName)) continue;
            // 只记录第一个，用于不带数字词
            if (!_cardNameToFirstHolder.ContainsKey(normalizedName) &&
                cardToHolder.TryGetValue(cardModel, out var holder))
                _cardNameToFirstHolder[normalizedName] = holder;
        }

        // 合并词表
        var allWords = new HashSet<string>(StringComparer.Ordinal);
        foreach (var word in _indexedWordToHolder.Keys)
            allWords.Add(word);
        foreach (var name in _cardNameToFirstHolder.Keys)
            allWords.Add(name);

        return allWords;
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
}