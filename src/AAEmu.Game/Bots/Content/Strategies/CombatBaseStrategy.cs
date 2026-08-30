using AAEmu.Game.Bots.Body;
using AAEmu.Game.Bots.Body.Positioning;
using AAEmu.Game.Bots.Content.Triggers;
using AAEmu.Game.Bots.Kernel;

namespace AAEmu.Game.Bots.Content.Strategies;

public sealed class CombatBaseStrategy : IBotStrategy
{
    public string Name => "combat-base";
    public string SiblingGroup => null;
    public IReadOnlyList<BotNextAction> DefaultActions { get; } = [];

    public void InitTriggers(List<BotTriggerNode> triggers)
    {
        new BodyBaseStrategy().InitTriggers(triggers);
        // Compiled rotations normally outrank the legacy tick. Detect target
        // stealth here so a continuously useful rotation cannot starve the
        // legacy brain's loss/search transition.
        triggers.Add(new BotTriggerNode(new TargetStealthedTrigger(),
            [new BotNextAction("begin-search", BotRelevance.Raid)]));
        triggers.Add(new BotTriggerNode(new NotFacingTargetTrigger(), [new BotNextAction("set-facing", BotRelevance.Move + 7)]));
    }

    public void InitMultipliers(List<IBotMultiplier> multipliers)
    {
    }
}
