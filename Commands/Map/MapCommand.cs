using Godot;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;
using VoiceToPlay.Util;
using VoiceToPlay.Voice.Core;

namespace VoiceToPlay.Commands.Map;

/// <summary>
///     地图选择命令。支持 "左边", "右边", "中间", "第一条", "第二条" 等。
/// </summary>
public sealed class MapCommand : IVoiceCommand
{
    private readonly Dictionary<string, int> _wordToIndex = new();

    /// <summary>
    ///     缓存的词表，SupportedWords getter 直接返回此缓存
    /// </summary>
    private HashSet<string> _cachedWords = new(StringComparer.Ordinal);

    public MapCommand()
    {
        Instance = this;
    }

    public static MapCommand? Instance { get; private set; }

    /// <summary>
    ///     只返回缓存，不做任何计算
    /// </summary>
    public IEnumerable<string> SupportedWords => _cachedWords;

    public CommandResult Execute(string word)
    {
        if (!_wordToIndex.TryGetValue(word, out var index))
        {
            MainFile.Logger.Warn($"MapCommand: word '{word}' not found");
            return CommandResult.Failed;
        }

        var mapScreen = NMapScreen.Instance;
        if (mapScreen == null)
        {
            MainFile.Logger.Warn("MapCommand: NMapScreen.Instance is null");
            return CommandResult.Failed;
        }

        var coords = GetTravelableCoords();
        if (index < 0 || index >= coords.Count)
        {
            MainFile.Logger.Warn($"MapCommand: invalid index {index}, count={coords.Count}");
            return CommandResult.Failed;
        }

        var targetCoord = coords[index];
        var mapPoint = FindMapPointByCoord(mapScreen, targetCoord);
        if (mapPoint == null)
        {
            MainFile.Logger.Warn($"MapCommand: map point not found for coord {targetCoord}");
            return CommandResult.Failed;
        }

        mapScreen.OnMapPointSelectedLocally(mapPoint);
        MainFile.Logger.Debug($"MapCommand: '{word}' -> index={index}, coord={targetCoord}");
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
            instance.VocabularyChanged?.Invoke(instance);
        }
    }

    /// <summary>
    ///     计算当前支持的词表（只在 RefreshVocabulary 中调用）
    /// </summary>
    private HashSet<string> ComputeSupportedWords()
    {
        _wordToIndex.Clear();

        var mapScreen = NMapScreen.Instance;
        if (mapScreen == null)
        {
            MainFile.Logger.Debug("MapCommand.ComputeSupportedWords: mapScreen is null");
            return [];
        }

        if (!mapScreen.IsOpen)
        {
            MainFile.Logger.Debug("MapCommand.ComputeSupportedWords: mapScreen.IsOpen is false");
            return [];
        }

        if (mapScreen.IsTraveling)
        {
            MainFile.Logger.Debug("MapCommand.ComputeSupportedWords: IsTraveling is true");
            return [];
        }

        var coords = GetTravelableCoords();
        if (coords.Count == 0)
        {
            MainFile.Logger.Debug("MapCommand.ComputeSupportedWords: no travelable coords");
            return [];
        }

        MainFile.Logger.Debug($"MapCommand.ComputeSupportedWords: {coords.Count} travelable coords");

        // 方位词
        _wordToIndex["左边"] = 0;
        _wordToIndex["右边"] = coords.Count - 1;
        _wordToIndex["上去"] = 0; // 默认选第一个

        // 中间（仅当恰好3条路）
        if (coords.Count == 3)
            _wordToIndex["中间"] = 1;

        // 序号词（中文+数字）
        for (var i = 0; i < coords.Count && i < 9; i++)
        {
            var oneBased = i + 1;
            // 中文: 第一条, 第二条...
            _wordToIndex[L10n.MapPathOrdinal(oneBased)] = i;
            _wordToIndex[L10n.Ordinal(oneBased, "")] = i;
        }

        return new HashSet<string>(_wordToIndex.Keys, StringComparer.Ordinal);
    }

    private static IReadOnlyList<MapCoord> GetTravelableCoords()
    {
        var runState = RunManager.Instance.State;
        if (runState?.Map == null) return [];

        var current = runState.CurrentMapPoint;
        if (current == null)
            return [runState.Map.StartingMapPoint.coord];

        return
        [
            ..current.Children
                .OrderBy(p => p.coord.col)
                .ThenBy(p => p.coord.row)
                .Select(p => p.coord)
        ];
    }

    private static NMapPoint? FindMapPointByCoord(Node root, MapCoord coord)
    {
        var stack = new Stack<Node>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is NMapPoint mapPoint && mapPoint.Point?.coord.Equals(coord) == true)
                return mapPoint;

            foreach (var child in node.GetChildren())
                if (child != null)
                    stack.Push(child);
        }

        return null;
    }
}