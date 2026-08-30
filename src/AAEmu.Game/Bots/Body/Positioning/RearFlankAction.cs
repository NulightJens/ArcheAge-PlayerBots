using System.Numerics;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Bots.Social;
using AAEmu.Game.Models.Game.Bots;

namespace AAEmu.Game.Bots.Body.Positioning;

public sealed class RearFlankAction : IBotAction
{
    private readonly IBotMover _mover;
    private readonly Func<float> _roll;

    public RearFlankAction(IBotMover mover = null, Func<float> roll = null)
    {
        _mover = mover ?? BotManagerMover.Instance;
        _roll = roll ?? (() => (BotHost.Instance.Roll() % 101) / 100f);
    }

    public string Name => "rear-flank";

    public bool IsUseful(BotContext context) => PositioningHelpers.IsValidTarget(PositioningHelpers.Target(context));

    public bool IsPossible(BotContext context) => IsUseful(context) && PositioningHelpers.CanMoveForCombat(context);

    public BotActionResult Execute(BotContext context, BotEvent ev)
    {
        var target = PositioningHelpers.Target(context);
        if (!PositioningHelpers.IsValidTarget(target))
            return BotActionResult.Impossible;

        var targetPosition = target.Transform.World.Position;
        var targetFacing = target.Transform.World.Rotation.Z;
        var offsetDegrees = 90f + Math.Clamp(_roll(), 0f, 1f) * 30f;
        var angle = targetFacing + offsetDegrees * MathF.PI / 180f;
        var radius = (float)Math.Max(0, context.Config.AttackRange) * 0.5f;
        var destination = targetPosition + new Vector3(MathF.Cos(angle), MathF.Sin(angle), 0f) * radius;
        var mover = context.Mover ?? _mover;
        mover.SetDestination(context.Bot, destination, true, float.MaxValue);
        return BotActionResult.Success;
    }
}
