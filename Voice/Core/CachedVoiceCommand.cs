namespace VoiceToPlay.Voice.Core;

/// <summary>
///     带缓存的命令抽象基类。
///     <para>
///         解决问题：SupportedWords 和 Execute 都需要访问目标对象，
///         执行时再查找是重复劳动。缓存后执行 O(1)，类型安全。
///     </para>
/// </summary>
/// <typeparam name="TTarget">目标对象类型（如 CardModel）</typeparam>
public abstract class CachedVoiceCommand<TTarget> : IVoiceCommand
{
    protected readonly Dictionary<string, TTarget> WordToTarget = new();

    public IEnumerable<string> SupportedWords
    {
        get
        {
            WordToTarget.Clear();
            return BuildWordToTargetMap(WordToTarget);
        }
    }

    public CommandResult Execute(string word)
    {
        if (WordToTarget.TryGetValue(word, out var target))
            return ExecuteTarget(target);
        return CommandResult.Pass;
    }

    public event Action<IVoiceCommand>? VocabularyChanged;

    /// <summary>
    ///     子类实现：构建词→目标映射
    /// </summary>
    protected abstract IEnumerable<string> BuildWordToTargetMap(Dictionary<string, TTarget> map);

    /// <summary>
    ///     子类实现：执行目标
    /// </summary>
    protected abstract CommandResult ExecuteTarget(TTarget target);

    /// <summary>
    ///     便捷方法：触发词表变化通知
    /// </summary>
    protected void OnVocabularyChanged()
    {
        VocabularyChanged?.Invoke(this);
    }
}