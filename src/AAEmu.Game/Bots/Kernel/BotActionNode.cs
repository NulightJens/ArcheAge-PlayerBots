namespace AAEmu.Game.Bots.Kernel;

public sealed class BotActionNode
{
    public BotActionNode(
        IBotAction action,
        IEnumerable<BotNextAction> prerequisites = null,
        IEnumerable<BotNextAction> alternatives = null,
        IEnumerable<BotNextAction> continuers = null)
    {
        Action = action ?? throw new ArgumentNullException(nameof(action));
        Name = action.Name;
        Prerequisites = Combine(action.Prerequisites, prerequisites);
        Alternatives = Combine(action.Alternatives, alternatives);
        Continuers = Combine(action.Continuers, continuers);
    }

    public string Name { get; }
    public IBotAction Action { get; }
    public BotNextAction[] Prerequisites { get; }
    public BotNextAction[] Alternatives { get; }
    public BotNextAction[] Continuers { get; }

    private static BotNextAction[] Combine(
        IReadOnlyList<BotNextAction> actionActions,
        IEnumerable<BotNextAction> nodeActions)
    {
        if (nodeActions == null)
            return Copy(actionActions);

        if (nodeActions is not IReadOnlyList<BotNextAction> nodeActionList)
        {
            var nodeActionBuffer = new List<BotNextAction>();
            foreach (var nodeAction in nodeActions)
                nodeActionBuffer.Add(nodeAction);
            nodeActionList = nodeActionBuffer;
        }

        if (nodeActionList.Count == 0)
            return Copy(actionActions);

        var combined = new BotNextAction[(actionActions?.Count ?? 0) + nodeActionList.Count];
        var index = 0;
        if (actionActions != null)
        {
            foreach (var action in actionActions)
                combined[index++] = action;
        }

        for (var i = 0; i < nodeActionList.Count; i++)
            combined[index++] = nodeActionList[i];
        return combined;
    }

    private static BotNextAction[] Copy(IReadOnlyList<BotNextAction> actions)
    {
        if (actions == null || actions.Count == 0)
            return [];

        var copy = new BotNextAction[actions.Count];
        for (var i = 0; i < actions.Count; i++)
            copy[i] = actions[i];
        return copy;
    }
}
