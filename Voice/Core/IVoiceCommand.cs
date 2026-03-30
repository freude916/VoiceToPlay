namespace VoiceToPlay.Voice.Core;

/// <summary>
///     命令执行结果
/// </summary>
public enum CommandResult
{
    /// <summary>
    ///     命令成功执行
    /// </summary>
    Success,

    /// <summary>
    ///     命令尝试执行但失败（条件不满足、目标无效等）
    /// </summary>
    Failed,

    /// <summary>
    ///     命令当前不适用（给其他命令让路）
    /// </summary>
    Pass
}

/// <summary>
///     语音命令接口。所有语音命令都实现此接口。
/// </summary>
public interface IVoiceCommand
{
    /// <summary>
    ///     该命令支持的词表。可能是动态的（如手牌名称）。
    /// </summary>
    IEnumerable<string> SupportedWords { get; }

    /// <summary>
    ///     执行命令。上层派发词给命令。
    /// </summary>
    /// <param name="word">触发命令的词</param>
    /// <returns>执行结果</returns>
    CommandResult Execute(string word);

    /// <summary>
    ///     词表变化事件。命令主动通知上层刷新词表。
    /// </summary>
    event Action<IVoiceCommand>? VocabularyChanged;
}