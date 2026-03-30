using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using VoiceToPlay.Commands.Card;
using VoiceToPlay.Commands.DeckView;
using VoiceToPlay.Util;
using VoiceToPlay.Voice;
using VoiceToPlay.Voice.Core;

namespace VoiceToPlay.Commands.CardGrid;

/// <summary>
///     牌堆选牌命令。用于 NCardGridSelectionScreen（如消耗、升级选择等），支持卡牌名和滚动。
/// </summary>
public sealed class CardGridSelectCommand : IVoiceCommand
{
    private const float ScrollStep = 420f;

    private static readonly string[] ScrollUpWords = ["向上滚", "上滚"];
    private static readonly string[] ScrollDownWords = ["向下滚", "下滚"];

    /// <summary>
    ///     卡牌名 → 首个 holder（用于不带数字词）
    /// </summary>
    private readonly Dictionary<string, NGridCardHolder> _cardNameToFirstHolder = new(StringComparer.Ordinal);

    /// <summary>
    ///     带数字后缀的词 → NGridCardHolder
    /// </summary>
    private readonly Dictionary<string, NGridCardHolder> _indexedWordToHolder = new();

    /// <summary>
    ///     序号词 → holder（第一张、第二张...）
    /// </summary>
    private readonly Dictionary<string, NGridCardHolder> _indexWordToHolder = new(StringComparer.Ordinal);

    /// <summary>
    ///     滚动词
    /// </summary>
    private readonly HashSet<string> _scrollWords = new(StringComparer.Ordinal);

    /// <summary>
    ///     缓存的词表，SupportedWords getter 直接返回此缓存
    /// </summary>
    private HashSet<string> _cachedWords = new(StringComparer.Ordinal);

    private NCardGrid? _lastCardGrid;

    public CardGridSelectCommand()
    {
        Instance = this;
    }

    public static CardGridSelectCommand? Instance { get; private set; }

    /// <summary>
    ///     只返回缓存，不做任何计算
    /// </summary>
    public IEnumerable<string> SupportedWords => _cachedWords;

    public CommandResult Execute(string word)
    {
        // Pass：牌组视图打开时让路
        if (DeckViewCommand.IsOpen) return CommandResult.Pass;

        // 检查滚动词
        if (_scrollWords.Contains(word))
        {
            var delta = ScrollUpWords.Contains(word) ? -ScrollStep : ScrollStep;
            Scroll(delta);
            return CommandResult.Success;
        }

        NGridCardHolder? holder = null;

        // 尝试匹配带数字后缀的词
        if (_indexedWordToHolder.TryGetValue(word, out var cachedHolder))
        {
            holder = cachedHolder;
        }
        // 尝试匹配不带数字的基础词
        else if (_cardNameToFirstHolder.TryGetValue(word, out var firstHolder))
        {
            holder = firstHolder;
        }
        // 尝试匹配序号词
        else if (_indexWordToHolder.TryGetValue(word, out var indexHolder))
        {
            holder = indexHolder;
        }
        else
        {
            MainFile.Logger.Warn($"CardGridSelectionCommand: word '{word}' not found");
            return CommandResult.Failed;
        }

        // 验证 holder 有效性
        if (!GodotObject.IsInstanceValid(holder) || !holder.IsInsideTree() || !holder.Visible)
        {
            MainFile.Logger.Warn("CardGridSelectionCommand: holder is invalid");
            return CommandResult.Failed;
        }

        holder.EmitSignal(NCardHolder.SignalName.Pressed, holder);
        MainFile.Logger.Debug($"CardGridSelectionCommand: '{word}' -> selected card");
        return CommandResult.Success;
    }

    public event Action<IVoiceCommand>? VocabularyChanged;

    private void Scroll(float delta)
    {
        var cardGrid = _lastCardGrid;
        if (cardGrid == null || !GodotObject.IsInstanceValid(cardGrid))
        {
            MainFile.Logger.Warn("CardGridSelectionCommand: card grid is null or invalid");
            return;
        }

        var scrollContainer = cardGrid.GetNodeOrNull<Control>("%ScrollContainer");
        if (scrollContainer == null || !GodotObject.IsInstanceValid(scrollContainer))
        {
            MainFile.Logger.Warn("CardGridSelectionCommand: scroll container not found");
            return;
        }

        var from = scrollContainer.Position.Y;
        var to = from + delta;
        cardGrid.SetScrollPosition(to);
        MainFile.Logger.Debug($"CardGridSelectionCommand: scrolled from {from:F1} to {to:F1}");
    }

