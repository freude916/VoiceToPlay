# 代码片段参考

## Audio - 麦克风捕获

### 环境检查

```csharp
// 检查 Godot 音频输入是否启用
public static bool IsAudioInputEnabled()
{
    var setting = ProjectSettings.GetSetting("audio/driver/enable_input", false);
    return setting.AsBool();
}

// 构建错误提示
public static string BuildAudioInputDisabledMessage()
{
    var overrideCfgPath = Path.Combine(
        Path.GetDirectoryName(Environment.ProcessPath) ?? ".", 
        "override.cfg");
    return
        $"Godot audio input is disabled (audio/driver/enable_input=false). " +
        $"Create '{overrideCfgPath}' with:\n[audio]\ndriver/enable_input=true\nThen restart game.";
}
```

### 创建 Capture Bus

```csharp
private const string CaptureBusName = "VoiceToPlayCapture";

private int EnsureCaptureBus()
{
    var busIndex = AudioServer.GetBusIndex(CaptureBusName);
    if (busIndex < 0)
    {
        AudioServer.AddBus();
        busIndex = AudioServer.BusCount - 1;
        AudioServer.SetBusName(busIndex, CaptureBusName);
    }
    AudioServer.SetBusMute(busIndex, false);
    AudioServer.SetBusVolumeDb(busIndex, 0f);
    return busIndex;
}
```

### 安装 AudioEffectCapture

```csharp
private static AudioEffectCapture GetOrCreateCaptureEffect(int busIndex)
{
    var effectCount = AudioServer.GetBusEffectCount(busIndex);
    for (var i = 0; i < effectCount; i++)
        if (AudioServer.GetBusEffect(busIndex, i) is AudioEffectCapture existing)
            return existing;

    var capture = new AudioEffectCapture();
    AudioServer.AddBusEffect(busIndex, capture);
    return capture;
}
```

### 挂载 AudioStreamMicrophone

```csharp
private void RecreateMicrophonePlayer()
{
    _microphonePlayer?.Stop();
    if (GodotObject.IsInstanceValid(_microphonePlayer))
        _microphonePlayer.QueueFree();

    _microphonePlayer = new AudioStreamPlayer
    {
        Name = "VoiceToPlayMicPlayer",
        Stream = new AudioStreamMicrophone(),
        Bus = CaptureBusName,
        VolumeDb = 0f,
        Autoplay = false
    };
    _owner.AddChild(_microphonePlayer);
    _microphonePlayer.Play();
}
```

---

## Audio - 重采样器

```csharp
internal sealed class LinearPcm16Resampler
{
    private readonly bool _passthrough;
    private readonly List<float> _samples = new();
    private readonly double _step;
    private double _sourcePosition;

    public LinearPcm16Resampler(int sourceRate, int targetRate)
    {
        _passthrough = sourceRate == targetRate;
        _step = (double)sourceRate / targetRate;
    }

    public void AddSamples(ReadOnlySpan<float> samples)
    {
        for (var i = 0; i < samples.Length; i++) 
            _samples.Add(samples[i]);
    }

    public int ReadSamples(Span<short> destination)
    {
        if (destination.IsEmpty || _samples.Count == 0) return 0;

        if (_passthrough)
        {
            // 直接转换
            var directCount = Math.Min(destination.Length, _samples.Count);
            for (var i = 0; i < directCount; i++) 
                destination[i] = FloatToPcm16(_samples[i]);
            _samples.RemoveRange(0, directCount);
            return directCount;
        }

        // 线性插值重采样
        var outputCount = 0;
        while (outputCount < destination.Length)
        {
            var leftIndex = (int)_sourcePosition;
            var rightIndex = leftIndex + 1;
            if (rightIndex >= _samples.Count) break;

            var left = _samples[leftIndex];
            var right = _samples[rightIndex];
            var fraction = (float)(_sourcePosition - leftIndex);
            var interpolated = left + (right - left) * fraction;
            destination[outputCount++] = FloatToPcm16(interpolated);
            _sourcePosition += _step;
        }

        var consumed = Math.Min((int)_sourcePosition, _samples.Count);
        if (consumed > 0)
        {
            _samples.RemoveRange(0, consumed);
            _sourcePosition -= consumed;
        }
        return outputCount;
    }

    public void Clear()
    {
        _samples.Clear();
        _sourcePosition = 0;
    }

    private static short FloatToPcm16(float sample)
    {
        var clamped = Math.Clamp(sample, -1f, 1f);
        return (short)Math.Clamp((int)MathF.Round(clamped * short.MaxValue), short.MinValue, short.MaxValue);
    }
}
```

