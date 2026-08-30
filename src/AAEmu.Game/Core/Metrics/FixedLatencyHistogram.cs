namespace AAEmu.Game.Core.Metrics;

/// <summary>
/// Fixed 0.1 ms buckets through 409.5 ms plus an overflow bucket. Recording is allocation-free;
/// snapshots are intentionally allocated only on the operator path.
/// </summary>
public sealed class FixedLatencyHistogram
{
    private const int BucketCount = 4096;
    private const long BucketWidthMicroseconds = 100;
    private readonly long[] _buckets = new long[BucketCount + 1];
    private long _count;
    private long _maxMicroseconds;
    private long _totalMicroseconds;

    public void RecordMilliseconds(double milliseconds)
    {
        if (!double.IsFinite(milliseconds) || milliseconds < 0)
            return;

        var saturated = milliseconds >= long.MaxValue / 1000d;
        var scaledMicroseconds = saturated ? long.MaxValue : milliseconds * 1000d;
        var microseconds = saturated
            ? long.MaxValue
            : (long)Math.Round(scaledMicroseconds, MidpointRounding.AwayFromZero);
        // A maximum must not understate the raw sample; totals and buckets keep nearest-microsecond quantization.
        var maxMicroseconds = saturated
            ? long.MaxValue
            : (long)Math.Ceiling(scaledMicroseconds);
        var bucket = Math.Min(BucketCount, (int)Math.Min(BucketCount, microseconds / BucketWidthMicroseconds));
        Interlocked.Increment(ref _buckets[bucket]);
        Interlocked.Increment(ref _count);
        Interlocked.Add(ref _totalMicroseconds, microseconds);
        UpdateMax(maxMicroseconds);
    }

    public LatencySnapshot Snapshot()
    {
        var count = Interlocked.Read(ref _count);
        if (count <= 0)
            return LatencySnapshot.Empty;

        var total = Interlocked.Read(ref _totalMicroseconds);
        var max = Interlocked.Read(ref _maxMicroseconds);
        return new LatencySnapshot(
            count,
            total / 1000d / count,
            Percentile(count, 0.50),
            Percentile(count, 0.95),
            Percentile(count, 0.99),
            max / 1000d);
    }

    private double Percentile(long count, double percentile)
    {
        var target = Math.Max(1, (long)Math.Ceiling(count * percentile));
        var seen = 0L;
        for (var i = 0; i < _buckets.Length; i++)
        {
            seen += Interlocked.Read(ref _buckets[i]);
            if (seen < target)
                continue;

            if (i == BucketCount)
                return Interlocked.Read(ref _maxMicroseconds) / 1000d;
            return (i + 1) * BucketWidthMicroseconds / 1000d;
        }

        return Interlocked.Read(ref _maxMicroseconds) / 1000d;
    }

    private void UpdateMax(long value)
    {
        var current = Interlocked.Read(ref _maxMicroseconds);
        while (value > current)
        {
            var observed = Interlocked.CompareExchange(ref _maxMicroseconds, value, current);
            if (observed == current)
                return;
            current = observed;
        }
    }
}

public sealed record LatencySnapshot(
    long Count,
    double MeanMs,
    double P50Ms,
    double P95Ms,
    double P99Ms,
    double MaxMs)
{
    public static LatencySnapshot Empty { get; } = new(0, 0, 0, 0, 0, 0);
}
