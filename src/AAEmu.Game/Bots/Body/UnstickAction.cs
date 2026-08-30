using System.Numerics;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;

namespace AAEmu.Game.Bots.Body;

public sealed class UnstickAction : IBotAction
{
    private readonly BotStuckWatch _watch;
    private readonly IBotMover _mover;
    private readonly BotConfig _config;
    private Vector3? _originalDestination;

    public UnstickAction(BotStuckWatch watch = null, IBotMover mover = null, BotConfig config = null)
    {
        _watch = watch;
        _mover = mover ?? BotManagerMover.Instance;
        _config = config ?? BotConfig.Instance;
    }

    public string Name => "unstick";

    public bool IsUseful(BotContext context) => context.Runtime.MovementState.Destination.HasValue;

    public bool IsPossible(BotContext context) => IsUseful(context) && Watch(context).IsStuck(
        context.Now,
        context.Bot.Transform.World.Position,
        context.Runtime.MovementState.Destination.HasValue);

    public BotActionResult Execute(BotContext context, BotEvent ev)
    {
        var state = context.Runtime.MovementState;
        var mover = context.Mover ?? _mover;
        if (!state.Destination.HasValue || !Watch(context).IsStuck(context.Now, context.Bot.Transform.World.Position, true))
            return BotActionResult.Impossible;

        _originalDestination ??= state.Destination;
        var attempt = state.Attempts + 1;
        var teleportAttempts = Math.Max(1, _config.StuckTeleportAttempts);
        var age = context.Now >= state.LastMoveAt ? context.Now - state.LastMoveAt : TimeSpan.Zero;
        if (attempt >= teleportAttempts || age >= TimeSpan.FromSeconds(Math.Max(0, _config.StuckTeleportSeconds)))
        {
            mover.Teleport(context.Bot, _originalDestination.Value);
            context.Runtime.HostMetrics?.RecordStuckRecovery(teleport: true);
            BotStuckWatch.LogUnstick(context.Bot, attempt, "teleport");
            Watch(context).Reset(context.Bot.Transform.World.Position, context.Now);
            _originalDestination = null;
            return BotActionResult.Success;
        }

        var current = context.Bot.Transform.World.Position;
        var direction = HorizontalDirection(current, _originalDestination.Value);
        if (direction == Vector3.Zero)
            direction = new Vector3(1, 0, 0);
        var side = new Vector3(-direction.Y, direction.X, 0f);
        var sign = attempt % 2 == 1 ? 1f : -1f;
        var nudge = current + side * sign * (float)Math.Max(0, _config.StuckNudgeMeters);
        mover.SetDestination(context.Bot, nudge, true, 0.5f);
        state.Attempts = attempt;
        context.Runtime.HostMetrics?.RecordStuckRecovery(teleport: false);
        BotStuckWatch.LogUnstick(context.Bot, attempt, "nudge");
        return BotActionResult.Success;
    }

    private BotStuckWatch Watch(BotContext context) => _watch ?? context.Runtime.StuckWatch;

    private static Vector3 HorizontalDirection(Vector3 from, Vector3 to)
    {
        var direction = new Vector2(to.X - from.X, to.Y - from.Y);
        if (direction.LengthSquared() < 0.0001f)
            return Vector3.Zero;
        direction = Vector2.Normalize(direction);
        return new Vector3(direction.X, direction.Y, 0f);
    }
}
