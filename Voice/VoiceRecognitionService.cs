using System.Collections.Concurrent;
using System.Text.Json;
using Godot;
using VoiceToPlay.Voice.Audio;
using VoiceToPlay.Voice.Core;
using VoiceToPlay.Voice.Grammar;
using Vosk;

namespace VoiceToPlay.Voice;

/// <summary>
///     Vosk 语音识别服务。整合音频捕获、重采样、识别和命令派发。
///     使用后台线程进行识别，避免阻塞主线程。
/// </summary>
internal sealed class VoiceRecognitionService : IDisposable
{
    // 调试开关：记录 Vosk 的 partial 和 final 结果
    private const bool DebugRecognition = true;

    // Partial 超时：静音超过此时间后清空误识别的 partial
    private const float PartialTimeoutSeconds = 1.5f;

    private const int SampleRate = 16000;
    private const int ReadFrames = 2048;
    private const float PeakDecayRate = 0.92f;
    private readonly VoiceAudioCaptureService _audioCapture;

    // 异步识别
    private readonly BlockingCollection<byte[]> _audioQueue = new(100);

    private readonly VoiceCommandEngine _commandEngine;
    private readonly GrammarSession _grammarSession;
    private readonly Node _owner;

    // 缓冲区 (主线程专用)
    private readonly short[] _pcmBuffer = new short[8192];
    private readonly object _recognizerLock = new();

    // 结果队列 (后台线程 -> 主线程)
    private readonly ConcurrentQueue<RecognitionResult> _resultQueue = new();
    private string _lastPartialText = string.Empty;  // 主线程用
    private string _lastPublishedText = string.Empty;

    // Partial 超时检测（后台线程专用，无需锁）
    private string _lastPartialTextInThread = string.Empty;
    private DateTime _lastPartialChangeTime = DateTime.UtcNow;

    // 状态
    private bool _listeningEnabled = true;

    private Model? _model;
    private float[] _monoBuffer = Array.Empty<float>();
    private Thread? _recognitionThread;
    private VoskRecognizer? _recognizer;
    private volatile bool _running;

    public VoiceRecognitionService(Node owner, VoiceCommandEngine commandEngine)
    {
        _owner = owner;
        _commandEngine = commandEngine;
        _grammarSession = new GrammarSession();
        _audioCapture = new VoiceAudioCaptureService(owner);

        // 订阅词表变化
        _commandEngine.VocabularyUpdated += OnVocabularyUpdated;
    }

    public bool IsAvailable { get; private set; }
    public string? FatalError { get; private set; }

    /// <summary>
    ///     当前音量峰值 (0-1)
    /// </summary>
    public float LastPeakAmplitude { get; private set; }

    /// <summary>
    ///     当前输入设备名称
    /// </summary>
    public static string CurrentInputDevice => VoiceAudioCaptureService.CurrentInputDevice;

    /// <summary>
    ///     设置音频效果参数
    /// </summary>
    /// <param name="highPassCutoffHz">高通滤波器截止频率 (Hz)</param>
    /// <param name="gainDb">增益 (dB)</param>
    public void SetAudioEffects(float highPassCutoffHz, float gainDb)
    {
        _audioCapture.SetHighPassCutoff(highPassCutoffHz);
        _audioCapture.SetGainDb(gainDb);
    }

    public void Dispose()
    {
        _running = false;

        // 清空引用，防止后台线程继续使用
        lock (_recognizerLock)
        {
            _recognizer = null;
        }

        _audioQueue.CompleteAdding();

        // 不 Dispose _model 和 _recognizer，因为：
        // 1. 后台线程可能还在用
        // 2. 进程退出时 OS 会自动回收
        // 如果需要重新初始化，OnVocabularyUpdated 会重建

        _audioCapture.Dispose();
    }

    /// <summary>
    ///     识别文本变化事件
    /// </summary>
    public event Action<string>? RecognitionTextChanged;

