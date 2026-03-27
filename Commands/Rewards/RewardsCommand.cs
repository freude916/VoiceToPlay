using Godot;
using MegaCrit.Sts2.Core.Nodes.Rewards;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Rewards;
using VoiceToPlay.Voice;
using VoiceToPlay.Voice.Core;

namespace VoiceToPlay.Commands.Rewards;

/// <summary>
///     奖励领取命令。支持领取金币、遗物、打开卡牌奖励。
/// </summary>
public sealed class RewardsCommand : IVoiceCommand
{
    private static readonly Dictionary<int, string> ChineseNumbers = new()
    {
        [1] = "一", [2] = "二", [3] = "三", [4] = "四", [5] = "五",
        [6] = "六", [7] = "七", [8] = "八", [9] = "九"
    };

    private readonly Dictionary<string, Func<bool>> _wordToAction = new();
    private HashSet<string> _lastWords = new(StringComparer.Ordinal);

    public RewardsCommand()
    {
        Instance = this;
    }

    public static RewardsCommand? Instance { get; private set; }

    public IEnumerable<string> SupportedWords
    {
        get
        {
            _wordToAction.Clear();

            if (NOverlayStack.Instance?.Peek() is not NRewardsScreen rewardsScreen || rewardsScreen.IsComplete) return [];

            var cardRewardIndex = 0;

            // 遍历奖励按钮
            foreach (var button in EnumerateRewardButtons(rewardsScreen))
            {
                if (button.Reward == null) continue;

                var reward = button.Reward;
                var capturedButton = button;

                // CardReward 需要序号
                if (reward is CardReward)
                {
                    cardRewardIndex++;
                    var word = $"卡牌{ChineseNumbers.GetValueOrDefault(cardRewardIndex, cardRewardIndex.ToString())}";
                    _wordToAction[word] = () =>
                    {
                        if (!GodotObject.IsInstanceValid(capturedButton)) return false;
                        capturedButton.ForceClick();
                        return true;

                    };
                    continue;
                }

                var rewardWord = GetRewardWord(reward);
                if (string.IsNullOrEmpty(rewardWord)) continue;

                _wordToAction[rewardWord] = () =>
                {
                    if (GodotObject.IsInstanceValid(capturedButton))
                    {
                        capturedButton.ForceClick();
                        return true;
                    }

                    return false;
                };
            }

            // 添加静态命令
            _wordToAction["金币"] = () => ClaimAllGold(rewardsScreen);

            return _wordToAction.Keys;
        }
    }

    public void Execute(string word)
    {
        if (!_wordToAction.TryGetValue(word, out var action))
        {
            MainFile.Logger.Warn($"RewardsCommand: word '{word}' not found");
            return;
        }

        var success = action();
        MainFile.Logger.Info($"RewardsCommand: '{word}' executed, success={success}");
    }

    public event Action<IVoiceCommand>? VocabularyChanged;

    public static void RefreshVocabulary(bool force = false)
    {
        var instance = Instance;
        if (instance == null) return;

        var currentWords = new HashSet<string>(instance.SupportedWords, StringComparer.Ordinal);
        if (!force && currentWords.SetEquals(instance._lastWords)) return;
        instance._lastWords = currentWords;
        instance.VocabularyChanged?.Invoke(instance);
    }

    private static string GetRewardWord(Reward reward)
    {
        return reward switch
        {
            GoldReward => "金币",
            RelicReward relic => VoiceText.Normalize(relic.ClaimedRelic?.Title.GetFormattedText()),
            PotionReward potion => VoiceText.Normalize(potion.ClaimedPotion?.Title.GetFormattedText()),
            SpecialCardReward special => VoiceText.Normalize(special.Description.GetFormattedText()),
            _ => VoiceText.Normalize(reward.Description?.GetFormattedText())
        };
    }

    private static bool ClaimAllGold(NRewardsScreen screen)
    {
        var clicked = false;
        foreach (var button in EnumerateRewardButtons(screen))
            if (button?.Reward is GoldReward && GodotObject.IsInstanceValid(button))
            {
                button.ForceClick();
                clicked = true;
            }

        return clicked;
    }

    /// <summary>
    ///     遍历奖励按钮。按钮在 %RewardsContainer 下，不是 screen 的直接子节点。
    /// </summary>
    private static IEnumerable<NRewardButton> EnumerateRewardButtons(NRewardsScreen screen)
    {
        var container = screen.GetNodeOrNull<Control>("%RewardsContainer");
        if (container == null)
        {
            MainFile.Logger.Warn("RewardsCommand: %RewardsContainer not found");
            yield break;
        }

        foreach (var child in container.GetChildren())
            if (child is NRewardButton button)
                yield return button;
    }
}