using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using NLog;

namespace AAEmu.Game.Bots.Ops;

public sealed record BotActivityDirectorSnapshot(
    bool Enabled,
    bool Valid,
    string Reason,
    uint ZoneId,
    int MinimumPopulation,
    int TargetPopulation,
    int MaximumPopulation,
    int EligibleIdentities,
    int LiveQualified,
    int LiveWrongZone,
    int InFlight,
    int Cooldown,
    long TickCount,
    long AttemptCount,
    long SuccessCount,
    long FailureCount,
    long RefillCount,
    uint? LastIdentity,
    string LastResult,
    string LastReason,
    DateTimeOffset? StartedAt,
    DateTimeOffset? StoppedAt,
    DateTimeOffset? LastTickAt)
{
    public static BotActivityDirectorSnapshot NotStarted { get; } = new(
        false,
        true,
        "not_started",
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        null,
        "not_started",
        "not_started",
        null,
        null,
        null);
}

/// <summary>Keeps a configured set of existing characters active in one zone.</summary>
public sealed class BotActivityDirectorTask : AAEmu.Game.Models.Tasks.Task
{
    private enum BoundaryResult
    {
        Qualified,
        WrongZone,
        WrongWorld
    }

    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static BotActivityDirectorTask s_current;

    private readonly object _executionGate = new();
    private readonly object _syncRoot = new();
    private readonly BotConfig _config;
    private readonly IBotManager _botManager;
    private readonly TimeProvider _timeProvider;
    private readonly Action<string> _log;
    private readonly HashSet<uint> _inFlight = [];
    private readonly Dictionary<uint, DateTimeOffset> _cooldownUntil = [];
    private readonly HashSet<uint> _everQualified = [];
    private HashSet<uint> _eligibleIds = [];
    private BotActivityDirectorConfiguration _configuration;
    private BotActivityDirectorSnapshot _snapshot;
    private int _started;
    private int _running;
    private long _tickCount;
    private long _attemptCount;
    private long _successCount;
    private long _failureCount;
    private long _refillCount;
    private int _liveQualified;
    private int _liveWrongZone;
    private uint? _lastIdentity;
    private string _lastResult;
    private string _lastReason;
    private DateTimeOffset? _startedAt;
    private DateTimeOffset? _stoppedAt;
    private DateTimeOffset? _lastTickAt;

    public BotActivityDirectorTask(
        BotConfig config,
        IBotManager botManager,
        TimeProvider timeProvider,
        Action<string> log = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _botManager = botManager ?? throw new ArgumentNullException(nameof(botManager));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _log = log ?? (message => Logger.Info(message));
        _configuration = _config.GetActivityDirectorConfiguration();
        _eligibleIds = _configuration.CharacterIds.ToHashSet();
        _lastResult = _configuration.Reason;
        _lastReason = _configuration.Reason;
        _snapshot = BuildSnapshot();
        Volatile.Write(ref s_current, this);
    }

    public static BotActivityDirectorSnapshot CurrentSnapshot =>
        Volatile.Read(ref s_current)?.Snapshot ?? BotActivityDirectorSnapshot.NotStarted;

    public BotActivityDirectorSnapshot Snapshot => Volatile.Read(ref _snapshot);
    public TimeSpan InitialDelay => _configuration.InitialDelay;
    public TimeSpan ReconciliationInterval => _configuration.ReconciliationInterval;

    public bool TryStart()
    {
        lock (_syncRoot)
        {
            RefreshConfiguration();
            if (!_configuration.Enabled || !_configuration.Valid)
            {
                _lastResult = "start_rejected";
                _lastReason = _configuration.Reason;
                PublishSnapshot();
                Log("start");
                return false;
            }

            if (Interlocked.CompareExchange(ref _started, 1, 0) != 0)
                return false;

            Cancelled = false;
            _startedAt = _timeProvider.GetUtcNow();
            _stoppedAt = null;
            _lastResult = "started";
            _lastReason = "valid";
            PublishSnapshot();
            Log("start");
            return true;
        }
    }

    public void Stop()
    {
        var wasStarted = Interlocked.Exchange(ref _started, 0) != 0;
        Cancelled = true;

        // Finish an active spawn decision before normal shutdown cleanup.
        lock (_executionGate)
        {
        }

        lock (_syncRoot)
        {
            if (!wasStarted && _stoppedAt.HasValue)
                return;

            _stoppedAt = _timeProvider.GetUtcNow();
            _lastResult = wasStarted ? "stopped" : "stop_noop";
            _lastReason = wasStarted ? "graceful_shutdown" : _configuration.Reason;
            PublishSnapshot();
            Log("stop");
        }
    }

    public override void Execute()
    {
        if (Cancelled || Volatile.Read(ref _started) == 0)
            return;

        if (Interlocked.CompareExchange(ref _running, 1, 0) != 0)
        {
            lock (_syncRoot)
            {
                _lastResult = "tick_skipped";
                _lastReason = "overlap";
                PublishSnapshot();
                Log("tick");
            }
            return;
        }

        try
        {
            lock (_executionGate)
            {
                if (Cancelled || Volatile.Read(ref _started) == 0)
                    return;

                ExecuteSerializedTick();
            }
        }
        finally
        {
            Volatile.Write(ref _running, 0);
        }
    }