    /// <summary>
    ///     初始化服务
    /// </summary>
    public void Initialize()
    {
        // 1. 确保 Vosk 程序集已加载
        if (!ModAssemblyResolver.EnsureLoaded("Vosk"))
        {
            FatalError = $"Vosk assembly not found or failed to load from '{ModAssemblyResolver.ModDirectory}'.";
            return;
        }

        // 2. 检查音频输入
        if (!VoiceAudioCaptureService.IsAudioInputEnabled())
        {
            FatalError = VoiceAudioCaptureService.BuildAudioInputDisabledMessage();
            return;
        }

        // 3. 初始化麦克风
        if (!_audioCapture.TryInitialize(out var error))
        {
            FatalError = error;
            return;
        }

        // 4. 初始化 Vosk
        var modelPath = FindModelPath();
        if (modelPath == null)
        {
            FatalError = "Vosk model not found: models/vosk-model-small-cn-0.22";
            return;
        }

        Vosk.Vosk.SetLogLevel(-1);
        _model = new Model(modelPath);

        // 5. 初始词表
        UpdateGrammar(_commandEngine.GetAllWords());

        // 6. 启动识别线程
        StartRecognitionThread();

        IsAvailable = true;
    }

    /// <summary>
    ///     设置监听状态
    /// </summary>
    public void SetListeningEnabled(bool enabled)
    {
        if (_listeningEnabled == enabled) return;

        _listeningEnabled = enabled;
        MainFile.Logger.Info($"Voice listening: {(enabled ? "enabled" : "disabled")}");

        if (enabled) return;
        
        LastPeakAmplitude = 0f;
        _lastPartialText = string.Empty;
        PublishRecognitionText(string.Empty, true);
        _audioCapture.ClearTransientBuffers();
        ClearAudioQueue();
    }

    /// <summary>
    ///     每帧调用，处理音频采集和结果分发
    /// </summary>
    public void Tick()
    {
        if (!IsAvailable || !_listeningEnabled) return;

        // 峰值衰减
        LastPeakAmplitude *= PeakDecayRate;

        // 处理后台线程的结果
        ProcessResults();

        // 采集音频
        CaptureAudio();

        // 处理 Jitter Buffer 播放
        _audioCapture.PlaybackService?.Tick();
    }

    private void CaptureAudio()
    {
        var capture = _audioCapture.CaptureEffect;
        if (capture == null) return;

        var framesAvailable = _audioCapture.FramesAvailable;
        if (framesAvailable <= 0) return;

        var framesToRead = Math.Min(framesAvailable, ReadFrames);
        var stereoData = capture.GetBuffer(framesToRead);
        if (stereoData.Length == 0) return;

        // 喂给 Jitter Buffer 播放器
        _audioCapture.PlaybackService?.FeedAudio(stereoData);

        // 混音为 mono，同时计算峰值
        EnsureMonoBuffer(stereoData.Length);
        var peakInBatch = 0f;
        for (var i = 0; i < stereoData.Length; i++)
        {
            var mono = (stereoData[i].X + stereoData[i].Y) * 0.5f;
            _monoBuffer[i] = mono;
            var abs = MathF.Abs(mono);
            if (abs > peakInBatch) peakInBatch = abs;
        }

        LastPeakAmplitude = MathF.Max(peakInBatch, LastPeakAmplitude);

        // 重采样并发送到队列
        _audioCapture.Resampler.AddSamples(_monoBuffer.AsSpan(0, stereoData.Length));
        DrainResampledPcmToQueue();
    }

    private void DrainResampledPcmToQueue()
    {
        while (true)
        {
            var sampleCount = _audioCapture.Resampler.ReadSamples(_pcmBuffer);
            if (sampleCount <= 0) break;

            var byteCount = sampleCount * sizeof(short);
            var data = new byte[byteCount];
            Buffer.BlockCopy(_pcmBuffer, 0, data, 0, byteCount);

            if (!_audioQueue.TryAdd(data))
            {
                // 队列满了，丢弃最旧的数据
                _audioQueue.TryTake(out _);
                _audioQueue.TryAdd(data);
            }
        }
    }

    private void ProcessResults()
    {
        while (_resultQueue.TryDequeue(out var result))
        {
            if (result.IsFinal)
            {
                var text = ExtractText(result.Json);
                if (!string.IsNullOrEmpty(text))
                {
                    PublishRecognitionText(text, true);
                    MainFile.Logger.Info($"Voice recognized: {text}");
                    _commandEngine.Process(text);
                }
            }
            else
            {
                var partial = ExtractPartialText(result.Json);
                if (partial == _lastPartialText) continue;

                if (DebugRecognition)
                    MainFile.Logger.Info($"[DEBUG] Partial changed: '{_lastPartialText}' -> '{partial}'");

                // 允许空 partial 清空 UI（超时丢弃）
                _lastPartialText = partial;
                PublishRecognitionText(partial);
            }
        }
    }

