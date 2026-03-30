# Commands/TODO - 缺失的 Actions

以下 Actions 在 MCP mod 中已实现，但在当前 VoiceToPlay Commands 中尚未对应实现。

---

## Bundle 选择（卡包选择）

### ExecuteSelectBundle - 选择卡包

```csharp
private static Dictionary<string, object?> ExecuteSelectBundle(Dictionary<string, JsonElement> data)
{
    var overlay = NOverlayStack.Instance?.Peek();
    if (overlay is not NChooseABundleSelectionScreen screen)
        return Error("No bundle selection screen is open");

    if (!data.TryGetValue("index", out var indexElem))
        return Error("Missing 'index' (bundle index)");

    int index = indexElem.GetInt32();
    var previewContainer = screen.GetNodeOrNull<Godot.Control>("%BundlePreviewContainer");
    if (previewContainer?.Visible == true)
        return Error("A bundle preview is already open - confirm or cancel it first");

    var bundles = FindAll<NCardBundle>(screen);
    if (index < 0 || index >= bundles.Count)
        return Error($"Bundle index {index} out of range ({bundles.Count} bundles available)");

    bundles[index].Hitbox.ForceClick();
    return new Dictionary<string, object?>
    {
        ["status"] = "ok",
        ["message"] = $"Selecting bundle {index}"
    };
}
```

**职责：**

- 验证卡包选择屏幕已打开
- 确保没有其他预览打开
- 根据索引选择卡包

**参数：**

- `index`: 卡包索引（必需）

---

### ExecuteConfirmBundleSelection - 确认卡包选择

```csharp
private static Dictionary<string, object?> ExecuteConfirmBundleSelection()
{
    var overlay = NOverlayStack.Instance?.Peek();
    if (overlay is not NChooseABundleSelectionScreen screen)
        return Error("No bundle selection screen is open");

    var confirmButton = screen.GetNodeOrNull<NConfirmButton>("%Confirm");
    if (confirmButton is not { IsEnabled: true })
        return Error("Bundle confirm button is not enabled");

    confirmButton.ForceClick();
    return new Dictionary<string, object?>
    {
        ["status"] = "ok",
        ["message"] = "Confirming bundle selection"
    };
}
```

**职责：**

- 验证卡包选择屏幕已打开
- 点击确认按钮确认选择

**参数：** 无

---

### ExecuteCancelBundleSelection - 取消卡包选择

```csharp
private static Dictionary<string, object?> ExecuteCancelBundleSelection()
{
    var overlay = NOverlayStack.Instance?.Peek();
    if (overlay is not NChooseABundleSelectionScreen screen)
        return Error("No bundle selection screen is open");

    var cancelButton = screen.GetNodeOrNull<NBackButton>("%Cancel");
    if (cancelButton is not { IsEnabled: true })
        return Error("Bundle cancel button is not enabled");

    cancelButton.ForceClick();
    return new Dictionary<string, object?>
    {
        ["status"] = "ok",
        ["message"] = "Cancelling bundle selection"
    };
}
```

**职责：**

- 验证卡包选择屏幕已打开
- 点击取消按钮取消选择

**参数：** 无

---

## 战斗选牌

### ExecuteCombatSelectCard - 战斗中选择卡牌

```csharp
private static Dictionary<string, object?> ExecuteCombatSelectCard(Dictionary<string, JsonElement> data)
{
    var hand = NPlayerHand.Instance;
    if (hand == null || !hand.IsInCardSelection)
        return Error("No in-combat card selection is active");

    if (!data.TryGetValue("card_index", out var indexElem))
        return Error("Missing 'card_index' (index of the card in hand)");

    int index = indexElem.GetInt32();
    var holders = hand.ActiveHolders;
    if (index < 0 || index >= holders.Count)
        return Error($"Card index {index} out of range ({holders.Count} selectable cards)");

    var holder = holders[index];
    string cardName = SafeGetText(() => holder.CardModel?.Title) ?? "unknown";

    // Emit the Pressed signal — same path the game UI uses
    holder.EmitSignal(NCardHolder.SignalName.Pressed, holder);

    return new Dictionary<string, object?>
    {
        ["status"] = "ok",
        ["message"] = $"Selecting card from hand: {cardName}"
    };
}
```

