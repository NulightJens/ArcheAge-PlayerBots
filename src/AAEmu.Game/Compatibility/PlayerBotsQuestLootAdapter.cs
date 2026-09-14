using System;
using System.Collections.Generic;
using System.Linq;

using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.NPChar;

using AAEmu.Game.Core.Managers;

namespace AAEmu.Game.Compatibility;

internal static class PlayerBotsQuestLootAdapter
{
    internal static bool CanLootCorpseTag(Character bot, Npc corpse)
    {
        if (bot == null || corpse == null) return false;
#if PLAYERBOTS_AAEMU_3_0
        var tag = corpse.CharacterTagging;
        return tag != null && tag.TagTeam == 0 && ReferenceEquals(tag.Tagger, bot);
#else
        // NPC death clears CharacterTagging after native loot generation.
        // The loot container retains the actual eligible player snapshot.
        return corpse.LootingContainer?.PlayerBotsCanLoot(bot) == true;
#endif
    }

    internal static IReadOnlyList<Item> GetCorpseLoot(Npc corpse)
    {
#if PLAYERBOTS_AAEMU_3_0
        return corpse == null ? [] : ItemManager.Instance.GetLootDropItems(corpse.ObjId).ToArray();
#else
        return corpse?.LootingContainer?.Items.Values.Select(entry => entry.Item).ToArray() ?? [];
#endif
    }

    internal static bool TryTakeCorpseLoot(Character bot, Npc corpse, Item item, out int remainingItems) =>
        TryTakeCorpseLoot(bot, corpse, item, out remainingItems, out _);

    internal static bool TryTakeCorpseLoot(Character bot, Npc corpse, Item item, out int remainingItems, out bool consumedByNativeDistribution)
    {
        remainingItems = 0;
        consumedByNativeDistribution = false;
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
        // RotateWinner can grant to another member and return false to the
        // initiator. Consumed entry means handled, never self quest credit.
        consumedByNativeDistribution = !taken &&
            !corpse.LootingContainer.Items.ContainsKey(exactEntry.Key);
        return taken;
#endif
    }
}
