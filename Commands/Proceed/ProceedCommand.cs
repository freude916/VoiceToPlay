using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using VoiceToPlay.Voice.Core;

namespace VoiceToPlay.Commands.Proceed;

/// <summary>
///     前进按钮命令。通用的"前进"支持，适用于奖励、任务、商人等场景。
/// </summary>
public sealed class ProceedCommand : IVoiceCommand
{
    private readonly HashSet<string> _words = new(StringComparer.Ordinal) { "前进" };
    private HashSet<string> _lastWords = new(StringComparer.Ordinal);

    public ProceedCommand()
    {
        Instance = this;
    }

    public static ProceedCommand? Instance { get; private set; }

    public IEnumerable<string> SupportedWords
    {
        get
        {
            var button = FindEnabledProceedButton();
            return button != null ? _words : [];
        }
    }

    public void Execute(string word)
    {
        var button = FindEnabledProceedButton();
        if (button == null)
        {
            MainFile.Logger.Warn("ProceedCommand: no enabled proceed button");
            return;
        }

        button.ForceClick();
        MainFile.Logger.Info("ProceedCommand: '前进' clicked");
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

    private static NProceedButton? FindEnabledProceedButton()
    {
        // 深度搜索场景树找到启用的 ProceedButton
        var root = GodotObject.IsInstanceValid(NRun.Instance) ? NRun.Instance as Node : null;
        if (root == null) return null;

        var stack = new Stack<Node>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is NProceedButton button &&
                GodotObject.IsInstanceValid(button) &&
                button.IsInsideTree() &&
                button.IsEnabled)
            {
                return button;
            }

            foreach (var child in node.GetChildren())
                if (child is Node childNode)
                    stack.Push(childNode);
        }

        return null;
    }
}
