using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using VoiceToPlay.Voice;
using VoiceToPlay.Voice.Core;

namespace VoiceToPlay.Commands.CharacterSelect;

/// <summary>
///     角色选择命令。支持角色名字、切换角色、调整进阶等级。
/// </summary>
public sealed class CharacterSelectCommand : IVoiceCommand
{
    private readonly Dictionary<string, NCharacterSelectButton> _wordToButton = new();

    /// <summary>
    ///     缓存的词表，SupportedWords getter 直接返回此缓存
    /// </summary>
    private HashSet<string> _cachedWords = new(StringComparer.Ordinal);

    public CharacterSelectCommand()
    {
        Instance = this;
    }

    public static CharacterSelectCommand? Instance { get; private set; }

    /// <summary>
    ///     只返回缓存，不做任何计算
    /// </summary>
    public IEnumerable<string> SupportedWords => _cachedWords;

    public CommandResult Execute(string word)
    {
        // 处理进阶命令
        if (word == "高进阶") return IncrementAscension();

        if (word == "低进阶") return DecrementAscension();

        // 处理角色选择
        if (_wordToButton.TryGetValue(word, out var button))
        {
            if (!GodotObject.IsInstanceValid(button))
            {
                MainFile.Logger.Warn($"CharacterSelectCommand: button for '{word}' is disposed");
                return CommandResult.Failed;
            }

            if (button.IsLocked)
            {
                MainFile.Logger.Info($"CharacterSelectCommand: character '{word}' is locked");
                return CommandResult.Failed;
            }

            button.Select();
            MainFile.Logger.Info($"CharacterSelectCommand: selected character '{word}'");
            return CommandResult.Success;
        }

        MainFile.Logger.Warn($"CharacterSelectCommand: word '{word}' not found");
        return CommandResult.Failed;
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
        _wordToButton.Clear();

        var charSelectScreen = GetCharacterSelectScreen();
        if (charSelectScreen == null)
        {
            MainFile.Logger.Warn("CharacterSelectCommand: GetCharacterSelectScreen returned null");
            return [];
        }

        MainFile.Logger.Info("CharacterSelectCommand: got CharacterSelectScreen");

        var buttonContainer = charSelectScreen.GetNodeOrNull<Control>("CharSelectButtons/ButtonContainer");
        if (buttonContainer == null)
        {
            MainFile.Logger.Warn("CharacterSelectCommand: buttonContainer not found");
            return [];
        }

        MainFile.Logger.Info(
            $"CharacterSelectCommand: buttonContainer found, children count: {buttonContainer.GetChildCount()}");

        // 角色按钮
        foreach (var child in buttonContainer.GetChildren())
        {
            if (child is not NCharacterSelectButton button || !GodotObject.IsInstanceValid(button))
            {
                MainFile.Logger.Debug("CharacterSelectCommand: button is not NCharacterButton or is not valid");
                continue;
            }

            // 跳过可见性检查，因为角色可能分页/未渲染
            // if (!button.IsVisibleInTree()) continue;

            var character = button.Character;

            // 角色名字
            var rawTitle = character.Title.GetFormattedText();
            MainFile.Logger.Debug($"CharacterSelectCommand: raw title '{rawTitle}'");

            var characterName = VoiceText.Normalize(rawTitle);
            if (string.IsNullOrEmpty(characterName))
            {
                MainFile.Logger.Warn($"CharacterSelectCommand: normalized name is empty for '{rawTitle}'");
                continue;
            }

            _wordToButton[characterName] = button;
            MainFile.Logger.Info($"CharacterSelectCommand: added character '{characterName}'");
        }

        // 进阶命令
        _wordToButton["高进阶"] = null!; // 占位，Execute 会特殊处理
        _wordToButton["低进阶"] = null!;

        return new HashSet<string>(_wordToButton.Keys, StringComparer.Ordinal);
    }

    private static NCharacterSelectScreen? GetCharacterSelectScreen()
    {
        var mainMenu = NGame.Instance?.MainMenu;
        if (mainMenu == null) return null;

        return mainMenu.SubmenuStack?.Peek() as NCharacterSelectScreen;
    }

    private static CommandResult IncrementAscension()
    {
        var screen = GetCharacterSelectScreen();
        if (screen == null)
        {
            MainFile.Logger.Warn("CharacterSelectCommand: CharacterSelectScreen not found");
            return CommandResult.Failed;
        }

        var ascensionPanel = screen.GetNodeOrNull<NAscensionPanel>("%AscensionPanel");
        if (ascensionPanel == null || !ascensionPanel.Visible)
        {
            MainFile.Logger.Warn("CharacterSelectCommand: AscensionPanel not found or not visible");
            return CommandResult.Failed;
        }

        ascensionPanel.GetNodeOrNull<NButton>("HBoxContainer/RightArrowContainer/RightArrow")?.ForceClick();
        MainFile.Logger.Info("CharacterSelectCommand: incremented ascension");
        return CommandResult.Success;
    }

    private static CommandResult DecrementAscension()
    {
        var screen = GetCharacterSelectScreen();
        if (screen == null)
        {
            MainFile.Logger.Warn("CharacterSelectCommand: CharacterSelectScreen not found");
            return CommandResult.Failed;
        }

        var ascensionPanel = screen.GetNodeOrNull<NAscensionPanel>("%AscensionPanel");
        if (ascensionPanel == null || !ascensionPanel.Visible)
        {
            MainFile.Logger.Warn("CharacterSelectCommand: AscensionPanel not found or not visible");
            return CommandResult.Failed;
        }

        ascensionPanel.GetNodeOrNull<NButton>("HBoxContainer/LeftArrowContainer/LeftArrow")?.ForceClick();
        MainFile.Logger.Info("CharacterSelectCommand: decremented ascension");
        return CommandResult.Success;
    }
}