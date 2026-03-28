using Godot;

namespace VoiceToPlay.Voice.UI;

/// <summary>
///     语音调试面板。显示识别状态、结果和音量。
/// </summary>
public sealed partial class VoiceDebugPanel : PanelContainer
{
    /// <summary>
    ///     是否显示音频效果调试控件
    /// </summary>
    private const bool DebugAudioEffects = true;

    /// <summary>
    ///     面板默认宽度
    /// </summary>
    private const float PanelWidth = 300f;

    /// <summary>
    ///     面板距离右侧的距离
    /// </summary>
    private const float MarginRight = 50f;

    /// <summary>
    ///     面板距离顶部的距离
    /// </summary>
    private const float MarginTop = 50f;

    private Label? _audioLabel;
    private Label? _debugEffectsLabel;
    private HSlider? _highPassSlider;
    private HSlider? _gainSlider;

    private string _currentResult = "-";
    private bool _hasError;
    private string _inputDevice = "-";
    private bool _isListening = true;
    private float _peakAmplitude;
    private Label? _resultLabel;
    private Label? _statusLabel;
    private ProgressBar? _volumeBar;

    /// <summary>
    ///     音频效果参数变化事件
    /// </summary>
    public event Action<float, float>? OnAudioEffectsChanged;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        AnchorLeft = 1f;
        AnchorRight = 1f;
        AnchorTop = 0f;
        AnchorBottom = 0f;
        OffsetLeft = -(MarginRight + PanelWidth);
        OffsetRight = -MarginRight;
        OffsetTop = MarginTop;

        // 调试模式下增加面板高度
        var baseHeight = DebugAudioEffects ? 200f : 120f;
        OffsetBottom = MarginTop + baseHeight;
        ZIndex = 1000;

        // 半透明背景
        var styleBox = new StyleBoxFlat
        {
            BgColor = new Color(0f, 0f, 0f, 0.6f),
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 8,
            ContentMarginBottom = 8
        };
        AddThemeStyleboxOverride("panel", styleBox);

