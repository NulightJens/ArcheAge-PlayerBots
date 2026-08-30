using AAEmu.Game.Bots.Body;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Units;
using NLog;

namespace AAEmu.Game.Bots.Content.Actions;

public sealed class RestAction : IBotAction
{
    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();
    private readonly IBotMover _mover;

    public RestAction(IBotMover mover = null)
    {
        _mover = mover ?? BotManagerMover.Instance;
    }

    public string Name => "rest tick";

    public bool IsUseful(BotContext context)
    {
        return context.Runtime.CombatState.CurrentState == BotCombatStateType.Resting;
    }

    public bool IsPossible(BotContext context) => true;

    public BotActionResult Execute(BotContext context, BotEvent ev)
    {
        var state = context.Runtime.CombatState;
        var mover = context.Mover ?? _mover;
        if (state.LastRestHealTick == DateTime.MinValue)
        {
            state.LastRestHealTick = context.Now;
        }

        var restHealInterval = (float)context.Config.RestHealInterval;
        var restHealPercent = context.Config.RestHealPercentPerTick;
        if ((context.Now - state.LastRestHealTick).TotalSeconds >= restHealInterval)
        {
            if (HpPercent(context.Bot) < 100)
            {
                var healAmount = Math.Max(1, (int)(context.Bot.MaxHp * restHealPercent / 100f));
                context.Bot.Hp = Math.Min(context.Bot.MaxHp, context.Bot.Hp + healAmount);
                context.Bot.BroadcastPacket(new SCUnitPointsPacket(context.Bot.ObjId, context.Bot.Hp, context.Bot.Mp
#if PLAYERBOTS_AAEMU_3_0
                    , context.Bot.HighAbilityRsc
#endif
                ), true);
            }

            state.LastRestHealTick = context.Now;
        }

        if (context.Bot.Hp >= context.Bot.MaxHp)
        {
            state.IsResting = false;
            state.RestorePreviousState();
            state.RevertToForcedState();
            state.SentRelaxedAfterCombat = false;
            Log(context, $"BOT id={context.Bot.Id} ev=rest_complete");
        }

        var attacker = context.TryDefend();
        if (attacker != null)
        {
            state.Target = attacker;
            state.TransitionTo(BotCombatStateType.Combat);
            state.SentRelaxedAfterCombat = false;
            Log(context, $"BOT id={context.Bot.Id} ev=rest_interrupted target={attacker.ObjId}");
        }
        else if (!context.Bot.IsInBattle || context.Bot.Hp >= context.Bot.MaxHp)
            mover.StopIfMoving(context.Bot);
        else if (context.Bot.AggroTable.Count > 0)
        {
            var firstAggro = context.Bot.AggroTable.Values.FirstOrDefault();
            if (firstAggro?.Owner != null && !firstAggro.Owner.IsDead)
            {
                state.Target = firstAggro.Owner;
                state.TransitionTo(BotCombatStateType.Combat);
                state.SentRelaxedAfterCombat = false;
                Log(context, $"BOT id={context.Bot.Id} ev=rest_interrupted target={state.Target.ObjId}");
            }
        }

        return BotActionResult.Success;
    }

    private static int HpPercent(AAEmu.Game.Models.Game.Units.Unit unit)
    {
        return unit.MaxHp <= 0 ? 0 : (int)((float)unit.Hp / unit.MaxHp * 100);
    }

    private static void Log(BotContext context, string message)
    {
        Logger.Trace(message);
        context.EventSink?.Invoke(message[(message.IndexOf("ev=", StringComparison.Ordinal))..]);
    }
}
