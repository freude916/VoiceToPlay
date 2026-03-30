using Godot;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using VoiceToPlay.Util;
using VoiceToPlay.Voice.Core;

namespace VoiceToPlay.Commands.Events;

/// <summary>
///     事件选项命令。支持 "第一个选项", "第二个选项", "继续" 等。
/// </summary>
public sealed class EventsCommand : IVoiceCommand
{
    private readonly Dictionary<string, int> _wordToIndex = new();

    /// <summary>
    ///     缓存的词表，SupportedWords getter 直接返回此缓存
    /// </summary>
    private HashSet<string> _cachedWords = new(StringComparer.Ordinal);

    public EventsCommand()
    {
        Instance = this;
    }

    public static EventsCommand? Instance { get; private set; }

    /// <summary>
    ///     只返回缓存，不做任何计算
    /// </summary>
    public IEnumerable<string> SupportedWords => _cachedWords;

    public CommandResult Execute(string word)
    {
        if (!_wordToIndex.TryGetValue(word, out var index))
        {
            MainFile.Logger.Warn($"EventCommand: word '{word}' not found");
            return CommandResult.Failed;
        }

        var eventRoom = NEventRoom.Instance;
        if (eventRoom == null)
        {
            MainFile.Logger.Warn("EventCommand: NEventRoom.Instance is null");
            return CommandResult.Failed;
        }

        var layout = eventRoom.Layout;
        if (layout == null)
        {
            MainFile.Logger.Warn("EventCommand: Layout is null");
            return CommandResult.Failed;
        }

        var buttons = layout.OptionButtons.ToList();
        if (index < 0 || index >= buttons.Count)
        {
            MainFile.Logger.Warn($"EventCommand: invalid index {index}, count={buttons.Count}");
            return CommandResult.Failed;
        }

        var button = buttons[index];
        if (!GodotObject.IsInstanceValid(button) || !button.IsInsideTree())
        {
            MainFile.Logger.Warn("EventCommand: button is not valid");
            return CommandResult.Failed;
        }

        if (!button.IsEnabled)
        {
            MainFile.Logger.Warn($"EventCommand: button is not enabled, index={index}");
            return CommandResult.Failed;
        }

        button.ForceClick();
        MainFile.Logger.Debug($"EventCommand: '{word}' -> index={index}");
        return CommandResult.Success;
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
        _wordToIndex.Clear();

        var eventRoom = NEventRoom.Instance;
        if (eventRoom == null) return [];

        var layout = eventRoom.Layout;
        if (layout == null) return [];

        var buttons = layout.OptionButtons
            .Where(b => GodotObject.IsInstanceValid(b) && b.IsInsideTree() && b.IsEnabled)
            .ToList();

        if (buttons.Count == 0) return [];

        MainFile.Logger.Debug($"EventCommand.ComputeSupportedWords: {buttons.Count} enabled buttons");

        for (var i = 0; i < buttons.Count && i < 9; i++)
        {
            var oneBased = i + 1;
            // 中文: 第一个选项, 第二个选项...
            _wordToIndex[L10n.EventOptionOrdinal(oneBased)] = i;
            _wordToIndex[L10n.EnemyOrdinal(oneBased)] = i;
            // 数字: 第1个选项, 第2个选项...
            _wordToIndex[$"第{oneBased}个选项"] = i;
            _wordToIndex[$"第{oneBased}个"] = i;
        }

        // "继续" 作为第一个选项的别名
        _wordToIndex["继续"] = 0;

        return new HashSet<string>(_wordToIndex.Keys, StringComparer.Ordinal);
    }
}