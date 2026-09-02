using System.Numerics;
using AAEmu.Game.Bots.Blackboard;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Bots.Life;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Bots.Ops;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.World;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models.Tasks.Bots;
using AAEmu.UnitTests.Bots.Sim;
using AAEmu.UnitTests.Utils.Mocks;
using Microsoft.Extensions.Time.Testing;

namespace AAEmu.UnitTests.Bots.Host;

[NotInParallel]
public class BotHostBehaviorTests
{
    [Test]
    public async Task IdleBot_BrainDoesNotRunMoreOftenThanOncePerSecond_AndMoverOnlyGroundChecks()
    {
        var config = BotConfig.Instance;
        var previousPercent = config.ActivityPercent;
        try
        {
            config.ActivityPercent = 100;
            var sim = new BotSim();
            var bot = sim.AddBot(10);

            sim.Advance(5000);

            await Assert.That(bot.Brain.FullStepTimes.Count).IsGreaterThan(0);
            await Assert.That(bot.Brain.FullStepTimes.Zip(bot.Brain.FullStepTimes.Skip(1), (a, b) => b - a)
                .All(delta => delta >= TimeSpan.FromSeconds(1))).IsTrue();
            await Assert.That(bot.Mover.StepCount).IsLessThanOrEqualTo(6);
        }
        finally
        {
            config.ActivityPercent = previousPercent;
        }
    }

    [Test]
    public async Task MovingBot_MovesEveryHostTick_AndBrainsAtMovingCadence()
    {
        var config = BotConfig.Instance;
        var previousPercent = config.ActivityPercent;
        try
        {
            config.ActivityPercent = 100;
            var sim = new BotSim();
            var bot = sim.AddBot(10);
            bot.Runtime.MovementState.Destination = System.Numerics.Vector3.One;

            sim.Advance(2000);

            await Assert.That(bot.Mover.StepCount).IsEqualTo(20);
            await Assert.That(bot.Brain.FullStepTimes.Count).IsBetween(6, 8);
            await Assert.That(bot.Brain.FullStepTimes.Zip(bot.Brain.FullStepTimes.Skip(1), (a, b) => b - a)
                .All(delta => delta >= TimeSpan.FromMilliseconds(300))).IsTrue();
        }
        finally
        {
            config.ActivityPercent = previousPercent;
        }
    }

    [Test]
    public async Task CombatState_EngineCarriesLegacyDefaultAction()
    {
        var config = BotConfig.Instance;
        var previousPercent = config.ActivityPercent;
        try
        {
            config.ActivityPercent = 100;
            var sim = new BotSim();
            var bot = sim.AddBot(10, BotCombatStateType.Combat);
            bot.Runtime.Schedule.NextBrainAt = sim.Time.GetUtcNow().UtcDateTime;

            sim.Tick();

            await Assert.That(bot.Runtime.Engines[(int)BotEngineKind.Combat].LastActionLog.Any(log =>
                log.Action == "legacy tick" && log.Result == BotActionResult.Success)).IsTrue();
        }
        finally
        {
            config.ActivityPercent = previousPercent;
        }
    }

    [Test]
    public async Task CombatState_DeadTargetExitsCombatWithinThreeHostTicks()
    {
        var config = BotConfig.Instance;
        var previousPercent = config.ActivityPercent;
        BotSim sim = null;
        try
        {
            config.ActivityPercent = 100;
            sim = new BotSim();
            var bot = sim.AddBot(10, BotCombatStateType.Combat, runLegacyBrain: true);
            var target = BotTestFixture.MakeBot(11, System.Numerics.Vector3.One);
            target.Hp = 0;
            target.MaxHp = 100;
            BotTestFixture.SetPrivateField(target, "_parentWorld", bot.Bot.ParentWorld);
            bot.Runtime.CombatState.Target = target;
            bot.Runtime.Schedule.NextBrainAt = sim.Time.GetUtcNow().UtcDateTime;

            sim.Tick();
            sim.Advance(300);

            await Assert.That(bot.Runtime.CombatState.CurrentState).IsNotEqualTo(BotCombatStateType.Combat);
        }
        finally
        {
            sim?.Reset();
            config.ActivityPercent = previousPercent;
        }
    }

    [Test]
    public async Task CombatState_DeadBotSchedulesRespawn()
    {
        BotTestFixture.RegisterTaskManager();
        var config = BotConfig.Instance;
        var previousPercent = config.ActivityPercent;
        BotSim sim = null;
        try
        {
            config.ActivityPercent = 100;
            sim = new BotSim();
            var bot = sim.AddBot(10, BotCombatStateType.Combat, runLegacyBrain: true);
            bot.Bot.Hp = 0;
            bot.Runtime.Schedule.NextBrainAt = sim.Time.GetUtcNow().UtcDateTime;

            sim.Tick();

            await Assert.That(bot.Runtime.CombatState.RespawnScheduled).IsTrue();
        }
        finally
        {
            sim?.Reset();
            config.ActivityPercent = previousPercent;
            BotTestFixture.ResetTaskManager();
        }
    }

    [Test]
    public async Task CombatState_DeadBotBypassesRotationEngineThatCannotRunLegacyLifecycle()
    {
        BotTestFixture.RegisterTaskManager();
        var config = BotConfig.Instance;
        var previousPercent = config.ActivityPercent;
        BotSim sim = null;
        try
        {
            config.ActivityPercent = 0;
            sim = new BotSim();
            var bot = sim.AddBot(10, BotCombatStateType.Combat, runLegacyBrain: true);
            bot.Runtime.Engines[(int)BotEngineKind.Combat] = new BotEngine(BotEngineKind.Combat, config);
            bot.Bot.Hp = 0;
            bot.Runtime.Schedule.NextBrainAt = sim.Time.GetUtcNow().UtcDateTime;

            sim.Tick();

            await Assert.That(bot.Runtime.CombatState.RespawnScheduled).IsTrue();
            await Assert.That(bot.Brain.FullStepTimes.Count).IsEqualTo(1);
            await Assert.That(bot.Brain.MinimalStepTimes.Count).IsEqualTo(0);
        }
        finally
        {
            sim?.Reset();
            config.ActivityPercent = previousPercent;
            BotTestFixture.ResetTaskManager();
        }
    }

