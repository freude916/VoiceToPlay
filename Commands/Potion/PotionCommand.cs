using Godot;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Potions;
using MegaCrit.Sts2.Core.Runs;
using VoiceToPlay.Util;
using VoiceToPlay.Voice;
using VoiceToPlay.Voice.Core;

namespace VoiceToPlay.Commands.Potion;

/// <summary>
///     药水命令。处理药水选择和使用。
/// </summary>
public sealed class PotionCommand : IVoiceCommand
{
    private readonly Dictionary<string, Action> _wordToAction = new();

    /// <summary>
    ///     缓存的词表，SupportedWords getter 直接返回此缓存
    /// </summary>
    private HashSet<string> _cachedWords = new(StringComparer.Ordinal);

    private Player? _subscribedPlayer;

    public PotionCommand()
    {
        Instance = this;
    }

    public static PotionCommand? Instance { get; private set; }

    /// <summary>
    ///     只返回缓存，不做任何计算
    /// </summary>
    public IEnumerable<string> SupportedWords => _cachedWords;

    public CommandResult Execute(string word)
    {
        if (!_wordToAction.TryGetValue(word, out var action))
        {
            MainFile.Logger.Warn($"PotionCommand: word '{word}' not found");
            return CommandResult.Failed;
        }

        try
        {
            action.Invoke();
            MainFile.Logger.Info($"PotionCommand: executed '{word}'");
            return CommandResult.Success;
        }
        catch (Exception e)
        {
            MainFile.Logger.Warn($"PotionCommand: failed to execute '{word}': {e.Message}");
            return CommandResult.Failed;
        }
    }

    public event Action<IVoiceCommand>? VocabularyChanged;

    /// <summary>
    ///     刷新词表缓存，由 Patch 调用
    /// </summary>
    public static void RefreshVocabulary()
    {
        var instance = Instance;
        if (instance == null) return;

        instance.ComputeAndRefresh();
    }

    private void ComputeAndRefresh()
    {
        // 先断开旧订阅
        UnsubscribePlayerEvents();

        var newWords = ComputeSupportedWords();
        if (!newWords.SetEquals(_cachedWords))
        {
            _cachedWords = newWords;
            VocabularyChanged?.Invoke(this);
        }
    }

    /// <summary>
    ///     计算当前支持的词表
    /// </summary>
    private HashSet<string> ComputeSupportedWords()
    {
        _wordToAction.Clear();

        var run = NRun.Instance;
        if (run == null || !GodotObject.IsInstanceValid(run)) return [];

        var topBar = run.GlobalUi?.TopBar;
        if (topBar == null) return [];

        var potionContainer = topBar.PotionContainer;
        if (!GodotObject.IsInstanceValid(potionContainer))
            return new HashSet<string>(_wordToAction.Keys, StringComparer.Ordinal);

        var holders = potionContainer.GetNode<Control>("MarginContainer/PotionHolders");
        if (holders == null) return new HashSet<string>(_wordToAction.Keys, StringComparer.Ordinal);

        // 订阅 Player 药水事件
        var player = LocalContext.GetMe(RunManager.Instance.DebugOnlyGetState());
        SubscribePlayerEvents(player);

        var index = 0;
        foreach (var child in holders.GetChildren())
        {
            if (child is not NPotionHolder holder || !GodotObject.IsInstanceValid(holder)) continue;

            index++;
            var potion = holder.Potion;
            if (potion == null || !GodotObject.IsInstanceValid(potion)) continue;

            var potionName = VoiceText.Normalize(potion.Model.Title.GetFormattedText());
            if (string.IsNullOrEmpty(potionName)) continue;

            // 药水名：使用药水
            _wordToAction[potionName] = () =>
            {
                // 打开药水弹窗
                holder.EmitSignal(NClickableControl.SignalName.Released, holder);
            };

            // 序号词：第一瓶、第二瓶药水
            var indexWord = L10n.PotionOrdinal(index);
            _wordToAction[indexWord] = () => { holder.EmitSignal(NClickableControl.SignalName.Released, holder); };
        }

        return new HashSet<string>(_wordToAction.Keys, StringComparer.Ordinal);
    }

    private void SubscribePlayerEvents(Player? player)
    {
        if (player == null) return;

        _subscribedPlayer = player;
        player.PotionProcured += OnPotionChanged;
        player.UsedPotionRemoved += OnPotionChanged;
        player.PotionDiscarded += OnPotionChanged;
        player.MaxPotionCountChanged += OnMaxPotionCountChanged;
    }

    private void UnsubscribePlayerEvents()
    {
        if (_subscribedPlayer == null) return;

        _subscribedPlayer.PotionProcured -= OnPotionChanged;
        _subscribedPlayer.UsedPotionRemoved -= OnPotionChanged;
        _subscribedPlayer.PotionDiscarded -= OnPotionChanged;
        _subscribedPlayer.MaxPotionCountChanged -= OnMaxPotionCountChanged;
        _subscribedPlayer = null;
    }

    private void OnPotionChanged(PotionModel _)
    {
        RefreshVocabulary();
    }

    private void OnMaxPotionCountChanged(int _)
    {
        RefreshVocabulary();
    }
}
