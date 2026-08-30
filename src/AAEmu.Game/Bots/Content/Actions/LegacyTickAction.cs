using AAEmu.Game.Bots.Kernel;

namespace AAEmu.Game.Bots.Content.Actions;

public sealed class LegacyTickAction : IBotAction
{
    public string Name => "legacy tick";

    public bool IsUseful(BotContext context) => context.Brain != null;

    public bool IsPossible(BotContext context) => context.Brain != null;

    public BotActionResult Execute(BotContext context, BotEvent ev)
    {
        context.Brain.Step();
        return BotActionResult.Success;
    }
}
