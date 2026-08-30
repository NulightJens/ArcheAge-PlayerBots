using AAEmu.Game.Bots.Kernel;

namespace AAEmu.Game.Bots.Content.Rotations;

public sealed class RotationStrategy : IBotStrategy
{
    private readonly IReadOnlyList<BotTriggerNode> _triggers;
    private readonly IReadOnlyList<IBotAction> _actions;

    public RotationStrategy(string rotationId, WeightedFillerAction filler,
        IEnumerable<BotTriggerNode> triggers, IEnumerable<IBotAction> actions)
    {
        RotationId = rotationId;
        Filler = filler ?? throw new ArgumentNullException(nameof(filler));
        _triggers = triggers?.ToArray() ?? [];
        _actions = actions?.ToArray() ?? [];
        var idle = _actions.OfType<RotationIdleAction>().FirstOrDefault();
        DefaultActions = idle == null
            ? [new BotNextAction(filler.Name, 11f)]
            : [new BotNextAction(filler.Name, 11f), new BotNextAction(idle.Name, 11f)];
    }

    public string RotationId { get; }
    public string Name => "rotation";
    public string SiblingGroup => "rotation";
    public WeightedFillerAction Filler { get; }
    public IReadOnlyList<IBotAction> Actions => _actions;
    public IReadOnlyList<BotTriggerNode> TriggerNodes => _triggers;
    public IReadOnlyList<BotNextAction> DefaultActions { get; }

    public void InitTriggers(List<BotTriggerNode> triggers)
    {
        triggers.AddRange(_triggers);
    }

    public void InitMultipliers(List<IBotMultiplier> multipliers)
    {
    }
}
