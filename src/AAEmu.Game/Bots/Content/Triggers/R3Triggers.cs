using System.Numerics;
using AAEmu.Game.Bots.Blackboard;
using AAEmu.Game.Bots.Social;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Bots.Content.Triggers;

public sealed class EnemyOutOfMeleeTrigger : IBotTrigger
{
    public string Name => "enemy-out-of-melee";
    public int CheckIntervalMs => 100;
    public BotEvent Event => new(Name);

    public bool IsActive(BotContext context)
    {
        var target = TriggerHelpers.Target(context);
        return TriggerHelpers.ValidTarget(context, target) && TriggerHelpers.Distance(context.Bot, target) > context.Config.AttackRange;
    }
}

public sealed class EnemyOutOfSpellRangeTrigger : IBotTrigger
{
    private readonly uint _skillId;
    private readonly Func<uint, SkillTemplate> _templateResolver;
    private readonly float? _maxRange;

    public EnemyOutOfSpellRangeTrigger()
    {
        // Legacy registration has no skill row, so this uses the per-context BowRange fallback.
    }

    public EnemyOutOfSpellRangeTrigger(float maxRange)
    {
        _maxRange = maxRange;
    }

    public EnemyOutOfSpellRangeTrigger(uint skillId, Func<uint, SkillTemplate> templateResolver)
    {
        _skillId = skillId;
        _templateResolver = templateResolver;
    }

    public string Name => "enemy-out-of-spell-range";
    public int CheckIntervalMs => 100;
    public BotEvent Event => new(Name, _skillId);

    public bool IsActive(BotContext context)
    {
        var target = TriggerHelpers.Target(context);
        if (!TriggerHelpers.ValidTarget(context, target))
            return false;

        var maxRange = _maxRange ?? _templateResolver?.Invoke(_skillId)?.MaxRange ?? context.Config.BowRange;
        return TriggerHelpers.Distance(context.Bot, target) > maxRange;
    }
}

public sealed class NotFacingTargetTrigger : IBotTrigger
{
    public string Name => "not-facing-target";
    public int CheckIntervalMs => 100;
    public BotEvent Event => new(Name);

    public bool IsActive(BotContext context)
    {
        var target = TriggerHelpers.Target(context);
        if (!TriggerHelpers.ValidTarget(context, target))
            return false;

        var from = context.Bot.Transform.World.Position;
        var to = target.Transform.World.Position;
        if (Vector2.DistanceSquared(new Vector2(from.X, from.Y), new Vector2(to.X, to.Y)) < 0.0001f)
            return false;
        var desired = MathF.Atan2(to.Y - from.Y, to.X - from.X);
        var facing = context.Bot.Transform.World.Rotation.Z + MathF.PI / 2f;
        return MathF.Abs(TriggerHelpers.NormalizeRadians(desired - facing)) > 0.1f;
    }
}

public sealed class NotBehindTargetTrigger : IBotTrigger
{
    public string Name => "not-behind-target";
    public int CheckIntervalMs => 100;
    public BotEvent Event => new(Name);

    public bool IsActive(BotContext context)
    {
        var target = TriggerHelpers.Target(context);
        if (!TriggerHelpers.ValidTarget(context, target))
            return false;

        var targetPosition = target.Transform.World.Position;
        var botPosition = context.Bot.Transform.World.Position;
        var fromTarget = MathF.Atan2(botPosition.Y - targetPosition.Y, botPosition.X - targetPosition.X);
        var behind = target.Transform.World.Rotation.Z + MathF.PI;
        return MathF.Abs(TriggerHelpers.NormalizeRadians(fromTarget - behind)) > MathF.PI / 3f;
    }
}

public sealed class LowHealthTrigger : IBotTrigger
{
    private readonly int _percent;

    public LowHealthTrigger(int percent = 50)
    {
        _percent = percent;
    }

    public string Name => "low-health";
    public int CheckIntervalMs => 250;
    public BotEvent Event => new(Name, _percent);

