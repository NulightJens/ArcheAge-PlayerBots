using AAEmu.Game.Bots.Host;

namespace AAEmu.UnitTests.Bots.Host;

public class BotHostMetricsTests
{
    [Test]
    public async Task Metrics_DefaultsAreZero()
    {
        var metrics = new BotHostMetrics();

        await Assert.That(metrics.TickMsEma).IsEqualTo(0d);
        await Assert.That(metrics.MaxTickMs).IsEqualTo(0d);
        await Assert.That(metrics.SkippedTicks).IsEqualTo(0L);
        await Assert.That(metrics.LastTickBots).IsEqualTo(0);
        await Assert.That(metrics.ActiveBots).IsEqualTo(0);
        await Assert.That(metrics.BrainStepsTotal).IsEqualTo(0L);
        await Assert.That(metrics.MoverStepsTotal).IsEqualTo(0L);
    }

    [Test]
    public async Task RuntimeMetrics_DefaultsAreZero()
    {
        var metrics = new BotRuntimeMetrics();

        await Assert.That(metrics.LastBrainMs).IsEqualTo(0d);
        await Assert.That(metrics.BrainMsEma).IsEqualTo(0d);
        await Assert.That(metrics.BrainSteps).IsEqualTo(0L);
        await Assert.That(metrics.MoverSteps).IsEqualTo(0L);
        await Assert.That(metrics.Errors).IsEqualTo(0);
    }

    [Test]
    public async Task Snapshot_AggregatesLatencyCadenceWorkAndLifecycleCounters()
    {
        var metrics = new BotHostMetrics();
        metrics.RecordHostTick(2.5, 10, 4, 50, 40);
        metrics.RecordBrainStep(BotCadence.Combat, true);
        metrics.RecordBrainStep(BotCadence.Inactive, false);
        metrics.RecordMoverStep();
        metrics.RecordWorldScan(BotWorldScanKind.Npc);
        metrics.RecordWorldScan(BotWorldScanKind.EnemyCount);
        metrics.RecordDecision(true);
        metrics.RecordInvalidTarget();
        metrics.RecordContextCreated();
        metrics.RecordActionBasketCreated();
        metrics.RecordCast(true);
        metrics.RecordStuckRecovery(false);
        metrics.RecordSpawn(12.5, true);
        metrics.RecordDespawn(7.5, true);
        metrics.RecordShutdownCleanup(0, 0);

        var snapshot = metrics.Snapshot();

        await Assert.That(snapshot.HostTick.Count).IsEqualTo(1L);
        await Assert.That(snapshot.HostTick.MaxMs).IsEqualTo(2.5);
        await Assert.That(snapshot.ConfiguredActivityPercent).IsEqualTo(50);
        await Assert.That(snapshot.GovernorEffectivePercent).IsEqualTo(40);
        await Assert.That(snapshot.EffectiveActivityPercent).IsEqualTo(50d);
        await Assert.That(snapshot.BrainSteps).IsEqualTo(2L);
        await Assert.That(snapshot.ActiveBrainSteps).IsEqualTo(1L);
        await Assert.That(snapshot.InactiveBrainSteps).IsEqualTo(1L);
        await Assert.That(snapshot.CombatBrainSteps).IsEqualTo(1L);
        await Assert.That(snapshot.WorldScans).IsEqualTo(2L);
        await Assert.That(snapshot.NpcScans).IsEqualTo(1L);
        await Assert.That(snapshot.EnemyCountScans).IsEqualTo(1L);
        await Assert.That(snapshot.DecisionSteps).IsEqualTo(1L);
        await Assert.That(snapshot.DecisionSuccesses).IsEqualTo(1L);
        await Assert.That(snapshot.InvalidTargets).IsEqualTo(1L);
        await Assert.That(snapshot.ContextsCreated).IsEqualTo(1L);
        await Assert.That(snapshot.ActionBasketsCreated).IsEqualTo(1L);
        await Assert.That(snapshot.CastAttempts).IsEqualTo(1L);
        await Assert.That(snapshot.CastSuccesses).IsEqualTo(1L);
        await Assert.That(snapshot.StuckNudges).IsEqualTo(1L);
        await Assert.That(snapshot.Spawn.Count).IsEqualTo(1L);
        await Assert.That(snapshot.Despawn.Count).IsEqualTo(1L);
        await Assert.That(snapshot.ShutdownCleanupRuns).IsEqualTo(1L);
        await Assert.That(snapshot.ShutdownRemainingBots).IsEqualTo(0);
    }

    [Test]
    public async Task HotPathRecording_AfterConstruction_DoesNotAllocateOnCallingThread()
    {
        var metrics = new BotHostMetrics();
        metrics.RecordHostTick(1, 100, 10, 10, 10);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 10_000; i++)
        {
            metrics.RecordHostTick(1, 100, 10, 10, 10);
            metrics.RecordBrainStep(BotCadence.Idle, true);
            metrics.RecordMoverStep();
            metrics.RecordWorldScan(BotWorldScanKind.Npc);
            metrics.RecordDecision(i % 2 == 0);
            metrics.RecordContextCreated();
            metrics.RecordActionBasketCreated();
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(allocated).IsEqualTo(0L);
    }
}
