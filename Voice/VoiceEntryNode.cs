using Godot;
using VoiceToPlay.Commands.Card;
using VoiceToPlay.Commands.CardGrid;
using VoiceToPlay.Commands.CardRow;
using VoiceToPlay.Commands.CharacterSelect;
using VoiceToPlay.Commands.Combat;
using VoiceToPlay.Commands.DeckView;
using VoiceToPlay.Commands.Event;
using VoiceToPlay.Commands.GlobalUi;
using VoiceToPlay.Commands.MainMenu;
using VoiceToPlay.Commands.Map;
using VoiceToPlay.Commands.Potion;
using VoiceToPlay.Commands.RestSite;
using VoiceToPlay.Commands.Rewards;
using VoiceToPlay.Commands.Shop;
using VoiceToPlay.Commands.Treasure;
using VoiceToPlay.Commands.Turn;
using VoiceToPlay.Commands.Ui;
using VoiceToPlay.Voice.Core;
using VoiceToPlay.Voice.UI;

namespace VoiceToPlay.Voice;

/// <summary>
///     语音模块入口节点。由 Harmony Patch 注入到 NGame。
/// </summary>
internal sealed partial class VoiceEntryNode : Node
{
    private VoiceCommandEngine? _commandEngine;
    private VoiceDebugPanel? _debugPanel;
    private bool _hasError;

    private bool _listening = true;
    private VoiceRecognitionService? _recognitionService;

    public override void _Ready()
    {
        CreateUI();

        // 1. 创建命令引擎
        _commandEngine = new VoiceCommandEngine();

        // 2. 注册命令（按功能分区）
        
        // 全局 - UI 按钮
        _commandEngine.Register(new ProceedCommand());
        _commandEngine.Register(new ConfirmCommand());
        _commandEngine.Register(new BackCommand());

        // 主菜单 / 角色选择
        _commandEngine.Register(new MainMenuCommand());
        _commandEngine.Register(new CharacterSelectCommand());
        
        // 战斗 - 全功能 UI
        _commandEngine.Register(new GlobalUiCommand());

        // 战斗 - 药水
        _commandEngine.Register(new PotionCommand());
        
        // 战斗 - 卡牌操作
        _commandEngine.Register(new PlayCardCommand());
        _commandEngine.Register(new HandCardSelectionCommand());
        _commandEngine.Register(new SelectEnemyCommand());
        _commandEngine.Register(new EndTurnCommand());

        // 奖励 / 选牌
        _commandEngine.Register(new RewardsCommand());
        _commandEngine.Register(new CardRowSelectCommand());
        _commandEngine.Register(new CardGridSelectCommand());

        // 地图 / 事件 / 宝箱 / 休息 / 商店
        _commandEngine.Register(new MapCommand());
        _commandEngine.Register(new EventCommand());
        _commandEngine.Register(new TreasureCommand());
        _commandEngine.Register(new RestSiteCommand());
        _commandEngine.Register(new ShopCommand());

        // 牌组视图 / 卡牌详情
        _commandEngine.Register(new DeckViewCommand());
        _commandEngine.Register(new InspectCardCommand());

        // 3. 创建识别服务
        _recognitionService = new VoiceRecognitionService(this, _commandEngine);
        _recognitionService.RecognitionTextChanged += OnRecognitionTextChanged;
        _recognitionService.Initialize();

        if (!_recognitionService.IsAvailable)
        {
            _hasError = true;
            _listening = false;
            MainFile.Logger.Error($"Voice init failed: {_recognitionService.FatalError}");
        }
        else
        {
            MainFile.Logger.Info("Voice entry ready. F8 to toggle.");
        }

        RefreshDebugPanel();
    }

    public override void _Process(double delta)
    {
        if (_recognitionService == null || _hasError) return;

        _recognitionService.Tick();
        RefreshDebugPanel();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Keycode: Key.F8 }) return;

        if (_hasError)
        {
            MainFile.Logger.Warn("Voice toggle ignored because runtime is faulted.");
            GetViewport().SetInputAsHandled();
            return;
        }

        _listening = !_listening;
        _recognitionService?.SetListeningEnabled(_listening);
        RefreshDebugPanel();
        MainFile.Logger.Debug($"Voice listening {(_listening ? "enabled" : "disabled")} (F8)");
        GetViewport().SetInputAsHandled();
    }

    public override void _ExitTree()
    {
        DisposeServiceAndQueueFree();
    }

    private void CreateUI()
    {
        _debugPanel = new VoiceDebugPanel
        {
            Name = "VoiceDebugPanel"
        };
        _debugPanel.OnAudioEffectsChanged += OnAudioEffectsChanged;
        AddChild(_debugPanel);
    }

    private void OnAudioEffectsChanged(float highPassHz, float lowPassHz, float gainDb)
    {
        _recognitionService?.SetAudioEffects(highPassHz, lowPassHz, gainDb);
    }

    private void OnRecognitionTextChanged(string text)
    {
        _debugPanel?.SetResult(text);
    }

    private void RefreshDebugPanel()
    {
        if (_debugPanel == null) return;

        _debugPanel.SetListening(_listening, _hasError);

        if (_recognitionService != null)
        {
            _debugPanel.SetAudioStats(
                _recognitionService.LastPeakAmplitude,
                VoiceRecognitionService.CurrentInputDevice);
            _debugPanel.SetSpectrumData(_recognitionService.GetSpectrumData());
        }
    }

    public void DisposeServiceAndQueueFree()
    {
        if (_recognitionService != null)
        {
            _recognitionService.RecognitionTextChanged -= OnRecognitionTextChanged;
            _recognitionService.Dispose();
            _recognitionService = null;
        }

        if (IsInstanceValid(this) && IsInsideTree())
            QueueFree();
    }
}