    public bool IsActive(BotContext context)
    {
        return context.Bot.MaxHp > 0 && context.Bot.Hp * 100 <= context.Bot.MaxHp * _percent;
    }
}

public sealed class TargetInvalidTrigger : IBotTrigger
{
    public string Name => "target-invalid";
    public int CheckIntervalMs => 100;
    public BotEvent Event => new(Name);

    public bool IsActive(BotContext context)
    {
        var target = TriggerHelpers.Target(context);
        var invalid = target != null && (target.IsDead ||
            (context.Bot.ParentWorld != null || target.ParentWorld != null) &&
            !ReferenceEquals(context.Bot.ParentWorld, target.ParentWorld) ||
            !context.Bot.CanAttack(target));
        if (invalid)
        {
            context.Runtime.HostMetrics?.RecordInvalidTarget();
            if (target.IsDead)
                context.Runtime.HostMetrics?.RecordObservedKill();
        }
        return invalid;
    }
}

public sealed class TargetStealthedTrigger : IBotTrigger
{
    public string Name => "target-stealthed";
    public int CheckIntervalMs => 100;
    public BotEvent Event => new(Name);

    public bool IsActive(BotContext context)
    {
        var target = TriggerHelpers.Target(context);
        var state = context.Runtime.CombatState;
        var isDuelOpponent = state.InDuel && ReferenceEquals(target, state.DuelOpponent);
        var isValidTarget = target?.Transform != null && !target.IsDead &&
                            (isDuelOpponent || context.Bot.CanAttack(target));
        return isValidTarget && target.Buffs.HasEffectsMatchingCondition(effect => effect.Template.Stealth);
    }
}

public sealed class StuckTrigger : IBotTrigger
{
    public string Name => "stuck";
    public int CheckIntervalMs => 250;
    public BotEvent Event => new(Name);

    public bool IsActive(BotContext context)
    {
        return context.Runtime.MovementState.Destination.HasValue && context.Bot.Transform != null && context.Runtime.StuckWatch.IsStuck(
            context.Now,
            context.Bot.Transform.World.Position,
            hasDestination: true);
    }
}

public sealed class LeaderInCombatTrigger : IBotTrigger
{
    public string Name => "leader-in-combat";
    public int CheckIntervalMs => 100;
    public BotEvent Event => new(Name);

    public bool IsActive(BotContext context)
    {
        var leader = context.Runtime.MovementState.FollowTarget;
        return leader?.IsInBattle == true && leader.CurrentTarget is Unit target && !target.IsDead;
    }
}

public sealed class FollowDistanceTrigger : IBotTrigger
{
    public string Name => "follow-distance";
    public int CheckIntervalMs => 100;
    public BotEvent Event => new(Name, "too-far");

    public bool IsActive(BotContext context)
    {
        var leader = context.Runtime.MovementState.FollowTarget;
        if (leader?.Transform == null || context.Bot.Transform == null)
            return false;

        var movement = context.Runtime.MovementState;
        var hasFormationSlot = movement.FormationSlot >= 0;
        var targetPosition = hasFormationSlot
            ? BotFormation.PositionFor(leader, movement)
            : leader.Transform.World.Position;
        var distance = Vector3.Distance(context.Bot.Transform.World.Position, targetPosition);
        var desired = hasFormationSlot ? 0.35f : movement.FollowDistance;
        return distance > desired + (float)context.Config.FollowStopBand;
    }
}

internal static class TriggerHelpers
{
    public static Unit Target(BotContext context)
    {
        return context.Runtime.CombatState.Target as Unit ?? context.Bot.CurrentTarget as Unit;
    }

    public static bool ValidTarget(BotContext context, Unit target)
    {
        return target?.Transform != null && !target.IsDead && context.Bot.CanAttack(target);
    }

    public static float Distance(Unit first, Unit second)
    {
        return Vector3.Distance(first.Transform.World.Position, second.Transform.World.Position);
    }

    public static float NormalizeRadians(float angle)
    {
        while (angle > MathF.PI)
            angle -= MathF.Tau;
        while (angle < -MathF.PI)
            angle += MathF.Tau;
        return angle;
    }
}