    internal static bool IsCurrentLifecycleEligible(Character bot)
    {
        var current = Volatile.Read(ref s_current);
        if (bot == null || current == null || Volatile.Read(ref current._started) == 0 || current.Cancelled)
            return false;

        lock (current._syncRoot)
        {
            return current._configuration.Enabled && current._configuration.Valid &&
                   current._eligibleIds.Contains(bot.Id) &&
                   current.Classify(bot) == BoundaryResult.Qualified;
        }
    }

    private void ExecuteSerializedTick()
    {
        var now = _timeProvider.GetUtcNow();
        Character[] liveBots;
        try
        {
            liveBots = _botManager.GetAllBots()?.Where(bot => bot != null).ToArray() ?? [];
        }
        catch (Exception exception)
        {
            Logger.Error(exception, "BOT director ev=tick_error reason=live_snapshot_exception");
            lock (_syncRoot)
            {
                _tickCount++;
                _lastTickAt = now;
                _failureCount++;
                _lastResult = "tick_failed";
                _lastReason = "live_snapshot_exception";
                PublishSnapshot();
                Log("tick");
            }
            return;
        }

        uint selectedIdentity;
        lock (_syncRoot)
        {
            RefreshConfiguration();
            _tickCount++;
            _lastTickAt = now;
            PruneCooldown(now);
            CountPopulation(liveBots);

            if (!_configuration.Enabled || !_configuration.Valid)
            {
                _lastResult = "tick_rejected";
                _lastReason = _configuration.Reason;
                PublishSnapshot();
                Log("tick");
                return;
            }

            var totalLive = _liveQualified + _liveWrongZone;
            if (totalLive > _configuration.MaximumPopulation)
            {
                _lastResult = "over_capacity";
                _lastReason = "maximum_lowered_below_live_population";
                PublishSnapshot();
                Log("tick");
                return;
            }

            if (_liveQualified >= _configuration.TargetPopulation)
            {
                _lastResult = "steady";
                _lastReason = "target_satisfied";
                PublishSnapshot();
                Log("tick");
                return;
            }

            if (totalLive + _inFlight.Count >= _configuration.MaximumPopulation)
            {
                _lastResult = "at_capacity";
                _lastReason = "maximum_reached";
                PublishSnapshot();
                Log("tick");
                return;
            }

            var liveIds = liveBots.Select(bot => bot.Id).ToHashSet();
            selectedIdentity = SelectIdentity(liveIds, now);
            if (selectedIdentity == 0)
            {
                _lastIdentity = null;
                _lastResult = "no_attempt";
                _lastReason = _cooldownUntil.Count > 0 ? "eligible_identities_cooling_down" : "no_eligible_identity";
                PublishSnapshot();
                Log("tick");
                return;
            }

            _inFlight.Add(selectedIdentity);
            _attemptCount++;
            _lastIdentity = selectedIdentity;
            _lastResult = "attempting";
            _lastReason = "below_target";
            PublishSnapshot();
        }

        AttemptSpawn(selectedIdentity, now);
    }

    private void AttemptSpawn(uint characterId, DateTimeOffset now)
    {
        SpawnResult result;
        Character bot;
        try
        {
            result = _botManager.SpawnBot(characterId, out bot);
        }
        catch (Exception exception)
        {
            Logger.Error(exception, $"BOT director ev=spawn id={characterId} result=Exception");
            CompleteFailure(characterId, now, "spawn_exception");
            return;
        }

        if (result != SpawnResult.Ok || bot == null)
        {
            CompleteFailure(characterId, now, bot == null && result == SpawnResult.Ok
                ? "spawn_ok_without_bot"
                : $"spawn_{result.ToString().ToLowerInvariant()}");
            return;
        }

        var boundary = Classify(bot);
        if (boundary != BoundaryResult.Qualified)
        {
            var cleanupSucceeded = false;
            try
            {
                cleanupSucceeded = _botManager.DespawnBot(characterId);
            }
            catch (Exception exception)
            {
                Logger.Error(exception, $"BOT director ev=boundary_cleanup id={characterId} result=Exception");
            }

            CompleteFailure(
                characterId,
                now,
                $"spawn_{boundary.ToString().ToLowerInvariant()}_cleanup_{(cleanupSucceeded ? "succeeded" : "failed")}");
            return;
        }

        lock (_syncRoot)
        {
            _inFlight.Remove(characterId);
            _cooldownUntil.Remove(characterId);
            var refill = !_everQualified.Add(characterId);
            _successCount++;
            if (refill)
                _refillCount++;
            _liveQualified++;
            _lastIdentity = characterId;
            _lastResult = refill ? "refill_succeeded" : "spawn_succeeded";
            _lastReason = "qualified_default_world_zone";
            PublishSnapshot();
            Log("spawn");
        }
    }

