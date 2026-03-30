namespace VoiceToPlay.Util;

/// <summary>
///     本地化工具类。提供中文数词转换等功能。
/// </summary>
internal static class L10n
{
    /// <summary>
    ///     中文数字映射（一到九）
    /// </summary>
    private static readonly string[] ChineseNumbers =
    [
        "零", "一", "二", "三", "四", "五", "六", "七", "八", "九"
    ];

    /// <summary>
    ///     中文数字后缀到数值的映射
    /// </summary>
    private static readonly Dictionary<string, int> SuffixToNumber = new(StringComparer.Ordinal)
    {
        ["一"] = 1, ["二"] = 2, ["三"] = 3, ["四"] = 4, ["五"] = 5,
        ["六"] = 6, ["七"] = 7, ["八"] = 8, ["九"] = 9
    };

    /// <summary>
    ///     获取所有中文数字后缀（一到九）
    /// </summary>
    public static IReadOnlyList<string> ChineseNumberSuffixes { get; } =
    [
        "一", "二", "三", "四", "五", "六", "七", "八", "九"
    ];

    /// <summary>
    ///     将数字转换为中文数字（一到九）。超出范围返回数字字符串。
    /// </summary>
    public static string ChineseNumber(int n)
    {
        return n is >= 0 and <= 9 ? ChineseNumbers[n] : n.ToString();
    }

    /// <summary>
    ///     生成序数词。如 Ordinal(1, "个") => "第一个"
    /// </summary>
    public static string Ordinal(int oneBasedIndex, string classifier)
    {
        return $"第{ChineseNumber(oneBasedIndex)}{classifier}";
    }

    /// <summary>
    ///     生成卡牌位置序数词。如 CardOrdinal(1) => "第一张"
    /// </summary>
    public static string CardOrdinal(int oneBasedIndex)
    {
        return Ordinal(oneBasedIndex, "张");
    }

    /// <summary>
    ///     生成敌人位置序数词。如 EnemyOrdinal(1) => "第一个"
    /// </summary>
    public static string EnemyOrdinal(int oneBasedIndex)
    {
        return Ordinal(oneBasedIndex, "个");
    }

    /// <summary>
    ///     生成地图路径序数词。如 MapPathOrdinal(1) => "第一条"
    /// </summary>
    public static string MapPathOrdinal(int oneBasedIndex)
    {
        return Ordinal(oneBasedIndex, "条");
    }

    /// <summary>
    ///     生成事件选项序数词。如 EventOptionOrdinal(1) => "第一个选项"
    /// </summary>
    public static string EventOptionOrdinal(int oneBasedIndex)
    {
        return $"{EnemyOrdinal(oneBasedIndex)}选项";
    }

    /// <summary>
    ///     生成药水序数词。如 PotionOrdinal(1) => "第一瓶"
    /// </summary>
    public static string PotionOrdinal(int oneBasedIndex)
    {
        return Ordinal(oneBasedIndex, "瓶");
    }

    /// <summary>
    ///     尝试解析中文数字后缀。如 TryParseSuffix("二", out var n) => n=2
    /// </summary>
    public static bool TryParseSuffix(string suffix, out int number)
    {
        return SuffixToNumber.TryGetValue(suffix, out number);
    }
}