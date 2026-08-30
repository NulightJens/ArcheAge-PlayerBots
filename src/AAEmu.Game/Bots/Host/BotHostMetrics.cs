using System.Diagnostics;
using AAEmu.Game.Core.Metrics;

namespace AAEmu.Game.Bots.Host;

public enum BotWorldScanKind
{
    Npc,
    RealPlayer,
    EnemyCount,
    Search
}

public sealed class BotHostMetrics
{
    private BotHostMetricsWindow _window = new();

    public double TickMsEma
    {
        get
        {
            var window = Volatile.Read(ref _window);
            return Volatile.Read(ref window.TickMsEma);
        }
    }
    public double MaxTickMs => Volatile.Read(ref _window).HostTick.Snapshot().MaxMs;
    public long SkippedTicks
    {
        get
        {
            var window = Volatile.Read(ref _window);
            return Interlocked.Read(ref window.SkippedTicks);
        }
    }
    public int LastTickBots => Volatile.Read(ref _window).LastTickBots;
    public int ActiveBots => Volatile.Read(ref _window).ActiveBots;
    public long TickErrors
    {
        get
        {
            var window = Volatile.Read(ref _window);
            return Interlocked.Read(ref window.TickErrors);
        }
    }

    public long BrainStepsTotal
    {
        get
        {
            var window = Volatile.Read(ref _window);
            return Interlocked.Read(ref window.BrainSteps);
        }
    }

    public long MoverStepsTotal
    {
        get
        {
            var window = Volatile.Read(ref _window);
            return Interlocked.Read(ref window.MoverSteps);
        }
    }

    internal void RecordHostTick(
        double elapsedMs,
        int bots,
        int activeBots,
        int configuredActivityPercent,
        int effectiveActivityPercent)
    {
        var window = Volatile.Read(ref _window);
        window.HostTick.RecordMilliseconds(elapsedMs);
        var oldEma = Volatile.Read(ref window.TickMsEma);
        Volatile.Write(ref window.TickMsEma, oldEma <= 0 ? elapsedMs : oldEma * 0.9 + elapsedMs * 0.1);
        window.LastTickBots = bots;
        window.ActiveBots = activeBots;
        window.ConfiguredActivityPercent = configuredActivityPercent;
        window.GovernorEffectivePercent = effectiveActivityPercent;
    }

    internal void RecordPopulation(int bots, int activeBots)
    {
        var window = Volatile.Read(ref _window);
        window.LastTickBots = bots;
        window.ActiveBots = activeBots;
    }

    internal void IncrementSkippedTicks()
    {
        var window = Volatile.Read(ref _window);
        Interlocked.Increment(ref window.SkippedTicks);
    }

    internal void IncrementRuntimeOverlaps()
    {
        var window = Volatile.Read(ref _window);
        Interlocked.Increment(ref window.RuntimeOverlaps);
    }

    internal void IncrementTickErrors()
    {
        var window = Volatile.Read(ref _window);
        Interlocked.Increment(ref window.TickErrors);
    }

    internal void RecordBrainStep(BotCadence cadence, bool active)
    {
        var window = Volatile.Read(ref _window);
        Interlocked.Increment(ref window.BrainSteps);
        if (active)
            Interlocked.Increment(ref window.ActiveBrainSteps);
        else
            Interlocked.Increment(ref window.InactiveBrainSteps);

        switch (cadence)
        {
            case BotCadence.Combat:
                Interlocked.Increment(ref window.CombatBrainSteps);
                break;
            case BotCadence.Moving:
                Interlocked.Increment(ref window.MovingBrainSteps);
                break;
            case BotCadence.Idle:
                Interlocked.Increment(ref window.IdleBrainSteps);
                break;
            case BotCadence.Resting:
                Interlocked.Increment(ref window.RestingBrainSteps);
                break;
            case BotCadence.Inactive:
                Interlocked.Increment(ref window.InactiveCadenceBrainSteps);
                break;
        }
    }

