using MegaCrit.Sts2.Core.Nodes.Rooms;
using VoiceToPlay.Voice.Core;

namespace VoiceToPlay.Commands.Turn;

/// <summary>
///     结束回合命令。静态词表。
/// </summary>
public sealed class EndTurnCommand : IVoiceCommand
{
    private static readonly string[] Words = ["结束", "结束回合"];

    public IEnumerable<string> SupportedWords => Words;

    public CommandResult Execute(string word)
    {
        var endTurnButton = NCombatRoom.Instance?.Ui?.EndTurnButton;
        if (endTurnButton == null)
        {
            MainFile.Logger.Warn($"EndTurnCommand: {word} - EndTurnButton not found");
            return CommandResult.Failed;
        }

        endTurnButton.ForceClick();
        MainFile.Logger.Debug($"EndTurnCommand: {word}");
        return CommandResult.Success;
    }

    // 静态词表，无需事件
    public event Action<IVoiceCommand>? VocabularyChanged
    {
        add { }
        remove { }
    }
}