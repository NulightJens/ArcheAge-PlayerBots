using System.Numerics;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Bots.Social;

namespace AAEmu.Game.Bots.Body.Positioning;

public sealed class ReachSpellRangeAction : IBotAction
{
    private readonly IBotMover _mover;
    private readonly float _maxRange;

    public ReachSpellRangeAction(float maxRange, IBotMover mover = null, string name = null)
    {
        _maxRange = maxRange;
        _mover = mover ?? BotManagerMover.Instance;
        Name = string.IsNullOrWhiteSpace(name) ? "reach-spell-range" : name;
    }

    public string Name { get; }

    public bool IsUseful(BotContext context) => PositioningHelpers.IsValidTarget(PositioningHelpers.Target(context));

    public bool IsPossible(BotContext context) =>
        IsUseful(context) && _maxRange > 1f && PositioningHelpers.CanMoveForCombat(context);

    public BotActionResult Execute(BotContext context, BotEvent ev)
    {
        var target = PositioningHelpers.Target(context);
        if (!PositioningHelpers.IsValidTarget(target) || _maxRange <= 1f)
            return BotActionResult.Impossible;

        var botPosition = context.Bot.Transform.World.Position;
        var targetPosition = target.Transform.World.Position;
        var effectiveRange = _maxRange - 1f;
        var distance = PositioningHelpers.Distance(context.Bot, target);
        var mover = context.Mover ?? _mover;
        if (distance <= effectiveRange)
        {
            mover.StopIfMoving(context.Bot);
            return BotActionResult.Success;
        }

        var direction = PositioningHelpers.HorizontalDirection(botPosition, targetPosition);
        if (direction == Vector3.Zero)
            return BotActionResult.Impossible;

        mover.SetDestination(context.Bot, new Vector3(
            targetPosition.X - direction.X * effectiveRange,
            targetPosition.Y - direction.Y * effectiveRange,
            targetPosition.Z), true, float.MaxValue);
        return BotActionResult.Success;
    }
}
