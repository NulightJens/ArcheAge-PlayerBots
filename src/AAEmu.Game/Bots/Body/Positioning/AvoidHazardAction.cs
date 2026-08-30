using System.Numerics;
using AAEmu.Game.Bots.Blackboard;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.World;

namespace AAEmu.Game.Bots.Body.Positioning;

public sealed class AvoidHazardAction : IBotAction
{
    private readonly Func<BotContext, IReadOnlyList<AreaTrigger>> _hazards;
    private readonly IBotMover _mover;

    public AvoidHazardAction(
        Func<BotContext, IReadOnlyList<AreaTrigger>> hazards = null,
        IBotMover mover = null)
    {
        _hazards = hazards ?? DefaultHazards;
        _mover = mover ?? BotManagerMover.Instance;
    }

    public string Name => "avoid-hazard";

    public bool IsUseful(BotContext context) => FindNearest(context) != null;

    public bool IsPossible(BotContext context) => IsUseful(context);

    public BotActionResult Execute(BotContext context, BotEvent ev)
    {
        var nearest = FindNearest(context);
        if (nearest == null)
            return BotActionResult.Impossible;

        var center = Center(nearest);
        if (center == null)
            return BotActionResult.Impossible;

        var botPosition = context.Bot.Transform.World.Position;
        var mover = context.Mover ?? _mover;
        if (nearest.Shape.Type == AreaShapeType.Cuboid)
        {
            var destination = CuboidExit(nearest, botPosition, center.Value);
            mover.SetDestination(context.Bot, destination, true, 0.5f);
            return BotActionResult.Success;
        }

        var radius = Radius(nearest);
        var fromCenter = new Vector2(botPosition.X - center.Value.X, botPosition.Y - center.Value.Y);
        var casterDirection = nearest.Caster?.Transform == null
            ? Vector2.Zero
            : new Vector2(
                nearest.Caster.Transform.World.Position.X - center.Value.X,
                nearest.Caster.Transform.World.Position.Y - center.Value.Y);
        Vector2 direction;
        if (casterDirection.LengthSquared() > 0.0001f)
        {
            casterDirection = Vector2.Normalize(casterDirection);
            var perpendicular = new Vector2(-casterDirection.Y, casterDirection.X);
            var first = perpendicular * (radius + 1f);
            var second = -perpendicular * (radius + 1f);
            direction = Vector2.Dot(first, fromCenter) >= Vector2.Dot(second, fromCenter) ? first : second;
        }
        else
        {
            direction = fromCenter.LengthSquared() > 0.0001f
                ? Vector2.Normalize(fromCenter) * (radius + 1f)
                : new Vector2(radius + 1f, 0f);
        }

        mover.SetDestination(context.Bot, new Vector3(center.Value.X + direction.X, center.Value.Y + direction.Y, botPosition.Z), true, 0.5f);
        return BotActionResult.Success;
    }

    private AreaTrigger FindNearest(BotContext context)
    {
        if (context.Bot.Transform == null)
            return null;

        AreaTrigger nearest = null;
        var nearestDistance = float.MaxValue;
        foreach (var hazard in _hazards(context) ?? [])
        {
            if (hazard?.Shape == null || hazard.TargetRelation != AAEmu.Game.Models.Game.Skills.SkillTargetRelation.Hostile)
                continue;
            var center = Center(hazard);
            if (center == null)
                continue;

            var botPosition = context.Bot.Transform.World.Position;
            var inside = AreaTriggerContainment.Contains(hazard, botPosition);
            if (!inside)
                continue;

            var distance = Vector2.Distance(
                new Vector2(botPosition.X, botPosition.Y),
                new Vector2(center.Value.X, center.Value.Y));
            if (distance < nearestDistance)
            {
                nearest = hazard;
                nearestDistance = distance;
            }
        }

        return nearest;
    }

    private static Vector3? Center(AreaTrigger trigger)
    {
        return trigger.Owner?.Transform?.World.Position ?? trigger.Caster?.Transform?.World.Position;
    }

    private static float Radius(AreaTrigger trigger)
    {
        return trigger.Shape.Value1 > 0 ? trigger.Shape.Value1 : BotConfig.DefaultHazardRadius;
    }

    private static Vector3 CuboidExit(AreaTrigger trigger, Vector3 position, Vector3 center)
    {
        var local = ToCuboidLocal(trigger, position, center);
        var halfX = MathF.Abs(trigger.Shape.Value1) * 0.5f;
        var halfY = MathF.Abs(trigger.Shape.Value2) * 0.5f;
        var distanceToXFace = halfX - MathF.Abs(local.X);
        var distanceToYFace = halfY - MathF.Abs(local.Y);
        if (distanceToXFace <= distanceToYFace)
            local.X = (local.X < 0 ? -1f : 1f) * (halfX + 1f);
        else
            local.Y = (local.Y < 0 ? -1f : 1f) * (halfY + 1f);

        var yaw = Yaw(trigger);
        var cos = MathF.Cos(yaw);
        var sin = MathF.Sin(yaw);
        return new Vector3(
            center.X + local.X * cos - local.Y * sin,
            center.Y + local.X * sin + local.Y * cos,
            position.Z);
    }

    private static Vector2 ToCuboidLocal(AreaTrigger trigger, Vector3 position, Vector3 center)
    {
        var relative = position - center;
        var yaw = Yaw(trigger);
        var cos = MathF.Cos(yaw);
        var sin = MathF.Sin(yaw);
        return new Vector2(
            relative.X * cos + relative.Y * sin,
            -relative.X * sin + relative.Y * cos);
    }

    private static float Yaw(AreaTrigger trigger)
    {
        return trigger.Owner?.Transform?.World.Rotation.Z ?? trigger.Caster?.Transform?.World.Rotation.Z ?? 0f;
    }

    private static IReadOnlyList<AreaTrigger> DefaultHazards(BotContext context)
    {
        return context.Blackboard.TryGet(BotValues.HostileAreaTriggersNearby, context.Now, out var hazards)
            ? hazards
            : Array.Empty<AreaTrigger>();
    }
}
