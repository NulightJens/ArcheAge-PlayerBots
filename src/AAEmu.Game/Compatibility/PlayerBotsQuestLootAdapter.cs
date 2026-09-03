using System;
using System.Collections.Generic;
using System.Linq;

using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.NPChar;

#if PLAYERBOTS_AAEMU_3_0
using AAEmu.Game.Core.Managers;
#endif

namespace AAEmu.Game.Compatibility;

internal static class PlayerBotsQuestLootAdapter
{
    internal static IReadOnlyList<Item> GetCorpseLoot(Npc corpse)
    {
#if PLAYERBOTS_AAEMU_3_0
        return corpse == null ? [] : ItemManager.Instance.GetLootDropItems(corpse.ObjId).ToArray();
#else
        return corpse?.LootingContainer?.Items.Values.Select(entry => entry.Item).ToArray() ?? [];
#endif
    }

    internal static bool TryTakeCorpseLoot(Character bot, Npc corpse, Item item, out int remainingItems)
    {
        remainingItems = 0;
        if (bot == null || corpse == null || item == null)
            return false;

#if PLAYERBOTS_AAEMU_3_0
        var loot = ItemManager.Instance.GetLootDropItems(corpse.ObjId);
        var exactItem = loot.FirstOrDefault(candidate =>
            ReferenceEquals(candidate, item) || candidate.Id == item.Id);
        if (exactItem == null)
        {
            remainingItems = loot.Count;
            return false;
        }

        var taken = ItemManager.Instance.TookLootDropItem(bot, loot, exactItem, exactItem.Count);
        remainingItems = loot.Count;
        return taken;
#else
        var exactEntry = corpse.LootingContainer.Items.FirstOrDefault(candidate =>
            ReferenceEquals(candidate.Value.Item, item) || candidate.Value.Item.Id == item.Id);
        if (exactEntry.Value == null)
        {
            remainingItems = corpse.LootingContainer.Items.Count;
            return false;
        }

        var taken = corpse.LootingContainer.TryTakeLoot(bot, exactEntry.Key, exactEntry.Value, false);
        remainingItems = corpse.LootingContainer.Items.Count;
        return taken;
#endif
    }
}
