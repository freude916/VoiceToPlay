using Godot;
using Environment = System.Environment;

namespace VoiceToPlay.Voice.Audio;

/// <summary>
///     麦克风音频捕获服务。处理 Godot AudioEffectCapture 和 AudioStreamMicrophone。
/// </summary>
internal sealed class VoiceAudioCaptureService : IDisposable
{
    private const string CaptureBusName = "VoiceToPlayCapture";
    private const string SinkBusName = "VoiceToPlayCaptureSink";
    private const string PlaybackBusName = "VoiceToPlayPlayback";

    /// <summary>
    ///     是否启用 Jitter Buffer 回放。
    /// </summary>
    private const bool EnablePlayback = true;

    private readonly Node _owner;
    private readonly int _targetSampleRate;

    private AudioStreamPlayer? _microphonePlayer;

    public VoiceAudioCaptureService(Node owner, int targetSampleRate = 16000)
    {
        _owner = owner;
        _targetSampleRate = targetSampleRate;
        var inputRate = Math.Max(1, (int)MathF.Round(AudioServer.GetMixRate()));
        Resampler = new LinearPcm16Resampler(inputRate, targetSampleRate);
    }

    public AudioEffectCapture? CaptureEffect { get; private set; }
    public AudioEffectHighPassFilter? HighPassFilter { get; private set; }
    public AudioEffectLowPassFilter? LowPassFilter { get; private set; }
    public AudioEffectAmplify? AmplifyEffect { get; private set; }
    public AudioEffectSpectrumAnalyzer? SpectrumAnalyzer { get; private set; }
    public LinearPcm16Resampler Resampler { get; }
    public int FramesAvailable => CaptureEffect?.GetFramesAvailable() ?? 0;
    public int BusIndex { get; private set; } = -1;

    /// <summary>
    ///     Jitter Buffer 播放服务，用于回放采集的音频。
    /// </summary>
    public JitterBufferPlaybackService? PlaybackService { get; private set; }

    /// <summary>
    ///     当前输入设备名称
    /// </summary>
    public static string CurrentInputDevice => AudioServer.GetInputDevice();

    public void Dispose()
    {
        _microphonePlayer?.Stop();
        if (GodotObject.IsInstanceValid(_microphonePlayer))
            _microphonePlayer.QueueFree();

        PlaybackService?.Dispose();
        PlaybackService = null;

        // 清理 bus effect
        if (BusIndex >= 0)
        {
            var effectCount = AudioServer.GetBusEffectCount(BusIndex);
            for (var i = effectCount - 1; i >= 0; i--)
                if (AudioServer.GetBusEffect(BusIndex, i) is AudioEffectCapture)
                    AudioServer.RemoveBusEffect(BusIndex, i);
        }
    }

    /// <summary>
    ///     设置高通滤波器截止频率 (Hz)
    /// </summary>
    public void SetHighPassCutoff(float hz)
    {
        if (HighPassFilter != null)
            HighPassFilter.CutoffHz = Math.Clamp(hz, 20f, 500f);
    }

    /// <summary>
    ///     设置低通滤波器截止频率 (Hz)
    /// </summary>
    public void SetLowPassCutoff(float hz)
    {
        if (LowPassFilter != null)
            LowPassFilter.CutoffHz = Math.Clamp(hz, 1000f, 8000f);
    }

    /// <summary>
    ///     设置增益 (dB)
    /// </summary>
    public void SetGainDb(float db)
    {
        if (AmplifyEffect != null)
            AmplifyEffect.VolumeDb = Math.Clamp(db, -20f, 20f);
    }

    /// <summary>
    ///     检查 Godot 音频输入是否启用
    /// </summary>
    public static bool IsAudioInputEnabled()
    {
        var setting = ProjectSettings.GetSetting("audio/driver/enable_input", false);
        return setting.AsBool();
    }

    /// <summary>
    ///     构建音频输入禁用错误提示
    /// </summary>
    public static string BuildAudioInputDisabledMessage()
    {
        var overrideCfgPath = Path.Combine(
            Path.GetDirectoryName(Environment.ProcessPath) ?? ".",
            "override.cfg");
        return
            $"Godot audio input is disabled (audio/driver/enable_input=false). " +
            $"Create '{overrideCfgPath}' with:\n[audio]\ndriver/enable_input=true\nThen restart game.";
    }

    /// <summary>
    ///     初始化麦克风捕获
    /// </summary>
    public bool TryInitialize(out string? error)
    {
        error = null;

        // 1. 创建 Capture Bus
        BusIndex = EnsureCaptureBus();

        // 2. 安装音频效果：高通滤波器 -> 低通滤波器 -> 增益 -> 频谱分析 -> 捕获
        HighPassFilter = GetOrCreateHighPassFilter(BusIndex);
        LowPassFilter = GetOrCreateLowPassFilter(BusIndex);
        AmplifyEffect = GetOrCreateAmplifyEffect(BusIndex);
        SpectrumAnalyzer = GetOrCreateSpectrumAnalyzer(BusIndex);
        CaptureEffect = GetOrCreateCaptureEffect(BusIndex);
        if (CaptureEffect != null) CaptureEffect.BufferLength = 1.0f;

        // 3. 创建 AudioStreamMicrophone
        RecreateMicrophonePlayer();

        // 4. 创建 Jitter Buffer 播放服务
        // ReSharper disable once HeuristicUnreachableCode
#pragma warning disable CS0162 // Temp Debug Bool
        if (EnablePlayback)
        {
            EnsurePlaybackBus();
            PlaybackService = new JitterBufferPlaybackService(_owner);
            PlaybackService.Initialize(PlaybackBusName);
        }
#pragma warning restore CS0162

        if (CaptureEffect == null)
        {
            error = "Failed to create AudioEffectCapture";
            return false;
        }

        if (_microphonePlayer == null)
        {
            error = "Failed to create AudioStreamMicrophone";
            return false;
        }

        return true;
    }

