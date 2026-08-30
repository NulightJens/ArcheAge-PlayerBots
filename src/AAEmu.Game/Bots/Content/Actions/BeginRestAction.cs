using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Models.Game.Bots;

namespace AAEmu.Game.Bots.Content.Actions;

public sealed class BeginRestAction : IBotAction
{
    public string Name => "begin-rest";

    public bool IsUseful(BotContext context)
    {
        return context.Runtime.CombatState.CurrentState != BotCombatStateType.Resting &&
            !context.Bot.IsInBattle &&
            context.Bot.MaxHp > 0 &&
            context.Bot.Hp * 100 <= context.Bot.MaxHp * context.Config.RestThresholdPercent;
    }

    public bool IsPossible(BotContext context) => IsUseful(context);

    public BotActionResult Execute(BotContext context, BotEvent ev)
    {
        context.Runtime.CombatState.IsResting = true;
        context.Runtime.CombatState.TransitionTo(BotCombatStateType.Resting);
        return BotActionResult.Success;
    }
}
