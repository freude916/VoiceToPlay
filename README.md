# 说戮尖塔 Say to Spire

Play the most accessible and vocal card game with Sironclad, The Un-Silent, The Dictator, The Re-chant, and The Microphonder!

Command the Spire by simply speaking out your strategy. Channel your inner energy using Echo Form to repeat your voice commands,
whisper your foes to their doom with Rapper Form,
shout a literal Warcry to draw your cards, and transcend physical limits with Voice Form.

Currently, only support Chinese command logic ... Maybe not too difficult to l10n ?

快来体验这款最废嗓子、最护手的硬核卡牌游戏！化身铁嗓战士、破音猎手、复读机器人、唇君以及亡语契约师，用你的声音征服高塔！

在这里，动动嘴皮子即可发号施令。从嘶声形态、群舌形态 再到 呃魔形态、回响形态，对着麦克风尖啸！或者在力竭之时，进入失声形态！

Slay the Spire 2 语音控制 Mod，让你可以用中文语音指令操控游戏。

## 介绍

- 🎤 实时语音识别
- 🎮 战斗中语音出牌、选目标、结束回合
- 🗺 地图导航、事件选择、奖励领取
- 📦 支持动态卡牌名/遗物名识别

- **语音识别**: Vosk -- vosk-model-small-cn-0.22
- **分词**: Jieba.NET
- 使用代码从 Vosk 里提取可以用的分词来过滤 jieba，然后 jieba 的分词结果限制 Vosk 的 语法词表，以提高准确率。
- 手动进行你的 [过滤](docs/filter_words/NOTES.md)

## TODO

- 商店
- 取消按钮

在 Windows, Linux 和 macOS ，Vosk 原生库的版本各不相同😧，所以目前为了 release 跨平台性，我们同时拷贝了三个版本的 lib ，体积不是很优雅。

考虑使用新一代的 Sherpa 模型和框架进行 ASR。

jieba 的逻辑可能可以简化，因为没有词频的必要。

deps 是 json 格式的，Slay the Spire 2 会对该 json 抛出无法识别 Mod 的 Error，带来大量日志噪音😧

## 首次使用


> [!IMPORTANT]
> 目前我们使用的是 Godot 原生的 Microphone 支持，所以必须
> 在游戏的二进制旁边创建 `override.cfg`，手动启用音频输入

```ini
[audio]
driver/enable_input=true
```

重启游戏生效。

## 操作

F8 开关麦。

命令详见 [COMMANDS.md](docs/COMMANDS.md)

### 快速参考

| 场景 | 示例命令                    |
|----|-------------------------|
| 战斗 | `打击`、`防御一`、`瞄准第一个`、`结束` |
| 地图 | `左边`、`右边`、`第一条`         |
| 事件 | `第一个选项`、`继续`            |
| 宝箱 | `打开宝箱`、`选择遗物`           |
| 奖励 | `金币`、`卡牌一`              |

## 配置

### 麦克风回放

通过 Playback 调试降噪和增益等。 使用了一个 Jitter Buffer 以减少每 _Process 提交音频造成的撕裂。

但是由于 AudioStreamMicrophone 只有有限大小的缓冲区，不时可能还是因为移除还是会撕裂。

如需调整回放延迟，修改 `Voice/Audio/JitterBufferPlaybackService.cs`：

```csharp
private const int MinBufferFrames = 4096;     // 越小延迟越低，但可能卡顿
private const float GeneratorBufferLength = 0.5f;  // 播放器缓冲（秒）
```


## 已知问题

- 某些特殊卡牌名可能无法识别
- 底噪很容易被识别为 “防御”。
