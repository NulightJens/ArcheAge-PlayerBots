using AAEmu.Game.Bots.Kernel;

namespace AAEmu.Game.Bots.Content.Strategies;

public sealed class LegacyStrategy : IBotStrategy
{
    public string Name => "legacy";
    public string SiblingGroup => null;
    public IReadOnlyList<BotNextAction> DefaultActions { get; } =
        [new BotNextAction("legacy tick", BotRelevance.Normal)];

    public void InitTriggers(List<BotTriggerNode> triggers)
    {
    }

    public void InitMultipliers(List<IBotMultiplier> multipliers)
    {
    }
}
