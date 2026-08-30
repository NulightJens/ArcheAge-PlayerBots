using System.Collections.Generic;

namespace AAEmu.Game.Bots.Kernel;

public interface IBotStrategy
{
    string Name { get; }
    string SiblingGroup { get; }
    IReadOnlyList<BotNextAction> DefaultActions { get; }
    void InitTriggers(List<BotTriggerNode> triggers);
    void InitMultipliers(List<IBotMultiplier> multipliers);
}
