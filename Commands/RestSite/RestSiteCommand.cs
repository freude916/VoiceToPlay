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
    private HashSet<string> _lastWords = new(StringComparer.Ordinal);

    public RestSiteCommand()
    {
        Instance = this;
    }

    public static RestSiteCommand? Instance { get; private set; }

    public IEnumerable<string> SupportedWords
    {
        get
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

            return _wordToButton.Keys;
        }
    }

    public void Execute(string word)
    {
        if (!_wordToButton.TryGetValue(word, out var button))
        {
            MainFile.Logger.Warn($"RestSiteCommand: word '{word}' not found");
            return;
        }

        if (!GodotObject.IsInstanceValid(button) || !button.IsInsideTree())
        {
            MainFile.Logger.Warn("RestSiteCommand: button is invalid");
            return;
        }

        button.ForceClick();
        MainFile.Logger.Info($"RestSiteCommand: '{word}' -> clicked");
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

        instance._wordToButton.Clear();
        if (instance._lastWords.Count > 0)
        {
            instance._lastWords = new HashSet<string>(StringComparer.Ordinal);
            instance.VocabularyChanged?.Invoke(instance);
        }
    }
}
