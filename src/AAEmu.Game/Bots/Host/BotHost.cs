using System.Collections.Concurrent;
using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using NLog;

namespace AAEmu.Game.Bots.Host;

public sealed class BotHost : Singleton<BotHost>, IBotHost
{
    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();

    private readonly ConcurrentDictionary<uint, BotRuntime> _runtimes = new();
    private readonly object _runtimeLock = new();
    private readonly ITaskManager _taskManager;
    private readonly TimeProvider _timeProvider;
    private readonly Func<int> _roll;
    private readonly Func<uint, bool> _logoutBot;
    private readonly ServerTickMetrics _serverMetrics;
    private readonly BotHostTask _hostTask;
    private BotRuntime[] _runtimeSnapshot = [];
    private int _started;

    internal BotHost()
    {
        _taskManager = null;
        _timeProvider = TimeProvider.System;
        _roll = Random.Shared.Next;
        _logoutBot = botId => BotManager.Instance.DespawnBot(botId);
        _serverMetrics = null;
        _hostTask = new BotHostTask(this);
    }

    public BotHost(
        ITaskManager taskManager,
        TimeProvider timeProvider,
        Func<int> roll = null,
        ITickManager tickManager = null,
        Func<uint, bool> logoutBot = null)
    {
        _taskManager = taskManager ?? throw new ArgumentNullException(nameof(taskManager));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _roll = roll ?? Random.Shared.Next;
        _logoutBot = logoutBot ?? (botId => BotManager.Instance.DespawnBot(botId));
        _serverMetrics = tickManager?.Metrics;
        _hostTask = new BotHostTask(this);
    }

    public BotHostMetrics Metrics { get; } = new();
    public int RuntimeCount => _runtimes.Count;
    public TimeProvider TimeProvider => _timeProvider;
    public Func<int> Roll => _roll;
    internal ServerTickMetrics ServerMetrics => _serverMetrics;
    internal BotHostTask HostTask => _hostTask;

