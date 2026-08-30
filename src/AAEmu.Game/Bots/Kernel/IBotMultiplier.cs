namespace AAEmu.Game.Bots.Kernel;

public interface IBotMultiplier
{
    float GetValue(IBotAction action, BotContext context);
}
