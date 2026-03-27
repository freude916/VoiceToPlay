using MegaCrit.Sts2.Core.Nodes.Events;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using VoiceToPlay.Voice.Core;

namespace VoiceToPlay.Commands.Event;

/// <summary>
///     事件选项命令。支持 "第一个选项", "第二个选项", "继续" 等。
/// </summary>
public sealed class EventCommand : IVoiceCommand
{
    private static readonly Dictionary<int, string> ChineseNumbers = new()
    {
        [1] = "一", [2] = "二", [3] = "三", [4] = "四", [5] = "五",
        [6] = "六", [7] = "七", [8] = "八", [9] = "九"
    };

    private readonly Dictionary<string, int> _wordToIndex = new();
    private HashSet<string> _lastWords = new(StringComparer.Ordinal);

    public EventCommand()
    {
        Instance = this;
    }

    public static EventCommand? Instance { get; private set; }

    public IEnumerable<string> SupportedWords => GetSupportedWords();

    public void Execute(string word)
    {
        if (!_wordToIndex.TryGetValue(word, out var index))
        {
            MainFile.Logger.Warn($"EventCommand: word '{word}' not found");
            return;
        }

        var eventRoom = NEventRoom.Instance;
        if (eventRoom == null)
        {
            MainFile.Logger.Warn("EventCommand: NEventRoom.Instance is null");
            return;
        }

        var layout = eventRoom.Layout;
        if (layout == null)
        {
            MainFile.Logger.Warn("EventCommand: Layout is null");
            return;
        }

        var buttons = layout.OptionButtons.ToList();
        if (index < 0 || index >= buttons.Count)
        {
            MainFile.Logger.Warn($"EventCommand: invalid index {index}, count={buttons.Count}");
            return;
        }

        var button = buttons[index];
        if (!Godot.GodotObject.IsInstanceValid(button) || !button.IsInsideTree())
        {
            MainFile.Logger.Warn("EventCommand: button is not valid");
            return;
        }

        if (!button.IsEnabled)
        {
            MainFile.Logger.Warn($"EventCommand: button is not enabled, index={index}");
            return;
        }

        button.ForceClick();
        MainFile.Logger.Info($"EventCommand: '{word}' -> index={index}");
    }

    public event Action<IVoiceCommand>? VocabularyChanged;

    private IEnumerable<string> GetSupportedWords()
    {
        _wordToIndex.Clear();

        var eventRoom = NEventRoom.Instance;
        if (eventRoom == null) return [];

        var layout = eventRoom.Layout;
        if (layout == null) return [];

        var buttons = layout.OptionButtons
            .Where(b => Godot.GodotObject.IsInstanceValid(b) && b.IsInsideTree() && b.IsEnabled)
            .ToList();

        if (buttons.Count == 0) return [];

        MainFile.Logger.Info($"EventCommand.GetSupportedWords: {buttons.Count} enabled buttons");

        for (var i = 0; i < buttons.Count && i < 9; i++)
        {
            var oneBased = i + 1;
            // 中文: 第一个选项, 第二个选项...
            _wordToIndex[$"第{ChineseNumbers[oneBased]}个选项"] = i;
            _wordToIndex[$"第{ChineseNumbers[oneBased]}个"] = i;
            // 数字: 第1个选项, 第2个选项...
            _wordToIndex[$"第{oneBased}个选项"] = i;
            _wordToIndex[$"第{oneBased}个"] = i;
        }

        // "继续" 作为第一个选项的别名
        _wordToIndex["继续"] = 0;

        return _wordToIndex.Keys;
    }

    public static void RefreshVocabulary()
    {
        var instance = Instance;
        if (instance == null) return;

        var currentWords = new HashSet<string>(instance.GetSupportedWords(), StringComparer.Ordinal);
        if (!currentWords.SetEquals(instance._lastWords))
        {
            instance._lastWords = currentWords;
            instance.VocabularyChanged?.Invoke(instance);
        }
    }
}
