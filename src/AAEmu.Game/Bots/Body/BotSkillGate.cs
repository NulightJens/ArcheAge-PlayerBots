using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Bots.Body;

public enum GateReason
{
    Ok,
    NoTemplate,
    Unlearned,
    Dead,
    TargetDead,
    Casting,
    Cooldown,
    GlobalCooldown,
    OutOfRange,
    NotEnoughMana,
    NotEnoughLabor,
    Controlled,
    WrongRelation
}

public readonly record struct GateResult(GateReason Reason, string Detail = null)
{
    public bool IsAllowed => Reason == GateReason.Ok;
}

public static class BotSkillGate
{
    internal static Func<Character, SkillTemplate, Unit, float, DateTime, BotConfig, bool, GateResult> CheckOverride { get; set; }

    public static GateResult Check(
        Character bot,
        SkillTemplate template,
        Unit target,
        float distance,
        DateTime now,
        BotConfig config = null,
        bool castWhileControlled = false)
    {
        return CheckOverride?.Invoke(bot, template, target, distance, now, config, castWhileControlled) ??
               CheckCore(bot, template, target, distance, now, config, castWhileControlled);
    }

    internal static GateResult CheckCore(
        Character bot,
        SkillTemplate template,
        Unit target,
        float distance,
        DateTime now,
        BotConfig config = null,
        bool castWhileControlled = false)
    {
        if (template == null)
            return Fail(GateReason.NoTemplate, "skill template is missing");
        if (bot == null || bot.IsDead)
            return Fail(GateReason.Dead, "bot is dead or missing");

        var selfTarget = template.TargetType == SkillTargetType.Self;
        var positionTarget = template.TargetType == SkillTargetType.Pos;
        if (!selfTarget && !positionTarget)
        {
            if (target == null)
                return Fail(GateReason.WrongRelation, "target is missing");
            if (target.IsDead && !template.TargetDead)
                return Fail(GateReason.TargetDead, "target is dead");
        }

        if (bot.SkillTask != null)
            return Fail(GateReason.Casting, "bot is already casting");

        if (!bot.IgnoreSkillCooldowns && bot.Cooldowns?.Cooldowns.TryGetValue(template.Id, out var cooldownEnd) == true && cooldownEnd > now)
            return Fail(GateReason.Cooldown, $"cooldown ends at {cooldownEnd:O}");

        if (!template.IgnoreGlobalCooldown)
        {
            if (bot.GlobalCooldown > now)
                return Fail(GateReason.GlobalCooldown, $"global cooldown ends at {bot.GlobalCooldown:O}");

            var globalDelayMs = Math.Max(0, (config ?? BotConfig.Instance).GlobalSkillDelayMs);
            if (globalDelayMs > 0 && bot.SkillLastUsed != DateTime.MinValue &&
                bot.SkillLastUsed.AddMilliseconds(globalDelayMs) > now)
                return Fail(GateReason.GlobalCooldown, $"global delay ends at {bot.SkillLastUsed.AddMilliseconds(globalDelayMs):O}");
        }

        if (!selfTarget && template.TargetType != SkillTargetType.Pos &&
            (distance < template.MinRange || distance > template.MaxRange))
            return Fail(GateReason.OutOfRange, $"distance {distance:F2} is outside [{template.MinRange}, {template.MaxRange}]");

        if (template.ManaCost > bot.Mp)
            return Fail(GateReason.NotEnoughMana, $"mana {bot.Mp} is below {template.ManaCost}");
        if (template.ConsumeLaborPower > bot.LaborPower)
            return Fail(GateReason.NotEnoughLabor, $"labor {bot.LaborPower} is below {template.ConsumeLaborPower}");

        if (!castWhileControlled && bot.Buffs?.HasEffectsMatchingCondition(static buff =>
                buff?.Template is { Stun: true } or { Root: true } or { Sleep: true } or { Knockdown: true }) == true)
            return Fail(GateReason.Controlled, "bot is stunned, rooted, sleeping, or knocked down");

        if (!castWhileControlled && template.CastingTime > 0 && bot.Buffs?.HasEffectsMatchingCondition(static buff => buff?.Template.Silence == true) == true)
            return Fail(GateReason.Controlled, "bot is silenced");

        if (!RelationMatches(bot, target, template, selfTarget))
            return Fail(GateReason.WrongRelation, "target relation does not match the skill");

        return new GateResult(GateReason.Ok);
    }

    private static bool RelationMatches(Character bot, Unit target, SkillTemplate template, bool selfTarget)
    {
        if (selfTarget || template.TargetType == SkillTargetType.Pos)
            return true;
        if (target == null)
            return false;

        var relation = template.TargetRelation;
        if (relation == SkillTargetRelation.Any)
        {
            relation = template.TargetType switch
            {
                SkillTargetType.Friendly or SkillTargetType.FriendlyOthers => SkillTargetRelation.Friendly,
                SkillTargetType.Party => SkillTargetRelation.Party,
                SkillTargetType.Raid => SkillTargetRelation.Raid,
                SkillTargetType.Hostile => SkillTargetRelation.Hostile,
                SkillTargetType.Others => SkillTargetRelation.Others,
                _ => SkillTargetRelation.Any
            };
        }

        return relation switch
        {
            SkillTargetRelation.Friendly => !bot.CanAttack(target),
            SkillTargetRelation.Hostile => bot.CanAttack(target),
            SkillTargetRelation.Others => bot.ObjId != target.ObjId,
            SkillTargetRelation.Party or SkillTargetRelation.Raid => true,
            _ => true
        };
    }

    private static GateResult Fail(GateReason reason, string detail) => new(reason, detail);
}
