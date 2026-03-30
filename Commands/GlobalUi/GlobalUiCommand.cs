using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using VoiceToPlay.Voice;
using VoiceToPlay.Voice.Core;

namespace VoiceToPlay.Commands.GlobalUi;

/// <summary>
///     全局 UI 命令。处理地图、牌组、暂停等。
/// </summary>
public sealed class GlobalUiCommand : IVoiceCommand
{
    private readonly Dictionary<string, Action> _wordToAction = new();

    /// <summary>
    ///     缓存的词表，SupportedWords getter 直接返回此缓存
    /// </summary>
    private HashSet<string> _cachedWords = new(StringComparer.Ordinal);

    public GlobalUiCommand()
    {
        Instance = this;
    }

    public static GlobalUiCommand? Instance { get; private set; }

    /// <summary>
    ///     只返回缓存，不做任何计算
    /// </summary>
    public IEnumerable<string> SupportedWords => _cachedWords;

    public CommandResult Execute(string word)
    {
        if (!_wordToAction.TryGetValue(word, out var action))
        {
            MainFile.Logger.Warn($"GlobalUiCommand: word '{word}' not found");
            return CommandResult.Failed;
        }

        try
        {
            action.Invoke();
            MainFile.Logger.Info($"GlobalUiCommand: executed '{word}'");
            return CommandResult.Success;
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"GlobalUiCommand: failed to execute '{word}': {e.Message}");
            return CommandResult.Failed;
        }
    }

    public event Action<IVoiceCommand>? VocabularyChanged;

    /// <summary>
    ///     计算当前支持的词表（只在 RefreshVocabulary 中调用）
    /// </summary>
    private HashSet<string> ComputeSupportedWords()
    {
        _wordToAction.Clear();

        var run = NRun.Instance;
        if (run == null || !GodotObject.IsInstanceValid(run)) return [];

        var topBar = run.GlobalUi?.TopBar;
        if (topBar == null) return [];

        // 地图按钮
        var mapButton = topBar.Map;
        if (GodotObject.IsInstanceValid(mapButton) && mapButton.IsEnabled)
        {
            var word = "地图";
            _wordToAction[word] = () => { NMapScreen.Instance?.Open(true); };
        }

        // 牌组按钮
        var deckButton = topBar.Deck;
        if (GodotObject.IsInstanceValid(deckButton) && deckButton.IsEnabled)
        {
            var word = "牌组";
            _wordToAction[word] = () => { deckButton.ForceClick(); };
        }

        // 暂停按钮
        var pauseButton = topBar.Pause;
        if (GodotObject.IsInstanceValid(pauseButton) && pauseButton.IsEnabled)
        {
            var word = "暂停";
            _wordToAction[word] = () => { pauseButton.ForceClick(); };
        }

        return new HashSet<string>(_wordToAction.Keys, StringComparer.Ordinal);
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