using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using VoiceToPlay.Voice.Core;

namespace VoiceToPlay.Commands.Ui;

/// <summary>
///     返回按钮命令。通用的"返回"支持。
///     按钮实例由 Patch 直接注入，无需搜索场景树。
///     支持多个按钮同时存在，点击最后一个有效按钮。
/// </summary>
public sealed class BackCommand : IVoiceCommand
{
    private static readonly List<NBackButton> ActiveButtons = [];

    private readonly HashSet<string> _words = new(StringComparer.Ordinal) { "返回" };
    private HashSet<string> _lastWords = new(StringComparer.Ordinal);

    public BackCommand()
    {
        Instance = this;
    }

    public static BackCommand? Instance { get; private set; }

    public IEnumerable<string> SupportedWords => HasActiveButton() ? _words : [];

    public CommandResult Execute(string word)
    {
        var button = GetValidButton();
        if (button == null)
        {
            MainFile.Logger.Warn("BackCommand: no active back button");
            return CommandResult.Failed;
        }

        button.ForceClick();
        MainFile.Logger.Info("BackCommand: '返回' clicked");
        return CommandResult.Success;
    }

    public event Action<IVoiceCommand>? VocabularyChanged;

    /// <summary>
    ///     注册按钮实例（由 Patch 调用）
    /// </summary>
    public static void RegisterButton(NBackButton button)
    {
        if (ActiveButtons.Contains(button)) return;
        ActiveButtons.Add(button);
        MainFile.Logger.Debug($"BackCommand: registered button, total: {ActiveButtons.Count}");
        RefreshVocabulary();
    }

    /// <summary>
    ///     注销按钮实例（由 Patch 调用）
    /// </summary>
    public static void UnregisterButton(NBackButton button)
    {
        if (ActiveButtons.Remove(button))
        {
            MainFile.Logger.Debug($"BackCommand: unregistered button, remaining: {ActiveButtons.Count}");
            RefreshVocabulary();
        }
    }

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

    private static bool HasActiveButton()
    {
        return GetValidButton() != null;
    }

    private static NBackButton? GetValidButton()
    {
        // 从后往前找最后一个有效按钮
        for (var i = ActiveButtons.Count - 1; i >= 0; i--)
        {
            var button = ActiveButtons[i];
            if (IsButtonValid(button))
                return button;
        }

        return null;
    }

    private static bool IsButtonValid(NBackButton? button)
    {
        return button != null &&
               GodotObject.IsInstanceValid(button) &&
               button.IsInsideTree() &&
               button.IsEnabled;
    }
}