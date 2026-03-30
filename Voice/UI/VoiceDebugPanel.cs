using Godot;

namespace VoiceToPlay.Voice.UI;

/// <summary>
///     语音调试面板。支持折叠的音频控制面板。
/// </summary>
public sealed partial class VoiceDebugPanel : PanelContainer
{
    private const float PanelWidth = 300f;
    private const float MarginRight = 50f;
    private const float MarginTop = 50f;
    private VBoxContainer? _collapsibleContent;

    // 状态数据
    private string _currentResult = "-";
    private Label? _deviceLabel;
    private Button? _expandButton;

    // 折叠状态
    private bool _expanded;
    private HSlider? _gainSlider;
    private Label? _gainValueLabel;
    private bool _hasError;
    private HSlider? _highPassSlider;
    private Label? _highPassValueLabel;
    private string _inputDevice = "-";
    private bool _isListening = true;
    private HSlider? _lowPassSlider;
    private Label? _lowPassValueLabel;
    private float _peakAmplitude;
    private Label? _resultLabel;
    private SpectrumDisplayControl? _spectrumDisplay;

    // 控件引用
    private Label? _statusLabel;
    private ProgressBar? _volumeBar;
    private Label? _volumeValueLabel;

    /// <summary>
    ///     音频效果参数变化事件: (高通Hz, 低通Hz, 增益dB)
    /// </summary>
    public event Action<float, float, float>? OnAudioEffectsChanged;

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
        UpdatePanelHeight();
        Refresh();
    }

    private void CreateChildControls()
    {
        var mainVbox = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(mainVbox);

        // === 标题行：语音状态 + 详情按钮 ===
        var headerRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };

        _statusLabel = new Label
        {
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Left,
            SizeFlagsHorizontal = SizeFlags.ExpandFill
        };
        _statusLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _statusLabel.AddThemeConstantOverride("outline_size", 2);
        headerRow.AddChild(_statusLabel);

        _expandButton = new Button
        {
            Text = "详情 ...",
            ToggleMode = true,
            ButtonPressed = _expanded,
            MouseFilter = MouseFilterEnum.Pass
        };
        _expandButton.Pressed += OnExpandButtonPressed;
        headerRow.AddChild(_expandButton);
        mainVbox.AddChild(headerRow);

        // === 识别结果行（始终可见）===
        var resultRow = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        var resultPrefix = new Label
        {
            Text = "识别:",
            MouseFilter = MouseFilterEnum.Ignore
        };
        resultPrefix.AddThemeColorOverride("font_outline_color", Colors.Black);
        resultPrefix.AddThemeConstantOverride("outline_size", 2);
        resultRow.AddChild(resultPrefix);

        _resultLabel = new Label
        {
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Left,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(PanelWidth - 60, 0)
        };
        _resultLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _resultLabel.AddThemeConstantOverride("outline_size", 2);
        resultRow.AddChild(_resultLabel);
        mainVbox.AddChild(resultRow);

        // === 峰值条行（始终可见）===
        var volumeRow = new HBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore
        };

        _volumeBar = new ProgressBar
        {
            MouseFilter = MouseFilterEnum.Ignore,
            ShowPercentage = false,
            MinValue = 0,
            MaxValue = 1,
            Step = 0.001,
            Value = 0,
            CustomMinimumSize = new Vector2(PanelWidth - 50, 12)
        };
        volumeRow.AddChild(_volumeBar);

        _volumeValueLabel = new Label
        {
            Text = "0.000",
            MouseFilter = MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(40, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        _volumeValueLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _volumeValueLabel.AddThemeConstantOverride("outline_size", 2);
        volumeRow.AddChild(_volumeValueLabel);
        mainVbox.AddChild(volumeRow);

        // === 折叠内容 ===
        _collapsibleContent = new VBoxContainer
        {
            MouseFilter = MouseFilterEnum.Ignore,
            Visible = false // 默认折叠
        };
        mainVbox.AddChild(_collapsibleContent);

        // 设备名
        _deviceLabel = new Label
        {
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Left
        };
        _deviceLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        _deviceLabel.AddThemeConstantOverride("outline_size", 2);
        _collapsibleContent.AddChild(_deviceLabel);

        // 增益滑块
        var gainRow = CreateSliderRow("增益:", -20, 20, 1, 0, out _gainSlider, out _gainValueLabel);
        _collapsibleContent.AddChild(gainRow);

        // 高通滑块
        var highPassRow = CreateSliderRow("高通:", 20, 500, 10, 80, out _highPassSlider, out _highPassValueLabel);
        _collapsibleContent.AddChild(highPassRow);

        // 低通滑块
        var lowPassRow = CreateSliderRow("低通:", 1000, 8000, 100, 4000, out _lowPassSlider, out _lowPassValueLabel);
        _collapsibleContent.AddChild(lowPassRow);

        // 频谱图
        _spectrumDisplay = new SpectrumDisplayControl
        {
            CustomMinimumSize = new Vector2(PanelWidth - 24, 50),
            MouseFilter = MouseFilterEnum.Ignore
        };
        _collapsibleContent.AddChild(_spectrumDisplay);

        // 绑定滑块事件
        _gainSlider!.ValueChanged += OnSliderChanged;
        _highPassSlider!.ValueChanged += OnSliderChanged;
        _lowPassSlider!.ValueChanged += OnSliderChanged;
    }

    private HBoxContainer CreateSliderRow(string label, double min, double max, double step, double defaultValue,
        out HSlider slider, out Label valueLabel)
    {
        var row = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };

        var labelText = new Label
        {
            Text = label,
            MouseFilter = MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(45, 0)
        };
        labelText.AddThemeColorOverride("font_outline_color", Colors.Black);
        labelText.AddThemeConstantOverride("outline_size", 2);
        row.AddChild(labelText);

        slider = new HSlider
        {
            MinValue = min,
            MaxValue = max,
            Step = step,
            Value = defaultValue,
            CustomMinimumSize = new Vector2(PanelWidth - 110, 20),
            MouseFilter = MouseFilterEnum.Pass
        };
        row.AddChild(slider);

        valueLabel = new Label
        {
            Text = ((int)defaultValue).ToString(),
            MouseFilter = MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(40, 0),
            HorizontalAlignment = HorizontalAlignment.Right
        };
        valueLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        valueLabel.AddThemeConstantOverride("outline_size", 2);
        row.AddChild(valueLabel);

        return row;
    }

    private void OnExpandButtonPressed()
    {
        _expanded = _expandButton?.ButtonPressed ?? false;
        _expandButton!.Text = _expanded ? "详情 ▲" : "详情 ...";
        _collapsibleContent!.Visible = _expanded;
        UpdatePanelHeight();
    }

    private void UpdatePanelHeight()
    {
        // 折叠时只显示标题 + 识别 + 峰值（约 80px）
        // 展开时额外显示设备 + 增益 + 高通 + 低通 + 频谱（约 160px）
        var baseHeight = _expanded ? 240f : 80f;
        OffsetBottom = MarginTop + baseHeight;
    }

    private void OnSliderChanged(double _)
    {
        if (_highPassSlider == null || _lowPassSlider == null || _gainSlider == null) return;

        _highPassValueLabel!.Text = ((int)_highPassSlider.Value).ToString();
        _lowPassValueLabel!.Text = ((int)_lowPassSlider.Value).ToString();
        _gainValueLabel!.Text = ((int)_gainSlider.Value).ToString();

        OnAudioEffectsChanged?.Invoke(
            (float)_highPassSlider.Value,
            (float)_lowPassSlider.Value,
            (float)_gainSlider.Value);
    }

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
    ///     设置频谱数据
    /// </summary>
    public void SetSpectrumData(float[]? spectrumData)
    {
        _spectrumDisplay?.UpdateData(spectrumData);
    }

    private void Refresh()
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
            _resultLabel.Text = "错误";
            _resultLabel.AddThemeColorOverride("font_color", new Color("FF9E9E"));
        }
        else
        {
            _resultLabel.Text = _currentResult;
            _resultLabel.AddThemeColorOverride("font_color",
                _isListening ? new Color("DCE7FF") : new Color("AEB8CC"));
        }
    }

    private void RefreshAudio()
    {
        if (_volumeBar == null || _volumeValueLabel == null || _deviceLabel == null) return;

        if (_hasError)
        {
            _deviceLabel.Text = "麦: 错误";
            _deviceLabel.AddThemeColorOverride("font_color", new Color("FF9E9E"));
            _volumeBar.Value = 0;
            _volumeValueLabel.Text = "0.000";
        }
        else
        {
            _deviceLabel.Text = $"麦: {_inputDevice}";
            _deviceLabel.AddThemeColorOverride("font_color", _isListening ? new Color("BBD4FF") : new Color("8EA4C8"));
            _volumeBar.Value = _peakAmplitude;
            _volumeValueLabel.Text = _peakAmplitude.ToString("F3");
        }

        // 峰值条颜色渐变
        UpdateVolumeBarColor();
    }

    private void UpdateVolumeBarColor()
    {
        if (_volumeBar == null) return;

        // 根据峰值大小设置颜色：绿 -> 黄 -> 红
        Color barColor;
        if (_peakAmplitude < 0.3f)
            barColor = new Color(0.2f, 0.8f, 0.2f);
        else if (_peakAmplitude < 0.6f)
            barColor = new Color(0.9f, 0.9f, 0.2f);
        else
            barColor = new Color(0.9f, 0.3f, 0.2f);

        // ProgressBar 的进度条颜色通过主题设置
        var styleBox = new StyleBoxFlat
        {
            BgColor = barColor,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4
        };
        _volumeBar.AddThemeStyleboxOverride("fill", styleBox);
    }
}

