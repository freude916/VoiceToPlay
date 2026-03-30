using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using VoiceToPlay.Commands.Card;
using VoiceToPlay.Commands.DeckView;
using VoiceToPlay.Util;
using VoiceToPlay.Voice;
using VoiceToPlay.Voice.Core;

namespace VoiceToPlay.Commands.CardRow;

/// <summary>
///     卡牌选择命令。用于卡牌奖励选择屏幕，支持 "卡牌名一"、"卡牌名二" 等。
/// </summary>
public sealed class CardRowSelectCommand : IVoiceCommand
{
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
    ///     按钮词 → NButton（备选按钮、跳过等）
    /// </summary>
    private readonly Dictionary<string, NButton> _wordToButton = new(StringComparer.Ordinal);

    /// <summary>
    ///     缓存的词表，SupportedWords getter 直接返回此缓存
    /// </summary>
    private HashSet<string> _cachedWords = new(StringComparer.Ordinal);

    public CardRowSelectCommand()
    {
        Instance = this;
    }

    public static CardRowSelectCommand? Instance { get; private set; }

    /// <summary>
    ///     只返回缓存，不做任何计算
    /// </summary>
    public IEnumerable<string> SupportedWords => _cachedWords;

    public CommandResult Execute(string word)
    {
        // Pass：牌组视图打开时让路
        if (DeckViewCommand.IsOpen) return CommandResult.Pass;

        // 尝试匹配按钮词
        if (_wordToButton.TryGetValue(word, out var button))
        {
            if (!GodotObject.IsInstanceValid(button))
            {
                MainFile.Logger.Warn("CardSelectionCommand: button is invalid");
                return CommandResult.Failed;
            }

            button.EmitSignal("Released", button);
            MainFile.Logger.Debug($"CardSelectionCommand: '{word}' -> clicked button");
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
            MainFile.Logger.Warn($"CardSelectionCommand: word '{word}' not found");
            return CommandResult.Failed;
        }

        // 验证 holder 有效性
        if (!GodotObject.IsInstanceValid(holder))
        {
            MainFile.Logger.Warn("CardSelectionCommand: holder is invalid");
            return CommandResult.Failed;
        }

        holder.EmitSignal("Pressed", holder);
        MainFile.Logger.Debug($"CardSelectionCommand: '{word}' -> selected card");
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
        _indexWordToHolder.Clear();
        _wordToButton.Clear();

        // 支持 NCardRewardSelectionScreen 和 NChooseACardSelectionScreen
        var screen = NOverlayStack.Instance?.Peek();
        Control? cardRow = null;
        Control? alternativesContainer = null;
        NButton? skipButton = null;

        if (screen is NCardRewardSelectionScreen rewardScreen)
        {
            cardRow = rewardScreen.GetNodeOrNull<Control>("UI/CardRow");
            alternativesContainer = rewardScreen.GetNodeOrNull<Control>("UI/RewardAlternatives");
        }
        else if (screen is NChooseACardSelectionScreen chooseScreen)
        {
            cardRow = chooseScreen.GetNodeOrNull<Control>("CardRow");
            // NChooseACardSelectionScreen 的跳过按钮
            skipButton = chooseScreen.GetNodeOrNull<NButton>("SkipButton");
        }

        if (cardRow == null) return [];

        // 收集有效的 holder 和对应的 cardModel
        var validHolders = new List<(NGridCardHolder Holder, CardModel CardModel)>();
        foreach (var child in cardRow.GetChildren())
        {
            if (child is not NGridCardHolder holder) continue;
            var model = holder.CardNode?.Model;
            if (model == null) continue;
            validHolders.Add((holder, model));
        }

        if (validHolders.Count > 0)
        {
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
                var normalizedName = VoiceText.Normalize(cardModel.TitleLocString.GetFormattedText());
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
        }

        // 备选按钮（默认有跳过，献祭卡牌等由遗物或其他来源添加，仅 NCardRewardSelectionScreen）
        if (alternativesContainer != null)
            foreach (var child in alternativesContainer.GetChildren())
            {
                if (child is not NCardRewardAlternativeButton button) continue;
                var buttonText = button.GetNodeOrNull<Label>("Label")?.Text;
                if (string.IsNullOrEmpty(buttonText)) continue;

                var normalizedText = VoiceText.Normalize(buttonText);
                if (!string.IsNullOrEmpty(normalizedText))
                    _wordToButton[normalizedText] = button;
            }

        // NChooseACardSelectionScreen 的跳过按钮
        if (skipButton != null && skipButton.IsEnabled) _wordToButton["跳过"] = skipButton;

        // 合并词表
        var allWords = new HashSet<string>(StringComparer.Ordinal);
        foreach (var word in _indexedWordToHolder.Keys)
            allWords.Add(word);
        foreach (var name in _cardNameToFirstHolder.Keys)
            allWords.Add(name);
        foreach (var word in _indexWordToHolder.Keys)
            allWords.Add(word);
        foreach (var word in _wordToButton.Keys)
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

    /// <summary>
    ///     强制清空词表并触发 VocabularyChanged
    /// </summary>
    public static void ClearVocabulary()
    {
        var instance = Instance;
        if (instance == null) return;

        instance._indexedWordToHolder.Clear();
        instance._cardNameToFirstHolder.Clear();
        instance._indexWordToHolder.Clear();
        instance._wordToButton.Clear();
        if (instance._cachedWords.Count > 0)
        {
            instance._cachedWords = new HashSet<string>(StringComparer.Ordinal);
            instance.VocabularyChanged?.Invoke(instance);
        }
    }
}