    public void Register(BotRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        if (_runtimes.TryGetValue(runtime.Bot.Id, out var staleRuntime))
        {
            if (ReferenceEquals(staleRuntime, runtime))
                return;

            Logger.Warn($"BOT id={runtime.Bot.Id} ev=duplicate_runtime_replace");
            Unregister(runtime.Bot.Id);
        }

        // Runtime lock order is SyncRoot-free inside _runtimeLock: a bot step holds runtime.SyncRoot and may
        // re-enter Unregister (which takes _runtimeLock), so never take SyncRoot while holding _runtimeLock.
        lock (runtime.SyncRoot)
        {
            runtime.Retired = false;
            runtime.LifeController.ResetPostSpawn(runtime.Bot.Id, _timeProvider.GetUtcNow());
            runtime.HostMetrics = Metrics;
            if (runtime.Brain != null)
                runtime.Brain.HostMetrics = Metrics;
            runtime.KillCreditSubscription.Metrics = Metrics;
        }

        lock (_runtimeLock)
        {
            if (!_runtimes.TryAdd(runtime.Bot.Id, runtime))
            {
                Logger.Warn($"BOT id={runtime.Bot.Id} ev=duplicate_runtime_race");
                Start();
                return;
            }

            runtime.KillCreditSubscription.Subscribe();
            PublishRuntimeSnapshot();
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        runtime.Schedule.Now = now;
        if (runtime.Schedule.NextBrainAt == default)
            runtime.Schedule.NextBrainAt = now + BotScheduler.InitialStagger(runtime.Bot.Id);

        Start();
    }

    public void Unregister(uint botId)
    {
        BotRuntime runtime;
        lock (_runtimeLock)
        {
            if (!_runtimes.TryRemove(botId, out runtime))
                return;

            PublishRuntimeSnapshot();
        }

        Retire(runtime);
    }

    public void Unregister(BotRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        lock (_runtimeLock)
        {
            if (!_runtimes.TryGetValue(runtime.Bot.Id, out var current) || !ReferenceEquals(current, runtime))
                return;

            _runtimes.TryRemove(runtime.Bot.Id, out _);
            PublishRuntimeSnapshot();
        }

        Retire(runtime);
    }

    public void Start()
    {
        if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
            return;

        _hostTask.Cancelled = false;
        _hostTask.InitializeStart(_timeProvider.GetUtcNow().UtcDateTime);
        var scheduler = _taskManager ?? TaskManager.Instance;
        if (!scheduler.Schedule(_hostTask, TimeSpan.Zero, TimeSpan.FromMilliseconds(100), -1))
            Interlocked.Exchange(ref _started, 0);
    }

    public void Stop()
    {
        if (Interlocked.Exchange(ref _started, 0) == 0)
            return;

        var scheduler = _taskManager ?? TaskManager.Instance;
        scheduler.Cancel(_hostTask);
        _hostTask.Cancelled = true;
        Metrics.RecordPopulation(0, 0);
    }

    public BotRuntime GetRuntime(uint botId)
    {
        _runtimes.TryGetValue(botId, out var runtime);
        return runtime;
    }

    internal BotRuntime[] GetRuntimeSnapshot()
    {
        return Volatile.Read(ref _runtimeSnapshot);
    }

    private void PublishRuntimeSnapshot()
    {
        var snapshot = new BotRuntime[_runtimes.Count];
        _runtimes.Values.CopyTo(snapshot, 0);
        Volatile.Write(ref _runtimeSnapshot, snapshot);
    }

    internal bool IsStarted => Volatile.Read(ref _started) != 0;

    internal bool LogoutBot(uint botId) => _logoutBot(botId);

    private void Retire(BotRuntime runtime)
    {
        lock (runtime.SyncRoot)
        {
            runtime.Retired = true;
            runtime.KillCreditSubscription.Unsubscribe();
            runtime.TeamHooks.Dispose();
            runtime.Mover?.OnCancel();
            runtime.Brain?.OnCancel();
            if (runtime.Mover != null)
                runtime.Mover.Cancelled = true;
            if (runtime.Brain != null)
            {
                runtime.Brain.HostMetrics = null;
                runtime.Brain.Cancelled = true;
            }
        }

        if (_runtimes.IsEmpty)
            Stop();
    }

    internal void LogMetrics()
    {
        var bot = Metrics.Snapshot();
        var server = _serverMetrics?.Snapshot();
        Logger.Info(
            $"BOT host ev=metrics bots={bot.Bots} active={bot.ActiveBots} configured_pct={bot.ConfiguredActivityPercent} governor_pct={bot.GovernorEffectivePercent} observed_pct={bot.EffectiveActivityPercent:F2} " +
            $"host_tick_p50={bot.HostTick.P50Ms:F2} host_tick_p95={bot.HostTick.P95Ms:F2} host_tick_p99={bot.HostTick.P99Ms:F2} host_tick_max={bot.HostTick.MaxMs:F2} host_tick_ema={bot.HostTickMsEma:F2} " +
            $"server_tick_p50={server?.Work.P50Ms ?? 0:F2} server_tick_p95={server?.Work.P95Ms ?? 0:F2} server_tick_p99={server?.Work.P99Ms ?? 0:F2} server_tick_max={server?.Work.MaxMs ?? 0:F2} server_pressure={server?.PressureMs ?? 0:F2} " +
            $"skipped={bot.SkippedTicks} runtime_overlap={bot.RuntimeOverlaps} brain_steps={bot.BrainSteps} mover_steps={bot.MoverSteps} scans={bot.WorldScans} path_requests={bot.PathRequests} " +
            $"decisions={bot.DecisionSteps} invalid_targets={bot.InvalidTargets} casts={bot.CastSuccesses}/{bot.CastAttempts} observed_kills={bot.ObservedKills} credited_kills={bot.CreditedKills} stuck={bot.StuckNudges + bot.StuckTeleports} " +
            $"allocated_bytes={bot.AllocatedBytes} gc={bot.Gen0Collections}/{bot.Gen1Collections}/{bot.Gen2Collections} working_set={bot.WorkingSetBytes} cpu_pct={bot.CpuPercent:F2} errors={bot.TickErrors}");
    }
}