    internal void RecordMoverStep()
    {
        var window = Volatile.Read(ref _window);
        Interlocked.Increment(ref window.MoverSteps);
    }

    internal void RecordWorldScan(BotWorldScanKind kind)
    {
        var window = Volatile.Read(ref _window);
        Interlocked.Increment(ref window.WorldScans);
        switch (kind)
        {
            case BotWorldScanKind.Npc:
                Interlocked.Increment(ref window.NpcScans);
                break;
            case BotWorldScanKind.RealPlayer:
                Interlocked.Increment(ref window.RealPlayerScans);
                break;
            case BotWorldScanKind.EnemyCount:
                Interlocked.Increment(ref window.EnemyCountScans);
                break;
            case BotWorldScanKind.Search:
                Interlocked.Increment(ref window.SearchScans);
                break;
        }
    }

    internal void RecordPathRequest()
    {
        var window = Volatile.Read(ref _window);
        Interlocked.Increment(ref window.PathRequests);
    }

    internal void RecordDecision(bool success)
    {
        var window = Volatile.Read(ref _window);
        Interlocked.Increment(ref window.DecisionSteps);
        if (success)
            Interlocked.Increment(ref window.DecisionSuccesses);
    }

    internal void RecordInvalidTarget()
    {
        var window = Volatile.Read(ref _window);
        Interlocked.Increment(ref window.InvalidTargets);
    }

    internal void RecordContextCreated()
    {
        var window = Volatile.Read(ref _window);
        Interlocked.Increment(ref window.ContextsCreated);
    }

    internal void RecordActionBasketCreated()
    {
        var window = Volatile.Read(ref _window);
        Interlocked.Increment(ref window.ActionBasketsCreated);
    }

    internal void RecordCast(bool success)
    {
        var window = Volatile.Read(ref _window);
        Interlocked.Increment(ref window.CastAttempts);
        if (success)
            Interlocked.Increment(ref window.CastSuccesses);
        else
            Interlocked.Increment(ref window.CastFailures);
    }

    internal void RecordObservedKill()
    {
        var window = Volatile.Read(ref _window);
        Interlocked.Increment(ref window.ObservedKills);
    }

    internal void RecordCreditedKill()
    {
        var window = Volatile.Read(ref _window);
        Interlocked.Increment(ref window.CreditedKills);
    }

    internal void RecordStuckRecovery(bool teleport)
    {
        var window = Volatile.Read(ref _window);
        if (teleport)
            Interlocked.Increment(ref window.StuckTeleports);
        else
            Interlocked.Increment(ref window.StuckNudges);
    }

    internal void RecordSpawn(double elapsedMs, bool success)
    {
        var window = Volatile.Read(ref _window);
        window.Spawn.RecordMilliseconds(elapsedMs);
        if (!success)
            Interlocked.Increment(ref window.SpawnFailures);
    }

    internal void RecordDespawn(double elapsedMs, bool success)
    {
        var window = Volatile.Read(ref _window);
        window.Despawn.RecordMilliseconds(elapsedMs);
        if (!success)
            Interlocked.Increment(ref window.DespawnFailures);
    }

    internal void RecordShutdownCleanup(int remainingBots, int remainingRuntimes)
    {
        var window = Volatile.Read(ref _window);
        Interlocked.Increment(ref window.ShutdownCleanupRuns);
        window.ShutdownRemainingBots = remainingBots;
        window.ShutdownRemainingRuntimes = remainingRuntimes;
    }

