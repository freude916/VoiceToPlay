using Godot;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Shops;
using VoiceToPlay.Voice;
using VoiceToPlay.Voice.Core;

namespace VoiceToPlay.Commands.Shop;

/// <summary>
///     商店命令。支持卡牌名、遗物名、药水名、"删牌"、"查看物品"、"下一个" 等。
/// </summary>
public sealed class ShopCommand : IVoiceCommand
{
    private const string RemoveCard = "删牌";
    private const string PreviewItem = "查看物品";
    private const string NextItem = "下一个";

    private readonly Dictionary<string, string> _normalizedToRaw = new();

    /// <summary>
    ///     当前 HoverTip 状态：用于在遗物和药水之间切换
    /// </summary>
    private int _currentPreviewIndex;

    /// <summary>
    ///     当前预览模式：0=遗物, 1=药水
    /// </summary>
    private int _previewMode;

    /// <summary>
    ///     当前显示 HoverTip 的 slot
    /// </summary>
    private NMerchantSlot? _currentPreviewSlot;

    /// <summary>
    ///     缓存的词表，SupportedWords getter 直接返回此缓存
    /// </summary>
    private HashSet<string> _cachedWords = new(StringComparer.Ordinal);

    public ShopCommand()
    {
        Instance = this;
    }

    public static ShopCommand? Instance { get; private set; }

    /// <summary>
    ///     只返回缓存，不做任何计算
    /// </summary>
    public IEnumerable<string> SupportedWords => _cachedWords;

    public CommandResult Execute(string word)
    {
        var merchantRoom = NMerchantRoom.Instance;
        if (merchantRoom == null)
        {
            MainFile.Logger.Warn("ShopCommand: NMerchantRoom.Instance is null");
            return CommandResult.Failed;
        }

        // 商店未打开时，先打开商店
        var inventory = merchantRoom.Inventory;

        // 打开商店界面
        if (!inventory.IsOpen)
        {
            merchantRoom.OpenInventory();
            MainFile.Logger.Debug("ShopCommand: opened inventory");
            return CommandResult.Success;
        }

        var normalizedRemove = VoiceText.Normalize(RemoveCard);
        var normalizedPreview = VoiceText.Normalize(PreviewItem);
        var normalizedNext = VoiceText.Normalize(NextItem);

        if (word == normalizedPreview)
        {
            PreviewFirstRelic(inventory);
            return CommandResult.Success;
        }

        if (word == normalizedNext)
        {
            PreviewNextItem(inventory);
            return CommandResult.Success;
        }

        if (word == normalizedRemove) return TryBuyCardRemoval(inventory);

        // 可能是物品名称
        return TryBuyByName(inventory, word);
    }

    public event Action<IVoiceCommand>? VocabularyChanged;

    /// <summary>
    ///     刷新词表缓存，由 Patch 调用
    /// </summary>
    public static void RefreshVocabulary()
    {
        var instance = Instance;
        if (instance == null) return;

        var newWords = instance.ComputeSupportedWords();
        if (!newWords.SetEquals(instance._cachedWords))
        {
            instance._cachedWords = newWords;
            instance.VocabularyChanged?.Invoke(instance);
        }
    }

    /// <summary>
    ///     清理 HoverTip，由退出房间的 Patch 调用
    /// </summary>
    public static void ClearHoverTips()
    {
        var instance = Instance;
        if (instance == null) return;

        instance._currentPreviewIndex = 0;
        instance._previewMode = 0;

        // 清理所有 slot 上的 HoverTip
        var merchantRoom = NMerchantRoom.Instance;
        if (merchantRoom?.Inventory == null) return;

        foreach (var slot in merchantRoom.Inventory.GetAllSlots())
            if (GodotObject.IsInstanceValid(slot))
                NHoverTipSet.Remove(slot);
    }

