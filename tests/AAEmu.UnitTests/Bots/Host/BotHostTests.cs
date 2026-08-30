using System.Collections.Concurrent;
using System.Numerics;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Tasks.Bots;
using AAEmu.UnitTests.Utils.Mocks;
using Microsoft.Extensions.Time.Testing;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace AAEmu.UnitTests.Bots.Host;

[NotInParallel]
public class BotHostTests
{
    [Test]
    public async Task Register_SchedulesOneRepeatingHostAndUnregisterCancelsCallbacksOnce()
    {
        var taskManager = Mock.Of<ITaskManager>();
        taskManager.Schedule(Any<AAEmu.Game.Models.Tasks.Task>(), Any<TimeSpan?>(), Any<TimeSpan?>(), Any<int>()).Returns(true);
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero));
        var host = new BotHost(taskManager.Object, time);
        var runtime = MakeRuntime(10, out var mover, out var brain);

        host.Register(runtime);
        host.Register(runtime);
        host.Unregister(runtime.Bot.Id);
        host.Unregister(runtime.Bot.Id);

        taskManager.Schedule(Any<AAEmu.Game.Models.Tasks.Task>(), TimeSpan.Zero, TimeSpan.FromMilliseconds(100), -1)
            .WasCalled(Times.Once);
        await Assert.That(mover.CancelCount).IsEqualTo(1);
        await Assert.That(brain.CancelCount).IsEqualTo(1);
        await Assert.That(host.GetRuntime(runtime.Bot.Id)).IsNull();
    }

    [Test]
    public async Task HostTask_ExecutesRegisteredMoverAndBrain()
    {
        var host = MakeHost(out var time);
        var runtime = MakeRuntime(10, out var mover, out var brain);
        runtime.MovementState.Destination = Vector3.One;

        host.Register(runtime);
        host.HostTask.Execute();

        await Assert.That(mover.StepCount).IsEqualTo(1);
        await Assert.That(brain.StepCount).IsEqualTo(1);
        await Assert.That(host.Metrics.LastTickBots).IsEqualTo(1);
        await Assert.That(host.Metrics.MoverStepsTotal).IsEqualTo(1);
        await Assert.That(host.Metrics.BrainStepsTotal).IsEqualTo(1);
        await Assert.That(runtime.Schedule.Now).IsEqualTo(time.GetUtcNow().UtcDateTime);
        await Assert.That(runtime.MovementState.LastPos).IsEqualTo(Vector3.Zero);
        await Assert.That(runtime.MovementState.LastMoveAt).IsEqualTo(time.GetUtcNow().UtcDateTime);
    }

    [Test]
    public async Task UnregisterLastRuntime_PublishesZeroPopulation()
    {
        var host = MakeHost(out _);
        var runtime = MakeRuntime(10, out _, out _);
        host.Register(runtime);
        host.HostTask.Execute();
        await Assert.That(host.Metrics.LastTickBots).IsEqualTo(1);

        host.Unregister(runtime.Bot.Id);

        await Assert.That(host.RuntimeCount).IsEqualTo(0);
        await Assert.That(host.Metrics.LastTickBots).IsEqualTo(0);
        await Assert.That(host.Metrics.ActiveBots).IsEqualTo(0);
    }

    [Test]
    public async Task HostTask_NullTransform_SkipsMoverAndLogsTraceOnce()
    {
        var host = MakeHost(out _);
        var runtime = MakeRuntime(10, out var mover, out _);
        runtime.Bot.Transform = null;
        var target = new MemoryTarget { Layout = "${message}" };
        var previousConfiguration = LogManager.Configuration;
        var configuration = new LoggingConfiguration();
        configuration.LoggingRules.Add(new LoggingRule("AAEmu.Game.Bots.Host.BotHostTask", LogLevel.Trace, target));
        LogManager.Configuration = configuration;
        LogManager.ReconfigExistingLoggers();
        try
        {
            host.Register(runtime);
            host.HostTask.Execute();
            host.HostTask.Execute();

            await Assert.That(mover.StepCount).IsEqualTo(0);
            await Assert.That(target.Logs.Count(message => message.Contains("ev=mover_skip reason=missing_transform")))
                .IsEqualTo(1);
        }
        finally
        {
            LogManager.Configuration = previousConfiguration;
            LogManager.ReconfigExistingLoggers();
        }
    }

    [Test]
    public async Task Stop_CancelsHostTaskAndPreventsFurtherSteps()
    {
        var host = MakeHost(out _);
        var runtime = MakeRuntime(10, out var mover, out _);
        host.Register(runtime);
        host.Stop();
        host.HostTask.Execute();

        await Assert.That(host.HostTask.Cancelled).IsTrue();
        await Assert.That(mover.StepCount).IsEqualTo(0);
    }

    [Test]
    public async Task ConcurrentRegisterUnregisterDuringTicks_DoesNotThrowOrStepAfterCancel()
    {
        var host = MakeHost(out _);
        var movers = new System.Collections.Concurrent.ConcurrentBag<ConcurrentMover>();
        var start = new Barrier(2);

        var ticking = System.Threading.Tasks.Task.Run(() =>
        {
            start.SignalAndWait();
            for (var i = 0; i < 500; i++)
                host.HostTask.Execute();
        });
        var mutating = System.Threading.Tasks.Task.Run(() =>
        {
            start.SignalAndWait();
            for (uint id = 100; id < 300; id++)
            {
                var runtime = MakeConcurrentRuntime(id, out var mover);
                movers.Add(mover);
                host.Register(runtime);
                host.Unregister(id);
            }
        });

        await System.Threading.Tasks.Task.WhenAll(ticking, mutating);

        foreach (var mover in movers)
        {
            await Assert.That(mover.CancelCount).IsEqualTo(1);
            await Assert.That(mover.StepsAfterCancel).IsEqualTo(0);
        }
    }

    [Test]
    public async Task UnchangedRuntimeSnapshot_DoesNotAllocateDuringIdleTick()
    {
        var host = MakeHost(out var time);
        var runtime = MakeRuntime(10, out _, out var brain);
        runtime.Schedule.NextBrainAt = time.GetUtcNow().UtcDateTime.AddHours(1);
        host.Register(runtime);
        host.HostTask.Execute();
        BotTestFixture.SetPrivateField(host.HostTask, "_lastMetricsLogAt", time.GetUtcNow().UtcDateTime);

        var before = GC.GetAllocatedBytesForCurrentThread();
        host.HostTask.Execute();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(brain.StepCount).IsEqualTo(0);
        await Assert.That(allocated).IsEqualTo(0);
    }

    [Test]
    public async Task HostTickException_IsContainedAndCounted()
    {
        var time = new ToggleTimeProvider();
        var taskManager = Mock.Of<ITaskManager>();
        taskManager.Schedule(Any<AAEmu.Game.Models.Tasks.Task>(), Any<TimeSpan?>(), Any<TimeSpan?>(), Any<int>()).Returns(true);
        var host = new BotHost(taskManager.Object, time);
        host.Register(MakeRuntime(10, out _, out _));
        time.Throw = true;

        host.HostTask.Execute();

        await Assert.That(host.Metrics.TickErrors).IsEqualTo(1);
    }

    [Test]
    public async Task BotTestFixture_RegistersBotHostWithFixtureDependencies()
    {
        var taskManager = BotTestFixture.RegisterTaskManager();

        try
        {
            var host = BotHost.Instance;

            await Assert.That(host.TimeProvider).IsTypeOf<FakeTimeProvider>();
            await Assert.That(host.TimeProvider).IsNotEqualTo(TimeProvider.System);
            await Assert.That(host.IsStarted).IsFalse();
            await Assert.That(taskManager.GetQueueCount()).IsEqualTo(0);
        }
        finally
        {
            BotTestFixture.ResetTaskManager();
        }
    }

    [Test]
    public async Task Register_DuplicateId_ReplacesOldRuntimeAndCancelsItOnce()
    {
        var host = MakeHost(out _);
        var oldRuntime = MakeRuntime(10, out var oldMover, out _);
        var newRuntime = MakeRuntime(10, out var newMover, out _);

        host.Register(oldRuntime);
        host.Register(newRuntime);

        await Assert.That(oldMover.CancelCount).IsEqualTo(1);
        await Assert.That(host.GetRuntime(10)).IsSameReferenceAs(newRuntime);
        host.Unregister(10);

        await Assert.That(newMover.CancelCount).IsEqualTo(1);
    }

    [Test]
    public async Task Register_DoesNotLogMetricsOnFirstTick()
    {
        var host = MakeHost(out var time);
        var runtime = MakeRuntime(10, out _, out _);
        host.Register(runtime);

        await Assert.That(BotTestFixture.GetPrivateField<DateTime>(host.HostTask, "_lastMetricsLogAt"))
            .IsEqualTo(time.GetUtcNow().UtcDateTime);
    }

    [Test]
    public async Task Unregister_StaleRuntimeReference_DoesNotRemoveReplacement()
    {
        var host = MakeHost(out _);
        var oldRuntime = MakeRuntime(10, out _, out _);
        var newRuntime = MakeRuntime(10, out _, out _);

        host.Register(oldRuntime);
        host.Register(newRuntime);
        host.Unregister(oldRuntime);

        await Assert.That(host.GetRuntime(10)).IsSameReferenceAs(newRuntime);
    }

    [Test]
    public async Task Register_ConcurrentDuplicateId_LogsRaceAndStartsHost()
    {
        var host = MakeHost(out _);
        var first = MakeRuntime(10, out _, out _);
        var second = MakeRuntime(10, out _, out _);
        var target = new RecordingTarget();
        var previousConfiguration = LogManager.Configuration;
        var configuration = new LoggingConfiguration();
        configuration.LoggingRules.Add(new LoggingRule("AAEmu.Game.Bots.Host.BotHost", LogLevel.Warn, target));
        LogManager.Configuration = configuration;
        LogManager.ReconfigExistingLoggers();
        try
        {
            var runtimeLock = BotTestFixture.GetPrivateField<object>(host, "_runtimeLock");
            System.Threading.Tasks.Task firstRegistration;
            System.Threading.Tasks.Task secondRegistration;
            lock (runtimeLock)
            {
                firstRegistration = System.Threading.Tasks.Task.Run(() => host.Register(first));
                secondRegistration = System.Threading.Tasks.Task.Run(() => host.Register(second));
                Thread.Sleep(100);
            }

            await System.Threading.Tasks.Task.WhenAll(firstRegistration, secondRegistration);

            await Assert.That(host.GetRuntime(10)).IsNotNull();
            await Assert.That(host.IsStarted).IsTrue();
            await Assert.That(target.Messages.Any(message => message.Contains("duplicate_runtime_race"))).IsTrue();
        }
        finally
        {
            host.Unregister(10);
            LogManager.Configuration = previousConfiguration;
            LogManager.ReconfigExistingLoggers();
        }
    }

    private static BotHost MakeHost(out FakeTimeProvider time)
    {
        var taskManager = Mock.Of<ITaskManager>();
        taskManager.Schedule(Any<AAEmu.Game.Models.Tasks.Task>(), Any<TimeSpan?>(), Any<TimeSpan?>(), Any<int>()).Returns(true);
        time = new FakeTimeProvider(new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero));
        return new BotHost(taskManager.Object, time);
    }

    private static BotRuntime MakeRuntime(uint id, out CountingMover mover, out CountingBrain brain)
    {
        var bot = BotTestFixture.MakeBot(id, Vector3.Zero);
        // Host scheduling tests model a live bot unless a test explicitly sets
        // HP to zero. Make that premise explicit now that dead bots correctly
        // bypass movement and enter lifecycle processing in the host.
        bot.MaxHp = 100;
        bot.Hp = 100;
        var movementState = new BotMovementState();
        var combatState = new BotCombatState();
        var broadcaster = new BotMovementBroadcaster(bot);
        mover = new CountingMover(bot, movementState, broadcaster);
        brain = new CountingBrain(bot, combatState, broadcaster);
        return new BotRuntime(bot, movementState, combatState, broadcaster, mover, brain);
    }

    private static BotRuntime MakeConcurrentRuntime(uint id, out ConcurrentMover mover)
    {
        var bot = BotTestFixture.MakeBot(id, Vector3.Zero);
        var movementState = new BotMovementState();
        var combatState = new BotCombatState();
        var broadcaster = new BotMovementBroadcaster(bot);
        mover = new ConcurrentMover(bot, movementState, broadcaster);
        return new BotRuntime(bot, movementState, combatState, broadcaster, mover);
    }

    private sealed class CountingMover(CharacterMock bot, BotMovementState state, BotMovementBroadcaster broadcaster)
        : BotMovementTask(bot, state, broadcaster)
    {
        public int StepCount { get; private set; }
        public int CancelCount { get; private set; }

        internal override void Step() => StepCount++;
        public override void OnCancel() => CancelCount++;
    }

    private sealed class CountingBrain(CharacterMock bot, BotCombatState state, BotMovementBroadcaster broadcaster)
        : BotCombatTask(bot, state, broadcaster)
    {
        public int StepCount { get; private set; }
        public int CancelCount { get; private set; }

        internal override void Step() => StepCount++;
        internal override void StepMinimal() => StepCount++;
        public override void OnCancel() => CancelCount++;
    }

    private sealed class ConcurrentMover(CharacterMock bot, BotMovementState state, BotMovementBroadcaster broadcaster)
        : BotMovementTask(bot, state, broadcaster)
    {
        private int _cancelled;
        private int _stepsAfterCancel;
        private int _stepCount;
        private int _cancelCount;

        public int StepsAfterCancel => Volatile.Read(ref _stepsAfterCancel);
        public int CancelCount => Volatile.Read(ref _cancelCount);

        internal override void Step()
        {
            if (Volatile.Read(ref _cancelled) != 0)
                Interlocked.Increment(ref _stepsAfterCancel);
            Interlocked.Increment(ref _stepCount);
        }

        public override void OnCancel()
        {
            Volatile.Write(ref _cancelled, 1);
            Interlocked.Increment(ref _cancelCount);
        }
    }

    private sealed class ToggleTimeProvider : TimeProvider
    {
        public bool Throw { get; set; }

        public override DateTimeOffset GetUtcNow()
        {
            if (Throw)
                throw new InvalidOperationException("simulated host clock failure");

            return base.GetUtcNow();
        }
    }

    private sealed class RecordingTarget : TargetWithLayout
    {
        public ConcurrentQueue<string> Messages { get; } = new();

        protected override void Write(LogEventInfo logEvent)
        {
            Messages.Enqueue(logEvent.FormattedMessage);
        }
    }
}
