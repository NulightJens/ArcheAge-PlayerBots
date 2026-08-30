using AAEmu.Game.Core.Metrics;

namespace AAEmu.UnitTests.Game.Core.Metrics;

public class FixedLatencyHistogramTests
{
    [Test]
    public async Task Snapshot_ReportsCountPercentilesMeanAndExactMax()
    {
        var histogram = new FixedLatencyHistogram();
        foreach (var value in new[] { 0.05, 0.15, 0.25, 0.35, 12.345 })
            histogram.RecordMilliseconds(value);

        var snapshot = histogram.Snapshot();

        await Assert.That(snapshot.Count).IsEqualTo(5L);
        await Assert.That(snapshot.P50Ms).IsBetween(0.2, 0.3);
        await Assert.That(snapshot.P95Ms).IsEqualTo(12.4);
        await Assert.That(snapshot.P99Ms).IsEqualTo(12.4);
        await Assert.That(snapshot.MaxMs).IsEqualTo(12.345);
        await Assert.That(snapshot.MeanMs).IsBetween(2.62, 2.64);
    }

    [Test]
    public async Task RecordMilliseconds_MaxDoesNotUnderstateSubMicrosecondBoundarySample()
    {
        const double observedMilliseconds = 0.0173;
        var histogram = new FixedLatencyHistogram();

        histogram.RecordMilliseconds(observedMilliseconds);

        var snapshot = histogram.Snapshot();
        await Assert.That(snapshot.MaxMs).IsEqualTo(0.018);
        await Assert.That(snapshot.MaxMs).IsGreaterThanOrEqualTo(observedMilliseconds);
    }

    [Test]
    public async Task RecordMilliseconds_AfterConstruction_DoesNotAllocateOnCallingThread()
    {
        var histogram = new FixedLatencyHistogram();
        histogram.RecordMilliseconds(1.25);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++)
            histogram.RecordMilliseconds(i % 250);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(allocated).IsEqualTo(0L);
    }
}