    public BotHostMetricsSnapshot Snapshot()
    {
        var window = Volatile.Read(ref _window);
        var brainSteps = Interlocked.Read(ref window.BrainSteps);
        var activeBrainSteps = Interlocked.Read(ref window.ActiveBrainSteps);
        using var process = Process.GetCurrentProcess();
        var elapsed = Stopwatch.GetElapsedTime(window.StartedTimestamp);
        var cpuTicks = process.TotalProcessorTime.Ticks - window.ProcessCpuTicksAtStart;
        var cpuPercent = elapsed.TotalSeconds <= 0
            ? 0
            : TimeSpan.FromTicks(Math.Max(0, cpuTicks)).TotalSeconds / elapsed.TotalSeconds / Environment.ProcessorCount * 100d;

        return new BotHostMetricsSnapshot(
            window.StartedAtUtc,
            DateTime.UtcNow,
            elapsed.TotalSeconds,
            window.HostTick.Snapshot(),
            Volatile.Read(ref window.TickMsEma),
            Interlocked.Read(ref window.SkippedTicks),
            Interlocked.Read(ref window.RuntimeOverlaps),
            window.LastTickBots,
            window.ActiveBots,
            window.ConfiguredActivityPercent,
            window.GovernorEffectivePercent,
            brainSteps == 0 ? 0 : activeBrainSteps * 100d / brainSteps,
            Interlocked.Read(ref window.TickErrors),
            brainSteps,
            activeBrainSteps,
            Interlocked.Read(ref window.InactiveBrainSteps),
            Interlocked.Read(ref window.MoverSteps),
            Interlocked.Read(ref window.CombatBrainSteps),
            Interlocked.Read(ref window.MovingBrainSteps),
            Interlocked.Read(ref window.IdleBrainSteps),
            Interlocked.Read(ref window.RestingBrainSteps),
            Interlocked.Read(ref window.InactiveCadenceBrainSteps),
            Interlocked.Read(ref window.WorldScans),
            Interlocked.Read(ref window.NpcScans),
            Interlocked.Read(ref window.RealPlayerScans),
            Interlocked.Read(ref window.EnemyCountScans),
            Interlocked.Read(ref window.SearchScans),
            Interlocked.Read(ref window.PathRequests),
            Interlocked.Read(ref window.DecisionSteps),
            Interlocked.Read(ref window.DecisionSuccesses),
            Interlocked.Read(ref window.InvalidTargets),
            Interlocked.Read(ref window.ContextsCreated),
            Interlocked.Read(ref window.ActionBasketsCreated),
            Interlocked.Read(ref window.CastAttempts),
            Interlocked.Read(ref window.CastSuccesses),
            Interlocked.Read(ref window.CastFailures),
            Interlocked.Read(ref window.ObservedKills),
            Interlocked.Read(ref window.CreditedKills),
            Interlocked.Read(ref window.StuckNudges),
            Interlocked.Read(ref window.StuckTeleports),
            window.Spawn.Snapshot(),
            Interlocked.Read(ref window.SpawnFailures),
            window.Despawn.Snapshot(),
            Interlocked.Read(ref window.DespawnFailures),
            Interlocked.Read(ref window.ShutdownCleanupRuns),
            window.ShutdownRemainingBots,
            window.ShutdownRemainingRuntimes,
            Math.Max(0, GC.GetTotalAllocatedBytes(false) - window.AllocatedBytesAtStart),
            Math.Max(0, GC.CollectionCount(0) - window.Gen0AtStart),
            Math.Max(0, GC.CollectionCount(1) - window.Gen1AtStart),
            Math.Max(0, GC.CollectionCount(2) - window.Gen2AtStart),
            GC.GetGCMemoryInfo().HeapSizeBytes,
            process.WorkingSet64,
            process.PrivateMemorySize64,
            cpuPercent);
    }

    public void Reset() => Interlocked.Exchange(ref _window, new BotHostMetricsWindow());

    private sealed class BotHostMetricsWindow
    {
        public BotHostMetricsWindow()
        {
            using var process = Process.GetCurrentProcess();
            ProcessCpuTicksAtStart = process.TotalProcessorTime.Ticks;
        }