**职责：**

- 验证战斗中的卡牌选择模式已激活
- 根据索引选择手牌中的卡牌
- 发送 Pressed 信号触发选择

**参数：**

- `card_index`: 手牌中的卡牌索引（必需）

---

### ExecuteCombatConfirmSelection - 确认战斗选牌

```csharp
private static Dictionary<string, object?> ExecuteCombatConfirmSelection()
{
    var hand = NPlayerHand.Instance;
    if (hand == null || !hand.IsInCardSelection)
        return Error("No in-combat card selection is active");

    var confirmBtn = hand.GetNodeOrNull<NConfirmButton>("%SelectModeConfirmButton");
    if (confirmBtn == null || !confirmBtn.IsEnabled)
        return Error("Confirm button is not enabled — select more cards first");

    confirmBtn.ForceClick();

    return new Dictionary<string, object?>
    {
        ["status"] = "ok",
        ["message"] = "Confirming combat card selection"
    };
}
```

**职责：**

- 验证战斗中的卡牌选择模式已激活
- 点击确认按钮确认选择

**参数：** 无

---

## 遗物跳过

### ExecuteSkipRelicSelection - 跳过遗物选择

```csharp
private static Dictionary<string, object?> ExecuteSkipRelicSelection()
{
    var overlay = NOverlayStack.Instance?.Peek();
    if (overlay is not NChooseARelicSelection screen)
        return Error("No relic selection screen is open");

    var skipButton = screen.GetNodeOrNull<NClickableControl>("SkipButton");
    if (skipButton is not { IsEnabled: true })
        return Error("No skip option available");

    skipButton.ForceClick();

    return new Dictionary<string, object?>
    {
        ["status"] = "ok",
        ["message"] = "Skipping relic selection"
    };
}
```

**职责：**

- 验证遗物选择屏幕已打开
- 点击跳过按钮跳过选择

**参数：** 无

---

## 水晶球小游戏

### ExecuteCrystalSphereSetTool - 设置水晶球工具

```csharp
private static Dictionary<string, object?> ExecuteCrystalSphereSetTool(Dictionary<string, JsonElement> data)
{
    var overlay = NOverlayStack.Instance?.Peek();
    if (overlay is not NCrystalSphereScreen screen)
        return Error("Crystal Sphere screen is not open");

    if (!data.TryGetValue("tool", out var toolElem))
        return Error("Missing 'tool' (expected 'big' or 'small')");

    string tool = toolElem.GetString() ?? "";
    var button = tool switch
    {
        "big" => screen.GetNodeOrNull<NClickableControl>("%BigDivinationButton"),
        "small" => screen.GetNodeOrNull<NClickableControl>("%SmallDivinationButton"),
        _ => null
    };

    if (button == null)
        return Error($"Unknown Crystal Sphere tool: {tool}");
    if (!button.Visible || !button.IsEnabled)
        return Error($"Crystal Sphere tool '{tool}' is not available");

    button.ForceClick();
    return new Dictionary<string, object?>
    {
        ["status"] = "ok",
        ["message"] = $"Setting Crystal Sphere tool to {tool}"
    };
}
```

**职责：**

- 验证水晶球屏幕已打开
- 根据工具类型点击对应按钮

**参数：**

- `tool`: 工具类型（`"big"` 或 `"small"`，必需）

---

### ExecuteCrystalSphereClickCell - 点击水晶球格子

