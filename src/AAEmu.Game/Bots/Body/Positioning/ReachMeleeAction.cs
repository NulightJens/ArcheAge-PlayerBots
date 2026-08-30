using System.Numerics;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Bots.Social;
using AAEmu.Game.Models.Game.Bots;

namespace AAEmu.Game.Bots.Body.Positioning;

public sealed class ReachMeleeAction : IBotAction
{
    private readonly IBotMover _mover;

    public ReachMeleeAction(IBotMover mover = null)
    {
        _mover = mover ?? BotManagerMover.Instance;
    }

    public string Name => "reach-melee";

    public bool IsUseful(BotContext context) => PositioningHelpers.IsValidTarget(PositioningHelpers.Target(context));

    public bool IsPossible(BotContext context) => IsUseful(context) && PositioningHelpers.CanMoveForCombat(context);

    public BotActionResult Execute(BotContext context, BotEvent ev)
    {
        var target = PositioningHelpers.Target(context);
        if (!PositioningHelpers.IsValidTarget(target))
            return BotActionResult.Impossible;

        var botPosition = context.Bot.Transform.World.Position;
        var targetPosition = target.Transform.World.Position;
        var distance = PositioningHelpers.Distance(context.Bot, target);
        var attackRange = (float)Math.Max(0, context.Config.AttackRange);
        var mover = context.Mover ?? _mover;
        if (distance <= attackRange)
        {
            mover.StopIfMoving(context.Bot);
            return BotActionResult.Success;
        }

        var direction = targetPosition - botPosition;
        if (direction.LengthSquared() < 0.0001f)
            return BotActionResult.Impossible;
        direction = Vector3.Normalize(direction);

        mover.SetDestination(context.Bot, new Vector3(
            targetPosition.X - direction.X * attackRange,
            targetPosition.Y - direction.Y * attackRange,
            targetPosition.Z - direction.Z * attackRange), true, float.MaxValue);
        return BotActionResult.Success;
    }
}
