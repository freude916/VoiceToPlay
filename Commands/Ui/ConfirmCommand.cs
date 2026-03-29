using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using VoiceToPlay.Voice.Core;

namespace VoiceToPlay.Commands.Ui;

/// <summary>
///     确认按钮命令。通用的"确定/确认"支持。
///     按钮实例由 Patch 直接注入，无需搜索场景树。
/// </summary>
public sealed class ConfirmCommand : IVoiceCommand
{
    private static NConfirmButton? _activeButton;

    private readonly HashSet<string> _words = new(StringComparer.Ordinal) { "确定", "确认" };
    private HashSet<string> _lastWords = new(StringComparer.Ordinal);

    public ConfirmCommand()
    {
        Instance = this;
    }

    public static ConfirmCommand? Instance { get; private set; }

    public IEnumerable<string> SupportedWords => HasActiveButton() ? _words : [];

    public void Execute(string word)
    {
        var button = _activeButton;
        if (button == null || !IsButtonValid(button))
        {
            MainFile.Logger.Warn("ConfirmCommand: no active confirm button");
            return;
        }

        button.ForceClick();
        MainFile.Logger.Info($"ConfirmCommand: '{word}' clicked");
    }

    public event Action<IVoiceCommand>? VocabularyChanged;

    /// <summary>
    ///     注册按钮实例（由 Patch 调用）
    /// </summary>
    public static void RegisterButton(NConfirmButton button)
    {
        _activeButton = button;
        MainFile.Logger.Info($"ConfirmCommand: registered button");
        RefreshVocabulary();
    }

    /// <summary>
    ///     注销按钮实例（由 Patch 调用）
    /// </summary>
    public static void UnregisterButton()
    {
        _activeButton = null;
        MainFile.Logger.Info("ConfirmCommand: unregistered button");
        RefreshVocabulary();
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
        return IsButtonValid(_activeButton);
    }

    private static bool IsButtonValid(NConfirmButton? button)
    {
        return button != null &&
               GodotObject.IsInstanceValid(button) &&
               button.IsInsideTree() &&
               button.IsEnabled;
    }
}