    /// <summary>
    ///     计算当前支持的词表（只在 RefreshVocabulary 中调用）
    /// </summary>
    private HashSet<string> ComputeSupportedWords()
    {
        _normalizedToRaw.Clear();

        var merchantRoom = NMerchantRoom.Instance;
        if (merchantRoom == null) return [];

        var inventory = merchantRoom.Inventory;
        if (inventory.Inventory == null) return [];

        MainFile.Logger.Debug("ShopCommand.ComputeSupportedWords: computing vocabulary");

        // 静态命令
        _normalizedToRaw[VoiceText.Normalize(PreviewItem)] = PreviewItem;
        _normalizedToRaw[VoiceText.Normalize(NextItem)] = NextItem;

        // 获取所有 slot
        var slots = inventory.GetAllSlots().ToList();

        foreach (var slot in slots)
        {
            if (!GodotObject.IsInstanceValid(slot)) continue;
            if (!slot.Visible) continue;

            var entry = slot.Entry;
            if (!entry.IsStocked) continue;

            // 删牌服务
            if (slot is NMerchantCardRemoval)
            {
                _normalizedToRaw[VoiceText.Normalize(RemoveCard)] = RemoveCard;
                continue;
            }

            // 卡牌名
            if (slot is NMerchantCard)
            {
                if (entry is MerchantCardEntry cardEntry)
                {
                    var cardModel = cardEntry.CreationResult?.Card;
                    if (cardModel != null)
                    {
                        var cardName = VoiceText.Normalize(cardModel.Title);
                        if (cardName.Length > 0)
                            _normalizedToRaw[cardName] = cardName;
                    }
                }

                continue;
            }

            // 遗物名
            if (slot is NMerchantRelic)
            {
                if (entry is MerchantRelicEntry relicEntry)
                {
                    var relicModel = relicEntry.Model;
                    if (relicModel != null)
                    {
                        var relicName = VoiceText.Normalize(relicModel.Title.GetFormattedText());
                        if (relicName.Length > 0)
                            _normalizedToRaw[relicName] = relicName;
                    }
                }

                continue;
            }

            // 药水名
            if (slot is NMerchantPotion)
            {
                if (entry is MerchantPotionEntry potionEntry)
                {
                    var potionModel = potionEntry.Model;
                    if (potionModel != null)
                    {
                        var potionName = VoiceText.Normalize(potionModel.Title.GetFormattedText());
                        if (potionName.Length > 0)
                            _normalizedToRaw[potionName] = potionName;
                    }
                }
            }
        }

        return new HashSet<string>(_normalizedToRaw.Keys, StringComparer.Ordinal);
    }

    private void PreviewFirstRelic(NMerchantInventory inventory)
    {
        _previewMode = 0;
        _currentPreviewIndex = 0;

        var relics = GetRelicSlots(inventory);
        if (relics.Count == 0)
        {
            // 没有遗物，尝试药水
            var potions = GetPotionSlots(inventory);
            if (potions.Count > 0)
            {
                _previewMode = 1;
                ShowHoverTipForSlot(potions[0]);
                MainFile.Logger.Debug("ShopCommand: preview first potion (no relics)");
            }
            else
            {
                MainFile.Logger.Warn("ShopCommand: no relics or potions to preview");
            }

            return;
        }

        ShowHoverTipForSlot(relics[0]);
        MainFile.Logger.Debug("ShopCommand: preview first relic");
    }

    private void PreviewNextItem(NMerchantInventory inventory)
    {
        var relics = GetRelicSlots(inventory);
        var potions = GetPotionSlots(inventory);

        if (relics.Count == 0 && potions.Count == 0)
        {
            MainFile.Logger.Warn("ShopCommand: no items to preview");
            return;
        }

        // 切换逻辑：
        // mode 0 = 遗物, mode 1 = 药水
        // 先遍历完遗物，再遍历药水，然后循环回第一个遗物
        if (_previewMode == 0)
        {
            // 当前在遗物模式
            if (relics.Count > 0)
            {
                _currentPreviewIndex++;
                if (_currentPreviewIndex >= relics.Count)
                {
                    // 切换到药水
                    _previewMode = 1;
                    _currentPreviewIndex = 0;
                    if (potions.Count > 0)
                    {
                        ShowHoverTipForSlot(potions[0]);
                        MainFile.Logger.Debug("ShopCommand: switch to first potion");
                    }
                    else
                    {
                        // 没有药水，回到第一个遗物
                        _previewMode = 0;
                        _currentPreviewIndex = 0;
                        ShowHoverTipForSlot(relics[0]);
                        MainFile.Logger.Debug("ShopCommand: no potions, back to first relic");
                    }
                }
                else
                {
                    ShowHoverTipForSlot(relics[_currentPreviewIndex]);
                    MainFile.Logger.Debug($"ShopCommand: preview relic {_currentPreviewIndex + 1}");
                }
            }
            else
            {
                // 没有遗物，切换到药水
                _previewMode = 1;
                _currentPreviewIndex = 0;
                if (potions.Count > 0)
                {
                    ShowHoverTipForSlot(potions[0]);
                    MainFile.Logger.Debug("ShopCommand: no relics, switch to first potion");
                }
            }
        }
        else
        {
            // 当前在药水模式
            if (potions.Count > 0)
            {
                _currentPreviewIndex++;
                if (_currentPreviewIndex >= potions.Count)
                {
                    // 切换回遗物
                    _previewMode = 0;
                    _currentPreviewIndex = 0;
                    if (relics.Count > 0)
                    {
                        ShowHoverTipForSlot(relics[0]);
                        MainFile.Logger.Debug("ShopCommand: switch back to first relic");
                    }
                    else
                    {
                        // 没有遗物，回到第一个药水
                        _previewMode = 1;
                        _currentPreviewIndex = 0;
                        ShowHoverTipForSlot(potions[0]);
                        MainFile.Logger.Debug("ShopCommand: no relics, back to first potion");
                    }
                }
                else
                {
                    ShowHoverTipForSlot(potions[_currentPreviewIndex]);
                    MainFile.Logger.Debug($"ShopCommand: preview potion {_currentPreviewIndex + 1}");
                }
            }
            else
            {
                // 没有药水，切换回遗物
                _previewMode = 0;
                _currentPreviewIndex = 0;
                if (relics.Count > 0)
                {
                    ShowHoverTipForSlot(relics[0]);
                    MainFile.Logger.Debug("ShopCommand: no potions, switch to first relic");
                }
            }
        }
    }

