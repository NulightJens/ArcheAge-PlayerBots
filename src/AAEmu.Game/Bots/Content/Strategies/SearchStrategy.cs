using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Bots.Content.Triggers;

namespace AAEmu.Game.Bots.Content.Strategies;

public sealed class SearchStrategy : IBotStrategy
{
    public string Name => "search";
    public string SiblingGroup => "activity";
    public IReadOnlyList<BotNextAction> DefaultActions { get; } =
        [new BotNextAction("search tick", BotRelevance.Normal)];

    public void InitTriggers(List<BotTriggerNode> triggers)
    {
        triggers.Add(new BotTriggerNode(new TargetStealthedTrigger(), [new BotNextAction("begin-search", BotRelevance.Raid)]));
    }

    public void InitMultipliers(List<IBotMultiplier> multipliers)
    {
    }
}
