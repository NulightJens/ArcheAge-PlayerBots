using System.Diagnostics;
using AAEmu.Game.Bots.Blackboard;
using AAEmu.Game.Bots.Body;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Bots.Content.Rotations;
using AAEmu.Game.Bots.Ops;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Tasks.Bots;
using NLog;

namespace AAEmu.Game.Bots.Host;

public sealed class BotHostTask : AAEmu.Game.Models.Tasks.Task
{
    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();
    private readonly BotHost _host;
    private readonly List<BotRuntime> _retiredRuntimes = [];
    private readonly List<BotRuntime> _logoutRequests = [];
    private int _running;
    private DateTime _lastMetricsLogAt = DateTime.MinValue;

    internal BotHostTask(BotHost host)
    {
        _host = host;
    }

    internal void InitializeStart(DateTime now)
    {
        _lastMetricsLogAt = now;
    }

    public override void Execute()
    {
        if (Cancelled)
            return;
        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
        {
            _host.Metrics.IncrementSkippedTicks();
            return;
        }

        var start = Stopwatch.GetTimestamp();
        try
        {
            ExecuteTick(start);
        }
        finally
        {
            Volatile.Write(ref _running, 0);
        }
    }

    private void ExecuteTick(long start)
    {
        try
        {
            ExecuteTickCore(start);
        }
        catch (Exception e)
        {
            _host.Metrics.IncrementTickErrors();
            Logger.Error(e, "BOT host ev=tick_error");
        }
    }

    private void ExecuteTickCore(long start)
    {
        var nowOffset = _host.TimeProvider.GetUtcNow();
        var now = nowOffset.UtcDateTime;
        var config = BotConfig.Instance;
        var configuredPercent = (int)Math.Clamp(config.ActivityPercent, 0, 100);
        var effectivePercent = BotActivityGovernor.EffectiveActivePercent(
            configuredPercent,
            _host.Metrics.TickMsEma,
            config.HostTickBudgetMs,
            _host.ServerMetrics?.PressureMs ?? double.NaN,
            config.ServerTickBudgetMs);
        var windowLength = Math.Max(1, (long)(config.ActivityWindowMs * TimeSpan.TicksPerMillisecond));
        var windowIndex = now.Ticks / windowLength;
        var runtimes = _host.GetRuntimeSnapshot();
        _retiredRuntimes.Clear();
        _logoutRequests.Clear();
        var isSoleRuntime = runtimes.Length == 1;

        for (var i = 0; i < runtimes.Length; i++)
        {
            var runtime = runtimes[i];
            var lifecycleEligible = isSoleRuntime ||
                                    BotActivityDirectorTask.IsCurrentLifecycleEligible(runtime.Bot);
            runtime.Schedule.Now = now;

            if (Interlocked.CompareExchange(ref runtime.Running, 1, 0) != 0)
            {
                _host.Metrics.IncrementRuntimeOverlaps();
                continue;
            }

            BotCombatTask brain;
            try
            {
                lock (runtime.SyncRoot)
                {
                    if (runtime.Retired || runtime.Mover?.Cancelled == true || runtime.Brain?.Cancelled == true)
                    {
                        _retiredRuntimes.Add(runtime);
                        continue;
                    }

                    var logoutRequested = runtime.LifeController.Step(
                        runtime,
                        lifecycleEligible,
                        nowOffset);
                    if (logoutRequested)
                    {
                        _logoutRequests.Add(runtime);
                        continue;
                    }
                    if (runtime.LifeController.ShouldSuspendRuntime)
                        continue;

                    runtime.Social.GuardLeader();
                    StepMover(runtime, now, config);
                    // A brain captured before Unregister may complete the one already-started step; no step starts after OnCancel.
                    brain = runtime.Brain;
                }

                StepBrain(runtime, brain, now, windowIndex, effectivePercent, config);

                lock (runtime.SyncRoot)
                {
                    if (!runtime.Retired &&
                        runtime.LifeController.Step(runtime, lifecycleEligible, nowOffset))
                    {
                        _logoutRequests.Add(runtime);
                    }
                }
            }
            catch (Exception e)
            {
                RecordError(runtime, e);
            }
            finally
            {
                Volatile.Write(ref runtime.Running, 0);
            }
        }

        foreach (var runtime in _retiredRuntimes)
            _host.Unregister(runtime);

        // Lifecycle evaluation only queues a request. The normal persisted
        // DespawnBot path runs after all runtime iteration and SyncRoot locks.
        foreach (var runtime in _logoutRequests)
            InvokeLogout(runtime);

        var activeBots = 0;
        for (var i = 0; i < runtimes.Length; i++)
        {
            if (runtimes[i].Schedule.LastActive)
                activeBots++;
        }

        var elapsedMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
        var liveBots = _host.RuntimeCount;
        _host.Metrics.RecordHostTick(elapsedMs, liveBots, Math.Min(activeBots, liveBots), configuredPercent, effectivePercent);
        if (!_host.IsStarted)
            _host.Metrics.RecordPopulation(0, 0);

        if (_lastMetricsLogAt == DateTime.MinValue || now - _lastMetricsLogAt >= TimeSpan.FromMilliseconds(config.MetricsLogIntervalMs))
        {
            _lastMetricsLogAt = now;
            _host.LogMetrics();
        }
    }

