# 当前问题与修复思路

## 问题分析

| # | 问题                           | 状态     | 说明        |
|---|------------------------------|--------|-----------|
| 1 | "打击然后打击" 会尝试两次放入 1号打击        | ✅ 已解决  | 出牌时强制刷新词表 |
| 2 | "打击4然后打击5" 抽掉打击4后找不到打击5      | 🐛 Bug | 词表刷新后引用丢失 |
| 3 | `Execute(string word)` 字符串编程 | ⚠️ 可接受 | 当前架构已足够清晰 |
| 4 | GetVocab 已获取目标，Execute 重复获取  | ⚠️ 可接受 | 性能影响可忽略   |
| 5 | 检查卡牌能否打出需要类内环境               | ⚠️ 待商榷 | 上交闭包确实较重  |
| 6 | 误识别导致部分卡牌成功打出，破坏顺序           | 🐛 Bug | 需要错误处理机制  |

## 错误类型定义

| 类型             | 含义                   | 处理方式      |
|----------------|----------------------|-----------|
| **ShouldPass** | 静默拒绝，"这个词现在我暂时要拱手让人" | 继续尝试下一个绑定 |
| **Failed**     | 显著拒绝，用户操作有问题         | 中断并显示 UI  |
| **NotFound**   | 找不到，识别层提交的结果有误       | 中断并显示 UI  |

```csharp
public enum CommandResult { Success, ShouldPass, Failed }
```

- 所有绑定都 `ShouldPass` → 引擎报 `NotFound` 并中断
- 静默拒绝场景示例：手牌选牌模式时，手牌命令要让位给选牌命令

## 卡牌命令修复思路

### 问题 2 修复：缓存 CardModel 引用

当前 `_wordToCard` 存储 `(normalizedName, occurrence)` 信息，Execute 时重新遍历手牌查找。

修复方案：直接缓存 `CardModel` 引用

```csharp
// PlayCardCommand 修改
private readonly Dictionary<string, CardModel> _wordToCardRef = new();

private HashSet<string> ComputeSupportedWords()
{
    _wordToCardRef.Clear();
    
    // ... 获取手牌 ...
    
    foreach (var (normalizedName, cards) in cardsByName)
    {
        for (int i = 0; i < cards.Count; i++)
        {
            var indexedWord = $"{normalizedName}{ToChineseNumber(i + 1)}";
            _wordToCardRef[indexedWord] = cards[i];  // 直接缓存引用
        }
    }
    
    return new HashSet<string>(_wordToCardRef.Keys);
}

public void Execute(string word)
{
    if (!_wordToCardRef.TryGetValue(word, out var card)) return;
    ExecuteCard(card);
}
```

**优势**：

- 词表刷新不影响已缓存的引用
- "打击4然后打击5" 执行时直接使用缓存的 CardModel

- 无指定数字的卡牌可能可以走另一条路经？

### 问题 6 修复：返回 CommandResult

```csharp
public CommandResult ExecuteCard(CardModel card)
{
    if (!card.CanPlay(out _, out _))
        return CommandResult.ShouldPass;
    
    // ... 目标检查 ...
    
    var queued = card.TryManualPlay(target);
    return queued ? CommandResult.Success : CommandResult.Failed;
}
```

Engine 处理：

```csharp
// VoiceCommandEngine.Process
foreach (var word in words)
{
    if (!_wordToCommands.TryGetValue(word, out var commands))
    {
        ShowErrorUI(word, "NotFound");
        return;
    }
    
    var allShouldPass = true;
    foreach (var cmd in commands)
    {
        var result = cmd.Execute(word);  // 需要接口变更
        if (result == CommandResult.Failed)
        {
            ShowErrorUI(word, "Failed");
            return;
        }
        if (result == CommandResult.Success)
        {
            allShouldPass = false;
            break;
        }
    }
    
    if (allShouldPass)
    {
        ShowErrorUI(word, "NotFound");
        return;
    }
}
```

## 待确认问题

- [ ] `IVoiceCommand.Execute` 是否需要返回 `CommandResult`？还是新增 `TryExecute` 方法？
- [ ] 错误 UI 如何显示？
