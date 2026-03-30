using VoiceToPlay.Util;
using VoiceToPlay.Voice;

namespace VoiceToPlay.Commands.Combat;

/// <summary>
///     敌人目标选择命令词表生成。支持 "瞄准第一", "打一", "第一个" 等格式。
/// </summary>
internal static class EnemyTargetCommandCatalog
{
    private static readonly Dictionary<string, int> IndexByNormalizedCommand;
    private static readonly List<string> GrammarPhrasesList;
    private static readonly List<string> TargetVerbsList;
    private static readonly List<int> SupportedTargetIndicesList;

    static EnemyTargetCommandCatalog()
    {
        TargetVerbsList = ["瞄准"];
        SupportedTargetIndicesList = [1, 2, 3, 4, 5, 6, 7, 8, 9];
        IndexByNormalizedCommand = BuildCommandIndex();
        GrammarPhrasesList = [.. IndexByNormalizedCommand.Keys.OrderBy(p => p, StringComparer.Ordinal)];
    }

    /// <summary>
    ///     目标选择动词
    /// </summary>
    public static IReadOnlyList<string> TargetVerbs => TargetVerbsList;

    /// <summary>
    ///     支持的目标索引（1-9）
    /// </summary>
    public static IReadOnlyList<int> SupportedTargetIndices => SupportedTargetIndicesList;

    /// <summary>
    ///     所有可能的命令词
    /// </summary>
    public static IReadOnlyList<string> GrammarPhrases => GrammarPhrasesList;

    /// <summary>
    ///     尝试解析命令
    /// </summary>
    public static bool TryParseNormalizedCommand(string normalizedCommand, out int oneBasedIndex)
    {
        return IndexByNormalizedCommand.TryGetValue(normalizedCommand, out oneBasedIndex);
    }

    private static Dictionary<string, int> BuildCommandIndex()
    {
        var indexByCommand = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var index in SupportedTargetIndicesList)
        {
            // "第一个" (中文)
            AddCommand(indexByCommand, L10n.EnemyOrdinal(index), index);

            foreach (var verb in TargetVerbsList)
                // "瞄准第一个"
                AddCommand(indexByCommand, $"{verb}{L10n.EnemyOrdinal(index)}", index);
        }

        return indexByCommand;
    }

    private static void AddCommand(Dictionary<string, int> indexByCommand, string rawCommand, int index)
    {
        var normalized = VoiceText.Normalize(rawCommand);
        if (!string.IsNullOrEmpty(normalized))
            indexByCommand[normalized] = index;
    }
}