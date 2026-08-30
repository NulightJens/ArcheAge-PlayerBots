using AAEmu.Game.Bots.Kernel;

namespace AAEmu.Game.Bots.Body.Positioning;

public sealed class SetFacingAction : IBotAction
{
    private const double DedupeMilliseconds = 50;
    private readonly IBotMover _mover;
    private DateTime _lastSentAt = DateTime.MinValue;
    private float _lastAngle;

    public SetFacingAction(IBotMover mover = null)
    {
        _mover = mover ?? BotManagerMover.Instance;
    }

    public string Name => "set-facing";

    public bool IsUseful(BotContext context) => PositioningHelpers.IsValidTarget(PositioningHelpers.Target(context));

    public bool IsPossible(BotContext context) => IsUseful(context);

    public BotActionResult Execute(BotContext context, BotEvent ev)
    {
        var target = PositioningHelpers.Target(context);
        if (!PositioningHelpers.IsValidTarget(target))
            return BotActionResult.Impossible;

        var botPosition = context.Bot.Transform.World.Position;
        var targetPosition = target.Transform.World.Position;
        var angle = MathF.Atan2(targetPosition.Y - botPosition.Y, targetPosition.X - botPosition.X) * 180f / MathF.PI;
        if (_lastSentAt != DateTime.MinValue && context.Now - _lastSentAt < TimeSpan.FromMilliseconds(DedupeMilliseconds) &&
            MathF.Abs(angle - _lastAngle) < 0.01f)
            return BotActionResult.Success;

        var mover = context.Mover ?? _mover;
        mover.Face(context.Bot, angle);
        _lastAngle = angle;
        _lastSentAt = context.Now;
        return BotActionResult.Success;
    }
}
