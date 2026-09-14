#if !PLAYERBOTS_AAEMU_3_0
using System.Runtime.CompilerServices;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.GameData;
using AAEmu.Game.Models.Game.Items.Templates;
using AAEmu.Game.Models.Game.Items.Loots;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Static;
using AAEmu.Game.Models.Game.Skills.Effects;
using AAEmu.Game.Models.Game.Skills.Templates;

namespace AAEmu.Game.Bots.Equipment;

internal sealed record BotGearContainer(SkillTemplate Skill, ItemTemplate[] Contents, int RequiredFreeSlots);

internal static class BotGearContainers
{
    private sealed record CachedContainer(BotGearContainer Container);
    private static readonly ConditionalWeakTable<ItemTemplate, CachedContainer> Cache = new();

    internal static BotGearContainer Read(ItemTemplate item)
    {
        if (item == null || item.UseSkillId == 0) return null;
        // Game-data templates are immutable after loading; a reload replaces their identities.
        // Cache negative results too, avoiding repeated global reagent-table scans for consumables.
        return Cache.GetValue(item, static template =>
        {
            var manager = SkillManager.Instance;
            var container = Read(template, manager.GetSkillTemplate,
                LootGameData.Instance.GetPack, ItemManager.Instance.GetTemplate);
            if (container != null && (manager.GetSkillReagentsBySkillId(template.UseSkillId).Count > 0 ||
                manager.GetSkillProductsBySkillId(template.UseSkillId).Count > 0)) container = null;
            return new CachedContainer(container);
        }).Container;
    }

    // Inspect metadata only: never roll a loot pack to choose a reward.
    internal static BotGearContainer Read(ItemTemplate item, Func<uint, SkillTemplate> skills,
        Func<uint, LootPack> packs, Func<uint, ItemTemplate> items)
    {
        if (item == null || item.UseSkillId == 0) return null;
        var skill = skills(item.UseSkillId);
        if (skill == null || skill.TargetType != SkillTargetType.Self || skill.ConsumeLaborPower != 0 ||
            skill.CastingTime < 0 || skill.EffectDelay < 0 ||
            (long)skill.CastingTime + skill.EffectDelay > 25000 ||
            skill.PlotOnly || skill.ChannelingTime != 0 || skill.EffectRepeatCount > 1 ||
            skill.Effects.Count != 1) return null;
        var effect = skill.Effects[0];
        if (effect.ConsumeItemId != 0 || effect.ItemSetId != 0 ||
            effect.Template is not GainLootPackItemEffect loot || loot.ConsumeItemId != 0) return null;
        // AAEmu can consume the package in Skill.ApplyEffects or in the loot effect.
        // Accept exactly one native source charge; never free generators or double consumption.
        var skillConsumes = effect.ConsumeSourceItem || item.UseSkillAsReagent;
        if (skillConsumes == loot.ConsumeSourceItem || skillConsumes && effect.ConsumeItemCount != 1)
            return null;
        var pack = packs(loot.LootPackId);
        if (pack?.Loots == null || pack.Loots.Count == 0 || pack.Loots.Count > 64) return null;
        var contents = new List<ItemTemplate>();
        var slots = 0;
        foreach (var entry in pack.Loots)
        {
            var output = items(entry.ItemId);
            if (output is not (ArmorTemplate or WeaponTemplate or AccessoryTemplate) || entry.MaxAmount <= 0)
                return null;
            contents.Add(output);
            // Reserve the upper bound even for random alternatives. Native consumption happens
            // before GiveLootPack; do not count the soon-to-be-consumed source slot as free.
            slots += (int)Math.Ceiling((double)entry.MaxAmount / Math.Max(1, output.MaxCount));
            if (slots > 256) return null;
        }
        return new(skill, contents.ToArray(), slots);
    }

    internal static int Rank(AbilityType role, ItemTemplate item)
    {
        var container = Read(item);
        if (container == null) return BotEquipmentPolicy.Rank(role, item);
        return container.Contents.Min(output => BotEquipmentPolicy.Rank(role, output));
    }
}
#endif
