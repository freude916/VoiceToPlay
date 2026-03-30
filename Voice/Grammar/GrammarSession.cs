using System.Text.Encodings.Web;
using System.Text.Json;

namespace VoiceToPlay.Voice.Grammar;

/// <summary>
///     词表生成会话。从命令引擎词表生成 Vosk 可用的 grammar JSON。
/// </summary>
internal sealed class GrammarSession
{
    /// <summary>
    ///     是否输出完整词表日志。
    /// </summary>
    private const bool LogFullGrammar = false;

    /// <summary>
    ///     是否输出词表变化 diff 日志。
    /// </summary>
    private const bool LogGrammarDiff = false;

    // 禁用 Unicode 转义，Vosk/Kaldi 不认 \uXXXX
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private HashSet<string> _activeWords = new(StringComparer.Ordinal);

    /// <summary>
    ///     从命令引擎更新词表，生成 grammar JSON。
    ///     如果词表无变化，返回空字符串。
    /// </summary>
    public string BuildGrammarJson(IReadOnlySet<string> commandWords)
    {
        var expandedWords = new HashSet<string>(StringComparer.Ordinal) { "[unk]" };

        foreach (var word in commandWords)
        {
            expandedWords.Add(word);

            // 分词扩展
            foreach (var token in JiebaTokenizer.Tokenize(word))
                expandedWords.Add(token);
        }

        // 检查是否有变化
        if (_activeWords.SetEquals(expandedWords))
            return string.Empty; // 无变化

#pragma warning disable CS0162 // Debug
        // ReSharper disable HeuristicUnreachableCode
        // 计算 diff
        if (LogGrammarDiff)
        {
            var added = expandedWords.Except(_activeWords).ToList();
            var removed = _activeWords.Except(expandedWords).ToList();

            if (added.Count > 0) MainFile.Logger.Info($"GrammarSession: added words: [{string.Join(", ", added)}]");

            if (removed.Count > 0)
                MainFile.Logger.Info($"GrammarSession: removed words: [{string.Join(", ", removed)}]");
        }

        _activeWords = expandedWords;

        // 生成 JSON（不转义中文）
        var sorted = expandedWords.OrderBy(w => w).ToList();
        var json = JsonSerializer.Serialize(sorted, JsonOptions);

        if (LogFullGrammar)
            MainFile.Logger.Info($"GrammarSession: generated grammar with {sorted.Count} words: {json}");

        // ReSharper restore HeuristicUnreachableCode
#pragma warning restore CS0162
        return json;
    }
}