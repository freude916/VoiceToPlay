using System.Text.Encodings.Web;
using System.Text.Json;

namespace VoiceToPlay.Voice.Grammar;

/// <summary>
///     词表生成会话。从命令引擎词表生成 Vosk 可用的 grammar JSON。
/// </summary>
internal sealed class GrammarSession
{
    // 禁用 Unicode 转义，Vosk/Kaldi 不认 \uXXXX
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly JiebaTokenizer _tokenizer = new();
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
            foreach (var token in _tokenizer.Tokenize(word))
                expandedWords.Add(token);
        }

        // 检查是否有变化
        if (_activeWords.SetEquals(expandedWords))
            return string.Empty; // 无变化

        _activeWords = expandedWords;

        // 生成 JSON（不转义中文）
        var sorted = expandedWords.OrderBy(w => w).ToList();
        var json = JsonSerializer.Serialize(sorted, JsonOptions);
        MainFile.Logger.Info($"GrammarSession: generated grammar with words: {json}");
        return json;
    }
}