/// <summary>
///     频谱显示控件
/// </summary>
internal sealed partial class SpectrumDisplayControl : Control
{
    private const int BarCount = 32;
    private float[]? _data;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public void UpdateData(float[]? data)
    {
        _data = data;
        QueueRedraw();
    }

    public override void _Draw()
    {
        var size = Size;
        var barWidth = size.X / BarCount - 2;
        var maxHeight = size.Y - 4;

        // 绘制背景网格线
        var gridColor = new Color(1f, 1f, 1f, 0.1f);
        for (var i = 0; i < 4; i++)
        {
            var y = i * maxHeight / 4 + 2;
            DrawLine(new Vector2(0, y), new Vector2(size.X, y), gridColor);
        }

        if (_data == null || _data.Length == 0)
        {
            DrawString(ThemeDB.FallbackFont, new Vector2(10, size.Y / 2 + 4),
                "无频谱数据", fontSize: 12, modulate: new Color(0.5f, 0.5f, 0.5f));
            return;
        }

        // 绘制频谱条形
        var count = Math.Min(_data.Length, BarCount);
        for (var i = 0; i < count; i++)
        {
            var magnitude = _data[i];
            var db = magnitude > 0.0001f ? 20f * MathF.Log10(magnitude) : -80f;
            var normalizedHeight = Math.Clamp((db + 80f) / 80f, 0f, 1f) * maxHeight;

            var x = i * (barWidth + 2) + 1;
            var rect = new Rect2(x, size.Y - normalizedHeight - 2, barWidth, normalizedHeight);

            // 颜色渐变：绿 -> 黄 -> 红
            Color barColor;
            if (normalizedHeight < maxHeight * 0.5f)
                barColor = new Color(0.2f, 0.8f, 0.2f);
            else if (normalizedHeight < maxHeight * 0.75f)
                barColor = new Color(0.9f, 0.9f, 0.2f);
            else
                barColor = new Color(0.9f, 0.3f, 0.2f);

            DrawRect(rect, barColor);
        }
    }
}