    [Test]
    public async Task DeadBot_DoesNotStepMoverWhileRespawnIsPending()
    {
        BotTestFixture.RegisterTaskManager();
        BotSim sim = null;
        try
        {
            sim = new BotSim();
            var bot = sim.AddBot(10, BotCombatStateType.Combat, runLegacyBrain: true);
            bot.Runtime.MovementState.Destination = System.Numerics.Vector3.One;
            bot.Bot.Hp = 0;
            bot.Runtime.Schedule.NextBrainAt = sim.Time.GetUtcNow().UtcDateTime;

            sim.Tick();

            await Assert.That(bot.Mover.StepCount).IsEqualTo(0);
            await Assert.That(bot.Runtime.MovementState.Destination).IsEqualTo(System.Numerics.Vector3.One);
            await Assert.That(bot.Runtime.CombatState.RespawnScheduled).IsTrue();
        }
        finally
        {
            sim?.Reset();
            BotTestFixture.ResetTaskManager();
        }
    }

    [Test]
    public async Task CombatState_ForcedStateRevertsThroughLegacyBrain()
    {
        var config = BotConfig.Instance;
        var previousPercent = config.ActivityPercent;
        BotSim sim = null;
        try
        {
            config.ActivityPercent = 100;
            sim = new BotSim();
            var bot = sim.AddBot(10, BotCombatStateType.Idle, runLegacyBrain: true);
            bot.Runtime.CombatState.IsActive = true;
            bot.Runtime.CombatState.ForcedState = BotCombatStateType.Grinding;
            bot.Runtime.Schedule.NextBrainAt = sim.Time.GetUtcNow().UtcDateTime;

            sim.Tick();

            await Assert.That(bot.Runtime.CombatState.CurrentState).IsEqualTo(BotCombatStateType.Grinding);
        }
        finally
        {
            sim?.Reset();
            config.ActivityPercent = previousPercent;
        }
    }

    [Test]
    public async Task DuelingState_DeadOpponentEndsDuelThroughLegacyBrain()
    {
        BotTestFixture.RegisterTaskManager();
        var manager = new BotCombatManager();
        BotTestFixture.RegisterSingletons(manager);
        var config = BotConfig.Instance;
        var previousPercent = config.ActivityPercent;
        BotSim sim = null;
        try
        {
            config.ActivityPercent = 100;
            sim = new BotSim();
            var bot = sim.AddBot(10, BotCombatStateType.Dueling, runLegacyBrain: true);
            var opponent = BotTestFixture.MakeBot(11, System.Numerics.Vector3.One);
            opponent.Hp = 0;
            opponent.MaxHp = 100;
            BotTestFixture.SetPrivateField(opponent, "_parentWorld", bot.Bot.ParentWorld);
            bot.Runtime.CombatState.InDuel = true;
            bot.Runtime.CombatState.DuelOpponent = opponent;
            BotTestFixture.GetDictionary<BotCombatState>(manager, "_combatStates")[bot.Bot.Id] = bot.Runtime.CombatState;
            bot.Runtime.Schedule.NextBrainAt = sim.Time.GetUtcNow().UtcDateTime;

            sim.Tick();

            await Assert.That(bot.Runtime.CombatState.CurrentState).IsEqualTo(BotCombatStateType.Idle);
            await Assert.That(bot.Runtime.CombatState.InDuel).IsFalse();
        }
        finally
        {
            sim?.Reset();
            config.ActivityPercent = previousPercent;
            BotTestFixture.ResetTaskManager();
        }
    }

    [Test]
    public async Task GrindingState_RestsAndReturnsToGrindingThroughLegacyBrain()
    {
        var config = BotConfig.Instance;
        var previousPercent = config.ActivityPercent;
        var previousThreshold = config.RestThresholdPercent;
        var previousInterval = config.RestHealInterval;
        var previousHeal = config.RestHealPercentPerTick;
        BotSim sim = null;
        try
        {
            config.ActivityPercent = 100;
            config.RestThresholdPercent = 50;
            config.RestHealInterval = 1;
            config.RestHealPercentPerTick = 50;
            sim = new BotSim();
            var bot = sim.AddBot(10, BotCombatStateType.Grinding, runLegacyBrain: true);
            bot.Bot.Hp = 50;
            bot.Runtime.Schedule.NextBrainAt = sim.Time.GetUtcNow().UtcDateTime;

            sim.Advance(5000);

            await Assert.That(bot.Runtime.CombatState.CurrentState).IsEqualTo(BotCombatStateType.Grinding);
            await Assert.That(bot.Bot.Hp).IsEqualTo(bot.Bot.MaxHp);
        }
        finally
        {
            sim?.Reset();
            config.ActivityPercent = previousPercent;
            config.RestThresholdPercent = previousThreshold;
            config.RestHealInterval = previousInterval;
            config.RestHealPercentPerTick = previousHeal;
        }
    }

    [Test]
    public async Task CombatState_StealthedTargetSearchesAndTimesOutToGrindingThroughLegacyBrain()
    {
        var config = BotConfig.Instance;
        var previousPercent = config.ActivityPercent;
        BotSim sim = null;
        try
        {
            config.ActivityPercent = 100;
            sim = new BotSim();
            var bot = sim.AddBot(10, BotCombatStateType.Grinding, runLegacyBrain: true);
            var target = BotTestFixture.MakeBot(11, System.Numerics.Vector3.Zero);
            target.Hp = 100;
            target.MaxHp = 100;
            BotTestFixture.SetPrivateField(target, "_parentWorld", bot.Bot.ParentWorld);
            var buffs = Mock.Of<IBuffs>();
            buffs.HasEffectsMatchingCondition(Any<Func<Buff, bool>>()).Returns(true);
            target.Buffs = buffs.Object;
            bot.Runtime.CombatState.Target = target;
            bot.Runtime.CombatState.TransitionTo(BotCombatStateType.Combat);
            bot.Runtime.Schedule.NextBrainAt = sim.Time.GetUtcNow().UtcDateTime;

            sim.Advance(100);
            await Assert.That(bot.Runtime.CombatState.CurrentState).IsEqualTo(BotCombatStateType.Searching);

            bot.Runtime.CombatState.SearchStartTime = sim.Time.GetUtcNow().UtcDateTime.AddSeconds(-51);
            sim.Advance(500);

            await Assert.That(bot.Runtime.CombatState.CurrentState).IsEqualTo(BotCombatStateType.Grinding);
        }
        finally
        {
            sim?.Reset();
            config.ActivityPercent = previousPercent;
        }
    }

