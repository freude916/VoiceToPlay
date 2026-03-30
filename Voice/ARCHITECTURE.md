# VoiceToPlay 语音命令系统架构

## 设计原则

1. **模块化** - 新增命令不改 Voice 核心，只需注册新命令
2. **命令自治** - 命令自己管理状态和依赖，不依赖集中 Context
3. **依赖倒置** - 上层负责分割和派发，命令只负责执行
4. **ASR 无关** - 切换 Vosk/Sherpa 不影响命令定义
5. **分层隔离** - Commands 层不依赖 Voice 层，通过事件通信

---

## 核心流程

```
语音流："交锋嗯过"
    ↓ 分割（按"嗯、然后、等等"等分割符）
命令词序列：["交锋", "过"]
    ↓ 查映射表（词 → 命令列表）
命令派发：
  "交锋" → [PlayCardCommand.Execute("交锋")]
  "过"   → [EndTurnCommand.Execute("过")]
```

---

## 接口设计

### IVoiceCommand - 命令接口

```csharp
public interface IVoiceCommand
{
    // 我支持哪些词（给上层汇总）
    IEnumerable<string> SupportedWords { get; }
    
    // 执行（上层派发词给我）
    void Execute(string word);
    
    // 词表变化事件（命令主动通知上层刷新）
    event Action<IVoiceCommand>? VocabularyChanged;
}
```

**职责：**

- 声明自己支持的词表（可能是动态的）
- 收到词后执行对应动作
- 自己管理内部状态
- 订阅游戏状态变化，词表变化时通知上层

---

## 命令生命周期管理

### 推荐：构造函数 Patch + Godot 信号

对于 Godot 节点（如 `NMainMenu`），推荐 Patch 构造函数并绑定原生信号：

```csharp
[HarmonyPatch(typeof(NMainMenu), MethodType.Constructor)]
internal static class NMainMenuConstructorPatch
{
    private static void Postfix(NMainMenu __instance)
    {
        __instance.Ready += () =>
        {
            MainMenuCommand.RefreshVocabulary();
        };

        __instance.TreeExited += () =>
        {
            MainMenuCommand.RefreshVocabulary();
        };
    }
}
```

**优势：**

- 避免方法不存在问题（`_ExitTree` 可能未重写）
- 利用 Godot 原生信号，性能更好
- 只对目标类型生效，无全局污染

---

## 命令示例

### MainMenuCommand - 主菜单按钮（动态词表）

```csharp
public sealed class MainMenuCommand : IVoiceCommand
{
    private Dictionary<string, NButton> _wordToButton = new();
    
    public static MainMenuCommand? Instance { get; private set; }
    
    public IEnumerable<string> SupportedWords => _cachedWords;
    
    public Update
    {
        get
        {
            _wordToButton.Clear();
            var mainMenu = NGame.Instance?.MainMenu;
            if (mainMenu == null) return [];
            
            foreach (var button in mainMenu.MainMenuButtons)
            {
                if (button == null || !button.IsEnabled) continue;
                var text = GetButtonText(button);
                if (!string.IsNullOrEmpty(text))
                    _wordToButton[VoiceText.Normalize(text)] = button;
            }
            return _wordToButton.Keys;
        }
    }
    
    public void Execute(string word)
    {
        if (_wordToButton.TryGetValue(word, out var button) 
            && GodotObject.IsInstanceValid(button))
        {
            button.ForceClick();
        }
    }
    
    public event Action<IVoiceCommand>? VocabularyChanged;
    
    public static void RefreshVocabulary()
    {
        Instance?.VocabularyChanged?.Invoke(Instance);
    }
}
```

**工作流程：**

1. Patch 绑定 `Ready`/`TreeExited` 信号
2. 主菜单加载/卸载 → `RefreshVocabulary()`
3. 触发 `VocabularyChanged` → 上层收到事件
4. 上层调用 `SupportedWords` 获取新词表
5. 词表更新到 Vosk 识别器

---

## 文件结构

```
Voice/
├── Core/
│   ├── IVoiceCommand.cs        # 命令接口
│   ├── VoiceCommandEngine.cs   # 命令引擎
│   ├── CachedVoiceCommand.cs   # 缓存基类
│   └── ModAssemblyResolver.cs  # 程序集解析
├── Audio/
│   ├── VoiceAudioCaptureService.cs
│   └── LinearPcm16Resampler.cs
├── Grammar/
│   ├── GrammarSession.cs       # Vosk 词表生成
│   └── JiebaTokenizer.cs       # jieba 分词
├── UI/
│   └── VoiceDebugPanel.cs      # 调试面板
├── VoiceRecognitionService.cs  # Vosk 服务
├── VoiceEntryNode.cs           # 入口节点
├── VoiceText.cs                # 文本规范化
└── Patches/
    ├── NGameVoiceBootstrapPatch.cs
    └── NGameVoiceCleanupPatch.cs

Commands/  # 按功能分目录，新增不改 Voice
├── MainMenu/
│   ├── MainMenuCommand.cs
│   └── Patches/
│       └── NMainMenuLifecyclePatches.cs
├── Card/
│   └── PlayCardCommand.cs
└── Turn/
    └── EndTurnCommand.cs
```

---

## 扩展方式

1. 新建 `Commands/Xxx/XxxCommand.cs` 实现 `IVoiceCommand`
2. 如需生命周期管理，新建 `Patches/XxxLifecyclePatches.cs`
3. 在 `VoiceEntryNode` 注册：`_engine.Register(new XxxCommand());`
4. 无需修改 Voice 核心代码