```csharp
private static Dictionary<string, object?> ExecuteCrystalSphereClickCell(Dictionary<string, JsonElement> data)
{
    var overlay = NOverlayStack.Instance?.Peek();
    if (overlay is not NCrystalSphereScreen screen)
        return Error("Crystal Sphere screen is not open");

    if (!data.TryGetValue("x", out var xElem))
        return Error("Missing 'x' (cell x-coordinate)");
    if (!data.TryGetValue("y", out var yElem))
        return Error("Missing 'y' (cell y-coordinate)");

    int x = xElem.GetInt32();
    int y = yElem.GetInt32();

    var cell = FindAll<NCrystalSphereCell>(screen)
        .FirstOrDefault(c => c.Entity.X == x && c.Entity.Y == y);
    if (cell == null)
        return Error($"Crystal Sphere cell ({x}, {y}) was not found");
    if (!cell.Entity.IsHidden || !cell.Visible)
        return Error($"Crystal Sphere cell ({x}, {y}) is not clickable");

    cell.EmitSignal(NClickableControl.SignalName.Released, cell);
    return new Dictionary<string, object?>
    {
        ["status"] = "ok",
        ["message"] = $"Clicking Crystal Sphere cell ({x}, {y})"
    };
}
```

**职责：**

- 验证水晶球屏幕已打开
- 根据坐标查找并点击对应格子

**参数：**

- `x`: 格子 X 坐标（必需）
- `y`: 格子 Y 坐标（必需）

---

### ExecuteCrystalSphereProceed - 水晶球继续

```csharp
private static Dictionary<string, object?> ExecuteCrystalSphereProceed()
{
    var overlay = NOverlayStack.Instance?.Peek();
    if (overlay is not NCrystalSphereScreen screen)
        return Error("Crystal Sphere screen is not open");

    var proceedButton = screen.GetNodeOrNull<NProceedButton>("%ProceedButton");
    if (proceedButton is not { IsEnabled: true })
        return Error("Crystal Sphere proceed button is not enabled");

    proceedButton.ForceClick();
    return new Dictionary<string, object?>
    {
        ["status"] = "ok",
        ["message"] = "Proceeding from Crystal Sphere"
    };
}
```

**职责：**

- 验证水晶球屏幕已打开
- 点击继续按钮退出水晶球

**参数：** 无

---

## 多人模式

### ExecuteUndoEndTurn - 撤销结束回合（多人模式）

```csharp
private static Dictionary<string, object?> ExecuteUndoEndTurn(Player player)
{
    if (!CombatManager.Instance.IsInProgress)
        return Error("Not in combat");
    if (!CombatManager.Instance.IsPlayPhase)
        return Error("Not in play phase — cannot act during enemy turn");
    if (CombatManager.Instance.PlayerActionsDisabled)
        return Error("Player actions are currently disabled");
    if (!player.Creature.IsAlive)
        return Error("Player creature is dead");
    if (!CombatManager.Instance.IsPlayerReadyToEndTurn(player))
        return Error("Not ready to end turn — nothing to undo");

    var combatState = player.Creature.CombatState;
    if (combatState == null)
        return Error("No combat state");

    int roundNumber = combatState.RoundNumber;
    RunManager.Instance.ActionQueueSynchronizer.RequestEnqueue(
        new UndoEndPlayerTurnAction(player, roundNumber));

    return new Dictionary<string, object?>
    {
        ["status"] = "ok",
        ["message"] = "Undid end turn — continue playing cards"
    };
}
```

**职责：**

- 验证战斗状态和玩家已提交结束回合
- 通过 ActionQueue 撤销结束回合
- 仅用于多人模式

**参数：** 无

---

# Corner Cases 检查结果

以下是根据 snippets.md 对照 C# 实现发现的遗漏检查。

---

## PlayCardCommand.cs

**遗漏的检查：**

1. `CombatManager.Instance.IsInProgress` - 未检查是否在战斗中
2. `CombatManager.Instance.IsPlayPhase` - 未检查是否在玩家回合
3. `CombatManager.Instance.PlayerActionsDisabled` - 未检查玩家行动是否被禁用
4. `player.Creature.IsAlive` - 未检查玩家是否存活

**当前状态：** 只检查了 `combatState != null`，但未检查战斗阶段和行动权限。

**建议修复：**