    [Test]
    public async Task SearchingState_LegacyWorldScanIsIncludedInHostMetrics()
    {
        var config = BotConfig.Instance;
        var previousPercent = config.ActivityPercent;
        BotSim sim = null;
        try
        {
            config.ActivityPercent = 100;
            sim = new BotSim();
            var bot = sim.AddBot(10, BotCombatStateType.Searching, runLegacyBrain: true);
            bot.Runtime.CombatState.IsActive = true;
            bot.Runtime.CombatState.IsSearching = true;
            bot.Runtime.CombatState.SearchStartTime = sim.Time.GetUtcNow().UtcDateTime;
            bot.Runtime.CombatState.LastKnownTargetPosition = new System.Numerics.Vector3(20f, 0f, 0f);
            bot.Runtime.Schedule.NextBrainAt = sim.Time.GetUtcNow().UtcDateTime;

            sim.Tick();

            var metrics = sim.Host.Metrics.Snapshot();
            await Assert.That(metrics.WorldScans).IsEqualTo(1L);
            await Assert.That(metrics.SearchScans).IsEqualTo(1L);
        }
        finally
        {
            sim?.Reset();
            config.ActivityPercent = previousPercent;
        }
    }

    [Test]
    public async Task FollowingState_KeepsFollowTargetAndBandThroughLegacyBrain()
    {
        var config = BotConfig.Instance;
        var previousPercent = config.ActivityPercent;
        BotSim sim = null;
        try
        {
            config.ActivityPercent = 100;
            sim = new BotSim();
            var bot = sim.AddBot(10, BotCombatStateType.Following, runLegacyBrain: true);
            var leader = BotTestFixture.MakeBot(11, new System.Numerics.Vector3(2, 0, 0));
            BotTestFixture.SetPrivateField(leader, "_parentWorld", bot.Bot.ParentWorld);
            bot.Runtime.MovementState.FollowTarget = leader;
            bot.Runtime.MovementState.FollowDistance = 2f;
            bot.Runtime.Schedule.NextBrainAt = sim.Time.GetUtcNow().UtcDateTime;

            sim.Advance(1000);

            await Assert.That(bot.Runtime.CombatState.CurrentState).IsEqualTo(BotCombatStateType.Following);
            await Assert.That(bot.Runtime.MovementState.FollowTarget).IsSameReferenceAs(leader);
            await Assert.That(bot.Runtime.MovementState.FollowDistance).IsEqualTo(2f);
        }
        finally
        {
            sim?.Reset();
            config.ActivityPercent = previousPercent;
        }
    }

    [Test]
    public async Task CombatBot_BrainsAtCombatCadence()
    {
        var config = BotConfig.Instance;
        var previousPercent = config.ActivityPercent;
        try
        {
            config.ActivityPercent = 100;
            var sim = new BotSim();
            var bot = sim.AddBot(10, BotCombatStateType.Combat);

            sim.Advance(2000);

            await Assert.That(bot.Brain.FullStepTimes.Count).IsBetween(6, 8);
        }
        finally
        {
            config.ActivityPercent = previousPercent;
        }
    }

    [Test]
    public async Task FirstBrainSchedulesAreStaggeredByBotId()
    {
        var sim = new BotSim();
        for (uint id = 1; id <= 10; id++)
            sim.AddBot(id);

        await Assert.That(sim.Bots.Select(bot => bot.Runtime.Schedule.NextBrainAt).Distinct().Count()).IsEqualTo(10);
    }

    [Test]
    public async Task ActivityRotationRunsFullOrMinimal_AndFollowIsAlwaysFull()
    {
        var config = BotConfig.Instance;
        var previousPercent = config.ActivityPercent;
        try
        {
            config.ActivityPercent = 10;
            var sim = new BotSim();
            foreach (var id in Enumerable.Range(1, 100))
            {
                var bot = sim.AddBot((uint)id);
                bot.Runtime.Schedule.NextBrainAt = sim.Time.GetUtcNow().UtcDateTime;
            }

            sim.Tick();

            var full = sim.Bots.Count(bot => bot.Brain.FullStepTimes.Count == 1);
            var minimal = sim.Bots.Count(bot => bot.Brain.MinimalStepTimes.Count == 1);
            await Assert.That(full).IsBetween(7, 13);
            await Assert.That(minimal).IsEqualTo(100 - full);

            var follow = sim.AddBot(1001);
            follow.Runtime.MovementState.FollowTarget = follow.Bot;
            follow.Runtime.Schedule.NextBrainAt = sim.Time.GetUtcNow().UtcDateTime;
            config.ActivityPercent = 0;
            sim.Tick();
            await Assert.That(follow.Brain.FullStepTimes.Count).IsEqualTo(1);
            await Assert.That(follow.Brain.MinimalStepTimes.Count).IsEqualTo(0);
        }
        finally
        {
            config.ActivityPercent = previousPercent;
        }
    }

    [Test]
    public async Task BrainFailureIsRecorded_AndDoesNotStopOtherBots()
    {
        var config = BotConfig.Instance;
        var previousPercent = config.ActivityPercent;
        try
        {
            config.ActivityPercent = 100;
            var sim = new BotSim();
            var failing = sim.AddBot(10);
            failing.Brain.ThrowOnFull = true;
            var healthy = sim.AddBot(11);
            failing.Runtime.Schedule.NextBrainAt = sim.Time.GetUtcNow().UtcDateTime;
            healthy.Runtime.Schedule.NextBrainAt = sim.Time.GetUtcNow().UtcDateTime;

            sim.Tick();

            await Assert.That(failing.Runtime.CombatState.Diagnostics.LastError).IsTypeOf<InvalidOperationException>();
            await Assert.That(healthy.Brain.FullStepTimes.Count).IsEqualTo(1);
        }
        finally
        {
            config.ActivityPercent = previousPercent;
        }
    }

