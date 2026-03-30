using MegaCrit.Sts2.Core.Models;
using VoiceToPlay.Util;
using VoiceToPlay.Voice;

namespace VoiceToPlay.Commands.Card;

/// <summary>
///     卡牌索引命令词表生成。支持 "防御一", "防御二" 等格式。
///     注意：不带数字的基础词（如 "防御"）不在此生成，需在执行时实时搜索。
/// </summary>
internal static class CardIndexedCommandCatalog
{
    /// <summary>
    ///     支持的数字后缀（1-9）
    /// </summary>
    public static IReadOnlyList<int> SupportedIndices { get; } = [1, 2, 3, 4, 5, 6, 7, 8, 9];

    /// <summary>
    ///     枚举某卡牌名称的带数字后缀命令（不含基础名称）
    /// </summary>
    public static IEnumerable<string> EnumerateIndexedCommands(string normalizedCardName)
    {
        if (normalizedCardName.Length == 0) yield break;

        // 只生成带数字后缀的词，不生成基础名称
        foreach (var suffix in L10n.ChineseNumberSuffixes)
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

        // 后缀匹配："防御二" -> occurrence=2
        if (!normalizedInput.StartsWith(normalizedCardName, StringComparison.Ordinal))
            return false;

        var suffix = normalizedInput[normalizedCardName.Length..];
        return L10n.TryParseSuffix(suffix, out oneBasedOccurrence);
    }

    /// <summary>
    ///     检查输入是否是带数字后缀的卡牌命令词（如 "防御一"）
    /// </summary>
    public static bool IsIndexedWord(string normalizedInput, string normalizedCardName)
    {
        return TryParseOccurrence(normalizedInput, normalizedCardName, out _);
    }

    /// <summary>
    ///     从卡牌列表构建命令词表。
    ///     返回 word -> CardModel 的映射（只包含带数字后缀的词）。
    /// </summary>
    /// <param name="cards">卡牌列表</param>
    /// <returns>命令词到 CardModel 的映射</returns>
    public static Dictionary<string, CardModel> BuildWordToCard(IEnumerable<CardModel> cards)
    {
        var result = new Dictionary<string, CardModel>(StringComparer.Ordinal);

        // 按 CardModel 分组，统计每个卡牌名出现的次数和对应的卡牌列表
        var cardsByName = new Dictionary<string, List<CardModel>>(StringComparer.Ordinal);
        foreach (var card in cards)
        {
            var normalizedName = VoiceText.Normalize(VoiceText.GetCardCommandName(card));
            if (string.IsNullOrEmpty(normalizedName)) continue;

            if (!cardsByName.TryGetValue(normalizedName, out var list))
                cardsByName[normalizedName] = list = [];
            list.Add(card);
        }

        // 按卡名长度降序（优先匹配长名）
        var sortedNames = cardsByName.Keys.OrderByDescending(n => n).ToList();

        // 生成带数字后缀的命令词
        foreach (var normalizedName in sortedNames)
        {
            var cardList = cardsByName[normalizedName];
            for (var i = 0; i < cardList.Count; i++)
            {
                // "防御一" -> 第1张, "防御二" -> 第2张
                var word = normalizedName + L10n.ChineseNumber(i + 1);
                result[word] = cardList[i];
            }
        }

        return result;
    }
}