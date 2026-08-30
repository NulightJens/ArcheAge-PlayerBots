using System.Numerics;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Models.Game.Bots;

namespace AAEmu.Game.Bots.Body.Positioning;

public sealed class FleeAction : IBotAction
{
    private readonly IBotMover _mover;
    private readonly float _distance;
    private readonly bool _jitter;

    public FleeAction(IBotMover mover = null, float? distance = null, bool jitter = false)
    {
        _mover = mover ?? BotManagerMover.Instance;
        _distance = distance ?? (float)BotConfig.Instance.FleeDistance;
        _jitter = jitter;
    }

    public FleeAction(IBotMover mover, AAEmu.Game.Models.Game.Bots.BotConfig config)
        : this(mover, (float)config.FleeDistance)
    {
    }

    public string Name => "flee";

    public bool IsUseful(BotContext context) => PositioningHelpers.IsValidTarget(PositioningHelpers.Target(context));

    public bool IsPossible(BotContext context) => IsUseful(context) && _distance > 0;

    public BotActionResult Execute(BotContext context, BotEvent ev)
    {
        var target = PositioningHelpers.Target(context);
        if (!PositioningHelpers.IsValidTarget(target) || _distance <= 0)
            return BotActionResult.Impossible;

        var botPosition = context.Bot.Transform.World.Position;
        var targetPosition = target.Transform.World.Position;
        var direction = PositioningHelpers.HorizontalDirection(botPosition, targetPosition);
        if (direction == Vector3.Zero)
            return BotActionResult.Impossible;

        var directionWithJitter = direction;
        if (_jitter)
        {
            var angleOffset = (Math.Sin(context.Now.Ticks / 1e7) * 15d + 15d) * Math.PI / 180d;
            directionWithJitter = Vector3.Transform(direction,
                Matrix4x4.CreateRotationZ((float)angleOffset));
        }
        var mover = context.Mover ?? _mover;
        mover.SetDestination(context.Bot, botPosition - Vector3.Normalize(directionWithJitter) * _distance,
            true, float.MaxValue);
        return BotActionResult.Success;
    }
}