    private void ShowHoverTipForSlot(NMerchantSlot slot)
    {
        if (!GodotObject.IsInstanceValid(slot) || !slot.IsInsideTree()) return;

        // 先清理上一个 HoverTip
        if (_currentPreviewSlot != null && GodotObject.IsInstanceValid(_currentPreviewSlot))
            NHoverTipSet.Remove(_currentPreviewSlot);

        // 更新当前 slot
        _currentPreviewSlot = slot;

        // 清理当前 slot 上已有的 HoverTip
        NHoverTipSet.Remove(slot);

        var entry = slot.Entry;

        switch (slot)
        {
            case NMerchantRelic:
            {
                if (entry is MerchantRelicEntry relicEntry)
                {
                    var relicModel = relicEntry.Model;
                    if (relicModel != null)
                    {
                        var tipSet = NHoverTipSet.CreateAndShow(slot, relicModel.HoverTips);
                        tipSet.GlobalPosition = slot.GlobalPosition;
                        if (slot.GlobalPosition.X > slot.GetViewport().GetVisibleRect().Size.X * 0.5f)
                        {
                            tipSet.SetAlignment(slot, HoverTipAlignment.Left);
                            tipSet.GlobalPosition -= slot.Size * 0.5f * slot.Scale;
                        }
                        else
                        {
                            tipSet.SetAlignment(slot, HoverTipAlignment.Right);
                            tipSet.GlobalPosition += Vector2.Right * slot.Size.X * 0.5f * slot.Scale +
                                                      Vector2.Up * slot.Size.Y * 0.5f * slot.Scale;
                        }
                    }
                }

                TryFocus(slot);
                break;
            }
            case NMerchantPotion:
            {
                if (entry is MerchantPotionEntry potionEntry)
                {
                    var potionModel = potionEntry.Model;
                    if (potionModel != null)
                    {
                        var tipSet = NHoverTipSet.CreateAndShow(slot, potionModel.HoverTips);
                        tipSet.GlobalPosition = slot.GlobalPosition;
                        if (slot.GlobalPosition.X > slot.GetViewport().GetVisibleRect().Size.X * 0.5f)
                        {
                            tipSet.SetAlignment(slot, HoverTipAlignment.Left);
                            tipSet.GlobalPosition -= slot.Size * 0.5f * slot.Scale;
                        }
                        else
                        {
                            tipSet.GlobalPosition += Vector2.Right * slot.Size.X * 0.5f * slot.Scale +
                                                      Vector2.Up * slot.Size.Y * 0.5f * slot.Scale;
                        }
                    }
                }

                TryFocus(slot);
                break;
            }
        }
    }

    private static List<NMerchantRelic> GetRelicSlots(NMerchantInventory inventory)
    {
        var result = new List<NMerchantRelic>();

        foreach (var slot in inventory.GetAllSlots())
        {
            if (slot is not NMerchantRelic relicSlot) continue;
            if (!GodotObject.IsInstanceValid(slot)) continue;
            if (!slot.Visible) continue;

            var entry = slot.Entry;
            if (!entry.IsStocked) continue;

            result.Add(relicSlot);
        }

        // 按 X 坐标排序
        result.Sort((a, b) => a.GlobalPosition.X.CompareTo(b.GlobalPosition.X));
        return result;
    }

