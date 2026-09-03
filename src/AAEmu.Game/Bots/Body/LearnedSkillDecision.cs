using System.Numerics;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Static;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Bots.Body;

/// <summary>
/// Chooses an offensive action from the character's live learned-skill set.
/// This path has no archetype rotation table and no fixed skill IDs.
/// </summary>
public static class LearnedSkillDecision
{
    internal static readonly TimeSpan ZeroCooldownReuseDelay = TimeSpan.FromSeconds(2);
    internal const int ManaReservePercent = 25;

    public static SkillTemplate Select(
        Character bot,
        Unit target,
        BotCombatState state,
        DateTime now,
        BotConfig config = null)
    {
        if (bot?.Skills?.Skills == null || target == null || state == null)
            return null;

        var reserve = Math.Max(0, bot.MaxMp * ManaReservePercent / 100);
        SkillTemplate selected = null;
        foreach (var learned in bot.Skills.Skills.Values)
        {
            var template = learned?.Template;
            if (!IsOffensive(template) || template.ManaCost > Math.Max(0, bot.Mp - reserve))
                continue;
            if (template.CooldownTime == 0 && state.LastSkillTime != DateTime.MinValue &&
                now - state.LastSkillTime < ZeroCooldownReuseDelay)
                continue;

            var distance = bot.Transform == null || target.Transform == null
                ? 0f
                : Vector3.Distance(bot.Transform.World.Position, target.Transform.World.Position);
            if (!BotSkillGate.Check(bot, template, target, distance, now, config).IsAllowed)
                continue;

            if (selected == null || Compare(template, selected) < 0)
                selected = template;
        }

        return selected;
    }

    internal static bool IsOffensive(SkillTemplate template) =>
        template != null &&
        template.Show &&
        template.NeedLearn &&
        (template.TargetType == SkillTargetType.Hostile || template.TargetRelation == SkillTargetRelation.Hostile);

    private static int Compare(SkillTemplate left, SkillTemplate right)
    {
        // Prefer a ready cooldown ability to filler, then conserve mana and keep
        // the choice deterministic without encoding a cast sequence.
        var comparison = (left.CooldownTime > 0 ? 0 : 1).CompareTo(right.CooldownTime > 0 ? 0 : 1);
        if (comparison != 0)
            return comparison;
        comparison = left.ManaCost.CompareTo(right.ManaCost);
        return comparison != 0 ? comparison : left.Id.CompareTo(right.Id);
    }
}
