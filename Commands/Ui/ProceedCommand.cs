using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using VoiceToPlay.Voice.Core;

namespace VoiceToPlay.Commands.Ui;

/// <summary>
///     前进按钮命令。通用的"前进"支持，适用于奖励、任务、商人等场景。
///     按钮实例由 Patch 直接注入，无需搜索场景树。
/// </summary>
public sealed class ProceedCommand : IVoiceCommand
{
    private static NProceedButton? _activeButton;

    private readonly HashSet<string> _words = new(StringComparer.Ordinal) { "前进" };
    private HashSet<string> _lastWords = new(StringComparer.Ordinal);

    public ProceedCommand()
    {
        Instance = this;
    }

    public static ProceedCommand? Instance { get; private set; }

    public IEnumerable<string> SupportedWords => HasActiveButton() ? _words : [];

    public void Execute(string word)
    {
        var button = _activeButton;
        if (button == null || !IsButtonValid(button))
        {
            MainFile.Logger.Warn("ProceedCommand: no active proceed button");
            return;
        }

        button.ForceClick();
        MainFile.Logger.Info("ProceedCommand: '前进' clicked");
    }

    public event Action<IVoiceCommand>? VocabularyChanged;

    /// <summary>
    ///     注册按钮实例（由 Patch 调用）
    /// </summary>
    public static void RegisterButton(NProceedButton button)
    {
        _activeButton = button;
        MainFile.Logger.Info("ProceedCommand: registered button");
        RefreshVocabulary();
    }

    /// <summary>
    ///     注销按钮实例（由 Patch 调用）
    /// </summary>
    public static void UnregisterButton()
    {
        _activeButton = null;
        MainFile.Logger.Info("ProceedCommand: unregistered button");
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

    private static bool IsButtonValid(NProceedButton? button)
    {
        return button != null &&
               GodotObject.IsInstanceValid(button) &&
               button.IsInsideTree() &&
               button.IsEnabled;
    }
}