using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;
using MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic;
using VoiceToPlay.Voice;
using VoiceToPlay.Voice.Core;

namespace VoiceToPlay.Commands.Treasure;

/// <summary>
///     宝箱遗物选择命令。支持 "打开宝箱", "查看遗物", "下一个遗物", "选择遗物" 等。
/// </summary>
public sealed class TreasureCommand : IVoiceCommand
{
    private const string OpenChest = "打开宝箱";
    private const string PreviewRelic = "查看遗物";
    private const string NextRelic = "下一个遗物";
    private const string ClaimRelic = "选择遗物";

    private readonly HashSet<string> _chestClosedWords = new(StringComparer.Ordinal)
    {
        VoiceText.Normalize(OpenChest)
    };

    private readonly HashSet<string> _relicWords = new(StringComparer.Ordinal)
    {
        VoiceText.Normalize(PreviewRelic),
        VoiceText.Normalize(NextRelic),
        VoiceText.Normalize(ClaimRelic),
        "拿取"
    };

    private readonly Dictionary<string, string> _normalizedToRaw = new();
    private HashSet<string> _lastWords = new(StringComparer.Ordinal);

    public TreasureCommand()
    {
        Instance = this;
    }

    public static TreasureCommand? Instance { get; private set; }

    public IEnumerable<string> SupportedWords => GetSupportedWords();

    public void Execute(string word)
    {
        var normalizedOpenChest = VoiceText.Normalize(OpenChest);
        if (word == normalizedOpenChest)
        {
            TryOpenChest();
            return;
        }

        var holders = GetEnabledHolders();
        if (holders.Count == 0)
        {
            MainFile.Logger.Warn("TreasureCommand: no enabled holders");
            return;
        }

        var normalizedPreview = VoiceText.Normalize(PreviewRelic);
        var normalizedNext = VoiceText.Normalize(NextRelic);
        var normalizedClaim = VoiceText.Normalize(ClaimRelic);

        if (word == normalizedPreview)
        {
            PreviewCurrent(holders);
        }
        else if (word == normalizedNext)
        {
            PreviewNext(holders);
        }
        else if (word == normalizedClaim || word == "拿取")
        {
            ClaimCurrent(holders);
        }
        else
        {
            // 可能是遗物名称
            TryClaimByName(holders, word);
        }
    }

    public event Action<IVoiceCommand>? VocabularyChanged;

    private IEnumerable<string> GetSupportedWords()
    {
        _normalizedToRaw.Clear();

        var currentScreen = ActiveScreenContext.Instance?.GetCurrentScreen();
        if (currentScreen is not NTreasureRoom treasureRoom) return [];

        var holders = GetEnabledHolders();

        // 宝箱未打开：只显示"打开宝箱"
        if (holders.Count == 0)
        {
            var chestButton = FindChestButton(treasureRoom);
            if (chestButton != null && chestButton.IsEnabled)
            {
                foreach (var word in _chestClosedWords)
                    _normalizedToRaw[word] = word;
                MainFile.Logger.Info("TreasureCommand: chest not opened, showing '打开宝箱'");
            }
            return _normalizedToRaw.Keys;
        }

        MainFile.Logger.Info($"TreasureCommand.GetSupportedWords: {holders.Count} enabled holders");

        // 遗物选择模式
        foreach (var word in _relicWords)
            _normalizedToRaw[word] = word;

        // 遗物名称
        foreach (var holder in holders)
        {
            var relicModel = holder.Relic?.Model;
            if (relicModel == null) continue;

            var relicName = VoiceText.Normalize(relicModel.Title.GetFormattedText());
            if (relicName.Length > 0)
                _normalizedToRaw[relicName] = relicName;
        }

        return _normalizedToRaw.Keys;
    }

    public static void RefreshVocabulary()
    {
        var instance = Instance;
        if (instance == null) return;

        var currentWords = new HashSet<string>(instance.GetSupportedWords(), StringComparer.Ordinal);
        if (!currentWords.SetEquals(instance._lastWords))
        {
            instance._lastWords = currentWords;
            instance.VocabularyChanged?.Invoke(instance);
        }
    }

    private static NButton? FindChestButton(NTreasureRoom treasureRoom)
    {
        var stack = new Stack<Node>();
        stack.Push(treasureRoom);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is NButton button && button.Name == "Chest")
                return button;

