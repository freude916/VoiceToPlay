using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using VoiceToPlay.Voice.Core;

namespace VoiceToPlay.Commands.Proceed;

/// <summary>
///     确认按钮命令。通用的"确定/确认"支持。
/// </summary>
public sealed class ConfirmCommand : IVoiceCommand
{
    private readonly HashSet<string> _words = new(StringComparer.Ordinal) { "确定", "确认" };
    private HashSet<string> _lastWords = new(StringComparer.Ordinal);

    public ConfirmCommand()
    {
        Instance = this;
    }

    public static ConfirmCommand? Instance { get; private set; }

    public IEnumerable<string> SupportedWords
    {
        get
        {
            var button = FindEnabledConfirmButton();
            return button != null ? _words : [];
        }
    }

    public void Execute(string word)
    {
        var button = FindEnabledConfirmButton();
        if (button == null)
        {
            MainFile.Logger.Warn("ConfirmCommand: no enabled confirm button");
            return;
        }

        button.ForceClick();
        MainFile.Logger.Info($"ConfirmCommand: '{word}' clicked");
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

    private static NConfirmButton? FindEnabledConfirmButton()
    {
        var root = GodotObject.IsInstanceValid(NRun.Instance) ? NRun.Instance as Node : null;
        if (root == null) return null;

        var stack = new Stack<Node>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is NConfirmButton button &&
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
