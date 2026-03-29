using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using VoiceToPlay.Voice.Core;

namespace VoiceToPlay.Commands.Ui;

/// <summary>
///     返回按钮命令。通用的"返回"支持。
///     按钮实例由 Patch 直接注入，无需搜索场景树。
/// </summary>
public sealed class BackCommand : IVoiceCommand
{
    private static NBackButton? _activeButton;

    private readonly HashSet<string> _words = new(StringComparer.Ordinal) { "返回" };
    private HashSet<string> _lastWords = new(StringComparer.Ordinal);

    public BackCommand()
    {
        Instance = this;
    }

    public static BackCommand? Instance { get; private set; }

    public IEnumerable<string> SupportedWords => HasActiveButton() ? _words : [];

    public void Execute(string word)
    {
        var button = _activeButton;
        if (button == null || !IsButtonValid(button))
        {
            MainFile.Logger.Warn("BackCommand: no active back button");
            return;
        }

        button.ForceClick();
        MainFile.Logger.Info("BackCommand: '返回' clicked");
    }

    public event Action<IVoiceCommand>? VocabularyChanged;

    /// <summary>
    ///     注册按钮实例（由 Patch 调用）
    /// </summary>
    public static void RegisterButton(NBackButton button)
    {
        _activeButton = button;
        MainFile.Logger.Info("BackCommand: registered button");
        RefreshVocabulary();
    }

    /// <summary>
    ///     注销按钮实例（由 Patch 调用）
    /// </summary>
    public static void UnregisterButton()
    {
        _activeButton = null;
        MainFile.Logger.Info("BackCommand: unregistered button");
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

    private static bool IsButtonValid(NBackButton? button)
    {
        return button != null &&
               GodotObject.IsInstanceValid(button) &&
               button.IsInsideTree() &&
               button.IsEnabled;
    }
}
