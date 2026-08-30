namespace AAEmu.Game.Bots.Kernel;

public sealed class BotActionBasket
{
    public BotActionBasket(
        BotActionNode node,
        float relevance,
        bool skipPrerequisites,
        BotEvent ev,
        DateTime createdAt)
    {
        Node = node ?? throw new ArgumentNullException(nameof(node));
        Relevance = relevance;
        SkipPrerequisites = skipPrerequisites;
        Event = ev;
        CreatedAt = createdAt;
    }

    public BotActionNode Node { get; }
    public float Relevance { get; internal set; }
    public long Sequence { get; internal set; }
    public bool SkipPrerequisites { get; }
    public BotEvent Event { get; }
    public DateTime CreatedAt { get; }
}
