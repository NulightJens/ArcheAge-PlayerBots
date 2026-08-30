namespace AAEmu.Game.Bots.Kernel;

public sealed class BotTriggerNode
{
    public BotTriggerNode(IBotTrigger trigger, IEnumerable<BotNextAction> actions)
    {
        Trigger = trigger ?? throw new ArgumentNullException(nameof(trigger));
        Actions = actions?.ToArray() ?? [];
    }

    public IBotTrigger Trigger { get; }
    public BotNextAction[] Actions { get; }
    public DateTime LastCheck { get; internal set; }
}
