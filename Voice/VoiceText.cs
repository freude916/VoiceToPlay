using MegaCrit.Sts2.Core.Models;

namespace VoiceToPlay.Voice;

/// <summary>
///     语音文本工具类。提供文本规范化功能。
/// </summary>
internal static class VoiceText
{
    /// <summary>
    ///     规范化：去空白、统一格式
    /// </summary>
    public static string Normalize(string? text)
    {
        return string.IsNullOrWhiteSpace(text) ? string.Empty : text.Trim().Replace(" ", "", StringComparison.Ordinal);
    }
    
    /// <summary>
    ///     获取卡牌命令名（从 CardModel）
    ///     使用 TitleLocString 获取原始标题，自动忽略升级后缀（+, +2 等）
    /// </summary>
    public static string GetCardCommandName(CardModel card)
    {
        return Normalize(card.TitleLocString.GetFormattedText());
    }
}