using System.Numerics;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Bots.Social;

namespace AAEmu.Game.Bots.Body.Positioning;

public sealed class MaintainSpellRangeAction : IBotAction
{
    private const float SafetyMargin = 1f;
    private const float Tolerance = 1f;
    private const float CloseEscapeOutwardComponent = 0.25f;
    private const float CloseEscapeTangentComponent = 0.96824586f;

    private readonly IBotMover _mover;
    private readonly bool _tangentCloseEscape;
    private readonly bool _continueAfterMove;

    public MaintainSpellRangeAction(float maxRange, IBotMover mover = null, string name = null,
        bool tangentCloseEscape = false, bool continueAfterMove = false)
    {
        MaximumRange = Math.Max(0f, maxRange);
        PreferredRange = Math.Max(0f, MaximumRange - SafetyMargin);
        MinimumRange = Math.Max(0f, PreferredRange - Tolerance);
        _mover = mover ?? BotManagerMover.Instance;
        _tangentCloseEscape = tangentCloseEscape;
        _continueAfterMove = continueAfterMove;
        Name = string.IsNullOrWhiteSpace(name) ? "maintain-spell-range" : name;
    }

    public string Name { get; }
    public float MinimumRange { get; }
    public float PreferredRange { get; }
    public float MaximumRange { get; }

    public bool IsUseful(BotContext context)
    {
        var target = PositioningHelpers.Target(context);
        if (!PositioningHelpers.IsValidTarget(target))
            return false;

        var distance = PositioningHelpers.Distance(context.Bot, target);
        return distance < MinimumRange || distance > MaximumRange;
    }

    public bool IsPossible(BotContext context)
    {
        return IsUseful(context) && PreferredRange > 1f && PositioningHelpers.CanMoveForCombat(context);
    }

    public BotActionResult Execute(BotContext context, BotEvent ev)
    {
        var target = PositioningHelpers.Target(context);
        if (!PositioningHelpers.IsValidTarget(target) || PreferredRange <= 1f)
            return BotActionResult.Impossible;

        var distance = PositioningHelpers.Distance(context.Bot, target);
        var mover = context.Mover ?? _mover;
        if (distance >= MinimumRange && distance <= MaximumRange)
        {
            mover.StopIfMoving(context.Bot);
            return BotActionResult.Success;
        }

        var botPosition = context.Bot.Transform.World.Position;
        var targetPosition = target.Transform.World.Position;
        var direction = PositioningHelpers.HorizontalDirection(targetPosition, botPosition);
        if (direction == Vector3.Zero)
        {
            var angle = context.Bot.Id % 360 * MathF.PI / 180f;
            direction = new Vector3(MathF.Cos(angle), MathF.Sin(angle), 0f);
        }

        var destination = distance < MinimumRange && _tangentCloseEscape
            ? ComputeTangentCloseEscape(context.Bot.Id, target.ObjId, botPosition, targetPosition, direction)
            : new Vector3(
                targetPosition.X + direction.X * PreferredRange,
                targetPosition.Y + direction.Y * PreferredRange,
                targetPosition.Z);
        mover.SetDestination(context.Bot, destination, true, 0.5f);
        return _continueAfterMove ? BotActionResult.Impossible : BotActionResult.Success;
    }

    private Vector3 ComputeTangentCloseEscape(uint botId, uint targetObjId, Vector3 botPosition,
        Vector3 targetPosition, Vector3 outward)
    {
        var side = ((botId ^ targetObjId) & 1) == 0 ? 1f : -1f;
        var tangent = new Vector3(-outward.Y * side, outward.X * side, 0f);
        var escapeDirection = outward * CloseEscapeOutwardComponent + tangent * CloseEscapeTangentComponent;
        var currentRadius = Vector2.Distance(
            new Vector2(botPosition.X, botPosition.Y),
            new Vector2(targetPosition.X, targetPosition.Y));
        var travel = -currentRadius * CloseEscapeOutwardComponent + MathF.Sqrt(MathF.Max(0f,
            PreferredRange * PreferredRange - currentRadius * currentRadius *
            (1f - CloseEscapeOutwardComponent * CloseEscapeOutwardComponent)));

        return new Vector3(
            botPosition.X + escapeDirection.X * travel,
            botPosition.Y + escapeDirection.Y * travel,
            targetPosition.Z);
    }
}
