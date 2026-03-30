using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens;
using VoiceToPlay.Voice;
using VoiceToPlay.Voice.Core;

namespace VoiceToPlay.Commands.DeckView;

/// <summary>
///     卡牌详情界面命令。处理查看升级、左右翻页、关闭等。
/// </summary>
public sealed class InspectCardCommand : IVoiceCommand
{
    private readonly Dictionary<string, Action> _wordToAction = new(StringComparer.Ordinal);
    private HashSet<string> _cachedWords = new(StringComparer.Ordinal);

    public InspectCardCommand()
    {
        Instance = this;
    }

    public static InspectCardCommand? Instance { get; private set; }

    /// <summary>
    ///     检查卡牌详情界面是否打开
    /// </summary>
    public static bool IsOpen => GetInspectScreen()?.Visible == true;

    public IEnumerable<string> SupportedWords => _cachedWords;

    public CommandResult Execute(string word)
    {
        if (!_wordToAction.TryGetValue(word, out var action))
        {
            MainFile.Logger.Warn($"InspectCardCommand: word '{word}' not found");
            return CommandResult.Failed;
        }

        try
        {
            action.Invoke();
            MainFile.Logger.Info($"InspectCardCommand: executed '{word}'");
            return CommandResult.Success;
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"InspectCardCommand: failed to execute '{word}': {e.Message}");
            return CommandResult.Failed;
        }
    }

    public event Action<IVoiceCommand>? VocabularyChanged;

    private static NInspectCardScreen? GetInspectScreen()
    {
        return NGame.Instance?.GetInspectCardScreen();
    }

    /// <summary>
    ///     计算当前支持的词表
    /// </summary>
    private HashSet<string> ComputeSupportedWords()
    {
        _wordToAction.Clear();

        var screen = GetInspectScreen();
        if (screen == null || !GodotObject.IsInstanceValid(screen) || !screen.Visible) return [];

        // "查看升级" - 切换升级预览
        var upgradeTickbox = screen.GetNode<NTickbox>("%Upgrade");
        if (upgradeTickbox != null && GodotObject.IsInstanceValid(upgradeTickbox) && upgradeTickbox.Visible)
        {
            _wordToAction["查看升级"] = () =>
            {
                if (upgradeTickbox.IsEnabled)
                    upgradeTickbox.ForceClick();
            };
        }

        // "上一张" / "下一张" - 翻页
        var leftButton = screen.GetNode<NButton>("LeftArrow");
        var rightButton = screen.GetNode<NButton>("RightArrow");

        if (leftButton != null && GodotObject.IsInstanceValid(leftButton) && leftButton.Visible)
        {
            _wordToAction["上一张"] = () =>
            {
                if (leftButton.IsEnabled)
                    leftButton.ForceClick();
            };
        }

        if (rightButton != null && GodotObject.IsInstanceValid(rightButton) && rightButton.Visible)
        {
            _wordToAction["下一张"] = () =>
            {
                if (rightButton.IsEnabled)
                    rightButton.ForceClick();
            };
        }

        // "关闭" - 关闭详情界面
        _wordToAction["关闭"] = () => screen.Close();

        return new HashSet<string>(_wordToAction.Keys, StringComparer.Ordinal);
    }

    /// <summary>
    ///     刷新词表缓存
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
}