    private void InvokeLogout(BotRuntime runtime)
    {
        var callbackAt = _host.TimeProvider.GetUtcNow();
        if (!runtime.LifeController.TryBeginLogoutCallback(callbackAt))
            return;

        var succeeded = false;
        try
        {
            succeeded = _host.LogoutBot(runtime.Bot.Id);
        }
        catch (Exception exception)
        {
            Logger.Error(exception, $"BOT id={runtime.Bot.Id} ev=life_logout_callback_failed");
        }

        runtime.LifeController.RecordLogoutResult(
            runtime.Bot.Id,
            succeeded,
            _host.TimeProvider.GetUtcNow());
    }

    private void StepMover(BotRuntime runtime, DateTime now, BotConfig config)
    {
        // Death is a lifecycle state, not a movement state. Preserve the pending
        // destination/follow intent for recovery, but never animate or advance a
        // corpse while the respawn task is pending.
        if (runtime.Bot.IsDead)
            return;

        var state = runtime.MovementState;
        var transform = runtime.Bot.Transform;
        var worldPosition = transform?.World;
        if (worldPosition == null)
        {
            if (!runtime.MissingTransformLogged)
            {
                runtime.MissingTransformLogged = true;
                Logger.Trace($"BOT id={runtime.Bot.Id} ev=mover_skip reason=missing_transform");
            }
            return;
        }

        runtime.StuckWatch.Update(now, worldPosition.Position, state.Destination.HasValue);
        if (runtime.Mover == null)
            return;

        var moving = state.Destination != null || state.IsMoving || state.IsFalling || state.FollowTarget != null ||
                     state.FallVelocity > 0 || state.JumpRequested || state.IsJumping;
        var groundCheckDue = runtime.Schedule.LastGroundCheckAt == DateTime.MinValue ||
                             now - runtime.Schedule.LastGroundCheckAt >= TimeSpan.FromMilliseconds(config.GroundCheckIntervalMs);
        if (!moving && !groundCheckDue)
            return;

        try
        {
            runtime.Mover.Step();
            runtime.Metrics.MoverSteps++;
            _host.Metrics.RecordMoverStep();
        }
        catch (Exception e)
        {
            RecordError(runtime, e);
        }

        runtime.Schedule.LastGroundCheckAt = now;
    }

    private void StepBrain(
        BotRuntime runtime,
        BotCombatTask brain,
        DateTime now,
        long windowIndex,
        int effectivePercent,
        BotConfig config)
    {
        if (brain == null || now < runtime.Schedule.NextBrainAt)
            return;

        var brainStart = Stopwatch.GetTimestamp();
        var active = false;
        try
        {
            active = BotActivityGovernor.IsAlwaysActive(runtime) ||
                     BotActivityGovernor.IsInRotation(runtime.Bot.Id, windowIndex, effectivePercent);

            // Lifecycle work must not compete with rotation actions. A compiled
            // combat rotation can remain continuously useful while the bot is
            // dead and otherwise starve LegacyTickAction forever, leaving the
            // bot at 0 HP in Combat with its target still attached. Dead bots are
            // always serviced regardless of the activity governor; BotCombatTask
            // schedules and completes the bounded respawn without running combat.
            if (runtime.Bot.IsDead)
            {
                active = true;
                brain.Step();
                runtime.Metrics.BrainSteps++;
                return;
            }

            // Compiled combat rotations can remain continuously useful and
            // starve LegacyTickAction. Enforce contained-attack floors before
            // any engine action so a rotation cannot bypass the safety gate.
            if (brain.TryEnforceNonlethalFloor())
            {
                runtime.Metrics.BrainSteps++;
                return;
            }

            var engineKind = runtime.CombatState.CurrentState is BotCombatStateType.Combat or BotCombatStateType.Dueling
                ? BotEngineKind.Combat
                : BotEngineKind.NonCombat;
            if (engineKind == BotEngineKind.Combat)
            {
                var archetype = BotArchetypeManager.Instance.GetState(runtime.Bot)?.ArchetypeName ??
                                 runtime.CombatState.ActiveArchetype;
                BotRotationManager.Instance.EnsureAttached(runtime, archetype);
            }
            var engine = runtime.Engines[(int)engineKind];
            if (!active)
                brain.StepMinimal();

            if (config.UseEngine && engine != null)
            {
                _host.Metrics.RecordContextCreated();
                var context = new BotContext(
                    runtime.Bot,
                    runtime,
                    runtime.Blackboard,
                    now,
                    config,
                    engineKind,
                    brain,
                    runtime.Mover);
                engine.DoNextAction(context, minimal: !active);
            }
            else if (active)
            {
                brain.Step();
            }

            runtime.Metrics.BrainSteps++;
        }
        catch (Exception e)
        {
            RecordError(runtime, e);
        }
        finally
        {
            var elapsedMs = Stopwatch.GetElapsedTime(brainStart).TotalMilliseconds;
            runtime.Metrics.LastBrainMs = elapsedMs;
            runtime.Metrics.BrainMsEma = runtime.Metrics.BrainMsEma <= 0
                ? elapsedMs
                : runtime.Metrics.BrainMsEma * 0.9 + elapsedMs * 0.1;
            var cadence = active ? BotScheduler.Classify(runtime) : BotCadence.Inactive;
            _host.Metrics.RecordBrainStep(cadence, active);
            runtime.Schedule.NextBrainAt = now + BotScheduler.NextDelay(cadence, runtime.Bot.Id, _host.Roll());
            runtime.Schedule.LastActive = active;
        }
    }

    private void RecordError(BotRuntime runtime, Exception error)
    {
        runtime.CombatState.Diagnostics.RecordError(error);
        runtime.Metrics.Errors++;
        _host.Metrics.IncrementTickErrors();
        Logger.Error(error, $"BOT id={runtime.Bot.Id} ev=host_tick_error");
    }
}