---

## Harmony Patch

### 启动注入

```csharp
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;

namespace VoiceToPlay.Voice.Patches;

[HarmonyPatch(typeof(NGame), nameof(NGame._Ready))]
internal static class NGameVoiceBootstrapPatch
{
    [HarmonyPostfix]
    private static void Postfix(NGame __instance)
    {
        ArgumentNullException.ThrowIfNull(__instance);
        
        if (__instance.GetNodeOrNull<VoiceEntryNode>("VoiceToPlayEntry") != null) return;

        var node = new VoiceEntryNode { Name = "VoiceToPlayEntry" };
        __instance.AddChild(node);
    }
}
```

### 退出清理

```csharp
[HarmonyPatch(typeof(NGame), nameof(NGame._ExitTree))]
internal static class NGameVoiceCleanupPatch
{
    [HarmonyPrefix]
    private static void Prefix(NGame __instance)
    {
        var node = __instance.GetNodeOrNull<VoiceEntryNode>("VoiceToPlayEntry");
        node?.DisposeServiceAndQueueFree();
    }
}
```

---

## Vosk 识别

### 初始化

```csharp
private Model? _model;
private VoskRecognizer? _recognizer;
private const int SampleRate = 16000;

private void InitializeVoskModel()
{
    Vosk.Vosk.SetLogLevel(-1);
    var modelPath = ResolveModelPath(); // 查找 models/vosk-model-small-cn-0.22
    _model = new Model(modelPath);
}
```

### 创建带词表的识别器

```csharp
private void UpdateRecognizer(IReadOnlySet<string> vocabulary)
{
    // 构建词表 JSON: ["词1", "词2", ... "[unk]"]
    var grammar = JsonSerializer.Serialize(vocabulary.Prepend("[unk]").ToList());
    // Kaldi 不支持 Unicode ，记得用 那个 Arg 取消 Json 转义
    _recognizer = new VoskRecognizer(_model, SampleRate, grammar);
}
```

### 处理音频

```csharp
private void ProcessAudio(byte[] pcmData, int length)
{
    if (_recognizer == null) return;

    if (_recognizer.AcceptWaveform(pcmData, length))
    {
        var resultJson = _recognizer.Result();
        var text = ExtractText(resultJson);
        // 派发命令
        _commandEngine.Process(text);
    }
    else
    {
        var partial = _recognizer.PartialResult();
        // 显示部分识别结果
    }
}
```

---

## Grammar - jieba 分词

### 初始化

```csharp
using JiebaNet.Segmenter;

internal sealed class JiebaTokenizer
{
    private static JiebaSegmenter? _segmenter;
    
    // 需要的资源文件
    private static readonly string[] ResourceFiles = 
    [
        "dict.txt", "idf.txt", "stopwords.txt",
        "char_state_tab.json", "prob_emit.json", "prob_trans.json"
    ];

    public bool TryInitialize()
    {
        var resourceDir = FindResourceDirectory();
        if (resourceDir != null)
            ConfigManager.ConfigFileBaseDir = resourceDir;
        
        _segmenter = new JiebaSegmenter();
        return true;
    }
    
    private string? FindResourceDirectory()
    {
        var candidates = new[]
        {
            Path.Combine(ModAssemblyResolver.ModDirectory, "Resources"),
            Path.Combine(AppContext.BaseDirectory, "Resources"),
        };
        return candidates.FirstOrDefault(Directory.Exists);
    }
}
```

### 分词 + 单字兜底

