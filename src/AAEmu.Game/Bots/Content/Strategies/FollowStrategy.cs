using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Bots.Content.Triggers;

namespace AAEmu.Game.Bots.Content.Strategies;

public sealed class FollowStrategy : IBotStrategy
{
    public string Name => "follow";
    public string SiblingGroup => "activity";
    public IReadOnlyList<BotNextAction> DefaultActions { get; } =
        [new BotNextAction("follow tick", BotRelevance.Normal)];

    public void InitTriggers(List<BotTriggerNode> triggers)
    {
        triggers.Add(new BotTriggerNode(new LeaderInCombatTrigger(), [new BotNextAction("follow tick", BotRelevance.High)]));
        triggers.Add(new BotTriggerNode(new FollowDistanceTrigger(), [new BotNextAction("follow tick", BotRelevance.Normal)]));
    }

    public void InitMultipliers(List<IBotMultiplier> multipliers)
    {
    }
}
