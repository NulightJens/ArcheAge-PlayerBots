using AAEmu.Game.Bots.Kernel;

namespace AAEmu.UnitTests.Bots.Kernel;

public class BotActionQueueTests
{
    [Test]
    public async Task Push_DuplicateName_KeepsMaxRelevanceAndOriginalCreatedAt()
    {
        var created = new DateTime(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);
        var original = MakeBasket("attack", 5f, created);
        var newer = MakeBasket("attack", 9f, created.AddSeconds(1));
        var queue = new BotActionQueue();

        queue.Push(original);
        queue.Push(newer);
        var popped = queue.Pop();

        await Assert.That(popped).IsSameReferenceAs(original);
        await Assert.That(popped.Relevance).IsEqualTo(9f);
        await Assert.That(popped.CreatedAt).IsEqualTo(created);
    }

    [Test]
    public async Task Pop_ReturnsHighestRelevance()
    {
        var queue = new BotActionQueue();
        queue.Push(MakeBasket("low", 1f));
        var highest = MakeBasket("high", 20f);
        queue.Push(highest);

        await Assert.That(queue.Pop()).IsSameReferenceAs(highest);
    }

    [Test]
    public async Task Pop_TiesWithSameCreatedAtReturnInsertionOrderAcrossExpiryCycles()
    {
        var now = new DateTime(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);
        var queue = new BotActionQueue();

        for (var i = 0; i < 100; i++)
        {
            var tick = now.AddSeconds(i);
            queue.Push(MakeBasket($"expired-a-{i}", 1f, tick.AddMilliseconds(-5001)));
            queue.Push(MakeBasket($"expired-b-{i}", 1f, tick.AddMilliseconds(-5001)));
            queue.RemoveExpired(tick, 5000);

            var first = MakeBasket($"first-{i}", 10f, tick);
            var second = MakeBasket($"second-{i}", 10f, tick);
            queue.Push(first);
            queue.Push(second);

            await Assert.That(queue.Pop()).IsSameReferenceAs(first);
            await Assert.That(queue.Pop()).IsSameReferenceAs(second);
        }
    }

    [Test]
    public async Task RemoveExpired_DropsBasketsOlderThanConfiguredLifetime()
    {
        var now = new DateTime(2026, 8, 26, 12, 0, 5, DateTimeKind.Utc);
        var expired = MakeBasket("expired", 10f, now.AddMilliseconds(-5001));
        var live = MakeBasket("live", 5f, now.AddMilliseconds(-4999));
        var queue = new BotActionQueue();
        queue.Push(expired);
        queue.Push(live);

        queue.RemoveExpired(now, 5000);

        await Assert.That(queue.Count).IsEqualTo(1);
        await Assert.That(queue.Pop()).IsSameReferenceAs(live);
    }

    [Test]
    public async Task Clear_RemovesAllBaskets()
    {
        var queue = new BotActionQueue();
        queue.Push(MakeBasket("one", 1f));
        queue.Push(MakeBasket("two", 2f));

        queue.Clear();

        await Assert.That(queue.Count).IsEqualTo(0);
        await Assert.That(queue.Pop()).IsNull();
    }

    private static BotActionBasket MakeBasket(string name, float relevance, DateTime? createdAt = null)
    {
        return new BotActionBasket(
            new BotActionNode(new StubAction(name)),
            relevance,
            skipPrerequisites: false,
            default,
            createdAt ?? new DateTime(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc));
    }

    private sealed class StubAction(string name) : IBotAction
    {
        public string Name { get; } = name;

        public bool IsUseful(BotContext context) => true;

        public bool IsPossible(BotContext context) => true;

        public BotActionResult Execute(BotContext context, BotEvent ev) => BotActionResult.Success;
    }
}