```csharp
// 将文本分词，返回词和单字的集合
public IReadOnlySet<string> Tokenize(string text)
{
    var result = new HashSet<string>(StringComparer.Ordinal);
    
    if (_segmenter == null)
    {
        // 降级：直接单字
        foreach (var c in text)
            if (!char.IsWhiteSpace(c))
                result.Add(c.ToString());
        return result;
    }
    
    // 分词
    foreach (var token in _segmenter.Cut(text, cutAll: false))
    {
        var normalized = VoiceText.Normalize(token);
        if (normalized.Length > 0)
            result.Add(normalized);
    }
    
    // 单字兜底（提高识别率）
    foreach (var c in text)
        if (!char.IsWhiteSpace(c))
            result.Add(c.ToString());
    
    return result;
}
```

---

## Grammar - 词表生成

```csharp
internal sealed class GrammarSession
{
    private readonly JiebaTokenizer _tokenizer = new();
    private HashSet<string> _activeWords = new();
    
    // 从命令引擎更新词表
    public string BuildGrammarJson(IReadOnlySet<string> commandWords)
    {
        var expandedWords = new HashSet<string> { "[unk]" };
        
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
        
        // 生成 JSON
        var sorted = expandedWords.OrderBy(w => w).ToList();
        return JsonSerializer.Serialize(sorted);
    }
}
```

---

## VoiceText - 文本规范化

```csharp
internal static class VoiceText
{
    // 规范化：去空白、统一大小写
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        return new string(text.Where(c => !char.IsWhiteSpace(c)).ToArray());
    }
    
    // 获取卡牌命令名（去掉特殊字符）
    public static string GetCardCommandName(string cardName)
    {
        return Normalize(cardName);
    }
}
```

---

## VoiceRecognitionService - 完整设计

```csharp
internal sealed class VoiceRecognitionService : IDisposable
{
    private const int SampleRate = 16000;
    private const int ReadFrames = 2048;
    
    private readonly VoiceCommandEngine _commandEngine;
    private readonly VoiceAudioCaptureService _audioCapture;
    private readonly GrammarSession _grammarSession;
    
    private Model? _model;
    private VoskRecognizer? _recognizer;
    
    public bool IsAvailable { get; private set; }
    public string? FatalError { get; private set; }
    
    public VoiceRecognitionService(Node owner, VoiceCommandEngine commandEngine)
    {
        _commandEngine = commandEngine;
        _grammarSession = new GrammarSession();
        _audioCapture = new VoiceAudioCaptureService(owner, SampleRate);
        
        // 订阅词表变化
        _commandEngine.VocabularyUpdated += OnVocabularyUpdated;
    }
    
    public void Initialize()
    {
        // 1. 检查音频输入
        if (!VoiceAudioCaptureService.IsAudioInputEnabled())
        {
            FatalError = VoiceAudioCaptureService.BuildAudioInputDisabledMessage();
            return;
        }
        
        // 2. 初始化麦克风
        if (!_audioCapture.TryInitialize(out var error))
        {
            FatalError = error;
            return;
        }
        
        // 3. 初始化 Vosk
        var modelPath = FindModelPath();
        if (modelPath == null)
        {
            FatalError = "Vosk model not found";
            return;
        }
        Vosk.Vosk.SetLogLevel(-1);
        _model = new Model(modelPath);
        
        // 4. 初始词表
        UpdateGrammar(_commandEngine.GetAllWords());
        
        IsAvailable = true;
    }
    
    public void Tick()
    {
        if (!IsAvailable) return;
        
        // 读取音频数据
        var frames = _audioCapture.FramesAvailable;
        if (frames <= 0) return;
        
        var stereoData = _audioCapture.CaptureEffect.GetBuffer(Math.Min(frames, ReadFrames));
        
        // 混音为 mono
        var monoData = new float[stereoData.Length];
        for (int i = 0; i < stereoData.Length; i++)
            monoData[i] = (stereoData[i].X + stereoData[i].Y) * 0.5f;
        
        // 重采样
        _audioCapture.Resampler.AddSamples(monoData);
        
        // 读取 PCM16
        var pcmBuffer = new short[8192];
        var pcmCount = _audioCapture.Resampler.ReadSamples(pcmBuffer);
        if (pcmCount <= 0) return;
        
        var pcmBytes = new byte[pcmCount * 2];
        Buffer.BlockCopy(pcmBuffer, 0, pcmBytes, 0, pcmCount * 2);
        
        // 识别
        ProcessAudio(pcmBytes);
    }
    
    private void ProcessAudio(byte[] pcmData)
    {
        if (_recognizer == null) return;
        
        if (_recognizer.AcceptWaveform(pcmData, pcmData.Length))
        {
            var json = _recognizer.Result();
            var text = ExtractText(json);
            if (!string.IsNullOrEmpty(text))
                _commandEngine.Process(text);
        }
    }
    
    private void OnVocabularyUpdated(IReadOnlySet<string> words)
    {
        UpdateGrammar(words);
    }
    
    private void UpdateGrammar(IReadOnlySet<string> words)
    {
        var grammarJson = _grammarSession.BuildGrammarJson(words);
        if (string.IsNullOrEmpty(grammarJson)) return;
        
        _recognizer?.Dispose();
        _recognizer = new VoskRecognizer(_model, SampleRate, grammarJson);
    }
    
    private static string? FindModelPath()
    {
        var candidates = new[]
        {
            Path.Combine(ModAssemblyResolver.ModDirectory, "models", "vosk-model-small-cn-0.22"),
            Path.Combine(AppContext.BaseDirectory, "models", "vosk-model-small-cn-0.22"),
        };
        return candidates.FirstOrDefault(Directory.Exists);
    }
    
    private static string ExtractText(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("text", out var el) 
            ? el.GetString() ?? "" 
            : "";
    }
    
    public void Dispose()
    {
        _recognizer?.Dispose();
        _model?.Dispose();
        _audioCapture.Dispose();
    }
}
```

