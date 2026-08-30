using AAEmu.Game.Bots.Blackboard;
using AAEmu.Game.Bots.Kernel;

namespace AAEmu.Game.Bots.Content.Triggers;

public sealed class InHostileAreaTrigger : IBotTrigger
{
    public string Name => "in-hostile-area";
    public int CheckIntervalMs => 500;
    public BotEvent Event => new(Name);

    public bool IsActive(BotContext context)
    {
        return context.Blackboard.TryGet(BotValues.HostileAreaTriggersNearby, context.Now, out var hazards) &&
            hazards is { Count: > 0 };
    }
}
