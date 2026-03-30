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
    private readonly Dictionary<string, NButton> _wordToButton = new();

    /// <summary>
    ///     缓存的词表，SupportedWords getter 直接返回此缓存
    /// </summary>
    private HashSet<string> _cachedWords = new(StringComparer.Ordinal);

    public MainMenuCommand()
    {
        Instance = this;
    }

    /// <summary>
    ///     静态实例引用，供 Patch 访问
    /// </summary>
    public static MainMenuCommand? Instance { get; private set; }

    /// <summary>
    ///     只返回缓存，不做任何计算
    /// </summary>
    public IEnumerable<string> SupportedWords => _cachedWords;

    public CommandResult Execute(string word)
    {
        if (!_wordToButton.TryGetValue(word, out var button))
        {
            MainFile.Logger.Warn($"MainMenuCommand: word '{word}' not found in cache");
            return CommandResult.Failed;
        }

        // 检查按钮是否还有效（主菜单可能已销毁）
        if (!GodotObject.IsInstanceValid(button))
        {
            MainFile.Logger.Warn($"MainMenuCommand: button for '{word}' is disposed, clearing cache");
            _wordToButton.Clear();
            return CommandResult.Failed;
        }

        MainFile.Logger.Debug($"MainMenuCommand: clicking '{word}'");
        button.ForceClick();
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
            MainFile.Logger.Debug("MainMenuCommand: vocabulary changed, notifying...");
            instance.VocabularyChanged?.Invoke(instance);
        }
    }

    /// <summary>
    ///     计算当前支持的词表（只在 RefreshVocabulary 中调用）
    /// </summary>
    private HashSet<string> ComputeSupportedWords()
    {
        _wordToButton.Clear();
        var mainMenu = NGame.Instance?.MainMenu;
        if (mainMenu == null) return []; // 不在主菜单，静默返回空

        var buttonInfos = new List<string>();

        // 1. 主菜单按钮
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
                buttonInfos.Add($"'{normalized}'->{button.Name}");
            }
        }

        // 2. 子菜单按钮（如单人模式下的"标准模式"、"每日挑战"等）
        var submenu = mainMenu.SubmenuStack?.Peek();
        if (submenu != null)
            foreach (var child in submenu.GetChildren())
            {
                if (child is not NSubmenuButton submenuButton) continue;
                if (!GodotObject.IsInstanceValid(submenuButton) || !submenuButton.IsEnabled ||
                    !submenuButton.IsVisibleInTree())
                    continue;

                var text = GetSubmenuButtonText(submenuButton);
                if (string.IsNullOrEmpty(text)) continue;

                var normalized = VoiceText.Normalize(text);
                if (!string.IsNullOrEmpty(normalized))
                {
                    _wordToButton[normalized] = submenuButton;
                    buttonInfos.Add($"'{normalized}'->submenu:{submenuButton.Name}");
                }
            }

        if (buttonInfos.Count > 0)
            MainFile.Logger.Debug($"MainMenuCommand: buttons=[{string.Join(", ", buttonInfos)}]");

        return new HashSet<string>(_wordToButton.Keys, StringComparer.Ordinal);
    }

    private static string? GetButtonText(NButton button)
    {
        // NMainMenuTextButton 有 label 字段
        if (button is NMainMenuTextButton mainMenuButton) return mainMenuButton.label?.Text;

        // 尝试从子节点找 Label
        var label = button.GetNodeOrNull<Label>("./");
        return label?.Text;
    }

    private static string? GetSubmenuButtonText(NSubmenuButton button)
    {
        // NSubmenuButton 有 _title 字段（MegaLabel）
        var titleLabel = button.GetNodeOrNull<Label>("%Title");
        return titleLabel?.Text;
    }
}