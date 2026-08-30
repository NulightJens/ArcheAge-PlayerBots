namespace AAEmu.Game.Bots.Kernel;

public interface IBotTrigger
{
    string Name { get; }
    int CheckIntervalMs { get; }
    bool IsActive(BotContext context);
    BotEvent Event { get; }
}
