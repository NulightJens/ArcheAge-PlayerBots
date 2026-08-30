using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Bots.Content.Triggers;

namespace AAEmu.Game.Bots.Content.Strategies;

public sealed class BodyBaseStrategy : IBotStrategy
{
    public string Name => "body-base";
    public string SiblingGroup => null;
    public IReadOnlyList<BotNextAction> DefaultActions { get; } = [];

    public void InitTriggers(List<BotTriggerNode> triggers)
    {
        triggers.Add(new BotTriggerNode(new TargetInvalidTrigger(), [new BotNextAction("drop-target", 99f)]));
        triggers.Add(new BotTriggerNode(new InHostileAreaTrigger(), [new BotNextAction("avoid-hazard", BotRelevance.Move + 5)]));
        triggers.Add(new BotTriggerNode(new StuckTrigger(), [new BotNextAction("unstick", BotRelevance.Move + 8)]));
    }

    public void InitMultipliers(List<IBotMultiplier> multipliers)
    {
    }
}