            foreach (var child in node.GetChildren())
                if (child is Node childNode)
                    stack.Push(childNode);
        }
        return null;
    }

    private static void TryOpenChest()
    {
        var currentScreen = ActiveScreenContext.Instance?.GetCurrentScreen();
        if (currentScreen is not NTreasureRoom treasureRoom)
        {
            MainFile.Logger.Warn("TreasureCommand: not in treasure room");
            return;
        }

        var chestButton = FindChestButton(treasureRoom);
        if (chestButton == null)
        {
            MainFile.Logger.Warn("TreasureCommand: chest button not found");
            return;
        }

        if (!chestButton.IsEnabled)
        {
            MainFile.Logger.Warn("TreasureCommand: chest button not enabled");
            return;
        }

        chestButton.ForceClick();
        MainFile.Logger.Info("TreasureCommand: opened chest");
    }

    private static List<NTreasureRoomRelicHolder> GetEnabledHolders()
    {
        var result = new List<NTreasureRoomRelicHolder>();

        var currentScreen = ActiveScreenContext.Instance?.GetCurrentScreen();
        if (currentScreen is not NTreasureRoom treasureRoom) return result;

        // 深度优先搜索所有 NTreasureRoomRelicHolder
        var stack = new Stack<Node>();
        stack.Push(treasureRoom);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is NTreasureRoomRelicHolder holder &&
                GodotObject.IsInstanceValid(holder) &&
                holder.IsInsideTree() &&
                holder.Visible &&
                holder.IsEnabled)
            {
                result.Add(holder);
            }

            foreach (var child in node.GetChildren())
                if (child is Node childNode)
                    stack.Push(childNode);
        }

        result.Sort((a, b) => a.GlobalPosition.X.CompareTo(b.GlobalPosition.X));
        return result;
    }

    private static int ResolveCurrentIndex(List<NTreasureRoomRelicHolder> holders)
    {
        var focusOwner = NGame.Instance?.GetViewport()?.GuiGetFocusOwner();
        if (focusOwner is NTreasureRoomRelicHolder focusedHolder)
        {
            var index = holders.IndexOf(focusedHolder);
            if (index >= 0) return index;
        }

        // 默认第一个
        return 0;
    }

    private static void PreviewCurrent(List<NTreasureRoomRelicHolder> holders)
    {
        var index = ResolveCurrentIndex(holders);
        var holder = holders[index];

        // 显示 hovertip（需要调用 SetAlignmentForRelic 正确定位）
        var relic = holder.Relic;
        var relicModel = relic?.Model;
        if (relicModel != null && relic != null)
        {
            var tipSet = NHoverTipSet.CreateAndShow(holder, relicModel.HoverTips);
            tipSet.SetAlignmentForRelic(relic);
        }

        TryFocus(holder);
        MainFile.Logger.Info($"TreasureCommand: preview current index={index + 1}");
    }

    private static void PreviewNext(List<NTreasureRoomRelicHolder> holders)
    {
        var currentIndex = ResolveCurrentIndex(holders);
        var nextIndex = (currentIndex + 1) % holders.Count;
        var holder = holders[nextIndex];

        // 显示 hovertip（需要调用 SetAlignmentForRelic 正确定位）
        var relic = holder.Relic;
        var relicModel = relic?.Model;
        if (relicModel != null && relic != null)
        {
            var tipSet = NHoverTipSet.CreateAndShow(holder, relicModel.HoverTips);
            tipSet.SetAlignmentForRelic(relic);
        }

        TryFocus(holder);
        MainFile.Logger.Info($"TreasureCommand: preview next from={currentIndex + 1} to={nextIndex + 1}");
    }

    private static void ClaimCurrent(List<NTreasureRoomRelicHolder> holders)
    {
        var index = ResolveCurrentIndex(holders);
        var holder = holders[index];

        if (!GodotObject.IsInstanceValid(holder) || !holder.IsInsideTree())
        {
            MainFile.Logger.Warn($"TreasureCommand: holder invalid at index={index + 1}");
            return;
        }

        holder.ForceClick();
        MainFile.Logger.Info($"TreasureCommand: claim current index={index + 1}");
    }

    private static void TryClaimByName(List<NTreasureRoomRelicHolder> holders, string normalizedRelicName)
    {
        foreach (var holder in holders)
        {
            var relicModel = holder.Relic?.Model;
            if (relicModel == null) continue;

            var relicName = VoiceText.Normalize(relicModel.Title.GetFormattedText());
            if (relicName == normalizedRelicName)
            {
                holder.ForceClick();
                MainFile.Logger.Info($"TreasureCommand: claim by name '{normalizedRelicName}'");
                return;
            }
        }

        MainFile.Logger.Warn($"TreasureCommand: relic name not found '{normalizedRelicName}'");
    }

    private static void TryFocus(NTreasureRoomRelicHolder holder)
    {
        if (!GodotObject.IsInstanceValid(holder) || !holder.IsInsideTree()) return;

        holder.SetFocusMode(Control.FocusModeEnum.All);
        holder.TryGrabFocus();
    }
}
