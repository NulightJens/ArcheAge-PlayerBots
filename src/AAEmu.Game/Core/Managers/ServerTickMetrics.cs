using AAEmu.Game.Core.Metrics;

namespace AAEmu.Game.Core.Managers;

public sealed class ServerTickMetrics
{
    private ServerTickMetricsWindow _window = new();

    public double PressureMs
    {
        get
        {
            var window = Volatile.Read(ref _window);
            return Pressure(window);
        }
    }

    public void RecordTick(double workMs, double intervalMs)
    {
        var window = Volatile.Read(ref _window);
        window.Work.RecordMilliseconds(workMs);
        window.Interval.RecordMilliseconds(intervalMs);
        var oldWorkEma = Volatile.Read(ref window.WorkMsEma);
        var oldIntervalEma = Volatile.Read(ref window.IntervalMsEma);
        Volatile.Write(ref window.WorkMsEma, oldWorkEma <= 0 ? workMs : oldWorkEma * 0.9 + workMs * 0.1);
        Volatile.Write(ref window.IntervalMsEma, oldIntervalEma <= 0 ? intervalMs : oldIntervalEma * 0.9 + intervalMs * 0.1);
    }

    public ServerTickMetricsSnapshot Snapshot()
    {
        var window = Volatile.Read(ref _window);
        return new ServerTickMetricsSnapshot(
            window.StartedAtUtc,
            DateTime.UtcNow,
            window.Work.Snapshot(),
            window.Interval.Snapshot(),
            Volatile.Read(ref window.WorkMsEma),
            Volatile.Read(ref window.IntervalMsEma),
            Pressure(window));
    }

    public void Reset()
    {
        Interlocked.Exchange(ref _window, new ServerTickMetricsWindow());
    }

    private sealed class ServerTickMetricsWindow
    {
        public DateTime StartedAtUtc { get; } = DateTime.UtcNow;
        public FixedLatencyHistogram Work { get; } = new();
        public FixedLatencyHistogram Interval { get; } = new();
        public double WorkMsEma;
        public double IntervalMsEma;
    }

    private static double Pressure(ServerTickMetricsWindow window) => Math.Max(
        Volatile.Read(ref window.WorkMsEma),
        Math.Max(0, Volatile.Read(ref window.IntervalMsEma) - TickManager.TickSleepMilliseconds));
}

public sealed record ServerTickMetricsSnapshot(
    DateTime StartedAtUtc,
    DateTime CapturedAtUtc,
    LatencySnapshot Work,
    LatencySnapshot Interval,
    double WorkMsEma,
    double IntervalMsEma,
    double PressureMs);
