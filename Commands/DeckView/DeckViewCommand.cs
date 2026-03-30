using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Capstones;
using VoiceToPlay.Commands.Card;
using VoiceToPlay.Voice;
using VoiceToPlay.Voice.Core;

namespace VoiceToPlay.Commands.DeckView;

/// <summary>
///     牌组视图命令。处理牌组浏览、卡牌详情、滚动等。
/// </summary>
public sealed class DeckViewCommand : IVoiceCommand
{
    /// <summary>
    ///     滚动步长（像素）
    /// </summary>
    private const float ScrollStep = 200f;

    /// <summary>
    ///     卡牌名 → 首个 CardModel（用于不带数字词）
    /// </summary>
    private readonly Dictionary<string, CardModel> _cardNameToFirstCard = new(StringComparer.Ordinal);

    /// <summary>
    ///     带数字后缀的词 → CardModel
    /// </summary>
    private readonly Dictionary<string, CardModel> _indexedWordToCard = new();

    private readonly Dictionary<string, Action> _wordToAction = new(StringComparer.Ordinal);
    private HashSet<string> _cachedWords = new(StringComparer.Ordinal);

    public DeckViewCommand()
    {
        Instance = this;
    }

    public static DeckViewCommand? Instance { get; private set; }

    /// <summary>
    ///     检查牌组视图是否打开
    /// </summary>
    public static bool IsOpen => NCapstoneContainer.Instance?.CurrentCapstoneScreen is NDeckViewScreen;

    public IEnumerable<string> SupportedWords => _cachedWords;

    public CommandResult Execute(string word)
    {
        CardModel? targetCard = null;

        // 尝试匹配带数字后缀的词
        if (_indexedWordToCard.TryGetValue(word, out var cachedCard))
        {
            targetCard = cachedCard;
        }
        // 尝试匹配不带数字的基础词
        else if (_cardNameToFirstCard.TryGetValue(word, out var firstCard))
        {
            targetCard = firstCard;
        }
        // 再检查其他命令
        else if (_wordToAction.TryGetValue(word, out var action))
        {
            try
            {
                action.Invoke();
                MainFile.Logger.Info($"DeckViewCommand: executed '{word}'");
                return CommandResult.Success;
            }
            catch (Exception e)
            {
                MainFile.Logger.Warn($"DeckViewCommand: failed to execute '{word}': {e.Message}");
                return CommandResult.Failed;
            }
        }
        else
        {
            MainFile.Logger.Warn($"DeckViewCommand: word '{word}' not found");
            return CommandResult.Failed;
        }

        // 执行卡牌选择
        return ExecuteCardCommand(targetCard!);
    }

    public event Action<IVoiceCommand>? VocabularyChanged;

    /// <summary>
    ///     执行卡牌选择命令
    /// </summary>
    private CommandResult ExecuteCardCommand(CardModel targetCard)
    {
        var grid = GetGrid(GetCurrentDeckViewScreen());
        if (grid == null) return CommandResult.Failed;

        var cards = grid.CurrentlyDisplayedCards?.ToList();
        if (cards == null || cards.Count == 0) return CommandResult.Failed;

        // 找到目标卡牌的索引
        var index = cards.IndexOf(targetCard);
        if (index < 0)
        {
            MainFile.Logger.Warn("DeckViewCommand: target card not found in grid");
            return CommandResult.Failed;
        }

        OpenCardDetail(grid, index);
        return CommandResult.Success;
    }

    /// <summary>
    ///     获取当前的牌组视图屏幕
    /// </summary>
    private static NDeckViewScreen? GetCurrentDeckViewScreen()
    {
        var capstone = NCapstoneContainer.Instance;
        if (capstone == null) return null;
        return capstone.CurrentCapstoneScreen as NDeckViewScreen;
    }

    /// <summary>
    ///     计算当前支持的词表
    /// </summary>
    private HashSet<string> ComputeSupportedWords()
    {
        _indexedWordToCard.Clear();
        _cardNameToFirstCard.Clear();
        _wordToAction.Clear();

        var deckViewScreen = GetCurrentDeckViewScreen();
        if (deckViewScreen == null || !GodotObject.IsInstanceValid(deckViewScreen)) return [];

        // 获取网格和卡牌列表
        var grid = GetGrid(deckViewScreen);
        if (grid == null) return [];

        var cards = grid.CurrentlyDisplayedCards?.ToList();
        if (cards == null || cards.Count == 0) return [];

        // 使用 CardIndexedCommandCatalog 生成带数字后缀的词表
        var wordToCard = CardIndexedCommandCatalog.BuildWordToCard(cards);
        foreach (var kvp in wordToCard)
            _indexedWordToCard[kvp.Key] = kvp.Value;

        // 收集基础卡牌名 → 首个 CardModel
        foreach (var card in cards)
        {
            var normalizedName = VoiceText.Normalize(VoiceText.GetCardCommandName(card));
            if (string.IsNullOrEmpty(normalizedName)) continue;
            if (!_cardNameToFirstCard.ContainsKey(normalizedName))
                _cardNameToFirstCard[normalizedName] = card;
        }

        // "查看升级" - 切换升级预览
        _wordToAction["查看升级"] = () => ToggleShowUpgrades(deckViewScreen);

        // "向上滚" / "向下滚"
        _wordToAction["向上滚"] = () => ScrollGrid(grid, -ScrollStep);
        _wordToAction["向下滚"] = () => ScrollGrid(grid, ScrollStep);

        // 合并词表
        var allWords = new HashSet<string>(StringComparer.Ordinal);
        foreach (var word in _indexedWordToCard.Keys)
            allWords.Add(word);
        foreach (var name in _cardNameToFirstCard.Keys)
            allWords.Add(name);
        foreach (var word in _wordToAction.Keys)
            allWords.Add(word);

        return allWords;
    }

    /// <summary>
    ///     获取牌组视图的网格组件
    /// </summary>
    private static NCardGrid? GetGrid(NDeckViewScreen? screen)
    {
        if (screen == null) return null;
        return screen.GetNode<NCardGrid>("CardGrid");
    }

    /// <summary>
    ///     打开卡牌详情
    /// </summary>
    private static void OpenCardDetail(NCardGrid grid, int index)
    {
        var cards = grid.CurrentlyDisplayedCards?.ToList();
        if (cards == null || index < 0 || index >= cards.Count) return;

        var inspectScreen = NGame.Instance.GetInspectCardScreen();
        if (inspectScreen == null) return;

        inspectScreen.Open(cards, index, grid.IsShowingUpgrades);
    }

    /// <summary>
    ///     切换升级预览
    /// </summary>
    private static void ToggleShowUpgrades(NDeckViewScreen screen)
    {
        var tickbox = screen.GetNode<NTickbox>("%Upgrades");
        if (tickbox != null && GodotObject.IsInstanceValid(tickbox) && tickbox.IsEnabled)
            tickbox.ForceClick();
    }

    /// <summary>
    ///     滚动网格
    /// </summary>
    private static void ScrollGrid(NCardGrid grid, float delta)
    {
        var scrollContainer = grid.GetNode<ScrollContainer>("ScrollContainer");
        if (scrollContainer == null) return;

        var currentY = scrollContainer.ScrollVertical;
        scrollContainer.ScrollVertical = (int)(currentY + delta);
    }

    /// <summary>
    ///     刷新词表缓存
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