        CreateChildControls();
        Refresh();
    }

    private void CreateChildControls()
    {
        var vbox = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        AddChild(vbox);

        // 状态行
        _statusLabel = new Label
        {
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _statusLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _statusLabel.AddThemeConstantOverride("outline_size", 2);
        vbox.AddChild(_statusLabel);

        // 识别结果行
        _resultLabel = new Label
        {
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _resultLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _resultLabel.AddThemeConstantOverride("outline_size", 2);
        vbox.AddChild(_resultLabel);

        // 音频信息行
        _audioLabel = new Label
        {
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _audioLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _audioLabel.AddThemeConstantOverride("outline_size", 2);
        vbox.AddChild(_audioLabel);

        // 音量条
        _volumeBar = new ProgressBar
        {
            MouseFilter = MouseFilterEnum.Ignore,
            ShowPercentage = false,
            MinValue = 0,
            MaxValue = 1,
            Step = 0.001,
            Value = 0,
            CustomMinimumSize = new Vector2(PanelWidth - 24, 16)
        };
        vbox.AddChild(_volumeBar);

#if DEBUG
        if (DebugAudioEffects)
            CreateDebugControls(vbox);
#endif
    }

#if DEBUG
    private void CreateDebugControls(VBoxContainer vbox)
    {
        // 分隔标题
        _debugEffectsLabel = new Label
        {
            Text = "── 音效调试 ──",
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore
        };
        _debugEffectsLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _debugEffectsLabel.AddThemeConstantOverride("outline_size", 2);
        _debugEffectsLabel.AddThemeColorOverride("font_color", new Color("FFD700"));
        vbox.AddChild(_debugEffectsLabel);

        // 高通滤波器滑块
        var highPassRow = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        var highPassLabel = new Label
        {
            Text = "高通(Hz):",
            MouseFilter = MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(70, 0)
        };
        highPassLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        highPassLabel.AddThemeConstantOverride("outline_size", 2);
        highPassRow.AddChild(highPassLabel);

        _highPassSlider = new HSlider
        {
            MinValue = 20,
            MaxValue = 500,
            Step = 10,
            Value = 80,
            CustomMinimumSize = new Vector2(PanelWidth - 100, 20),
            MouseFilter = MouseFilterEnum.Pass
        };
        _highPassSlider.ValueChanged += OnHighPassChanged;
        highPassRow.AddChild(_highPassSlider);
        vbox.AddChild(highPassRow);

        // 增益滑块
        var gainRow = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };
        var gainLabel = new Label
        {
            Text = "增益(dB):",
            MouseFilter = MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(70, 0)
        };
        gainLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        gainLabel.AddThemeConstantOverride("outline_size", 2);
        gainRow.AddChild(gainLabel);

        _gainSlider = new HSlider
        {
            MinValue = -20,
            MaxValue = 20,
            Step = 1,
            Value = 0,
            CustomMinimumSize = new Vector2(PanelWidth - 100, 20),
            MouseFilter = MouseFilterEnum.Pass
        };
        _gainSlider.ValueChanged += OnGainChanged;
        gainRow.AddChild(_gainSlider);
        vbox.AddChild(gainRow);
    }

    private void OnHighPassChanged(double value)
    {
        if (_gainSlider != null)
            OnAudioEffectsChanged?.Invoke((float)value, (float)_gainSlider.Value);
    }

    private void OnGainChanged(double value)
    {
        if (_highPassSlider != null)
            OnAudioEffectsChanged?.Invoke((float)_highPassSlider.Value, (float)value);
    }
#endif

    /// <summary>
    ///     设置监听状态
    /// </summary>
    public void SetListening(bool listening, bool hasError = false)
    {
        _isListening = listening;
        _hasError = hasError;
        Refresh();
    }

    /// <summary>
    ///     设置识别结果文本
    /// </summary>
    public void SetResult(string? text)
    {
        _currentResult = string.IsNullOrWhiteSpace(text) ? "-" : text;
        RefreshResult();
    }

    /// <summary>
    ///     设置音频统计
    /// </summary>
    public void SetAudioStats(float peakAmplitude, string? inputDevice)
    {
        _peakAmplitude = Math.Clamp(peakAmplitude, 0f, 1f);
        _inputDevice = string.IsNullOrWhiteSpace(inputDevice) ? "-" : inputDevice;
        RefreshAudio();
    }

    /// <summary>
    ///     刷新所有显示
    /// </summary>
    public void Refresh()
    {
        RefreshStatus();
        RefreshResult();
        RefreshAudio();
    }

    private void RefreshStatus()
    {
        if (_statusLabel == null) return;

        if (_hasError)
        {
            _statusLabel.Text = "语音: 错误";
            _statusLabel.AddThemeColorOverride("font_color", new Color("FF9E9E"));
        }
        else
        {
            _statusLabel.Text = _isListening ? "语音: 开" : "语音: 关";
            _statusLabel.AddThemeColorOverride("font_color",
                _isListening ? new Color("9EF9A6") : new Color("FF9E9E"));
        }
    }

    private void RefreshResult()
    {
        if (_resultLabel == null) return;

        if (_hasError)
        {
            _resultLabel.Text = "识别: 错误";
            _resultLabel.AddThemeColorOverride("font_color", new Color("FF9E9E"));
        }
        else
        {
            _resultLabel.Text = $"识别: {_currentResult}";
            _resultLabel.AddThemeColorOverride("font_color",
                _isListening ? new Color("DCE7FF") : new Color("AEB8CC"));
        }
    }

    private void RefreshAudio()
    {
        if (_audioLabel == null || _volumeBar == null) return;

        if (_hasError)
        {
            _audioLabel.Text = "麦: 错误";
            _audioLabel.AddThemeColorOverride("font_color", new Color("FF9E9E"));
            _volumeBar.Value = 0;
        }
        else
        {
            _audioLabel.Text = $"峰值: {_peakAmplitude:F3}  麦: {_inputDevice}";
            _audioLabel.AddThemeColorOverride("font_color", _isListening ? new Color("BBD4FF") : new Color("8EA4C8"));
            _volumeBar.Value = _peakAmplitude;
        }
    }
}