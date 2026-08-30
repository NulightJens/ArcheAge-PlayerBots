using AAEmu.Game.Core.Managers;

namespace AAEmu.UnitTests.Game.Core.Managers;

public class ServerTickMetricsTests
{
    [Test]
    public async Task RecordAndReset_ExposeWholeServerWorkIntervalAndPressure()
    {
        var metrics = new ServerTickMetrics();
        metrics.RecordTick(5, 25);
        metrics.RecordTick(15, 45);

        var snapshot = metrics.Snapshot();

        await Assert.That(snapshot.Work.Count).IsEqualTo(2L);
        await Assert.That(snapshot.Interval.Count).IsEqualTo(2L);
        await Assert.That(snapshot.Work.MaxMs).IsEqualTo(15d);
        await Assert.That(snapshot.Interval.MaxMs).IsEqualTo(45d);
        await Assert.That(snapshot.PressureMs).IsGreaterThanOrEqualTo(snapshot.WorkMsEma);

        metrics.Reset();

        await Assert.That(metrics.Snapshot().Work.Count).IsEqualTo(0L);
    }

    [Test]
    public async Task RecordTick_AfterConstruction_DoesNotAllocateOnCallingThread()
    {
        var metrics = new ServerTickMetrics();
        metrics.RecordTick(1, 21);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++)
            metrics.RecordTick(1, 21);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(allocated).IsEqualTo(0L);
    }
}
