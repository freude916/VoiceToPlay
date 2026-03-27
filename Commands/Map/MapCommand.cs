using Godot;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;
using VoiceToPlay.Voice.Core;

namespace VoiceToPlay.Commands.Map;

/// <summary>
///     地图选择命令。支持 "左边", "右边", "中间", "第一条", "第二条" 等。
/// </summary>
public sealed class MapCommand : IVoiceCommand
{
    private static readonly Dictionary<int, string> ChineseNumbers = new()
    {
        [1] = "一", [2] = "二", [3] = "三", [4] = "四", [5] = "五",
        [6] = "六", [7] = "七", [8] = "八", [9] = "九"
    };

    private readonly Dictionary<string, int> _wordToIndex = new();
    private HashSet<string> _lastWords = new(StringComparer.Ordinal);

    public MapCommand()
    {
        Instance = this;
    }

    public static MapCommand? Instance { get; private set; }

    public IEnumerable<string> SupportedWords => GetSupportedWords(NMapScreen.Instance);

    public void Execute(string word)
    {
        if (!_wordToIndex.TryGetValue(word, out var index))
        {
            MainFile.Logger.Warn($"MapCommand: word '{word}' not found");
            return;
        }

        var mapScreen = NMapScreen.Instance;
        if (mapScreen == null)
        {
            MainFile.Logger.Warn("MapCommand: NMapScreen.Instance is null");
            return;
        }

        var coords = GetTravelableCoords();
        if (index < 0 || index >= coords.Count)
        {
            MainFile.Logger.Warn($"MapCommand: invalid index {index}, count={coords.Count}");
            return;
        }

        var targetCoord = coords[index];
        var mapPoint = FindMapPointByCoord(mapScreen, targetCoord);
        if (mapPoint == null)
        {
            MainFile.Logger.Warn($"MapCommand: map point not found for coord {targetCoord}");
            return;
        }

        mapScreen.OnMapPointSelectedLocally(mapPoint);
        MainFile.Logger.Info($"MapCommand: '{word}' -> index={index}, coord={targetCoord}");
    }

    public event Action<IVoiceCommand>? VocabularyChanged;

    /// <summary>
    ///     获取支持的词汇，可传入指定的 mapScreen 避免依赖 NMapScreen.Instance
    /// </summary>
    private IEnumerable<string> GetSupportedWords(NMapScreen? mapScreen)
    {
        _wordToIndex.Clear();

        if (mapScreen == null)
        {
            MainFile.Logger.Info("MapCommand.GetSupportedWords: mapScreen is null");
            return [];
        }

        if (!mapScreen.IsOpen)
        {
            MainFile.Logger.Info("MapCommand.GetSupportedWords: mapScreen.IsOpen is false");
            return [];
        }

        if (!mapScreen.IsTravelEnabled)
        {
            MainFile.Logger.Info("MapCommand.GetSupportedWords: IsTravelEnabled is false");
            return [];
        }

        if (mapScreen.IsTraveling)
        {
            MainFile.Logger.Info("MapCommand.GetSupportedWords: IsTraveling is true");
            return [];
        }

        var coords = GetTravelableCoords();
        if (coords.Count == 0)
        {
            MainFile.Logger.Info("MapCommand.GetSupportedWords: no travelable coords");
            return [];
        }

        MainFile.Logger.Info($"MapCommand.GetSupportedWords: {coords.Count} travelable coords");

        // 方位词
        _wordToIndex["左边"] = 0;
        _wordToIndex["右边"] = coords.Count - 1;
        _wordToIndex["上去"] = 0;  // 默认选第一个

        // 中间（仅当恰好3条路）
        if (coords.Count == 3)
            _wordToIndex["中间"] = 1;

        // 序号词（中文+数字）
        for (var i = 0; i < coords.Count && i < 9; i++)
        {
            var oneBased = i + 1;
            // 中文: 第一条, 第二条...
            _wordToIndex[$"第{ChineseNumbers[oneBased]}条"] = i;
            _wordToIndex[$"第{ChineseNumbers[oneBased]}"] = i;
            // 数字: 第1条, 第2条...
            _wordToIndex[$"第{oneBased}条"] = i;
            _wordToIndex[$"第{oneBased}"] = i;
        }

        return _wordToIndex.Keys;
    }

    public static void RefreshVocabulary(NMapScreen? mapScreen = null)
    {
        var instance = Instance;
        if (instance == null) return;

        var currentWords = new HashSet<string>(instance.GetSupportedWords(mapScreen), StringComparer.Ordinal);
        if (!currentWords.SetEquals(instance._lastWords))
        {
            instance._lastWords = currentWords;
            instance.VocabularyChanged?.Invoke(instance);
        }
    }

    private static IReadOnlyList<MapCoord> GetTravelableCoords()
    {
        var runState = RunManager.Instance?.State;
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
        if (root == null) return null;

        var stack = new Stack<Node>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is NMapPoint mapPoint && mapPoint.Point?.coord.Equals(coord) == true)
                return mapPoint;

            foreach (var child in node.GetChildren())
                if (child is Node childNode)
                    stack.Push(childNode);
        }

        return null;
    }
}