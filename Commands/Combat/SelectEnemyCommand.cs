using MegaCrit.Sts2.Core.Combat;
using VoiceToPlay.Voice.Core;

namespace VoiceToPlay.Commands.Combat;

/// <summary>
///     选择敌人目标命令。支持 "瞄准第1", "打1个" 等格式。
/// </summary>
public sealed class SelectEnemyCommand : IVoiceCommand
{
    private readonly Dictionary<string, int> _wordToIndex = new();

    public SelectEnemyCommand()
    {
        Instance = this;
    }

    public static SelectEnemyCommand? Instance { get; private set; }

    public IEnumerable<string> SupportedWords
    {
        get
        {
            _wordToIndex.Clear();
            foreach (var phrase in EnemyTargetCommandCatalog.GrammarPhrases)
                if (EnemyTargetCommandCatalog.TryParseNormalizedCommand(phrase, out var index))
                    _wordToIndex[phrase] = index;
            return _wordToIndex.Keys;
        }
    }

    public void Execute(string word)
    {
        if (!_wordToIndex.TryGetValue(word, out var index))
        {
            MainFile.Logger.Warn($"SelectEnemyCommand: word '{word}' not found");
            return;
        }

        var combatState = CombatManager.Instance?.DebugOnlyGetState();
        if (combatState == null)
        {
            MainFile.Logger.Warn("SelectEnemyCommand: combat not active");
            return;
        }

        var hittableEnemies = combatState.HittableEnemies;
        if (index < 1 || index > hittableEnemies.Count)
        {
            MainFile.Logger.Warn($"SelectEnemyCommand: invalid index {index}, enemies={hittableEnemies.Count}");
            return;
        }

        var enemy = hittableEnemies[index - 1];
        if (enemy == null || !enemy.IsAlive)
        {
            MainFile.Logger.Warn($"SelectEnemyCommand: enemy at index {index} is not valid");
            return;
        }

        // 存储选中状态并显示准星
        CombatTargetState.SetSelectedEnemy(index, enemy);
        MainFile.Logger.Info($"SelectEnemyCommand: selected enemy {index}/{hittableEnemies.Count}");
    }

    // 静态词表，不需要事件
    public event Action<IVoiceCommand>? VocabularyChanged
    {
        add { }
        remove { }
    }
}