using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using VoiceToPlay.Voice;
using VoiceToPlay.Voice.Core;

namespace VoiceToPlay.Commands.MainMenu;

/// <summary>
///     主菜单按钮命令。动态读取按钮文本作为词表。
/// </summary>
public sealed class MainMenuCommand : IVoiceCommand
{
    private HashSet<string> _lastWords = new();
    private readonly Dictionary<string, NButton> _wordToButton = new();

    public MainMenuCommand()
    {
        Instance = this;
    }

    /// <summary>
    ///     静态实例引用，供 Patch 访问
    /// </summary>
    public static MainMenuCommand? Instance { get; private set; }

    public IEnumerable<string> SupportedWords
    {
        get
        {
            _wordToButton.Clear();
            var mainMenu = NGame.Instance?.MainMenu;
            if (mainMenu == null)
            {
                MainFile.Logger.Warn("MainMenuCommand: MainMenu is null");
                return [];
            }

            var count = 0;
            foreach (var button in mainMenu.MainMenuButtons)
            {
                if (button == null || !button.IsEnabled || !button.IsVisibleInTree())
                    continue;

                var text = GetButtonText(button);
                if (string.IsNullOrEmpty(text)) continue;

                var normalized = VoiceText.Normalize(text);
                if (!string.IsNullOrEmpty(normalized))
                {
                    _wordToButton[normalized] = button;
                    count++;
                    MainFile.Logger.Info($"MainMenuCommand: word='{normalized}' button={button.Name}");
                }
            }

            MainFile.Logger.Info($"MainMenuCommand: total {count} words");
            return _wordToButton.Keys;
        }
    }

    public void Execute(string word)
    {
        if (!_wordToButton.TryGetValue(word, out var button))
        {
            MainFile.Logger.Warn($"MainMenuCommand: word '{word}' not found in cache");
            return;
        }

        // 检查按钮是否还有效（主菜单可能已销毁）
        if (!GodotObject.IsInstanceValid(button))
        {
            MainFile.Logger.Warn($"MainMenuCommand: button for '{word}' is disposed, clearing cache");
            _wordToButton.Clear();
            return;
        }

        MainFile.Logger.Info($"MainMenuCommand: clicking '{word}'");
        button.ForceClick();
    }

    public event Action<IVoiceCommand>? VocabularyChanged;

    /// <summary>
    ///     刷新词表并通知上层（由 Patch 调用）
    /// </summary>
    public static void RefreshVocabulary()
    {
        var instance = Instance;
        if (instance == null) return;

        var currentWords = new HashSet<string>(instance.SupportedWords);
        if (!currentWords.SetEquals(instance._lastWords))
        {
            instance._lastWords = currentWords;
            MainFile.Logger.Info("MainMenuCommand: vocabulary changed, notifying...");
            instance.VocabularyChanged?.Invoke(instance);
        }
    }

    private static string? GetButtonText(NButton button)
    {
        // NMainMenuTextButton 有 label 字段
        if (button is NMainMenuTextButton mainMenuButton) return mainMenuButton.label?.Text;

        // 尝试从子节点找 Label
        var label = button.GetNodeOrNull<Label>("./");
        return label?.Text;
    }
}