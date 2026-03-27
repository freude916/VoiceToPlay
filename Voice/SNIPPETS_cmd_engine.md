### VoiceCommandEngine - 命令引擎

```csharp
public class VoiceCommandEngine
{
    // 词 → 命令列表（一个词可能对应多个命令）
    private readonly Dictionary<string, List<IVoiceCommand>> _wordToCommands = new();
    
    // 所有已注册的命令
    private readonly List<IVoiceCommand> _commands = new();
    
    // 分割符
    private static readonly string[] Delimiters = ["嗯", "然后", "接着", "再", "和"];
    
    // 词表变化事件（供外部订阅，如 Vosk 服务）
    public event Action<IReadOnlySet<string>>? VocabularyUpdated;
    
    // 注册命令
    public void Register(IVoiceCommand command)
    {
        _commands.Add(command);
        command.VocabularyChanged += OnCommandVocabularyChanged;
        RebuildWordToCommandsMap();
    }
    
    // 命令通知词表变化
    private void OnCommandVocabularyChanged(IVoiceCommand cmd)
    {
        RebuildWordToCommandsMap();
        VocabularyUpdated?.Invoke(GetAllWords());
    }
    
    // 重建映射表
    private void RebuildWordToCommandsMap()
    {
        _wordToCommands.Clear();
        foreach (var cmd in _commands)
        {
            foreach (var word in cmd.SupportedWords)
            {
                if (!_wordToCommands.TryGetValue(word, out var list))
                    _wordToCommands[word] = list = new();
                list.Add(cmd);
            }
        }
    }
    
    // 获取所有词表（给 Vosk/Sherpa）
    public IReadOnlySet<string> GetAllWords() => _wordToCommands.Keys.ToHashSet();
    
    // 处理语音文本
    public void Process(string text)
    {
        var words = SplitWords(text);
        foreach (var word in words)
        {
            if (_wordToCommands.TryGetValue(word, out var commands))
            {
                foreach (var cmd in commands)
                    cmd.Execute(word);
            }
        }
    }
    
    // 按分割符切分
    private List<string> SplitWords(string text)
    {
        var result = new List<string>();
        var remaining = text;
        foreach (var delimiter in Delimiters)
            remaining = remaining.Replace(delimiter, "|");
        foreach (var part in remaining.Split('|'))
        {
            var word = part.Trim();
            if (!string.IsNullOrEmpty(word))
                result.Add(word);
        }
        return result;
    }
}
```

**职责：**

- 维护词→命令映射表
- 监听命令的词表变化事件
- 按分割符切分语音文本
- 派发命令词给对应命令
- 汇总词表并通过事件通知外部

---

## 抽象基类

### CachedVoiceCommand<TTarget> - 带缓存的命令基类

**解决问题：**

- `SupportedWords` 和 `Execute` 都需要访问目标对象
- 执行时再查找是重复劳动
- 缓存后执行 O(1)，类型安全

```csharp
public abstract class CachedVoiceCommand<TTarget> : IVoiceCommand
{
    protected readonly Dictionary<string, TTarget> _wordToTarget = new();
    
    public IEnumerable<string> SupportedWords
    {
        get
        {
            _wordToTarget.Clear();
            return BuildWordToTargetMap(_wordToTarget);
        }
    }
    
    // 子类实现：构建词→目标映射
    protected abstract IEnumerable<string> BuildWordToTargetMap(Dictionary<string, TTarget> map);
    
    // 子类实现：执行目标
    protected abstract void ExecuteTarget(TTarget target);
    
    public void Execute(string word)
    {
        if (_wordToTarget.TryGetValue(word, out var target))
            ExecuteTarget(target);
    }
    
    public event Action<IVoiceCommand>? VocabularyChanged;
    
    // 便捷方法：触发词表变化通知
    protected void OnVocabularyChanged() => VocabularyChanged?.Invoke(this);
}
```

**优势：**

- 词表获取时顺便缓存目标对象
- 执行时直接用缓存，无需再查找
- 类型安全，编译期检查

---