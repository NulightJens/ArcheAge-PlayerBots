using System.Numerics;
using AAEmu.Game.Bots.Body;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Bots.Content.Actions;

public sealed class BeginSearchAction : IBotAction
{
    private readonly IBotMover _mover;

    public BeginSearchAction(IBotMover mover = null)
    {
        _mover = mover ?? BotManagerMover.Instance;
    }

    public string Name => "begin-search";

    public bool IsUseful(BotContext context)
    {
        var target = context.Runtime.CombatState.Target as Unit ?? context.Bot.CurrentTarget as Unit;
        return target?.Transform != null && target.Buffs.HasEffectsMatchingCondition(effect => effect.Template.Stealth);
    }

    public bool IsPossible(BotContext context) => IsUseful(context);

    public BotActionResult Execute(BotContext context, BotEvent ev)
    {
        var state = context.Runtime.CombatState;
        var target = state.Target as Unit ?? context.Bot.CurrentTarget as Unit;
        if (target?.Transform == null)
            return BotActionResult.Impossible;

        state.LastKnownTargetPosition = target.Transform.World.Position;
        state.Target = null;
        state.SearchStartTime = context.Now;
        state.IsSearching = true;
        state.SearchRadius = 0f;
        state.SearchAngle = 0f;
        state.TransitionTo(BotCombatStateType.Searching);
        var mover = context.Mover ?? _mover;
        mover.StopIfMoving(context.Bot);
        return BotActionResult.Success;
    }
}
