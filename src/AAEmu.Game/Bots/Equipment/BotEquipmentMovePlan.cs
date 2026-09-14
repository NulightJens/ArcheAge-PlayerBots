#if !PLAYERBOTS_AAEMU_3_0
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.Items.Containers;
using AAEmu.Game.Models.Game.Items.Templates;
using AAEmu.Game.Models.Game.Skills;

namespace AAEmu.Game.Bots.Equipment;

/// <summary>A read-only selection snapshot, not packet admission or permission to execute.</summary>
internal sealed class BotEquipmentMovePlan
{
    private readonly Character _character;
    private readonly Inventory _inventory;
    private readonly ItemContainer _bag, _equipment;
    private readonly AbilityType _ability;
    private readonly int _level;
    private readonly Resident[] _residents;

    internal ulong ItemId { get; }
    internal ulong ExpectedDestinationItemId { get; }
    internal SlotType SourceType { get; }
    internal byte SourceSlot { get; }
    internal SlotType DestinationType { get; }
    internal byte DestinationSlot { get; }
    internal int RequiredFreeBagSlots { get; }

    private BotEquipmentMovePlan(Character bot, Item item, SlotType destinationType, byte destinationSlot,
        Resident[] residents, int requiredFreeBagSlots)
    {
        _character = bot;
        _inventory = bot.Inventory;
        _bag = _inventory.Bag;
        _equipment = _inventory.Equipment;
        _ability = bot.Ability1;
        _level = bot.Level;
        _residents = residents;
        ItemId = item.Id;
        ExpectedDestinationItemId = residents[1].Id;
        SourceType = item.SlotType;
        SourceSlot = (byte)item.Slot;
        DestinationType = destinationType;
        DestinationSlot = destinationSlot;
        RequiredFreeBagSlots = requiredFreeBagSlots;
    }

    internal static BotEquipmentMovePlan Create(Character bot, Item item, SlotType destinationType, byte destinationSlot)
    {
        var inventory = bot?.Inventory;
        if (inventory?.Bag == null || inventory.Equipment == null || item == null || item.Count != 1 ||
            item.Slot is < 0 or > byte.MaxValue ||
            item.SlotType is not (SlotType.Inventory or SlotType.Equipment) ||
            destinationType is not (SlotType.Inventory or SlotType.Equipment) ||
            (item.SlotType == destinationType && item.Slot == destinationSlot)) return null;

        var source = item.SlotType == SlotType.Inventory ? inventory.Bag : inventory.Equipment;
        var destination = destinationType == SlotType.Inventory ? inventory.Bag : inventory.Equipment;
        var target = destination.GetItemBySlot(destinationSlot);
        if (destinationType == SlotType.Equipment &&
            (!BotEquipmentPolicy.Usable(item.Template, bot.Level) ||
             !EquipmentContainer.GetAllowedGearSlots(item.Template).Contains((EquipmentItemSlot)destinationSlot))) return null;
        // Preserve native swap legality in both directions without invoking CanAccept or moving anything.
        if (item.SlotType == SlotType.Equipment && target != null &&
            (!BotEquipmentPolicy.Usable(target.Template, bot.Level) ||
             !EquipmentContainer.GetAllowedGearSlots(target.Template).Contains((EquipmentItemSlot)item.Slot))) return null;

        var residents = new List<Resident> { Resident.Capture(source, item.Slot), Resident.Capture(destination, destinationSlot) };
        if (!ReferenceEquals(residents[0].Item, item)) return null;
        var hands = (item.SlotType == SlotType.Equipment && item.Slot is 15 or 16) ||
                    (destinationType == SlotType.Equipment && destinationSlot is 15 or 16);
        var required = 0;
        if (hands)
        {
            var main = Resident.Capture(inventory.Equipment, (int)EquipmentItemSlot.Mainhand);
            var off = Resident.Capture(inventory.Equipment, (int)EquipmentItemSlot.Offhand);
            residents.Add(main);
            residents.Add(off);
            var unequipOff = (item.SlotType == SlotType.Equipment && item.Slot == 15 && TwoHanded(target)) ||
                             (destinationType == SlotType.Equipment && destinationSlot == 15 && TwoHanded(item));
            var unequipMain = TwoHanded(main.Item) &&
                ((item.SlotType == SlotType.Equipment && item.Slot == 16) ||
                 (destinationType == SlotType.Equipment && destinationSlot == 16));
            if (unequipOff && off.Item != null) required++;
            if (unequipMain && main.Item != null) required++;
        }
        var plan = new BotEquipmentMovePlan(bot, item, destinationType, destinationSlot, residents.ToArray(), required);
        return plan.IsCurrent(bot) ? plan : null;
    }

    internal bool IsCurrent(Character bot)
    {
        if (!ReferenceEquals(bot, _character) || !ReferenceEquals(bot.Inventory, _inventory) ||
            !ReferenceEquals(_inventory.Owner, bot) || !ReferenceEquals(_inventory.Bag, _bag) ||
            !ReferenceEquals(_inventory.Equipment, _equipment) || bot.Ability1 != _ability || bot.Level != _level ||
            _bag.OwnerId != bot.Id || _equipment.OwnerId != bot.Id ||
            _bag.ContainerType != SlotType.Inventory || _equipment.ContainerType != SlotType.Equipment ||
            _bag.ContainerSize is < 1 or > 256 || _equipment.ContainerSize is < 1 or > 256) return false;
        foreach (var resident in _residents)
            if (!resident.Matches(bot, _bag, _equipment)) return false;
        // Native two-hand swaps require a free bag slot before the candidate leaves the bag.
        return _bag.FreeSlotCount >= RequiredFreeBagSlots &&
               _bag.ContainerSize - _bag.Items.Count(item => item != null) >= RequiredFreeBagSlots;
    }

    private static bool TwoHanded(Item item) => item?.Template is WeaponTemplate weapon &&
        weapon.HoldableTemplate?.SlotTypeId == (uint)EquipmentItemSlotType.TwoHanded;

    private sealed record Resident(ItemContainer Container, int Slot, Item Item, ulong Id,
        ItemTemplate Template, uint TemplateId, int Count, byte Grade)
    {
        internal static Resident Capture(ItemContainer container, int slot)
        {
            var item = container.GetItemBySlot(slot);
            return new(container, slot, item, item?.Id ?? 0, item?.Template, item?.TemplateId ?? 0,
                item?.Count ?? 0, item?.Grade ?? 0);
        }

        internal bool Matches(Character bot, ItemContainer bag, ItemContainer equipment)
        {
            if (Slot < 0 || Slot >= Container.ContainerSize ||
                !ReferenceEquals(Container.GetItemBySlot(Slot), Item) ||
                Container.Items.Count(item => item != null && item.Slot == Slot) != (Item == null ? 0 : 1)) return false;
            if (Item == null) return true;
            return Id != 0 && Template != null && Count > 0 && Item.Id == Id && Item.OwnerId == bot.Id &&
                ReferenceEquals(Item._holdingContainer, Container) && Item.SlotType == Container.ContainerType &&
                Item.Slot == Slot && ReferenceEquals(Item.Template, Template) && Item.TemplateId == TemplateId &&
                Item.Count == Count && Item.Grade == Grade &&
                bag.Items.Concat(equipment.Items).Count(item => item?.Id == Id) == 1;
        }
    }
}
#endif
