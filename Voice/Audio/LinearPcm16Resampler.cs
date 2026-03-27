namespace VoiceToPlay.Voice.Audio;

/// <summary>
///     线性插值 PCM16 重采样器。将任意采样率转换为 16kHz。
/// </summary>
internal sealed class LinearPcm16Resampler
{
    private readonly bool _passthrough;
    private readonly List<float> _samples = [];
    private readonly double _step;
    private double _sourcePosition;

    public LinearPcm16Resampler(int sourceRate, int targetRate)
    {
        _passthrough = sourceRate == targetRate;
        _step = (double)sourceRate / targetRate;
    }

    /// <summary>
    ///     添加采样数据
    /// </summary>
    public void AddSamples(ReadOnlySpan<float> samples)
    {
        for (var i = 0; i < samples.Length; i++)
            _samples.Add(samples[i]);
    }

    /// <summary>
    ///     读取重采样后的 PCM16 数据
    /// </summary>
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

    /// <summary>
    ///     清空缓冲区
    /// </summary>
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