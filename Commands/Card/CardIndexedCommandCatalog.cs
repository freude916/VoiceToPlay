namespace VoiceToPlay.Commands.Card;

/// <summary>
///     卡牌索引命令词表生成。支持 "防御", "防御一", "防御二" 等格式。
/// </summary>
internal static class CardIndexedCommandCatalog
{
    /// <summary>
    ///     支持的数字后缀（1-9）
    /// </summary>
    public static IReadOnlyList<int> SupportedIndices { get; } = [1, 2, 3, 4, 5, 6, 7, 8, 9];

    private static readonly Dictionary<string, int> IndexByNormalizedSuffix = BuildIndexBySuffix();

    /// <summary>
    ///     枚举某卡牌名称的所有可能命令
    /// </summary>
    public static IEnumerable<string> EnumerateIndexedCommands(string normalizedCardName)
    {
        if (normalizedCardName.Length == 0) yield break;

        // 基础名称（默认 occurrence=1）
        yield return normalizedCardName;

        // 带数字后缀
        foreach (var suffix in IndexByNormalizedSuffix.Keys)
            yield return normalizedCardName + suffix;
    }

    /// <summary>
    ///     解析输入文本，提取卡牌名和 occurrence
    /// </summary>
    /// <param name="normalizedInput">用户输入（已规范化）</param>
    /// <param name="normalizedCardName">卡牌名称（已规范化）</param>
    /// <param name="oneBasedOccurrence">第几张同名卡（1-based）</param>
    public static bool TryParseOccurrence(string normalizedInput, string normalizedCardName, out int oneBasedOccurrence)
    {
        oneBasedOccurrence = 1;
        if (normalizedInput.Length == 0 || normalizedCardName.Length == 0)
            return false;

        // 精确匹配：occurrence=1
        if (normalizedInput.Equals(normalizedCardName, StringComparison.Ordinal))
        {
            oneBasedOccurrence = 1;
            return true;
        }

        // 后缀匹配："防御二" -> occurrence=2
        if (!normalizedInput.StartsWith(normalizedCardName, StringComparison.Ordinal))
            return false;

        var suffix = normalizedInput[normalizedCardName.Length..];
        return IndexByNormalizedSuffix.TryGetValue(suffix, out oneBasedOccurrence);
    }

    private static Dictionary<string, int> BuildIndexBySuffix()
    {
        // 中文数字后缀映射
        var chineseNumbers = new Dictionary<int, string>
        {
            [1] = "一", [2] = "二", [3] = "三", [4] = "四", [5] = "五",
            [6] = "六", [7] = "七", [8] = "八", [9] = "九"
        };

        var indexBySuffix = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var index in SupportedIndices)
            indexBySuffix[chineseNumbers[index]] = index;
        return indexBySuffix;
    }
}