    private static List<NMerchantPotion> GetPotionSlots(NMerchantInventory inventory)
    {
        var result = new List<NMerchantPotion>();

        foreach (var slot in inventory.GetAllSlots())
        {
            if (slot is not NMerchantPotion potionSlot) continue;
            if (!GodotObject.IsInstanceValid(slot)) continue;
            if (!slot.Visible) continue;

            var entry = slot.Entry;
            if (!entry.IsStocked) continue;

            result.Add(potionSlot);
        }

        // 按 X 坐标排序
        result.Sort((a, b) => a.GlobalPosition.X.CompareTo(b.GlobalPosition.X));
        return result;
    }

    private static CommandResult TryBuyCardRemoval(NMerchantInventory inventory)
    {
        foreach (var slot in inventory.GetAllSlots())
        {
            if (slot is not NMerchantCardRemoval removalSlot) continue;
            if (!GodotObject.IsInstanceValid(slot)) continue;

            var entry = slot.Entry;
            if (!entry.IsStocked) continue;

            // 检查金币是否足够
            if (!entry.EnoughGold)
            {
                MainFile.Logger.Warn("ShopCommand: not enough gold for card removal");
                return CommandResult.Failed;
            }

            // 删牌需要打开卡牌选择界面，使用模拟点击方式
            // 先让 slot 获得焦点（这会设置 _isHovered = true）
            TryFocus(removalSlot);
            removalSlot.Hitbox.DebugPress();
            removalSlot.Hitbox.DebugRelease();
            MainFile.Logger.Debug("ShopCommand: triggered card removal");
            return CommandResult.Success;
        }

        MainFile.Logger.Warn("ShopCommand: card removal not found");
        return CommandResult.Failed;
    }

    private static CommandResult TryBuyByName(NMerchantInventory inventory, string normalizedName)
    {
        foreach (var slot in inventory.GetAllSlots())
        {
            if (!GodotObject.IsInstanceValid(slot)) continue;
            if (!slot.Visible) continue;

            var entry = slot.Entry;
            if (!entry.IsStocked) continue;

            string? itemName = null;

            switch (slot)
            {
                case NMerchantCard:
                {
                    if (entry is MerchantCardEntry cardEntry)
                    {
                        var cardModel = cardEntry.CreationResult?.Card;
                        if (cardModel != null)
                            itemName = VoiceText.Normalize(cardModel.Title);
                    }

                    break;
                }
                case NMerchantRelic:
                {
                    if (entry is MerchantRelicEntry relicEntry)
                    {
                        var relicModel = relicEntry.Model;
                        if (relicModel != null)
                            itemName = VoiceText.Normalize(relicModel.Title.GetFormattedText());
                    }

                    break;
                }
                case NMerchantPotion:
                {
                    if (entry is MerchantPotionEntry potionEntry)
                    {
                        var potionModel = potionEntry.Model;
                        if (potionModel != null)
                            itemName = VoiceText.Normalize(potionModel.Title.GetFormattedText());
                    }

                    break;
                }
            }

            if (itemName == normalizedName)
            {
                // 检查金币是否足够
                if (!entry.EnoughGold)
                {
                    MainFile.Logger.Warn($"ShopCommand: not enough gold for '{normalizedName}'");
                    return CommandResult.Failed;
                }

                // 清理 HoverTip
                NHoverTipSet.Remove(slot);

                // 直接调用购买逻辑（绕过 UI 点击）
                var success = entry.OnTryPurchaseWrapper(inventory.Inventory).GetAwaiter().GetResult();
                if (success)
                {
                    MainFile.Logger.Debug($"ShopCommand: bought '{normalizedName}'");
                    return CommandResult.Success;
                }

                MainFile.Logger.Warn($"ShopCommand: purchase failed for '{normalizedName}'");
                return CommandResult.Failed;
            }
        }

        MainFile.Logger.Warn($"ShopCommand: item '{normalizedName}' not found");
        return CommandResult.Failed;
    }

    private static void TryFocus(NMerchantSlot slot)
    {
        if (!GodotObject.IsInstanceValid(slot) || !slot.IsInsideTree()) return;

        slot.SetFocusMode(Control.FocusModeEnum.All);
        slot.TryGrabFocus();
    }
}