    private void StartRecognitionThread()
    {
        _running = true;
        _recognitionThread = new Thread(RecognitionLoop)
        {
            Name = "VoskRecognition",
            IsBackground = true
        };
        _recognitionThread.Start();
    }

    private void RecognitionLoop()
    {
        MainFile.Logger.Info("Voice recognition thread started");

        try
        {
            foreach (var data in _audioQueue.GetConsumingEnumerable())
            {
                if (!_running) break;

                // 检查 partial 超时：如果 partial 非空且超时，发送清空信号
                var now = DateTime.UtcNow;
                if (!string.IsNullOrEmpty(_lastPartialTextInThread) &&
                    (now - _lastPartialChangeTime).TotalSeconds > PartialTimeoutSeconds)
                {
                    if (DebugRecognition)
                        MainFile.Logger.Info($"[DEBUG] Partial timeout, clearing: '{_lastPartialTextInThread}'");
                    _lastPartialTextInThread = string.Empty;
                    _resultQueue.Enqueue(new RecognitionResult("{\"partial\":\"\"}", false));
                }

                VoskRecognizer? recognizer;
                lock (_recognizerLock)
                {
                    recognizer = _recognizer;
                }

                if (recognizer == null) continue;

                bool isFinal;
                string json;
                lock (recognizer)
                {
                    isFinal = recognizer.AcceptWaveform(data, data.Length);
                    json = isFinal ? recognizer.Result() : recognizer.PartialResult();
                }

                // 更新 partial 变化时间（用于超时检测）
                if (!isFinal)
                {
                    var partial = ExtractPartialText(json);
                    if (partial != _lastPartialTextInThread)
                    {
                        _lastPartialTextInThread = partial;
                        _lastPartialChangeTime = now;
                    }
                }

                var result = new RecognitionResult(json, isFinal);
                _resultQueue.Enqueue(result);
            }
        }
        catch (Exception ex)
        {
            MainFile.Logger.Error($"Voice recognition thread error: {ex}");
        }

        MainFile.Logger.Info("Voice recognition thread stopped");
    }

    private void ClearAudioQueue()
    {
        while (_audioQueue.TryTake(out _))
        {
        }
    }

    private void OnVocabularyUpdated(IReadOnlySet<string> words)
    {
        UpdateGrammar(words);
    }

    private void UpdateGrammar(IReadOnlySet<string> words)
    {
        if (!_running) return;

        var grammarJson = _grammarSession.BuildGrammarJson(words);
        if (string.IsNullOrEmpty(grammarJson))
        {
            MainFile.Logger.Info("VoiceRecognitionService: grammar unchanged, skip update");
            return;
        }

        lock (_recognizerLock)
        {
            if (!_running) return;
            _recognizer?.Dispose();
            if (!_running) return;
            _recognizer = new VoskRecognizer(_model, SampleRate, grammarJson);
        }

        _lastPartialText = string.Empty;
    }

    private void PublishRecognitionText(string text, bool force = false)
    {
        if (!force && text == _lastPublishedText) return;

        _lastPublishedText = text;
        RecognitionTextChanged?.Invoke(text);
    }

    private void EnsureMonoBuffer(int requiredLength)
    {
        if (_monoBuffer.Length >= requiredLength) return;
        _monoBuffer = new float[requiredLength];
    }

    private static string? FindModelPath()
    {
        var modDirectory = ModAssemblyResolver.ModDirectory;
        var candidates = new[]
        {
            Path.Combine(modDirectory, "models", "vosk-model-small-cn-0.22"),
            Path.Combine(AppContext.BaseDirectory, "models", "vosk-model-small-cn-0.22"),
            Path.Combine(AppContext.BaseDirectory, "..", "models", "vosk-model-small-cn-0.22")
        };
        return candidates.FirstOrDefault(Directory.Exists);
    }

    private static string ExtractText(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("text", out var el)
                ? el.GetString() ?? ""
                : "";
        }
        catch
        {
            return "";
        }
    }

    private static string ExtractPartialText(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("partial", out var el)
                ? el.GetString() ?? ""
                : "";
        }
        catch
        {
            return "";
        }
    }

    private readonly record struct RecognitionResult(string Json, bool IsFinal);
}