using System.Numerics;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Bots.Social;

namespace AAEmu.Game.Bots.Body.Positioning;

public sealed class HealRecipientRangeAction : IBotAction
{
    private const float SafetyMargin = 1f;

    private readonly IBotMover _mover;

    public HealRecipientRangeAction(float maximumRange, IBotMover mover = null, string name = null)
    {
        MaximumRange = Math.Max(0f, maximumRange);
        PreferredRange = Math.Max(0f, MaximumRange - SafetyMargin);
        _mover = mover ?? BotManagerMover.Instance;
        Name = string.IsNullOrWhiteSpace(name) ? "position:heal-recipient" : name;
    }

    public string Name { get; }
    public float PreferredRange { get; }
    public float MaximumRange { get; }

    public bool IsUseful(BotContext context)
    {
        return PreferredRange > 0f && context.Runtime.Social.ResolveCommittedHealRecipient() != null;
    }

    public bool IsPossible(BotContext context)
    {
        return IsUseful(context);
    }

    public BotActionResult Execute(BotContext context, BotEvent ev)
    {
        var recipient = context.Runtime.Social.ResolveCommittedHealRecipient();
        if (recipient?.Transform == null || context.Bot.Transform == null || PreferredRange <= 0f)
            return BotActionResult.Impossible;

        var mover = context.Mover ?? _mover;
        if (!PositioningHelpers.CanMoveForCombat(context))
        {
            mover.StopIfMoving(context.Bot);
            return BotActionResult.Success;
        }

        var botPosition = context.Bot.Transform.World.Position;
        var recipientPosition = recipient.Transform.World.Position;
        if (Vector3.Distance(botPosition, recipientPosition) <= MaximumRange)
        {
            mover.StopIfMoving(context.Bot);
            return BotActionResult.Success;
        }

        var direction = PositioningHelpers.HorizontalDirection(recipientPosition, botPosition);
        if (direction == Vector3.Zero)
            return BotActionResult.Impossible;

        mover.SetDestination(context.Bot, new Vector3(
            recipientPosition.X + direction.X * PreferredRange,
            recipientPosition.Y + direction.Y * PreferredRange,
            recipientPosition.Z), true, 0.5f);
        return BotActionResult.Success;
    }
}
