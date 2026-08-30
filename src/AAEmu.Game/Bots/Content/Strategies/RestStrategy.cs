using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Bots.Content.Triggers;

namespace AAEmu.Game.Bots.Content.Strategies;

public sealed class RestStrategy : IBotStrategy
{
    public string Name => "rest";
    public string SiblingGroup => "activity";
    public IReadOnlyList<BotNextAction> DefaultActions { get; } =
        [new BotNextAction("rest tick", BotRelevance.Normal)];

    public void InitTriggers(List<BotTriggerNode> triggers)
    {
        triggers.Add(new BotTriggerNode(new LowHealthTrigger(), [new BotNextAction("begin-rest", 4.1f)]));
    }

    public void InitMultipliers(List<IBotMultiplier> multipliers)
    {
    }
}