```csharp
public CommandResult Execute(string word)
{
    // ... existing Pass checks ...

    if (!CombatManager.Instance.IsInProgress)
        return CommandResult.Failed;
    if (!CombatManager.Instance.IsPlayPhase)
        return CommandResult.Failed;
    if (CombatManager.Instance.PlayerActionsDisabled)
        return CommandResult.Failed;

    var combatState = CombatManager.Instance?.DebugOnlyGetState();
    // ... rest of the code ...

    var player = LocalContext.GetMe(combatState);
    if (player?.Creature?.IsAlive != true)
        return CommandResult.Failed;

    // ... rest of the code ...
}
```

---

## CardGridSelectCommand.cs

**遗漏的检查：**

1. holder `IsEnabled` - 未检查卡牌是否可选

**当前状态：** 只检查了 `Visible`，未检查 `IsEnabled`。

**建议修复：**

```csharp
// 在 ComputeSupportedWords 中，validHolders 过滤条件添加 IsEnabled
foreach (var holder in holders)
{
    if (!GodotObject.IsInstanceValid(holder) || !holder.IsInsideTree() || !holder.Visible || !holder.IsEnabled)
        continue;
    // ...
}
```

---

## CardRowSelectCommand.cs

**遗漏的检查：**

1. holder `IsEnabled` - 未检查卡牌是否可选

**当前状态：** 只检查了 `GodotObject.IsInstanceValid`，未检查 `IsEnabled`。

**建议修复：**

```csharp
// 在 Execute 中添加 IsEnabled 检查
if (!GodotObject.IsInstanceValid(holder) || !holder.IsEnabled)
{
    MainFile.Logger.Warn("CardSelectionCommand: holder is invalid or disabled");
    return CommandResult.Failed;
}
```

---

## EventCommand.cs

**遗漏的检查：**

1. `button.Option.IsLocked` - 未过滤锁定的选项

**当前状态：** 词表生成时检查了 `IsEnabled`，但 snippets.md 提到应该过滤 `IsLocked`。

**建议修复：**

```csharp
// 在 ComputeSupportedWords 中
var buttons = layout.OptionButtons
    .Where(b => GodotObject.IsInstanceValid(b) && b.IsInsideTree() && b.IsEnabled && !b.Option.IsLocked)
    .ToList();
```

---

## EndTurnCommand.cs

**遗漏的检查：**

1. `CombatManager.Instance.IsInProgress` - 未检查是否在战斗中
2. `CombatManager.Instance.IsPlayPhase` - 未检查是否在玩家回合
3. `CombatManager.Instance.PlayerActionsDisabled` - 未检查玩家行动是否被禁用
4. `hand.InCardPlay` - 未检查是否有卡牌正在被打出
5. `hand.CurrentMode != Mode.Play` - 未检查手牌是否在选择模式

**当前状态：** 只检查了 `endTurnButton != null`，完全没有战斗状态验证。

**建议修复：**

```csharp
public CommandResult Execute(string word)
{
    if (!CombatManager.Instance.IsInProgress)
        return CommandResult.Failed;
    if (!CombatManager.Instance.IsPlayPhase)
        return CommandResult.Failed;
    if (CombatManager.Instance.PlayerActionsDisabled)
        return CommandResult.Failed;

    var hand = NCombatRoom.Instance?.Ui?.Hand;
    if (hand != null && (hand.InCardPlay || hand.CurrentMode != NPlayerHand.Mode.Play))
        return CommandResult.Failed;

    var endTurnButton = NCombatRoom.Instance?.Ui?.EndTurnButton;
    if (endTurnButton == null)
        return CommandResult.Failed;

    endTurnButton.ForceClick();
    return CommandResult.Success;
}
```

---

## 汇总表

| 模块                    | 遗漏的检查                                                        | 严重程度 |
|-----------------------|--------------------------------------------------------------|------|
| PlayCardCommand       | `IsInProgress`, `IsPlayPhase`, `PlayerActionsDisabled`, 玩家存活 | 高    |
| CardGridSelectCommand | holder `IsEnabled`                                           | 中    |
| CardRowSelectCommand  | holder `IsEnabled`                                           | 中    |
| EventCommand          | 过滤 `IsLocked` 选项                                             | 低    |
| EndTurnCommand        | `IsInProgress`, `IsPlayPhase`, `PlayerActionsDisabled`, 手牌状态 | 高    |

---