        public DateTime StartedAtUtc { get; } = DateTime.UtcNow;
        public long StartedTimestamp { get; } = Stopwatch.GetTimestamp();
        public long ProcessCpuTicksAtStart { get; }
        public long AllocatedBytesAtStart { get; } = GC.GetTotalAllocatedBytes(false);
        public int Gen0AtStart { get; } = GC.CollectionCount(0);
        public int Gen1AtStart { get; } = GC.CollectionCount(1);
        public int Gen2AtStart { get; } = GC.CollectionCount(2);
        public FixedLatencyHistogram HostTick { get; } = new();
        public FixedLatencyHistogram Spawn { get; } = new();
        public FixedLatencyHistogram Despawn { get; } = new();
        public double TickMsEma;
        public long SkippedTicks;
        public long RuntimeOverlaps;
        public long TickErrors;
        public long BrainSteps;
        public long ActiveBrainSteps;
        public long InactiveBrainSteps;
        public long MoverSteps;
        public long CombatBrainSteps;
        public long MovingBrainSteps;
        public long IdleBrainSteps;
        public long RestingBrainSteps;
        public long InactiveCadenceBrainSteps;
        public long WorldScans;
        public long NpcScans;
        public long RealPlayerScans;
        public long EnemyCountScans;
        public long SearchScans;
        public long PathRequests;
        public long DecisionSteps;
        public long DecisionSuccesses;
        public long InvalidTargets;
        public long ContextsCreated;
        public long ActionBasketsCreated;
        public long CastAttempts;
        public long CastSuccesses;
        public long CastFailures;
        public long ObservedKills;
        public long CreditedKills;
        public long StuckNudges;
        public long StuckTeleports;
        public long SpawnFailures;
        public long DespawnFailures;
        public long ShutdownCleanupRuns;
        public int LastTickBots;
        public int ActiveBots;
        public int ConfiguredActivityPercent;
        public int GovernorEffectivePercent;
        public int ShutdownRemainingBots;
        public int ShutdownRemainingRuntimes;
    }
}

public sealed record BotHostMetricsSnapshot(
    DateTime StartedAtUtc,
    DateTime CapturedAtUtc,
    double DurationSeconds,
    LatencySnapshot HostTick,
    double HostTickMsEma,
    long SkippedTicks,
    long RuntimeOverlaps,
    int Bots,
    int ActiveBots,
    int ConfiguredActivityPercent,
    int GovernorEffectivePercent,
    double EffectiveActivityPercent,
    long TickErrors,
    long BrainSteps,
    long ActiveBrainSteps,
    long InactiveBrainSteps,
    long MoverSteps,
    long CombatBrainSteps,
    long MovingBrainSteps,
    long IdleBrainSteps,
    long RestingBrainSteps,
    long InactiveCadenceBrainSteps,
    long WorldScans,
    long NpcScans,
    long RealPlayerScans,
    long EnemyCountScans,
    long SearchScans,
    long PathRequests,
    long DecisionSteps,
    long DecisionSuccesses,
    long InvalidTargets,
    long ContextsCreated,
    long ActionBasketsCreated,
    long CastAttempts,
    long CastSuccesses,
    long CastFailures,
    long ObservedKills,
    long CreditedKills,
    long StuckNudges,
    long StuckTeleports,
    LatencySnapshot Spawn,
    long SpawnFailures,
    LatencySnapshot Despawn,
    long DespawnFailures,
    long ShutdownCleanupRuns,
    int ShutdownRemainingBots,
    int ShutdownRemainingRuntimes,
    long AllocatedBytes,
    int Gen0Collections,
    int Gen1Collections,
    int Gen2Collections,
    long ManagedHeapBytes,
    long WorkingSetBytes,
    long PrivateMemoryBytes,
    double CpuPercent);

public sealed class BotRuntimeMetrics
{
    public double LastBrainMs { get; internal set; }
    public double BrainMsEma { get; internal set; }
    public long BrainSteps { get; internal set; }
    public long MoverSteps { get; internal set; }
    public int Errors { get; internal set; }
}
