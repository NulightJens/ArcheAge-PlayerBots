using System.Numerics;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Bots.Social;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Bots.Body.Positioning;

internal static class PositioningHelpers
{
    public static Unit Target(BotContext context)
    {
        return context.Runtime.CombatState.Target as Unit ?? context.Bot.CurrentTarget as Unit;
    }

    public static bool IsValidTarget(Unit target)
    {
        return target?.Transform != null && !target.IsDead;
    }

    public static bool CanMoveForCombat(BotContext context)
    {
        return context.Runtime.Social.TeamId == 0 ||
               context.Runtime.Social.MovementOrder != BotMovementOrder.Stay ||
               context.Runtime.CombatState.CurrentState is BotCombatStateType.Combat or BotCombatStateType.Dueling;
    }

    public static float Distance(Unit first, Unit second)
    {
        if (first?.Transform == null || second?.Transform == null)
            return float.MaxValue;
        return Vector3.Distance(first.Transform.World.Position, second.Transform.World.Position);
    }

    public static Vector3 HorizontalDirection(Vector3 from, Vector3 to)
    {
        var direction = new Vector2(to.X - from.X, to.Y - from.Y);
        if (direction.LengthSquared() < 0.0001f)
            return Vector3.Zero;
        direction = Vector2.Normalize(direction);
        return new Vector3(direction.X, direction.Y, 0f);
    }
}