    private void CompleteFailure(uint characterId, DateTimeOffset now, string reason)
    {
        lock (_syncRoot)
        {
            _inFlight.Remove(characterId);
            _cooldownUntil[characterId] = now + _configuration.RetryBackoff;
            _failureCount++;
            _lastIdentity = characterId;
            _lastResult = "spawn_failed";
            _lastReason = reason;
            PublishSnapshot();
            Log("spawn");
        }
    }

    private uint SelectIdentity(IReadOnlySet<uint> liveIds, DateTimeOffset now)
    {
        for (var index = 0; index < _configuration.CharacterIds.Length; index++)
        {
            var characterId = _configuration.CharacterIds[index];
            if (liveIds.Contains(characterId) || _inFlight.Contains(characterId))
                continue;
            if (_cooldownUntil.TryGetValue(characterId, out var retryAt) && retryAt > now)
                continue;
            return characterId;
        }

        return 0;
    }

    private void CountPopulation(IEnumerable<Character> liveBots)
    {
        _liveQualified = 0;
        _liveWrongZone = 0;
        foreach (var bot in liveBots)
        {
            if (!_eligibleIds.Contains(bot.Id))
                continue;

            if (Classify(bot) == BoundaryResult.Qualified)
            {
                _liveQualified++;
                _everQualified.Add(bot.Id);
            }
            else
            {
                _liveWrongZone++;
            }
        }
    }

    private BoundaryResult Classify(Character bot)
    {
        var transform = bot?.Transform;
        var parentWorld = bot?.ParentWorld;
#if PLAYERBOTS_AAEMU_3_0
        var worldTemplateId = parentWorld?.TemplateId ?? 0;
#else
        var worldTemplateId = parentWorld?.Template?.Id ?? 0;
#endif
        if (transform == null || parentWorld == null ||
            transform.World == null || transform.InstanceId != WorldManager.DefaultInstanceId ||
            parentWorld.Id != WorldManager.DefaultInstanceId || transform.WorldId != worldTemplateId)
        {
            return BoundaryResult.WrongWorld;
        }

        return transform.ZoneId == _configuration.ZoneId
            ? BoundaryResult.Qualified
            : BoundaryResult.WrongZone;
    }

    private void RefreshConfiguration()
    {
        _configuration = _config.GetActivityDirectorConfiguration();
        _eligibleIds = _configuration.CharacterIds.ToHashSet();
        var retainedIds = _eligibleIds;
        foreach (var characterId in _cooldownUntil.Keys.Where(id => !retainedIds.Contains(id)).ToArray())
            _cooldownUntil.Remove(characterId);
    }

    private void PruneCooldown(DateTimeOffset now)
    {
        foreach (var characterId in _cooldownUntil.Where(entry => entry.Value <= now).Select(entry => entry.Key).ToArray())
            _cooldownUntil.Remove(characterId);
    }

    private void PublishSnapshot()
    {
        Volatile.Write(ref _snapshot, BuildSnapshot());
    }

    private BotActivityDirectorSnapshot BuildSnapshot() => new(
        _configuration.Enabled,
        _configuration.Valid,
        _configuration.Reason,
        _configuration.ZoneId,
        _configuration.MinimumPopulation,
        _configuration.TargetPopulation,
        _configuration.MaximumPopulation,
        _configuration.CharacterIds.Length,
        _liveQualified,
        _liveWrongZone,
        _inFlight.Count,
        _cooldownUntil.Count,
        _tickCount,
        _attemptCount,
        _successCount,
        _failureCount,
        _refillCount,
        _lastIdentity,
        _lastResult,
        _lastReason,
        _startedAt,
        _stoppedAt,
        _lastTickAt);

    private void Log(string eventName)
    {
        var snapshot = _snapshot;
        _log(
            $"BOT director ev={eventName} enabled={Lower(snapshot.Enabled)} valid={Lower(snapshot.Valid)} " +
            $"zone={snapshot.ZoneId} min={snapshot.MinimumPopulation} target={snapshot.TargetPopulation} max={snapshot.MaximumPopulation} " +
            $"eligible={snapshot.EligibleIdentities} live_qualified={snapshot.LiveQualified} live_wrong_zone={snapshot.LiveWrongZone} " +
            $"in_flight={snapshot.InFlight} cooldown={snapshot.Cooldown} attempts={snapshot.AttemptCount} successes={snapshot.SuccessCount} " +
            $"failures={snapshot.FailureCount} refills={snapshot.RefillCount} last_id={snapshot.LastIdentity?.ToString() ?? "none"} " +
            $"last_result={snapshot.LastResult} reason={snapshot.LastReason} started_at={Timestamp(snapshot.StartedAt)} " +
            $"stopped_at={Timestamp(snapshot.StoppedAt)} tick_at={Timestamp(snapshot.LastTickAt)}");
    }

    private static string Lower(bool value) => value.ToString().ToLowerInvariant();
    private static string Timestamp(DateTimeOffset? value) => value?.ToUniversalTime().ToString("O") ?? "none";
}