    [Test]
    public async Task MoverFailureIsRecorded_AndBrainStillRunsAtItsCadence()
    {
        var config = BotConfig.Instance;
        var previousPercent = config.ActivityPercent;
        try
        {
            config.ActivityPercent = 100;
            var sim = new BotSim();
            var bot = sim.AddBot(10);
            bot.Runtime.Mover = new ThrowingMover(bot.Bot, bot.Runtime.MovementState, bot.Runtime.Broadcaster);
            var now = sim.Time.GetUtcNow().UtcDateTime;
            bot.Runtime.Schedule.NextBrainAt = now;

            sim.Tick();

            await Assert.That(bot.Runtime.CombatState.Diagnostics.LastError).IsTypeOf<InvalidOperationException>();
            await Assert.That(bot.Brain.FullStepTimes.Count).IsEqualTo(1);
            await Assert.That(bot.Runtime.Schedule.NextBrainAt).IsGreaterThan(now);
        }
        finally
        {
            config.ActivityPercent = previousPercent;
        }
    }

    [Test]
    public async Task SeededBotSims_ProduceIdenticalIdleBrainSchedules()
    {
        static List<DateTime> GetSchedule(int seed)
        {
            var sim = new BotSim(seed);
            var bot = sim.AddBot(10);
            var schedule = new List<DateTime>();
            for (var i = 0; i < 8; i++)
            {
                bot.Runtime.Schedule.NextBrainAt = sim.Time.GetUtcNow().UtcDateTime;
                sim.Tick();
                schedule.Add(bot.Runtime.Schedule.NextBrainAt);
            }

            return schedule;
        }

        await Assert.That(GetSchedule(12345)).IsEquivalentTo(GetSchedule(12345));
    }

    [Test]
    public async Task ActiveBotsMetricReflectsLastDecisionUntilNextDecision()
    {
        var config = BotConfig.Instance;
        var previousPercent = config.ActivityPercent;
        try
        {
            config.ActivityPercent = 100;
            var sim = new BotSim();
            var bot = sim.AddBot(10);
            bot.Runtime.Schedule.NextBrainAt = sim.Time.GetUtcNow().UtcDateTime;

            sim.Tick();
            var activeAfterDecision = sim.Host.Metrics.ActiveBots;
            sim.Time.Advance(TimeSpan.FromMilliseconds(100));
            sim.Tick();

            await Assert.That(activeAfterDecision).IsEqualTo(1);
            await Assert.That(sim.Host.Metrics.ActiveBots).IsEqualTo(1);

            config.ActivityPercent = 0;
            bot.Runtime.Schedule.NextBrainAt = sim.Time.GetUtcNow().UtcDateTime;
            sim.Tick();

            await Assert.That(sim.Host.Metrics.ActiveBots).IsEqualTo(0);
        }
        finally
        {
            config.ActivityPercent = previousPercent;
        }
    }