    /// <summary>
    ///     计算当前支持的词表（只在 RefreshVocabulary 中调用）
    /// </summary>
    private HashSet<string> ComputeSupportedWords()
    {
        _indexedWordToHolder.Clear();
        _cardNameToFirstHolder.Clear();
        _indexWordToHolder.Clear();
        _scrollWords.Clear();
        _lastCardGrid = null;

        var screen = NOverlayStack.Instance?.Peek() as NCardGridSelectionScreen;
        if (screen == null) return [];

        var cardGrid = screen.GetNodeOrNull<NCardGrid>("%CardGrid");
        if (cardGrid == null) return [];

        _lastCardGrid = cardGrid;

        var holders = cardGrid.CurrentlyDisplayedCardHolders;

        // 收集有效的 holder 和对应的 cardModel
        var validHolders = new List<(NGridCardHolder Holder, CardModel CardModel)>();
        foreach (var holder in holders)
        {
            if (!GodotObject.IsInstanceValid(holder) || !holder.IsInsideTree() || !holder.Visible)
                continue;
            var cardModel = holder.CardModel;
            if (cardModel == null) continue;
            validHolders.Add((holder, cardModel));
        }

        if (validHolders.Count == 0) return [];

        // 构建 CardModel → Holder 映射
        var cardToHolder = new Dictionary<CardModel, NGridCardHolder>();
        foreach (var (holder, cardModel) in validHolders)
            cardToHolder[cardModel] = holder;

        // 使用 CardIndexedCommandCatalog 生成带数字后缀的词表
        var cardModels = validHolders.Select(h => h.CardModel);
        var wordToCard = CardIndexedCommandCatalog.BuildWordToCard(cardModels);

        // 映射回 holder
        foreach (var kvp in wordToCard)
        {
            var cardModel = kvp.Value;
            if (cardToHolder.TryGetValue(cardModel, out var holder))
                _indexedWordToHolder[kvp.Key] = holder;
        }

        // 收集基础卡牌名 → 首个 holder
        foreach (var (holder, cardModel) in validHolders)
        {
            var normalizedName = VoiceText.Normalize(VoiceText.GetCardCommandName(cardModel));
            if (string.IsNullOrEmpty(normalizedName)) continue;
            if (!_cardNameToFirstHolder.ContainsKey(normalizedName))
                _cardNameToFirstHolder[normalizedName] = holder;
        }

        // 序号词（第一张、第二张...）
        for (var i = 0; i < validHolders.Count; i++)
        {
            var indexWord = L10n.CardOrdinal(i + 1);
            _indexWordToHolder[indexWord] = validHolders[i].Holder;
        }

        // 滚动词
        foreach (var word in ScrollUpWords)
            _scrollWords.Add(word);
        foreach (var word in ScrollDownWords)
            _scrollWords.Add(word);

        // 合并词表
        var allWords = new HashSet<string>(StringComparer.Ordinal);
        foreach (var word in _indexedWordToHolder.Keys)
            allWords.Add(word);
        foreach (var name in _cardNameToFirstHolder.Keys)
            allWords.Add(name);
        foreach (var word in _indexWordToHolder.Keys)
            allWords.Add(word);
        foreach (var word in _scrollWords)
            allWords.Add(word);

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

    public static void ClearVocabulary()
    {
        var instance = Instance;
        if (instance == null) return;

        instance._indexedWordToHolder.Clear();
        instance._cardNameToFirstHolder.Clear();
        instance._indexWordToHolder.Clear();
        instance._scrollWords.Clear();
        instance._lastCardGrid = null;
        if (instance._cachedWords.Count > 0)
        {
            instance._cachedWords = new HashSet<string>(StringComparer.Ordinal);
            instance.VocabularyChanged?.Invoke(instance);
        }
    }
}