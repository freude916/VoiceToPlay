using Godot;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using VoiceToPlay.Voice;
using VoiceToPlay.Voice.Core;

namespace VoiceToPlay.Commands.RestSite;

/// <summary>
///     火堆选项命令。支持按选项名字选择，如 "休息"、"锻造"、"挖掘" 等。
/// </summary>
public sealed class RestSiteCommand : IVoiceCommand
{
    private readonly Dictionary<string, NRestSiteButton> _wordToButton = new();

    /// <summary>
    ///     缓存的词表，SupportedWords getter 直接返回此缓存
    /// </summary>
    private HashSet<string> _cachedWords = new(StringComparer.Ordinal);

    public RestSiteCommand()
    {
        Instance = this;
    }

    public static RestSiteCommand? Instance { get; private set; }

    /// <summary>
    ///     只返回缓存，不做任何计算
    /// </summary>
    public IEnumerable<string> SupportedWords => _cachedWords;

    public CommandResult Execute(string word)
    {
        if (!_wordToButton.TryGetValue(word, out var button))
        {
            MainFile.Logger.Warn($"RestSiteCommand: word '{word}' not found");
            return CommandResult.Failed;
        }

        if (!GodotObject.IsInstanceValid(button) || !button.IsInsideTree())
        {
            MainFile.Logger.Warn("RestSiteCommand: button is invalid");
            return CommandResult.Failed;
        }

        button.ForceClick();
        MainFile.Logger.Debug($"RestSiteCommand: '{word}' -> clicked");
        return CommandResult.Success;
    }

    public event Action<IVoiceCommand>? VocabularyChanged;

    /// <summary>
    ///     计算当前支持的词表（只在 RefreshVocabulary 中调用）
    /// </summary>
    private HashSet<string> ComputeSupportedWords()
    {
        _wordToButton.Clear();

        var restSiteRoom = NRestSiteRoom.Instance;
        if (restSiteRoom == null) return [];

        var choicesContainer = restSiteRoom.GetNodeOrNull<Control>("%ChoicesContainer");
        if (choicesContainer == null) return [];

        foreach (var child in choicesContainer.GetChildren())
        {
            if (child is not NRestSiteButton button) continue;
            if (!GodotObject.IsInstanceValid(button) || !button.IsInsideTree() || !button.IsEnabled)
                continue;

            var option = button.Option;
            if (option == null) continue;

            var title = option.Title?.GetFormattedText();
            if (string.IsNullOrEmpty(title)) continue;

            var normalizedTitle = VoiceText.Normalize(title);
            if (!string.IsNullOrEmpty(normalizedTitle))
                _wordToButton[normalizedTitle] = button;
        }

        return new HashSet<string>(_wordToButton.Keys, StringComparer.Ordinal);
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

        instance._wordToButton.Clear();
        if (instance._cachedWords.Count > 0)
        {
            instance._cachedWords = new HashSet<string>(StringComparer.Ordinal);
            instance.VocabularyChanged?.Invoke(instance);
        }
    }
}