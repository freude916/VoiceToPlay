using Godot;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using VoiceToPlay.Voice;
using VoiceToPlay.Voice.Core;

namespace VoiceToPlay.Commands.CardRow;

/// <summary>
///     卡牌选择命令。用于卡牌奖励选择屏幕，支持 "卡牌名"、"卡牌名一"、"卡牌名二" 等。
/// </summary>
public sealed class CardRowSelectCommand : IVoiceCommand
{
    private static readonly Dictionary<int, string> ChineseNumbers = new()
    {
        [1] = "一", [2] = "二", [3] = "三", [4] = "四", [5] = "五",
        [6] = "六", [7] = "七", [8] = "八", [9] = "九"
    };

    private readonly Dictionary<string, object> _wordToTarget = new(); // NGridCardHolder 或 NCardRewardAlternativeButton
    private HashSet<string> _lastWords = new(StringComparer.Ordinal);

    public CardRowSelectCommand()
    {
        Instance = this;
    }

    public static CardRowSelectCommand? Instance { get; private set; }

    public IEnumerable<string> SupportedWords
    {
        get
        {
            _wordToTarget.Clear();

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

            var index = 0;
            var cardNameCounts = new Dictionary<string, int>(StringComparer.Ordinal);

            // 第一遍：统计每个卡牌名出现的次数
            foreach (var child in cardRow.GetChildren())
            {
                if (child is not NGridCardHolder holder) continue;
                var model = holder.CardNode?.Model;
                if (model == null) continue;

                var cardName = VoiceText.Normalize(model.TitleLocString.GetFormattedText());
                if (string.IsNullOrEmpty(cardName)) continue;

                cardNameCounts.TryGetValue(cardName, out var count);
                cardNameCounts[cardName] = count + 1;
            }

            // 第二遍：生成词汇
            foreach (var child in cardRow.GetChildren())
            {
                if (child is not NGridCardHolder holder) continue;
                index++;

                var model = holder.CardNode?.Model;
                if (model == null) continue;

                var cardName = VoiceText.Normalize(model.TitleLocString.GetFormattedText());
                if (string.IsNullOrEmpty(cardName)) continue;

                // 序号词（第一张、第二张...）
                var indexWord = $"第{ChineseNumbers.GetValueOrDefault(index, index.ToString())}张";
                _wordToTarget[indexWord] = holder;

                // 卡牌名：如果有重复，加序号；否则不加
                if (cardNameCounts.GetValueOrDefault(cardName) > 1)
                {
                    var namedWord = $"{cardName}{ChineseNumbers.GetValueOrDefault(index, index.ToString())}";
                    _wordToTarget[namedWord] = holder;
                }
                else
                {
                    _wordToTarget[cardName] = holder;
                }
            }

            // 备选按钮（默认有跳过，献祭卡牌等由遗物或其他来源添加，仅 NCardRewardSelectionScreen）
            if (alternativesContainer != null)
            {
                foreach (var child in alternativesContainer.GetChildren())
                {
                    if (child is not NCardRewardAlternativeButton button) continue;
                    var buttonText = button.GetNodeOrNull<Label>("Label")?.Text;
                    if (string.IsNullOrEmpty(buttonText)) continue;

                    var normalizedText = VoiceText.Normalize(buttonText);
                    if (!string.IsNullOrEmpty(normalizedText))
                        _wordToTarget[normalizedText] = button;
                }
            }

            // NChooseACardSelectionScreen 的跳过按钮
            if (skipButton != null && skipButton.IsEnabled)
            {
                _wordToTarget["跳过"] = skipButton;
            }

            return _wordToTarget.Keys;
        }
    }

    public void Execute(string word)
    {
        if (!_wordToTarget.TryGetValue(word, out var target))
        {
            MainFile.Logger.Warn($"CardSelectionCommand: word '{word}' not found");
            return;
        }

        switch (target)
        {
            case NGridCardHolder holder:
                if (!GodotObject.IsInstanceValid(holder))
                {
                    MainFile.Logger.Warn("CardSelectionCommand: holder is invalid");
                    return;
                }

                holder.EmitSignal("Pressed", holder);
                MainFile.Logger.Info($"CardSelectionCommand: '{word}' -> selected card");
                break;

            case NButton button:
                if (!GodotObject.IsInstanceValid(button))
                {
                    MainFile.Logger.Warn("CardSelectionCommand: button is invalid");
                    return;
                }

                button.EmitSignal("Released", button);
                MainFile.Logger.Info($"CardSelectionCommand: '{word}' -> clicked button");
                break;

            default:
                MainFile.Logger.Warn($"CardSelectionCommand: unknown target type {target?.GetType()}");
                break;
        }
    }

    public event Action<IVoiceCommand>? VocabularyChanged;

    public static void RefreshVocabulary()
    {
        var instance = Instance;
        if (instance == null) return;

        var currentWords = new HashSet<string>(instance.SupportedWords, StringComparer.Ordinal);
        if (!currentWords.SetEquals(instance._lastWords))
        {
            instance._lastWords = currentWords;
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

        instance._wordToTarget.Clear();
        if (instance._lastWords.Count > 0)
        {
            instance._lastWords = new HashSet<string>(StringComparer.Ordinal);
            instance.VocabularyChanged?.Invoke(instance);
        }
    }
}
