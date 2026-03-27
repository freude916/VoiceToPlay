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

    /// <summary>
    ///     是否启用音频播放抑制。设为 false 可调试麦克风回放。
    /// </summary>
    private const bool EnablePlayback = true;

    private readonly Node _owner;
    private readonly int _targetSampleRate;
    private int _busIndex = -1;

    private AudioStreamPlayer? _microphonePlayer;

    public VoiceAudioCaptureService(Node owner, int targetSampleRate = 16000)
    {
        _owner = owner;
        _targetSampleRate = targetSampleRate;
        var inputRate = Math.Max(1, (int)MathF.Round(AudioServer.GetMixRate()));
        Resampler = new LinearPcm16Resampler(inputRate, targetSampleRate);
    }

    public AudioEffectCapture? CaptureEffect { get; private set; }
    public LinearPcm16Resampler Resampler { get; }
    public int FramesAvailable => CaptureEffect?.GetFramesAvailable() ?? 0;

    /// <summary>
    ///     当前输入设备名称
    /// </summary>
    public string CurrentInputDevice => AudioServer.GetInputDevice();

    public void Dispose()
    {
        _microphonePlayer?.Stop();
        if (GodotObject.IsInstanceValid(_microphonePlayer))
            _microphonePlayer.QueueFree();

        // 清理 bus effect
        if (_busIndex >= 0)
        {
            var effectCount = AudioServer.GetBusEffectCount(_busIndex);
            for (var i = effectCount - 1; i >= 0; i--)
                if (AudioServer.GetBusEffect(_busIndex, i) is AudioEffectCapture)
                    AudioServer.RemoveBusEffect(_busIndex, i);
        }
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
        _busIndex = EnsureCaptureBus();

        // 2. 安装 AudioEffectCapture
        CaptureEffect = GetOrCreateCaptureEffect(_busIndex);
        if (CaptureEffect != null) CaptureEffect.BufferLength = 1.0f;

        // 3. 创建 AudioStreamMicrophone
        RecreateMicrophonePlayer();

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
    }

    private int EnsureCaptureBus()
    {
        var busIndex = AudioServer.GetBusIndex(CaptureBusName);
        if (busIndex < 0)
        {
            AudioServer.AddBus();
            busIndex = AudioServer.BusCount - 1;
            AudioServer.SetBusName(busIndex, CaptureBusName);
        }

        AudioServer.SetBusVolumeDb(busIndex, 0f);

        if (!EnablePlayback)
        {
            // 创建 sink 总线接收音频，防止意外路由到输出
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
        }

        AudioServer.SetBusMute(busIndex, false);

        return busIndex;
    }

    private static AudioEffectCapture? GetOrCreateCaptureEffect(int busIndex)
    {
        var effectCount = AudioServer.GetBusEffectCount(busIndex);
        for (var i = 0; i < effectCount; i++)
            if (AudioServer.GetBusEffect(busIndex, i) is AudioEffectCapture existing)
                return existing;

        var capture = new AudioEffectCapture();
        AudioServer.AddBusEffect(busIndex, capture, 0);
        return capture;
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