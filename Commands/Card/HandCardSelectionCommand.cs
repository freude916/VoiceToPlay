using System.Linq;
using Godot;
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
    private readonly Dictionary<string, NHandCardHolder> _wordToHolder = new();

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

    public void Execute(string word)
    {
        if (!_wordToHolder.TryGetValue(word, out var holder))
        {
            MainFile.Logger.Warn($"HandCardSelectionCommand: word '{word}' not found");
            return;
        }

        if (!GodotObject.IsInstanceValid(holder))
        {
            MainFile.Logger.Warn("HandCardSelectionCommand: holder is invalid");
            return;
        }

        holder.EmitSignal(NCardHolder.SignalName.Pressed, holder);
        MainFile.Logger.Info($"HandCardSelectionCommand: '{word}' -> selected holder");
    }

    public event Action<IVoiceCommand>? VocabularyChanged;

    /// <summary>
    ///     计算当前支持的词表（只在 RefreshVocabulary 中调用）
    /// </summary>
    private HashSet<string> ComputeSupportedWords()
    {
        _wordToHolder.Clear();

        var hand = NPlayerHand.Instance;
        if (hand == null || !hand.IsInCardSelection) return [];

        var holders = hand.ActiveHolders;
        if (holders.Count == 0) return [];

        // 统计每个卡牌名出现的次数
        var cardCountByName = new Dictionary<string, int>(StringComparer.Ordinal);
        var holdersByName = new Dictionary<string, List<NHandCardHolder>>(StringComparer.Ordinal);

        foreach (var holder in holders)
        {
            var cardModel = holder.CardModel;
            if (cardModel == null) continue;

            var normalizedName = VoiceText.Normalize(VoiceText.GetCardCommandName(cardModel));
            if (string.IsNullOrEmpty(normalizedName)) continue;

            if (!holdersByName.TryGetValue(normalizedName, out var list))
                holdersByName[normalizedName] = list = [];
            list.Add(holder);

            cardCountByName.TryGetValue(normalizedName, out var count);
            cardCountByName[normalizedName] = count + 1;
        }

        // 按卡名长度降序（优先匹配长名）
        var sortedNames = holdersByName.Keys.OrderByDescending(n => n).ToList();

        foreach (var normalizedName in sortedNames)
        {
            var count = cardCountByName[normalizedName];
            var holderList = holdersByName[normalizedName];

            // 使用 CardIndexedCommandCatalog 生成带中文数字后缀的词表
            foreach (var word in CardIndexedCommandCatalog.EnumerateIndexedCommands(normalizedName))
                if (CardIndexedCommandCatalog.TryParseOccurrence(word, normalizedName, out var occurrence))
                    // 只生成实际存在的数量
                    if (occurrence <= count && occurrence > 0 && occurrence <= holderList.Count)
                        _wordToHolder[word] = holderList[occurrence - 1];
        }

        return new HashSet<string>(_wordToHolder.Keys, StringComparer.Ordinal);
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