    [Test]
    public async Task OneKillLifecycle_UsesAuthoritativeCreditAndInvokesLogoutOutsideIterationAndLock()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero));
        BotRuntime runtime = null;
        var callbackCount = 0;
        var callbackState = BotLifeState.Offline;
        var callbackHeldRuntimeLock = true;
        var callbackRunning = -1;
        var callbackHadCompletion = false;
        var callbackHadDelta = false;
        var host = MakeLifecycleHost(time, _ =>
        {
            callbackCount++;
            var callbackView = runtime.LifeController.Inspect();
            callbackState = callbackView.Life.State;
            callbackHadCompletion = callbackView.ProgressionCompletion.HasValue;
            callbackHadDelta = callbackView.ProgressionDelta.HasValue;
            callbackHeldRuntimeLock = Monitor.IsEntered(runtime.SyncRoot);
            callbackRunning = Volatile.Read(ref runtime.Running);
            return true;
        });
        runtime = MakeLifecycleRuntime(6301, time);

        host.Register(runtime);
        try
        {
            host.HostTask.Execute();

            await Assert.That(runtime.CombatState.CurrentState).IsEqualTo(BotCombatStateType.Grinding);
            await Assert.That(runtime.CombatState.KillGoal).IsEqualTo(1);
            await Assert.That(runtime.CombatState.Target).IsNull();
            await Assert.That(runtime.Bot.CurrentTarget).IsNull();
            await Assert.That(runtime.MovementState.Destination).IsNull();

            var victim = new Npc { ObjId = 9901, TemplateId = 42, Hp = 0, MaxHp = 100 };
            runtime.Bot.Events.OnKill(victim, new OnKillArgs { Killer = runtime.Bot, Victim = victim });
            runtime.CombatState.KillGoal = null;
            runtime.CombatState.TransitionTo(BotCombatStateType.Idle);
            time.Advance(TimeSpan.FromMilliseconds(100));

            host.HostTask.Execute();
            host.HostTask.Execute();

            var life = runtime.LifeController.Inspect();
            await Assert.That(runtime.CombatState.KillCount).IsEqualTo(1);
            await Assert.That(callbackCount).IsEqualTo(1);
            await Assert.That(callbackState).IsEqualTo(BotLifeState.Despawning);
            await Assert.That(callbackHadCompletion).IsTrue();
            await Assert.That(callbackHadDelta).IsTrue();
            await Assert.That(callbackHeldRuntimeLock).IsFalse();
            await Assert.That(callbackRunning).IsEqualTo(0);
            await Assert.That(life.Life.State).IsEqualTo(BotLifeState.Offline);
            await Assert.That(life.LogoutSucceeded).IsTrue();
        }
        finally
        {
            host.Unregister(runtime.Bot.Id);
        }
    }

    [Test]
    public async Task NaturalRecoveryWait_SuspendsMoverAndBrainUntilWorldResourcesAreFull()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 2, 0, TimeSpan.Zero));
        var callbackCount = 0;
        var host = MakeLifecycleHost(time, _ =>
        {
            callbackCount++;
            return true;
        });
        var runtime = MakeLifecycleRuntime(6304, time);
        var mover = (BotSim.SimMover)runtime.Mover;

        host.Register(runtime);
        try
        {
            host.HostTask.Execute();
            var moverStepsBeforeRecovery = mover.StepCount;
            var brainStepsBeforeRecovery = host.Metrics.BrainStepsTotal;
            runtime.CombatState.KillCount = 1;
            runtime.CombatState.KillGoal = null;
            runtime.CombatState.TransitionTo(BotCombatStateType.Idle);
            runtime.Bot.Mp = 60;
            time.Advance(TimeSpan.FromSeconds(1));

            host.HostTask.Execute();
            host.HostTask.Execute();

            var pending = runtime.LifeController.Inspect();
            await Assert.That(callbackCount).IsEqualTo(0);
            await Assert.That(pending.Recovery.State).IsEqualTo(BotLifeRecoveryState.Pending);
            await Assert.That(pending.ProgressionCompletion).IsNull();
            await Assert.That(runtime.LifeController.ShouldSuspendRuntime).IsTrue();
            await Assert.That(mover.StepCount).IsEqualTo(moverStepsBeforeRecovery);
            await Assert.That(host.Metrics.BrainStepsTotal).IsEqualTo(brainStepsBeforeRecovery);

            runtime.Bot.Mp = 100;
            time.Advance(TimeSpan.FromSeconds(1));
            host.HostTask.Execute();

            var completed = runtime.LifeController.Inspect();
            await Assert.That(callbackCount).IsEqualTo(1);
            await Assert.That(completed.Recovery.State).IsEqualTo(BotLifeRecoveryState.Completed);
            await Assert.That(completed.ProgressionCompletion?.Mp).IsEqualTo(100L);
            await Assert.That(completed.Life.State).IsEqualTo(BotLifeState.Offline);
        }
        finally
        {
            host.Unregister(runtime.Bot.Id);
        }
    }

    [Test]
    public async Task MultipleRuntimes_PendingRecoveryCountersStayFixedWhilePeerAndHostAdvance()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 4, 0, TimeSpan.Zero));
        var logoutIds = new List<uint>();
        var host = MakeLifecycleHost(time, id =>
        {
            logoutIds.Add(id);
            return true;
        });
        var pending = MakeLifecycleRuntime(6305, time, directorZone: 137);
        var advancing = MakeLifecycleRuntime(6306, time, directorZone: 137);
        var director = new BotActivityDirectorTask(
            new BotConfig
            {
                ActivityDirectorEnabled = true,
                ActivityDirectorZoneId = 137,
                ActivityDirectorCharacterIds = [pending.Bot.Id, advancing.Bot.Id],
                ActivityDirectorMinimumPopulation = 1,
                ActivityDirectorTargetPopulation = 2,
                ActivityDirectorMaximumPopulation = 2
            },
            Mock.Of<IBotManager>().Object,
            time);

        try
        {
            await Assert.That(director.TryStart()).IsTrue();
            host.Register(pending);
            host.Register(advancing);
            host.HostTask.Execute();

            await Assert.That(pending.LifeController.Inspect().Activity).IsEqualTo("grind");
            await Assert.That(advancing.LifeController.Inspect().Activity).IsEqualTo("grind");

            advancing.Brain = new NoopBrain(
                advancing.Bot,
                advancing.CombatState,
                advancing.Broadcaster);
            pending.CombatState.KillCount = 1;
            pending.CombatState.KillGoal = null;
            pending.CombatState.TransitionTo(BotCombatStateType.Idle);
            pending.Bot.Mp = 60;
            advancing.MovementState.Destination = Vector3.One;
            time.Advance(TimeSpan.FromSeconds(1));
            advancing.Schedule.NextBrainAt = time.GetUtcNow().UtcDateTime;
            host.HostTask.Execute();

            var recovery = pending.LifeController.Inspect();
            await Assert.That(recovery.Recovery.State).IsEqualTo(BotLifeRecoveryState.Pending);
            await Assert.That(pending.LifeController.ShouldSuspendRuntime).IsTrue();
            await Assert.That(logoutIds).IsEmpty();

            var pendingBrainBefore = pending.Metrics.BrainSteps;
            var pendingMoverBefore = pending.Metrics.MoverSteps;
            var advancingBrainBefore = advancing.Metrics.BrainSteps;
            var advancingMoverBefore = advancing.Metrics.MoverSteps;
            var hostBrainBefore = host.Metrics.BrainStepsTotal;
            var hostMoverBefore = host.Metrics.MoverStepsTotal;

            for (var tick = 0; tick < 3; tick++)
            {
                time.Advance(TimeSpan.FromSeconds(1));
                advancing.Schedule.NextBrainAt = time.GetUtcNow().UtcDateTime;
                host.HostTask.Execute();
            }

            var advancingBrainDelta = advancing.Metrics.BrainSteps - advancingBrainBefore;
            var advancingMoverDelta = advancing.Metrics.MoverSteps - advancingMoverBefore;
            var hostBrainDelta = host.Metrics.BrainStepsTotal - hostBrainBefore;
            var hostMoverDelta = host.Metrics.MoverStepsTotal - hostMoverBefore;
            await Assert.That(pending.LifeController.ShouldSuspendRuntime).IsTrue();
            await Assert.That(pending.Metrics.BrainSteps).IsEqualTo(pendingBrainBefore);
            await Assert.That(pending.Metrics.MoverSteps).IsEqualTo(pendingMoverBefore);
            await Assert.That(advancingBrainDelta).IsGreaterThan(0L);
            await Assert.That(advancingMoverDelta).IsGreaterThan(0L);
            await Assert.That(hostBrainDelta).IsEqualTo(advancingBrainDelta);
            await Assert.That(hostMoverDelta).IsEqualTo(advancingMoverDelta);
            await Assert.That(logoutIds).IsEmpty();

            pending.Bot.Mp = 100;
            time.Advance(TimeSpan.FromSeconds(1));
            host.HostTask.Execute();
            host.HostTask.Execute();

            var completed = pending.LifeController.Inspect();
            await Assert.That(logoutIds.Count(id => id == pending.Bot.Id)).IsEqualTo(1);
            await Assert.That(logoutIds).DoesNotContain(advancing.Bot.Id);
            await Assert.That(completed.Recovery.State).IsEqualTo(BotLifeRecoveryState.Completed);
            await Assert.That(completed.ProgressionCompletion?.Mp).IsEqualTo(100L);
            await Assert.That(completed.Life.State).IsEqualTo(BotLifeState.Offline);
        }
        finally
        {
            director.Stop();
            host.Unregister(pending.Bot.Id);
            host.Unregister(advancing.Bot.Id);
        }
    }

    [Test]
    public async Task LogoutCallbackFailure_IsRecordedAndNeverRetried()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 5, 0, TimeSpan.Zero));
        var callbackCount = 0;
        var host = MakeLifecycleHost(time, _ =>
        {
            callbackCount++;
            throw new InvalidOperationException("simulated logout failure");
        });
        var runtime = MakeLifecycleRuntime(6302, time);
        var mover = (BotSim.SimMover)runtime.Mover;

        host.Register(runtime);
        try
        {
            host.HostTask.Execute();
            runtime.CombatState.KillCount = 1;
            runtime.CombatState.KillGoal = null;
            runtime.CombatState.TransitionTo(BotCombatStateType.Idle);

            host.HostTask.Execute();
            time.Advance(TimeSpan.FromMilliseconds(100));
            host.HostTask.Execute();

            var life = runtime.LifeController.Inspect();
            var completion = life.ProgressionCompletion;
            runtime.Bot.Hp = 64;
            host.HostTask.Execute();
            var afterDuplicateTick = runtime.LifeController.Inspect();
            await Assert.That(callbackCount).IsEqualTo(1);
            await Assert.That(life.Life.State).IsEqualTo(BotLifeState.Despawning);
            await Assert.That(life.LogoutSucceeded).IsFalse();
            await Assert.That(life.LastTransition?.Event.Kind).IsEqualTo(BotLifeEventKind.LogoutRequested);
            await Assert.That(completion.HasValue).IsTrue();
            await Assert.That(completion.Value.Hp).IsEqualTo(100L);
            await Assert.That(afterDuplicateTick.ProgressionCompletion).IsEqualTo(completion);
            await Assert.That(afterDuplicateTick.ProgressionDelta).IsEqualTo(life.ProgressionDelta);
            await Assert.That(mover.StepCount).IsEqualTo(1);
        }
        finally
        {
            host.Unregister(runtime.Bot.Id);
        }
    }

    [Test]
    public async Task ReaddingPersistentIdentity_ResetsControllerAndPermitsFreshIteration()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 10, 0, TimeSpan.Zero));
        var host = MakeLifecycleHost(time, _ => false);
        var controller = new BotLifeController();
        var first = MakeLifecycleRuntime(6303, time, controller);
        host.Register(first);
        host.HostTask.Execute();
        first.CombatState.KillCount = 1;
        first.CombatState.KillGoal = null;
        first.CombatState.TransitionTo(BotCombatStateType.Idle);
        host.HostTask.Execute();
        host.Unregister(first.Bot.Id);

        time.Advance(TimeSpan.FromSeconds(1));
        var second = MakeLifecycleRuntime(6303, time, controller);
        host.Register(second);
        try
        {
            var fresh = controller.Inspect();
            await Assert.That(fresh.Life.State).IsEqualTo(BotLifeState.Idle);
            await Assert.That(fresh.Activity).IsNull();
            await Assert.That(fresh.DecisionAt).IsNull();
            await Assert.That(fresh.Recovery.State).IsEqualTo(BotLifeRecoveryState.NotRequired);
            await Assert.That(fresh.Recovery.StartedAt).IsNull();
            await Assert.That(fresh.Recovery.ObservedAt).IsNull();
            await Assert.That(fresh.LogoutRequestedAt).IsNull();
            await Assert.That(fresh.LogoutSucceeded).IsNull();
            await Assert.That(fresh.ProgressionBaseline).IsNull();
            await Assert.That(fresh.ProgressionCompletion).IsNull();
            await Assert.That(fresh.ProgressionDelta).IsNull();

            host.HostTask.Execute();

            var restarted = controller.Inspect();
            await Assert.That(restarted.Life.State).IsEqualTo(BotLifeState.Active);
            await Assert.That(restarted.Activity).IsEqualTo("grind");
            await Assert.That(restarted.ProgressionBaseline.HasValue).IsTrue();
            await Assert.That(restarted.ProgressionCompletion).IsNull();
            await Assert.That(restarted.ProgressionDelta).IsNull();
            await Assert.That(second.CombatState.KillGoal).IsEqualTo(1);
        }
        finally
        {
            host.Unregister(second.Bot.Id);
        }
    }

    [Test]
    public async Task MultipleRuntimes_OnlyQualifiedDirectorIdentitiesRunIndependentLifecycle()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 20, 0, TimeSpan.Zero));
        var logoutIds = new List<uint>();
        var host = MakeLifecycleHost(time, id =>
        {
            logoutIds.Add(id);
            return true;
        });
        var qualifiedOne = MakeLifecycleRuntime(6401, time, directorZone: 137);
        var qualifiedTwo = MakeLifecycleRuntime(6402, time, directorZone: 137);
        var configuredWrongZone = MakeLifecycleRuntime(6403, time, directorZone: 999);
        var manual = MakeLifecycleRuntime(6404, time, directorZone: 137);
        var director = new BotActivityDirectorTask(
            new BotConfig
            {
                ActivityDirectorEnabled = true,
                ActivityDirectorZoneId = 137,
                ActivityDirectorCharacterIds = [6401, 6402, 6403],
                ActivityDirectorMinimumPopulation = 1,
                ActivityDirectorTargetPopulation = 2,
                ActivityDirectorMaximumPopulation = 3
            },
            Mock.Of<IBotManager>().Object,
            time);
        var runtimes = new[] { qualifiedOne, qualifiedTwo, configuredWrongZone, manual };

        try
        {
            await Assert.That(director.TryStart()).IsTrue();
            foreach (var runtime in runtimes)
                host.Register(runtime);

            host.HostTask.Execute();

            await Assert.That(qualifiedOne.LifeController.Inspect().Activity).IsEqualTo("grind");
            await Assert.That(qualifiedTwo.LifeController.Inspect().Activity).IsEqualTo("grind");
            await Assert.That(configuredWrongZone.LifeController.Inspect().Activity).IsNull();
            await Assert.That(manual.LifeController.Inspect().Activity).IsNull();
            await Assert.That(qualifiedOne.CombatState.ForcedState).IsNull();
            await Assert.That(qualifiedTwo.CombatState.ForcedState).IsNull();
            await Assert.That(qualifiedOne.CombatState.Target).IsNull();
            await Assert.That(qualifiedTwo.CombatState.Target).IsNull();

            foreach (var runtime in new[] { qualifiedOne, qualifiedTwo })
            {
                runtime.CombatState.KillCount = 1;
                runtime.CombatState.KillGoal = null;
                runtime.CombatState.TransitionTo(BotCombatStateType.Idle);
            }
            time.Advance(TimeSpan.FromSeconds(1));
            host.HostTask.Execute();

            await Assert.That(string.Join(",", logoutIds.OrderBy(id => id))).IsEqualTo("6401,6402");
            await Assert.That(qualifiedOne.LifeController.Inspect().LogoutSucceeded).IsTrue();
            await Assert.That(qualifiedTwo.LifeController.Inspect().LogoutSucceeded).IsTrue();
            await Assert.That(configuredWrongZone.LifeController.Inspect().LogoutRequestedAt).IsNull();
            await Assert.That(manual.LifeController.Inspect().LogoutRequestedAt).IsNull();
        }
        finally
        {
            director.Stop();
            foreach (var runtime in runtimes)
                host.Unregister(runtime.Bot.Id);
        }
    }

    [Test]
    public async Task TickMetrics_TickMsEmaMovesForSlowBrain()
    {
        var sim = new BotSim();
        var bot = sim.AddBot(10);
        bot.Brain.SpinMilliseconds = 5;
        bot.Runtime.Schedule.NextBrainAt = sim.Time.GetUtcNow().UtcDateTime;
        var before = sim.Host.Metrics.TickMsEma;

        sim.Tick();

        await Assert.That(sim.Host.Metrics.TickMsEma).IsGreaterThan(before);
    }

    [Test]
    public async Task OverlappingHostTickIsSkippedAndCounted()
    {
        var sim = new BotSim();
        var bot = sim.AddBot(10);
        bot.Runtime.Schedule.NextBrainAt = sim.Time.GetUtcNow().UtcDateTime;
        bot.Brain.Release.Reset();

        var first = System.Threading.Tasks.Task.Run(sim.Tick);
        await Assert.That(bot.Brain.Entered.Wait(TimeSpan.FromSeconds(2))).IsTrue();
        sim.Tick();
        bot.Brain.Release.Set();
        await first;

        await Assert.That(sim.Host.Metrics.SkippedTicks).IsEqualTo(1);
    }

    [Test]
    public async Task UnregisterStopsStepsAndInvokesCancellationCallbacksOnce()
    {
        var sim = new BotSim();
        var bot = sim.AddBot(10);
        sim.Host.Unregister(bot.Bot.Id);
        sim.Tick();
        sim.Host.Unregister(bot.Bot.Id);

        await Assert.That(bot.Mover.CancelCount).IsEqualTo(1);
        await Assert.That(bot.Brain.CancelCount).IsEqualTo(1);
        await Assert.That(bot.Mover.StepCount).IsEqualTo(0);
        await Assert.That(bot.Brain.FullStepTimes.Count).IsEqualTo(0);
    }

    [Test]
    public async Task UnscheduledMoverSelfRetirement_UnregistersOnNextTickAndCancelsOnce()
    {
        var sim = new BotSim();
        var bot = sim.AddBot(10);
        var cancelCount = 0;
        bot.Runtime.Brain = null;
        bot.Runtime.Mover = new BotMovementTask(
            bot.Bot,
            bot.Runtime.MovementState,
            bot.Runtime.Broadcaster,
            _ => cancelCount++);
        BotTestFixture.SetPrivateField<AAEmu.Game.Models.Game.World.WorldInstance>(bot.Bot, "_parentWorld", null);

        sim.Tick();
        sim.Tick();

        await Assert.That(bot.Runtime.Mover.Cancelled).IsTrue();
        await Assert.That(cancelCount).IsEqualTo(1);
        await Assert.That(sim.Host.GetRuntime(bot.Bot.Id)).IsNull();
    }

    [Test]
    public async Task UnscheduledBrainSelfRetirement_UnregistersOnNextTickAndCancelsOnce()
    {
        var sim = new BotSim();
        var bot = sim.AddBot(11);
        var cancelCount = 0;
        bot.Runtime.Mover = null;
        bot.Runtime.Brain = new BotCombatTask(
            bot.Bot,
            bot.Runtime.CombatState,
            bot.Runtime.Broadcaster,
            _ => cancelCount++,
            timeProvider: sim.Time);
        bot.Runtime.Schedule.NextBrainAt = sim.Time.GetUtcNow().UtcDateTime;
        BotTestFixture.SetPrivateField<AAEmu.Game.Models.Game.World.WorldInstance>(bot.Bot, "_parentWorld", null);

        sim.Tick();
        sim.Tick();

        await Assert.That(bot.Runtime.Brain.Cancelled).IsTrue();
        await Assert.That(cancelCount).IsEqualTo(1);
        await Assert.That(sim.Host.GetRuntime(bot.Bot.Id)).IsNull();
    }

    [Test]
    public async Task TickMetricsHaveNonNegativeEmaAndMaxAtLeastEma()
    {
        var sim = new BotSim();
        var bot = sim.AddBot(10);
        bot.Runtime.Schedule.NextBrainAt = sim.Time.GetUtcNow().UtcDateTime;

        sim.Tick();

        await Assert.That(sim.Host.Metrics.TickMsEma).IsGreaterThanOrEqualTo(0d);
        await Assert.That(sim.Host.Metrics.MaxTickMs).IsGreaterThanOrEqualTo(sim.Host.Metrics.TickMsEma);
    }

    [Test]
    public async Task DisableCombatDuringBrainStep_KeepsRuntimeAndMoverRegistered()
    {
        BotTestFixture.RegisterTaskManager();
        try
        {
            var botManager = new BotManager(_ => null, onlineLookup: _ => null);
            BotTestFixture.RegisterSingletons(botManager);
            var manager = new BotCombatManager();
            var host = BotHost.Instance;
            var bot = BotTestFixture.MakeBot(10, default);
            var movementState = new BotMovementState();
            var combatState = new BotCombatState { BotId = bot.Id, IsActive = true };
            var broadcaster = new BotMovementBroadcaster(bot);
            var mover = new BotSim.SimMover(bot, movementState, broadcaster);
            var brain = new BlockingBrain(bot, combatState, broadcaster);
            var runtime = new BotRuntime(bot, movementState, combatState, broadcaster, mover, brain);
            BotTestFixture.GetDictionary<BotCombatState>(manager, "_combatStates")[bot.Id] = combatState;
            BotTestFixture.GetDictionary<BotCombatTask>(manager, "_combatTasks")[bot.Id] = brain;
            host.Register(runtime);
            runtime.Schedule.NextBrainAt = host.TimeProvider.GetUtcNow().UtcDateTime;

            var tick = System.Threading.Tasks.Task.Run(host.HostTask.Execute);
            await Assert.That(brain.Entered.Wait(TimeSpan.FromSeconds(2))).IsTrue();

            var disable = System.Threading.Tasks.Task.Run(() => manager.DisableCombat(bot));
            brain.Release.Set();
            await System.Threading.Tasks.Task.WhenAll(tick, disable);

            await Assert.That(host.GetRuntime(bot.Id)).IsSameReferenceAs(runtime);
            await Assert.That(mover.Cancelled).IsFalse();
        }
        finally
        {
            BotTestFixture.ResetTaskManager();
        }
    }

    [Test]
    public async Task EndDuelDuringBrainStep_WhenCombatWasInactive_KeepsRuntimeAndMoverRegistered()
    {
        BotTestFixture.RegisterTaskManager();
        try
        {
            var botManager = new BotManager(_ => null, onlineLookup: _ => null);
            BotTestFixture.RegisterSingletons(botManager);
            var manager = new BotCombatManager();
            var host = BotHost.Instance;
            var bot = BotTestFixture.MakeBot(11, default);
            var movementState = new BotMovementState();
            var combatState = new BotCombatState
            {
                BotId = bot.Id,
                IsActive = true,
                WasCombatActive = false,
                InDuel = true,
                CurrentState = BotCombatStateType.Dueling
            };
            var broadcaster = new BotMovementBroadcaster(bot);
            var mover = new BotSim.SimMover(bot, movementState, broadcaster);
            var brain = new BlockingBrain(bot, combatState, broadcaster);
            var runtime = new BotRuntime(bot, movementState, combatState, broadcaster, mover, brain);
            BotTestFixture.GetDictionary<BotCombatState>(manager, "_combatStates")[bot.Id] = combatState;
            BotTestFixture.GetDictionary<BotCombatTask>(manager, "_combatTasks")[bot.Id] = brain;
            host.Register(runtime);
            runtime.Schedule.NextBrainAt = host.TimeProvider.GetUtcNow().UtcDateTime;

            var tick = System.Threading.Tasks.Task.Run(host.HostTask.Execute);
            await Assert.That(brain.Entered.Wait(TimeSpan.FromSeconds(2))).IsTrue();

            var endDuel = System.Threading.Tasks.Task.Run(() => manager.EndDuel(bot));
            brain.Release.Set();
            await System.Threading.Tasks.Task.WhenAll(tick, endDuel);

            await Assert.That(host.GetRuntime(bot.Id)).IsSameReferenceAs(runtime);
            await Assert.That(mover.Cancelled).IsFalse();
        }
        finally
        {
            BotTestFixture.ResetTaskManager();
        }
    }

    private sealed class ThrowingMover(AAEmu.Game.Models.Game.Char.Character bot, BotMovementState state, BotMovementBroadcaster broadcaster)
        : BotMovementTask(bot, state, broadcaster)
    {
        internal override void Step() => throw new InvalidOperationException("simulated mover failure");
    }

    private static BotHost MakeLifecycleHost(FakeTimeProvider time, Func<uint, bool> logout)
    {
        var taskManager = Mock.Of<ITaskManager>();
        taskManager.Schedule(Any<AAEmu.Game.Models.Tasks.Task>(), Any<TimeSpan?>(), Any<TimeSpan?>(), Any<int>())
            .Returns(true);
        return new BotHost(taskManager.Object, time, logoutBot: logout);
    }

    private static BotRuntime MakeLifecycleRuntime(
        uint id,
        FakeTimeProvider time,
        BotLifeController controller = null,
        uint? directorZone = null)
    {
        var bot = new LifecycleCharacterMock
        {
            Id = id,
            ObjId = 1000 + id,
            Name = $"bot{id}",
            Hp = 100,
            Mp = 100
        };
        bot.Transform.Local.SetPosition(Vector3.Zero);
        var instanceId = directorZone.HasValue ? WorldManager.DefaultInstanceId : 1u;
        var templateId = directorZone.HasValue ? WorldManager.DefaultWorldTemplateId : 1u;
        var world = new WorldInstance(
            new WorldTemplate { Id = templateId, Name = $"world{instanceId}" },
            0,
            true,
            instanceId);
        BotTestFixture.SetPrivateField(bot, "_parentWorld", world);
        if (directorZone.HasValue)
        {
            BotTestFixture.SetPrivateField(bot.Transform, "_instanceId", instanceId);
            BotTestFixture.SetPrivateField(bot.Transform, "_zoneId", directorZone.Value);
        }
        var movement = new BotMovementState();
        var combat = new BotCombatState();
        var blackboard = new BotBlackboard();
        blackboard.Register(BotValues.NearbyHostileNpcIds, new ManualValue<List<uint>>([9901u]));
        world.AddObject(new Npc { ObjId = 9901, Hp = 100, MaxHp = 100 });
        var broadcaster = new BotMovementBroadcaster(bot, time);
        var mover = new BotSim.SimMover(bot, movement, broadcaster);
        var brain = new BotCombatTask(
            bot,
            combat,
            broadcaster,
            onCancel: null,
            blackboard: blackboard,
            timeProvider: time);
        return new BotRuntime(
            bot,
            movement,
            combat,
            broadcaster,
            mover,
            brain,
            blackboard,
            new BotConfig { UseEngine = false },
            controller);
    }

    private sealed class LifecycleCharacterMock : CharacterMock
    {
        public override int MaxHp { get; set; } = 100;
        public override int MaxMp => 100;
    }

    private sealed class BlockingBrain(AAEmu.Game.Models.Game.Char.Character bot, BotCombatState state, BotMovementBroadcaster broadcaster)
        : BotCombatTask(bot, state, broadcaster)
    {
        public ManualResetEventSlim Entered { get; } = new(false);
        public ManualResetEventSlim Release { get; } = new(false);

        internal override void Step()
        {
            Entered.Set();
            Release.Wait();
        }
    }

    private sealed class NoopBrain(AAEmu.Game.Models.Game.Char.Character bot, BotCombatState state, BotMovementBroadcaster broadcaster)
        : BotCombatTask(bot, state, broadcaster)
    {
        internal override void Step()
        {
        }

        internal override void StepMinimal()
        {
        }
    }
}
