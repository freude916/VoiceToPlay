using Godot;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using VoiceToPlay.Voice;
using VoiceToPlay.Voice.Core;

namespace VoiceToPlay.Commands.CardGrid;

/// <summary>
///     牌堆选牌命令。用于 NCardGridSelectionScreen（如消耗、升级选择等），支持卡牌名和滚动。
/// </summary>
public sealed class CardGridSelectCommand : IVoiceCommand
{
    private const float ScrollStep = 420f;

    private static readonly Dictionary<int, string> ChineseNumbers = new()
    {
        [1] = "一", [2] = "二", [3] = "三", [4] = "四", [5] = "五",
        [6] = "六", [7] = "七", [8] = "八", [9] = "九"
    };

    private static readonly string[] ScrollUpWords = ["向上滚", "上滚"];
    private static readonly string[] ScrollDownWords = ["向下滚", "下滚"];

    private readonly Dictionary<string, object> _wordToTarget = new(); // NGridCardHolder 或 "scroll_up" / "scroll_down"
    private NCardGrid? _lastCardGrid;
    private HashSet<string> _lastWords = new(StringComparer.Ordinal);

    public CardGridSelectCommand()
    {
        Instance = this;
    }

    public static CardGridSelectCommand? Instance { get; private set; }

    public IEnumerable<string> SupportedWords
    {
        get
        {
            _wordToTarget.Clear();
            _lastCardGrid = null;

            var screen = NOverlayStack.Instance?.Peek() as NCardGridSelectionScreen;
            if (screen == null) return [];

            var cardGrid = screen.GetNodeOrNull<NCardGrid>("%CardGrid");
            if (cardGrid == null) return [];

            _lastCardGrid = cardGrid;

            var holders = cardGrid.CurrentlyDisplayedCardHolders;
            var cardNameCounts = new Dictionary<string, int>(StringComparer.Ordinal);
            var holdersByName = new Dictionary<string, List<NGridCardHolder>>(StringComparer.Ordinal);

            // 统计卡牌名
            foreach (var holder in holders)
            {
                if (!GodotObject.IsInstanceValid(holder) || !holder.IsInsideTree() || !holder.Visible)
                    continue;

                var cardModel = holder.CardModel;
                if (cardModel == null) continue;

                var cardName = VoiceText.Normalize(VoiceText.GetCardCommandName(cardModel));
                if (string.IsNullOrEmpty(cardName)) continue;

                if (!holdersByName.TryGetValue(cardName, out var list))
                    holdersByName[cardName] = list = [];
                list.Add(holder);

                cardNameCounts.TryGetValue(cardName, out var count);
                cardNameCounts[cardName] = count + 1;
            }

            // 生成词表
            var index = 0;
            foreach (var holder in holders)
            {
                if (!GodotObject.IsInstanceValid(holder) || !holder.IsInsideTree() || !holder.Visible)
                    continue;

                var cardModel = holder.CardModel;
                if (cardModel == null) continue;

                var cardName = VoiceText.Normalize(VoiceText.GetCardCommandName(cardModel));
                if (string.IsNullOrEmpty(cardName)) continue;

                index++;

                // 序号词
                var indexWord = $"第{ChineseNumbers.GetValueOrDefault(index, index.ToString())}张";
                _wordToTarget[indexWord] = holder;

                // 卡牌名（重复时加序号）
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

            // 滚动词
            foreach (var word in ScrollUpWords)
                _wordToTarget[word] = "scroll_up";
            foreach (var word in ScrollDownWords)
                _wordToTarget[word] = "scroll_down";

            return _wordToTarget.Keys;
        }
    }

    public void Execute(string word)
    {
        if (!_wordToTarget.TryGetValue(word, out var target))
        {
            MainFile.Logger.Warn($"CardGridSelectionCommand: word '{word}' not found");
            return;
        }

        switch (target)
        {
            case NGridCardHolder holder:
                if (!GodotObject.IsInstanceValid(holder))
                {
                    MainFile.Logger.Warn("CardGridSelectionCommand: holder is invalid");
                    return;
                }

                holder.EmitSignal(NCardHolder.SignalName.Pressed, holder);
                MainFile.Logger.Info($"CardGridSelectionCommand: '{word}' -> selected card");
                break;

            case "scroll_up":
                Scroll(-ScrollStep);
                break;

            case "scroll_down":
                Scroll(ScrollStep);
                break;

            default:
                MainFile.Logger.Warn($"CardGridSelectionCommand: unknown target type {target?.GetType()}");
                break;
        }
    }

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
        MainFile.Logger.Info($"CardGridSelectionCommand: scrolled from {from:F1} to {to:F1}");
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

    public static void ClearVocabulary()
    {
        var instance = Instance;
        if (instance == null) return;

        instance._wordToTarget.Clear();
        instance._lastCardGrid = null;
        if (instance._lastWords.Count > 0)
        {
            instance._lastWords = new HashSet<string>(StringComparer.Ordinal);
            instance.VocabularyChanged?.Invoke(instance);
        }
    }
}
