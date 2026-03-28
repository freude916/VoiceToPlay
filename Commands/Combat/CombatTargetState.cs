using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace VoiceToPlay.Commands.Combat;

/// <summary>
///     战斗目标选择状态。在 SelectEnemyCommand 和 PlayCardCommand 之间共享。
/// </summary>
public static class CombatTargetState
{
    private static WeakReference<Creature>? _selectedEnemyRef;
    private static WeakReference<NCreature>? _selectedEnemyNodeRef;

    /// <summary>
    ///     当前选中的敌人索引（1-based）
    /// </summary>
    public static int? SelectedEnemyIndex { get; private set; }

    /// <summary>
    ///     尝试获取选中的敌人
    /// </summary>
    public static bool TryGetSelectedEnemy(out Creature? enemy)
    {
        enemy = null;
        if (_selectedEnemyRef == null) return false;
        if (!_selectedEnemyRef.TryGetTarget(out var target) || target == null || !target.IsAlive)
        {
            Clear();
            return false;
        }

        enemy = target;
        return true;
    }

    /// <summary>
    ///     设置选中的敌人（显示准星）
    /// </summary>
    public static void SetSelectedEnemy(int index, Creature enemy)
    {
        // 隐藏旧的准星
        if (TryGetSelectedEnemyNode(out var previousNode) && previousNode != null)
            previousNode.HideSingleSelectReticle();

        // 显示新的准星
        var nextNode = NCombatRoom.Instance?.GetCreatureNode(enemy);
        if (nextNode != null)
        {
            nextNode.ShowSingleSelectReticle();
            _selectedEnemyNodeRef = new WeakReference<NCreature>(nextNode);
        }
        else
        {
            _selectedEnemyNodeRef = null;
        }

        _selectedEnemyRef = new WeakReference<Creature>(enemy);
        SelectedEnemyIndex = index;
        MainFile.Logger.Info($"CombatTargetState: selected enemy index={index}");
    }

    /// <summary>
    ///     清除选中状态
    /// </summary>
    public static void Clear()
    {
        if (TryGetSelectedEnemyNode(out var node) && node != null)
            node.HideSingleSelectReticle();

        _selectedEnemyNodeRef = null;
        _selectedEnemyRef = null;
        SelectedEnemyIndex = null;
    }

    /// <summary>
    ///     获取预设的敌人目标（如果有效）
    /// </summary>
    public static Creature? ResolvePreferredEnemy()
    {
        if (!TryGetSelectedEnemy(out var enemy) || enemy == null)
        {
            Clear();
            return null;
        }

        return enemy;
    }

    private static bool TryGetSelectedEnemyNode(out NCreature? node)
    {
        node = null;
        if (_selectedEnemyNodeRef == null) return false;
        if (!_selectedEnemyNodeRef.TryGetTarget(out var target) || !GodotObject.IsInstanceValid(target))
        {
            _selectedEnemyNodeRef = null;
            return false;
        }

        node = target;
        return true;
    }
}