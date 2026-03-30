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
    ///     缓存的词表，SupportedWords getter 直接返回此缓存
    /// </summary>
    private HashSet<string> _cachedWords = new(StringComparer.Ordinal);

    /// <summary>
    ///     当前预览索引
    /// </summary>
    private int _currentPreviewIndex;

    /// <summary>
    ///     当前显示 HoverTip 的 slot
    /// </summary>
    private NMerchantSlot? _currentPreviewSlot;

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

        if (inventory.Inventory == null)
        {
            MainFile.Logger.Warn("ShopCommand: shop inventory not ready");
            return CommandResult.Failed;
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

            if (!slot.Entry.IsStocked) continue;

            switch (slot)
            {
                // 删牌服务
                case NMerchantCardRemoval:
                    _normalizedToRaw[VoiceText.Normalize(RemoveCard)] = RemoveCard;
                    continue;
                // 卡牌名
                case NMerchantCard { Entry : MerchantCardEntry { CreationResult.Card: { } card } }:
                {
                    var cardName = VoiceText.Normalize(card.Title);
                    if (cardName.Length > 0) _normalizedToRaw[cardName] = cardName;
                    continue;
                }
                // 遗物名
                case NMerchantRelic { Entry: MerchantRelicEntry { Model: { } relic } }:
                {
                    var relicName = VoiceText.Normalize(relic.Title.GetFormattedText());
                    if (relicName.Length > 0)
                        _normalizedToRaw[relicName] = relicName;

                    continue;
                }
                // 药水名
                case NMerchantPotion { Entry: MerchantPotionEntry { Model: { } potion } }:
                {
                    var potionName = VoiceText.Normalize(potion.Title.GetFormattedText());
                    if (potionName.Length > 0)
                        _normalizedToRaw[potionName] = potionName;
                    break;
                }
            }
        }

        return new HashSet<string>(_normalizedToRaw.Keys, StringComparer.Ordinal);
    }

    private void PreviewFirstRelic(NMerchantInventory inventory)
    {
        _currentPreviewIndex = 0;

        List<NMerchantSlot> items = [.. GetRelicSlots(inventory), ..GetPotionSlots(inventory)];
        if (items.Count == 0)
        {
            MainFile.Logger.Warn("ShopCommand: no relics or potions to preview");
            return;
        }

        ShowHoverTipForSlot(items[0]);
        MainFile.Logger.Debug("ShopCommand: preview first item");
    }

    private void PreviewNextItem(NMerchantInventory inventory)
    {
        List<NMerchantSlot> items = [.. GetRelicSlots(inventory), .. GetPotionSlots(inventory)];
        if (items.Count == 0)
        {
            MainFile.Logger.Warn("ShopCommand: no items to preview");
            return;
        }

        _currentPreviewIndex = (_currentPreviewIndex + 1) % items.Count;
        ShowHoverTipForSlot(items[_currentPreviewIndex]);
        MainFile.Logger.Debug($"ShopCommand: preview item {_currentPreviewIndex + 1}/{items.Count}");
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

        switch (slot)
        {
            case NMerchantRelic { Entry: MerchantRelicEntry { Model: { } relic } }:
                NHoverTipSet.CreateAndShow(slot, relic.HoverTips, HoverTipAlignment.Center);
                TryFocus(slot);
                break;
            case NMerchantPotion { Entry: MerchantPotionEntry { Model: { } potion } }:
                NHoverTipSet.CreateAndShow(slot, potion.HoverTips, HoverTipAlignment.Center);
                TryFocus(slot);
                break;
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

            // 直接调用购买逻辑（和 MCP 一致）
            _ = entry.OnTryPurchaseWrapper(inventory.Inventory);
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

            var itemName = slot switch
            {
                NMerchantCard { Entry: MerchantCardEntry { CreationResult.Card: { } card } } => VoiceText.Normalize(
                    card.Title),
                NMerchantRelic { Entry: MerchantRelicEntry { Model: { } relic } } => VoiceText.Normalize(
                    relic.Title.GetFormattedText()),
                NMerchantPotion { Entry: MerchantPotionEntry { Model: { } potion } } => VoiceText.Normalize(
                    potion.Title.GetFormattedText()),
                _ => null
            };

            if (itemName != normalizedName) continue;

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