using AAEmu.Game.Bots.Blackboard;

namespace AAEmu.UnitTests.Bots.Blackboard;

[NotInParallel]
public class BotBlackboardTests
{
    [Test]
    public async Task CalculatedValue_RecomputesAtTtlOrAfterInvalidation()
    {
        var now = new DateTime(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);
        var calls = 0;
        var value = new CalculatedValue<int>(() => ++calls, TimeSpan.FromSeconds(2));

        await Assert.That(value.Get(now)).IsEqualTo(1);
        await Assert.That(value.Get(now.AddSeconds(1))).IsEqualTo(1);
        await Assert.That(value.Get(now.AddSeconds(2))).IsEqualTo(2);

        value.Invalidate();
        await Assert.That(value.Get(now.AddSeconds(2))).IsEqualTo(3);
    }

    [Test]
    public async Task Get_CachedValuesHaveNoPerReadAllocationAndPreserveLists()
    {
        var now = new DateTime(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);
        var board = new BotBlackboard();
        var list = new List<uint> { 1, 2, 3 };
        board.Register(new ValueKey<float>("float"), new CalculatedValue<float>(() => 12.5f, TimeSpan.FromMinutes(1)));
        board.Register(new ValueKey<List<uint>>("list"), new ManualValue<List<uint>>(list));
        _ = board.Get(new ValueKey<float>("float"), now);
        var firstList = board.Get(new ValueKey<List<uint>>("list"), now);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1000; i++)
            _ = board.Get(new ValueKey<float>("float"), now);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(allocated).IsEqualTo(0);
        await Assert.That(board.Get(new ValueKey<List<uint>>("list"), now)).IsSameReferenceAs(firstList);
    }

    [Test]
    public async Task Snapshot_FormatsRegisteredValuesAndComputedTime()
    {
        var now = new DateTime(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);
        var calls = 0;
        var board = new BotBlackboard();
        var value = new CalculatedValue<float>(() =>
        {
            calls++;
            return 12.5f;
        }, TimeSpan.Zero);
        board.Register(new ValueKey<float>("distance"), value);
        _ = board.Get(new ValueKey<float>("distance"), now);

        var entry = board.Snapshot().Single();

        await Assert.That(entry.name).IsEqualTo("distance");
        await Assert.That(entry.value).IsEqualTo("12.5");
        await Assert.That(entry.computedAt).IsEqualTo(now);
        await Assert.That(calls).IsEqualTo(1);
    }

    [Test]
    public async Task ManualValue_InfiniteTtlDoesNotRecomputeWhenTimeAdvancesOrMovesBack()
    {
        var now = new DateTime(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);
        var value = new ManualValue<int>(7);

        _ = value.Get(now);
        _ = value.Get(now.AddHours(1));
        _ = value.Get(now.AddHours(-1));

        await Assert.That(value.ComputedAt).IsEqualTo(now);
    }

    [Test]
    public async Task InvalidateAll_ForcesEveryRegisteredValueToRecompute()
    {
        var calls = 0;
        var board = new BotBlackboard();
        board.Register(new ValueKey<int>("one"), new CalculatedValue<int>(() => ++calls, TimeSpan.FromMinutes(1)));
        board.Register(new ValueKey<int>("two"), new CalculatedValue<int>(() => ++calls, TimeSpan.FromMinutes(1)));
        var now = new DateTime(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);

        _ = board.Get(new ValueKey<int>("one"), now);
        _ = board.Get(new ValueKey<int>("two"), now);
        board.InvalidateAll();
        _ = board.Get(new ValueKey<int>("one"), now);
        _ = board.Get(new ValueKey<int>("two"), now);

        await Assert.That(calls).IsEqualTo(4);
    }
}
