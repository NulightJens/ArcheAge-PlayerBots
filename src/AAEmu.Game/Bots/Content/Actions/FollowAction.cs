using System.Numerics;
using AAEmu.Game.Bots.Body;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Bots.Social;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Units;
using NLog;

namespace AAEmu.Game.Bots.Content.Actions;

public sealed class FollowAction : IBotAction
{
    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();
    private readonly IBotMover _mover;

    public FollowAction(IBotMover mover = null)
    {
        _mover = mover ?? BotManagerMover.Instance;
    }

    public string Name => "follow tick";

    public bool IsUseful(BotContext context)
    {
        return context.Runtime.CombatState.CurrentState == BotCombatStateType.Following;
    }

    public bool IsPossible(BotContext context)
    {
        return context.Runtime.MovementState.FollowTarget != null;
    }

    public BotActionResult Execute(BotContext context, BotEvent ev)
    {
        var state = context.Runtime.CombatState;
        var followTarget = context.Runtime.MovementState.FollowTarget;
        var mover = context.Mover ?? _mover;
        if (followTarget == null)
        {
            state.TransitionTo(BotCombatStateType.Idle);
            return BotActionResult.Success;
        }

        var social = context.Runtime.Social;
        if (social.TeamId != 0 && !social.IsMasterAvailable(followTarget))
        {
            social.SafeHold();
            mover.StopImmediately(context.Bot);
            return BotActionResult.Success;
        }

        var partyControlled = social.TeamId != 0;
        if ((!partyControlled || social.CombatOrder == BotCombatOrder.Assist) &&
            followTarget.IsInBattle && followTarget.CurrentTarget is Unit unit)
        {
            if (!IsStealthed(followTarget) && state.Target != unit)
            {
                state.Target = unit;
                state.TransitionTo(BotCombatStateType.Combat);
                Log(context, $"BOT id={context.Bot.Id} ev=assist target={unit.ObjId}");
            }
        }
        else
        {
            if (state.CurrentState == BotCombatStateType.Combat && state.Target != null)
            {
                state.Target = null;
                state.TransitionTo(BotCombatStateType.Following);
                mover.StopImmediately(context.Bot);
                BotCombatManager.SendRelaxedStance(context.Bot, mover);
            }

            ApplyFollowBand(context, followTarget);
            RelaxOnce(context);
        }

        var playerAttacker = partyControlled && social.CombatOrder == BotCombatOrder.Passive
            ? null
            : context.TryDefend();
        if (playerAttacker != null)
        {
            state.Target = playerAttacker;
            state.TransitionTo(BotCombatStateType.Combat);
            Log(context, $"BOT id={context.Bot.Id} ev=defend target={playerAttacker.ObjId} state=following");
        }

        return BotActionResult.Success;
    }

    private void ApplyFollowBand(BotContext context, Character followTarget)
    {
        if (context.Bot.Transform == null || followTarget.Transform == null)
            return;

        var movement = context.Runtime.MovementState;
        var desired = movement.FollowDistance;
        var hasFormationSlot = movement.FormationSlot >= 0;
        var followPosition = hasFormationSlot
            ? BotFormation.PositionFor(followTarget, movement)
            : followTarget.Transform.World.Position;
        var distance = Vector3.Distance(context.Bot.Transform.World.Position, followPosition);
        var band = (float)context.Config.FollowStopBand;
        var mover = context.Mover ?? _mover;

        // Formation bots must retain FollowTarget after reaching their slot so the
        // slot keeps rotating and translating with the leader. StopFollow is only
        // valid for legacy direct-distance following because it clears that target.
        if (hasFormationSlot)
        {
            if (distance > 0.35f + band)
                mover.Follow(context.Bot, followTarget, desired);
            else if (distance <= 0.35f)
                mover.StopIfMoving(context.Bot);
            return;
        }

        if (distance > desired + band)
            mover.Follow(context.Bot, followTarget, desired);
        else if (distance < Math.Max(0, desired - band))
            mover.StopFollow(context.Bot);
    }

    private static bool IsStealthed(Unit unit)
    {
        return unit != null && unit.Buffs.HasEffectsMatchingCondition(effect => effect.Template.Stealth);
    }

    private void RelaxOnce(BotContext context)
    {
        var state = context.Runtime.CombatState;
        if (state.SentRelaxedAfterCombat)
            return;

        var mover = context.Mover ?? _mover;
        BotCombatManager.SendRelaxedStance(context.Bot, mover);
        state.SentRelaxedAfterCombat = true;
    }

    private static void Log(BotContext context, string message)
    {
        Logger.Trace(message);
        context.EventSink?.Invoke(message[(message.IndexOf("ev=", StringComparison.Ordinal))..]);
    }
}
