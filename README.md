# VoiceToPlay

Slay the Spire 2 语音控制 Mod，让你可以用中文语音指令操控游戏。

## 功能

- 🎤 实时语音识别（Vosk 离线引擎）
- 🎮 战斗中语音出牌、选目标、结束回合
- 🗺️ 地图导航、事件选择、奖励领取
- 📦 支持动态卡牌名/遗物名识别

## 首次使用

需要在游戏的二进制旁边创建 `override.cfg`，手动启用音频输入：

```ini
[audio]
driver/enable_input=true
```

然后重启游戏。

## 语音命令

详见 [COMMANDS.md](docs/COMMANDS.md)

### 快速参考

| 场景 | 示例命令 |
|------|---------|
| 战斗 | `打击`、`防御一`、`瞄准第一个`、`结束` |
| 地图 | `左边`、`右边`、`第一条` |
| 事件 | `第一个`、`继续` |
| 宝箱 | `打开宝箱`、`选择遗物` |
| 奖励 | `金币`、`卡牌一` |

## 配置

### 麦克风回放延迟

如需调整回放延迟，修改 `Voice/Audio/JitterBufferPlaybackService.cs`：

```csharp
private const int MinBufferFrames = 4096;     // 越小延迟越低，但可能卡顿
private const float GeneratorBufferLength = 0.5f;  // 播放器缓冲（秒）
```

## 技术栈

- **语音识别**: Vosk（中文小模型）
- **分词**: Jieba.NET
- **游戏引擎**: Godot 4.x
- **Mod 框架**: BaseLib + BepInEx

## 已知问题

- 麦克风回放可能有轻微断续（Godot AudioStreamMicrophone 的已知问题）
- 某些特殊卡牌名可能无法识别
