using System.Numerics;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.World;

namespace AAEmu.Game.Bots.Body;

public static class AreaTriggerContainment
{
    public static bool Contains(AreaTrigger trigger, Vector3 position)
    {
        if (trigger?.Shape == null)
            return false;

        var center = trigger.Owner?.Transform?.World.Position ?? trigger.Caster?.Transform?.World.Position;
        if (center == null)
            return false;

        if (trigger.Shape.Type == AreaShapeType.Sphere)
        {
            var radius = trigger.Shape.Value1 > 0 ? trigger.Shape.Value1 : BotConfig.DefaultHazardRadius;
            return Vector3.Distance(center.Value, position) <= radius;
        }

        if (trigger.Shape.Type != AreaShapeType.Cuboid)
            return false;

        var relative = position - center.Value;
        var yaw = trigger.Owner?.Transform?.World.Rotation.Z ?? trigger.Caster?.Transform?.World.Rotation.Z ?? 0f;
        var cos = MathF.Cos(yaw);
        var sin = MathF.Sin(yaw);
        var localX = relative.X * cos + relative.Y * sin;
        var localY = -relative.X * sin + relative.Y * cos;
        return MathF.Abs(localX) <= MathF.Abs(trigger.Shape.Value1) * 0.5f &&
            MathF.Abs(localY) <= MathF.Abs(trigger.Shape.Value2) * 0.5f &&
            MathF.Abs(relative.Z) <= MathF.Abs(trigger.Shape.Value3);
    }
}
