using VoiceToPlay.Voice.Core;

namespace VoiceToPlay.Commands.Turn;

/// <summary>
///     结束回合命令。静态词表。
/// </summary>
public sealed class EndTurnCommand : IVoiceCommand
{
    private static readonly string[] Words = ["结束", "结束回合"];

    public IEnumerable<string> SupportedWords => Words;

    public void Execute(string word)
    {
        // TODO: 实现结束回合逻辑
        // var state = CombatManager.Instance.DebugOnlyGetState();
        // var me = LocalContext.GetMe(state);
        // CombatManager.Instance.SetReadyToEndTurn(me, true);
        MainFile.Logger.Info($"EndTurnCommand: {word}");
    }

    // 静态词表，无需事件
    public event Action<IVoiceCommand>? VocabularyChanged
    {
        add { }
        remove { }
    }
}