---

## VoiceEntryNode - 组装

```csharp
using Godot;

namespace VoiceToPlay.Voice;

internal sealed partial class VoiceEntryNode : Node
{
    private const string NodeName = "VoiceToPlayEntry";
    
    private VoiceCommandEngine? _commandEngine;
    private VoiceRecognitionService? _recognitionService;
    
    private Label? _statusLabel;
    private bool _listening = true;
    
    public override void _Ready()
    {
        CreateUI();
        
        // 1. 创建命令引擎
        _commandEngine = new VoiceCommandEngine();
        
        // 2. 注册命令
        _commandEngine.Register(new EndTurnCommand());
        _commandEngine.Register(new PlayCardCommand());
        // ... 其他命令
        
        // 3. 创建识别服务
        _recognitionService = new VoiceRecognitionService(this, _commandEngine);
        _recognitionService.Initialize();
        
        if (!_recognitionService.IsAvailable)
        {
            MainFile.Logger.Error($"Voice init failed: {_recognitionService.FatalError}");
            UpdateStatus("错误");
            return;
        }
        
        UpdateStatus("开");
        MainFile.Logger.Info("Voice entry ready. F8 to toggle.");
    }
    
    public override void _Process(double delta)
    {
        _recognitionService?.Tick();
    }
    
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Keycode: Key.F8 })
        {
            _listening = !_listening;
            UpdateStatus(_listening ? "开" : "关");
            GetViewport().SetInputAsHandled();
        }
    }
    
    private void CreateUI()
    {
        _statusLabel = new Label
        {
            Text = "语音: ...",
            HorizontalAlignment = HorizontalAlignment.Right,
            OffsetLeft = -200f,
            OffsetRight = -20f,
            OffsetTop = 20f,
            OffsetBottom = 50f,
        };
        _statusLabel.AnchorLeft = 1f;
        _statusLabel.AnchorRight = 1f;
        _statusLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _statusLabel.AddThemeConstantOverride("outline_size", 4);
        AddChild(_statusLabel);
    }
    
    private void UpdateStatus(string status)
    {
        if (_statusLabel == null) return;
        _statusLabel.Text = $"语音: {status}";
        _statusLabel.AddThemeColorOverride("font_color", 
            status == "开" ? new Color("9EF9A6") : new Color("FF9E9E"));
    }
    
    public void DisposeServiceAndQueueFree()
    {
        _recognitionService?.Dispose();
        if (IsInstanceValid(this) && IsInsideTree())
            QueueFree();
    }
}
```