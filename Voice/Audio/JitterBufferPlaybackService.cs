using Godot;

namespace VoiceToPlay.Voice.Audio;

/// <summary>
///     Jitter Buffer 播放服务。使用 AudioStreamGeneratorPlayback 实现平滑的麦克风回放。
///     通过初始缓冲和动态缓冲来解决 AudioStreamMicrophone 直接播放的断续问题。
/// </summary>
internal sealed class JitterBufferPlaybackService : IDisposable
{
    /// <summary>
    ///     最小缓冲帧数（约 90ms @ 44.1kHz），用于吸收采集抖动
    /// </summary>
    private const int MinBufferFrames = 2048;

    /// <summary>
    ///     最大缓冲帧数，超过则丢弃旧数据
    /// </summary>
    private const int MaxBufferFrames = 16384;

    /// <summary>
    ///     AudioStreamGenerator 内部缓冲区长度（秒）
    /// </summary>
    private const float GeneratorBufferLength = 0.5f;

    private readonly Queue<Vector2[]> _jitterBuffer = new();
    private readonly Node _owner;

    private AudioStreamPlayer? _generatorPlayer;
    private AudioStreamGeneratorPlayback? _playback;

    public JitterBufferPlaybackService(Node owner)
    {
        _owner = owner;
    }

    /// <summary>
    ///     是否处于缓冲状态
    /// </summary>
    public bool IsBuffering { get; private set; } = true;

    /// <summary>
    ///     当前缓冲帧数
    /// </summary>
    public int BufferedFrames { get; private set; }

    /// <summary>
    ///     Buffer underrun 次数
    /// </summary>
    public int SkipCount { get; private set; }

    public void Dispose()
    {
        _generatorPlayer?.Stop();
        if (GodotObject.IsInstanceValid(_generatorPlayer))
            _generatorPlayer.QueueFree();
        _generatorPlayer = null;
        _playback = null;
    }

    /// <summary>
    ///     初始化播放器
    /// </summary>
    /// <param name="outputBusName">输出音频总线名称</param>
    public void Initialize(string outputBusName)
    {
        var generator = new AudioStreamGenerator
        {
            MixRateMode = AudioStreamGenerator.AudioStreamGeneratorMixRate.Input,
            BufferLength = GeneratorBufferLength
        };

        _generatorPlayer = new AudioStreamPlayer
        {
            Name = "JitterBufferPlayback",
            Stream = generator,
            Bus = outputBusName,
            VolumeDb = 0f,
            Autoplay = false
        };

        _owner.AddChild(_generatorPlayer);
        _generatorPlayer.Play();

        // 获取 playback 实例
        _playback = (AudioStreamGeneratorPlayback)_generatorPlayer.GetStreamPlayback();
    }

    /// <summary>
    ///     清空缓冲区并重置状态
    /// </summary>
    public void Clear()
    {
        _jitterBuffer.Clear();
        BufferedFrames = 0;
        IsBuffering = true;
        _playback?.ClearBuffer();
    }

    /// <summary>
    ///     喂入音频数据。应在主线程调用。
    /// </summary>
    /// <param name="frames">Stereo 音频帧</param>
    public void FeedAudio(Vector2[] frames)
    {
        if (frames.Length == 0) return;

        // 限制最大缓冲
        while (BufferedFrames + frames.Length > MaxBufferFrames && _jitterBuffer.Count > 0)
        {
            var old = _jitterBuffer.Dequeue();
            BufferedFrames -= old.Length;
        }

        _jitterBuffer.Enqueue(frames);
        BufferedFrames += frames.Length;

        // 缓冲足够后退出缓冲状态
        if (IsBuffering && BufferedFrames >= MinBufferFrames)
            IsBuffering = false;
    }

    /// <summary>
    ///     每帧调用，推送数据到播放器。应在主线程调用。
    /// </summary>
    public void Tick()
    {
        if (_playback == null || IsBuffering) return;

        var framesAvailable = _playback.GetFramesAvailable();
        if (framesAvailable <= 0) return;

        // 记录 underrun
        var skips = _playback.GetSkips();
        if (skips > SkipCount)
        {
            SkipCount = skips;
            MainFile.Logger.Warn($"JitterBufferPlayback: buffer underrun detected (skips={skips})");
        }

        // 推送数据
        while (_jitterBuffer.Count > 0 && framesAvailable > 0)
        {
            var frames = _jitterBuffer.Peek();
            if (frames.Length > framesAvailable)
            {
                // 部分推送：拆分帧
                var partial = new Vector2[framesAvailable];
                var remaining = new Vector2[frames.Length - framesAvailable];

                for (var i = 0; i < framesAvailable; i++)
                    partial[i] = frames[i];
                for (var i = 0; i < remaining.Length; i++)
                    remaining[i] = frames[framesAvailable + i];

                _playback.PushBuffer(partial);
                _jitterBuffer.Dequeue();
                _jitterBuffer.Enqueue(remaining);
                BufferedFrames -= framesAvailable;
                break;
            }

            _playback.PushBuffer(frames);
            framesAvailable -= frames.Length;
            BufferedFrames -= frames.Length;
            _jitterBuffer.Dequeue();
        }

        // 缓冲不足，重新进入缓冲状态
        if (BufferedFrames >= MinBufferFrames / 2 || IsBuffering) return;
        IsBuffering = true;
        // MainFile.Logger.Info("JitterBufferPlayback: re-buffering...");
        // Too easy to enter rebuffering :angry: , be silent then
    }
}