    /// <summary>
    ///     清空缓冲区
    /// </summary>
    public void ClearTransientBuffers()
    {
        CaptureEffect?.ClearBuffer();
        Resampler.Clear();
        PlaybackService?.Clear();
    }

    private static int EnsureCaptureBus()
    {
        var busIndex = AudioServer.GetBusIndex(CaptureBusName);
        if (busIndex < 0)
        {
            AudioServer.AddBus();
            busIndex = AudioServer.BusCount - 1;
            AudioServer.SetBusName(busIndex, CaptureBusName);
        }

        AudioServer.SetBusVolumeDb(busIndex, 0f);

        // 创建 sink 总线接收音频，防止意外路由到输出
        // CaptureBus 始终静音，播放由 JitterBufferPlaybackService 处理
        var sinkBusIndex = AudioServer.GetBusIndex(SinkBusName);
        if (sinkBusIndex < 0)
        {
            AudioServer.AddBus();
            sinkBusIndex = AudioServer.BusCount - 1;
            AudioServer.SetBusName(sinkBusIndex, SinkBusName);
        }

        AudioServer.SetBusMute(sinkBusIndex, true);
        AudioServer.SetBusVolumeDb(sinkBusIndex, 0f);

        // 静音捕获总线并路由到 sink
        AudioServer.SetBusMute(busIndex, true);
        var currentSend = AudioServer.GetBusSend(busIndex).ToString();
        if (!string.Equals(currentSend, SinkBusName, StringComparison.Ordinal))
            AudioServer.SetBusSend(busIndex, SinkBusName);

        return busIndex;
    }

    private static void EnsurePlaybackBus()
    {
        var busIndex = AudioServer.GetBusIndex(PlaybackBusName);
        if (busIndex < 0)
        {
            AudioServer.AddBus();
            busIndex = AudioServer.BusCount - 1;
            AudioServer.SetBusName(busIndex, PlaybackBusName);
        }

        AudioServer.SetBusVolumeDb(busIndex, 0f);
        AudioServer.SetBusMute(busIndex, false);
        // 默认发送到 Master
    }

    private static AudioEffectCapture? GetOrCreateCaptureEffect(int busIndex)
    {
        var effectCount = AudioServer.GetBusEffectCount(busIndex);
        for (var i = 0; i < effectCount; i++)
            if (AudioServer.GetBusEffect(busIndex, i) is AudioEffectCapture existing)
                return existing;

        var capture = new AudioEffectCapture();
        AudioServer.AddBusEffect(busIndex, capture);
        return capture;
    }

    private static AudioEffectHighPassFilter? GetOrCreateHighPassFilter(int busIndex)
    {
        var effectCount = AudioServer.GetBusEffectCount(busIndex);
        for (var i = 0; i < effectCount; i++)
            if (AudioServer.GetBusEffect(busIndex, i) is AudioEffectHighPassFilter existing)
                return existing;

        var filter = new AudioEffectHighPassFilter { CutoffHz = 80f };
        AudioServer.AddBusEffect(busIndex, filter, 0);
        return filter;
    }

    private static AudioEffectLowPassFilter? GetOrCreateLowPassFilter(int busIndex)
    {
        var effectCount = AudioServer.GetBusEffectCount(busIndex);
        for (var i = 0; i < effectCount; i++)
            if (AudioServer.GetBusEffect(busIndex, i) is AudioEffectLowPassFilter existing)
                return existing;

        var filter = new AudioEffectLowPassFilter { CutoffHz = 4000f };
        AudioServer.AddBusEffect(busIndex, filter, 1);
        return filter;
    }

    private static AudioEffectAmplify? GetOrCreateAmplifyEffect(int busIndex)
    {
        var effectCount = AudioServer.GetBusEffectCount(busIndex);
        for (var i = 0; i < effectCount; i++)
            if (AudioServer.GetBusEffect(busIndex, i) is AudioEffectAmplify existing)
                return existing;

        var amplify = new AudioEffectAmplify { VolumeDb = 0f };
        AudioServer.AddBusEffect(busIndex, amplify, 2);
        return amplify;
    }

    private static AudioEffectSpectrumAnalyzer? GetOrCreateSpectrumAnalyzer(int busIndex)
    {
        var effectCount = AudioServer.GetBusEffectCount(busIndex);
        for (var i = 0; i < effectCount; i++)
            if (AudioServer.GetBusEffect(busIndex, i) is AudioEffectSpectrumAnalyzer existing)
                return existing;

        var spectrum = new AudioEffectSpectrumAnalyzer
        {
            BufferLength = 0.1f,
            FftSize = AudioEffectSpectrumAnalyzer.FftSizeEnum.Size256
        };
        AudioServer.AddBusEffect(busIndex, spectrum, 3);
        return spectrum;
    }

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
}