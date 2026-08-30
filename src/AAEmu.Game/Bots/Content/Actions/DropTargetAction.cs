using AAEmu.Game.Bots.Body.Positioning;
using AAEmu.Game.Bots.Kernel;

namespace AAEmu.Game.Bots.Content.Actions;

public sealed class DropTargetAction : IBotAction
{
    public string Name => "drop-target";

    public bool IsUseful(BotContext context)
    {
        var target = PositioningHelpers.Target(context);
        return target == null || target.IsDead ||
               (context.Bot.ParentWorld != null || target.ParentWorld != null) &&
               !ReferenceEquals(context.Bot.ParentWorld, target.ParentWorld) ||
               !context.Bot.CanAttack(target);
    }

    public bool IsPossible(BotContext context) => IsUseful(context);

    public BotActionResult Execute(BotContext context, BotEvent ev)
    {
        var state = context.Runtime.CombatState;
        state.Target = null;
        context.Bot.CurrentTarget = null;
        return BotActionResult.Success;
    }
}
