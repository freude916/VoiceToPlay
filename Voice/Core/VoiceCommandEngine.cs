namespace VoiceToPlay.Voice.Core;

/// <summary>
///     语音命令引擎。维护词→命令映射，分割语音文本并派发命令。
/// </summary>
public sealed class VoiceCommandEngine
{
    /// <summary>
    ///     分割符：用于从语音流切分到多个命令
    /// </summary>
    private static readonly string[] Delimiters = ["嗯", "然后", "接着", "再", "和", "一下", "就", "得了", "吧"];

    /// <summary>
    ///     所有已注册的命令
    /// </summary>
    private readonly List<IVoiceCommand> _commands = [];

    /// <summary>
    ///     词 → 命令列表（一个词可能对应多个命令）
    /// </summary>
    private readonly Dictionary<string, List<IVoiceCommand>> _wordToCommands = [];

    /// <summary>
    ///     词表变化事件（供外部订阅，如 Vosk 服务）
    /// </summary>
    public event Action<IReadOnlySet<string>>? VocabularyUpdated;

    /// <summary>
    ///     注册命令
    /// </summary>
    public void Register(IVoiceCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        _commands.Add(command);
        command.VocabularyChanged += OnCommandVocabularyChanged;
        RebuildWordToCommandsMap();
    }

    /// <summary>
    ///     注销命令
    /// </summary>
    public void Unregister(IVoiceCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (_commands.Remove(command))
        {
            command.VocabularyChanged -= OnCommandVocabularyChanged;
            RebuildWordToCommandsMap();
        }
    }

    /// <summary>
    ///     命令通知词表变化
    /// </summary>
    private void OnCommandVocabularyChanged(IVoiceCommand cmd)
    {
        RebuildWordToCommandsMap();
        var words = GetAllWords();
        MainFile.Logger.Debug(
            $"VoiceCommandEngine: received VocabularyChanged from {cmd.GetType().Name}, now {words.Count} words");
        VocabularyUpdated?.Invoke(words);
    }

    /// <summary>
    ///     重建映射表（所有命令现在都用缓存，不会抛异常）
    /// </summary>
    private void RebuildWordToCommandsMap()
    {
        _wordToCommands.Clear();
        foreach (var cmd in _commands)
        foreach (var word in cmd.SupportedWords)
        {
            if (!_wordToCommands.TryGetValue(word, out var list))
                _wordToCommands[word] = list = [];
            list.Add(cmd);
        }
    }

    /// <summary>
    ///     获取所有词表（给 Vosk/Sherpa）
    /// </summary>
    public IReadOnlySet<string> GetAllWords()
    {
        var words = _wordToCommands.Keys.ToHashSet();
        words.UnionWith(Delimiters);
        return words;
    }

    /// <summary>
    ///     处理语音文本
    /// </summary>
    public void Process(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var words = SplitWords(text);
        foreach (var word in words)
        {
            if (string.IsNullOrWhiteSpace(word)) continue;

            if (!_wordToCommands.TryGetValue(word, out var commands))
            {
                // 词不存在于任何命令的词表中，识别结果有错误，停止执行
                MainFile.Logger.Warn($"VoiceCommandEngine: word '{word}' not found in any command, stopping");
                break;
            }

            // 执行所有对应的命令，收集结果
            var results = commands.Select(cmd => cmd.Execute(word)).ToList();

            // 所有命令都 Pass：用户操作有问题，停止执行
            if (results.All(r => r == CommandResult.Pass))
            {
                MainFile.Logger.Info($"VoiceCommandEngine: all commands passed for word '{word}', stopping");
                break;
            }

            // 有任何命令 Failed：停止执行
            if (results.Any(r => r == CommandResult.Failed))
            {
                MainFile.Logger.Info($"VoiceCommandEngine: command failed for word '{word}', stopping");
                break;
            }

            // 有 Success：继续下一个词
        }
    }

    /// <summary>
    ///     按分割符切分
    /// </summary>
    private static List<string> SplitWords(string text)
    {
        var remaining = Delimiters.Aggregate(text, (current, delimiter) => current.Replace(delimiter, "|"));

        return [.. remaining.Split('|').Select(VoiceText.Normalize).Where(word => !string.IsNullOrEmpty(word))];
    }
}