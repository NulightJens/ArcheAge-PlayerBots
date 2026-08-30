using System.Collections.Generic;

namespace AAEmu.Game.Bots.Kernel;

public interface IBotAction
{
    string Name { get; }
    bool IsUseful(BotContext context);
    bool IsPossible(BotContext context);
    BotActionResult Execute(BotContext context, BotEvent ev);
    IReadOnlyList<BotNextAction> Prerequisites => Array.Empty<BotNextAction>();
    IReadOnlyList<BotNextAction> Alternatives => Array.Empty<BotNextAction>();
    IReadOnlyList<BotNextAction> Continuers => Array.Empty<BotNextAction>();
}
