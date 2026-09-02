using System.Numerics;
using AAEmu.Game.Bots.Blackboard;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Quests.Templates;
using AAEmu.Game.Models.Tasks.Bots;
using NLog;

namespace AAEmu.Game.Bots.Questing;

public enum BotQuestIntakeState
{
    Disabled,
    Idle,
    Moving,
    Interacting,
    Backoff,
    Blocked
}

public readonly record struct BotQuestIntakeView(
    BotQuestIntakeState State,
    uint? NpcObjectId,
    uint? NpcTemplateId,
    uint? QuestId,
    bool? MainStory,
    string DecisionReason,
    DateTimeOffset? DecisionAt,
    DateTimeOffset? LastAcceptedAt,
    DateTimeOffset? RetryAt,
    long AcceptedCount,
    long RejectedCount);

/// <summary>
/// Owns the bounded, opt-in path from nearby quest discovery to normal AAEmu
/// NPC acceptance. Objective execution and quest reporting deliberately remain
/// outside this controller.
/// </summary>
public sealed class BotQuestIntakeController
{
    internal const float MaximumScanRadius = BotCombatTask.MaximumQuestTargetSearchRadius;
    private const float DestinationTolerance = 0.25f;

    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();

    private readonly object _syncRoot = new();
    private readonly Func<uint, IReadOnlyList<QuestTemplate>> _questStarts;
    private readonly Func<Character, uint, uint, bool> _acceptQuest;
    private readonly Action<Character, Vector3, bool> _setDestination;
    private readonly Action<Character> _stopMovement;
    private readonly Func<Character, Vector3, float> _heightProvider;
    private readonly Action<string> _eventSink;
    private readonly Dictionary<QuestCandidateKey, DateTimeOffset> _retryAfter = [];

    private NpcPlan _plan;
    private Vector3? _ownedDestination;
    private BotQuestIntakeState _state = BotQuestIntakeState.Disabled;
    private string _decisionReason = "not_started";
    private DateTimeOffset? _decisionAt;
    private DateTimeOffset? _lastAcceptedAt;
    private DateTimeOffset? _retryAt;
    private long _acceptedCount;
    private long _rejectedCount;

    public BotQuestIntakeController()
        : this(null, null, null, null, null, null)
    {
    }

    internal BotQuestIntakeController(
        Func<uint, IReadOnlyList<QuestTemplate>> questStarts,
        Func<Character, uint, uint, bool> acceptQuest,
        Action<Character, Vector3, bool> setDestination,
        Action<Character> stopMovement,
        Func<Character, Vector3, float> heightProvider,
        Action<string> eventSink)
    {
        _questStarts = questStarts ??
            (npcTemplateId => QuestManager.Instance.GetPlayerBotNpcQuestStarts(npcTemplateId));
        _acceptQuest = acceptQuest ??
            ((bot, questId, npcObjectId) => bot.Quests.AddQuestFromNpc(questId, npcObjectId));
        _setDestination = setDestination ??
            ((bot, destination, run) => BotManager.Instance.SetBotDestination(
                bot, destination.X, destination.Y, destination.Z, run));
        _stopMovement = stopMovement ?? (bot => BotManager.Instance.StopBot(bot));
        _heightProvider = heightProvider ??
            ((bot, position) => bot.ParentWorld.GetHeight(position.X, position.Y));
        _eventSink = eventSink;
    }

    /// <summary>
    /// Returns true while quest intake owns this tick. The host uses that result
    /// to prevent the one-kill grind lifecycle from activating concurrently.
    /// </summary>
    internal bool Step(BotRuntime runtime, BotConfig config, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(config);

        lock (_syncRoot)
        {
            if (!config.QuestIntakeEnabled)
            {
                Disable(runtime, now);
                return false;
            }

            RemoveExpiredBackoffs(now);
            if (!CanOwnTick(runtime, out var blockedReason))
            {
                InvalidatePlan(runtime, blockedReason, now, stopOwnedMovement: true);
                SetState(BotQuestIntakeState.Blocked, blockedReason, now);
                return false;
            }

            if (_plan != null)
                return StepPlan(runtime, config, now);

            var candidates = FindCandidates(runtime, config, now);
            if (candidates.Count == 0)
            {
                _retryAt = EarliestRetry();
                if (_retryAt.HasValue)
                {
                    if (SetState(BotQuestIntakeState.Backoff, "candidate_retry_backoff", now))
                        Log(runtime.Bot.Id, "backoff", $"retry_at={Timestamp(_retryAt.Value)}");
                    return true;
                }

                if (SetState(BotQuestIntakeState.Idle, "no_eligible_nearby_quest", now))
                    Log(runtime.Bot.Id, "idle", "reason=no_eligible_nearby_quest");
                return false;
            }

            var selected = candidates[0];
            var quests = candidates
                .Where(candidate => candidate.Npc.ObjId == selected.Npc.ObjId)
                .OrderBy(candidate => candidate.MainStory ? 0 : 1)
                .ThenBy(candidate => candidate.Quest.Id)
                .Select(candidate => candidate.Quest)
                .ToArray();
            _plan = new NpcPlan(
                selected.Npc.ObjId,
                selected.Npc.TemplateId,
                quests);
            _retryAt = null;
            SetState(BotQuestIntakeState.Interacting, "candidate_selected", now);
            Log(runtime.Bot.Id, "selected",
                $"npc_obj={selected.Npc.ObjId} npc_template={selected.Npc.TemplateId} " +
                $"quest={selected.Quest.Id} main_story={selected.MainStory.ToString().ToLowerInvariant()} " +
                $"distance={selected.Distance:F2} planned={quests.Length}");
            return StepPlan(runtime, config, now);
        }
    }

    public BotQuestIntakeView Inspect()
    {
        lock (_syncRoot)
        {
            var quest = _plan?.Quests.FirstOrDefault();
            return new BotQuestIntakeView(
                _state,
                _plan?.NpcObjectId,
                _plan?.NpcTemplateId,
                quest?.Id,
                quest == null ? null : IsMainStory(quest),
                _decisionReason,
                _decisionAt,
                _lastAcceptedAt,
                _retryAt,
                _acceptedCount,
                _rejectedCount);
        }
    }

    internal static bool IsMainStory(QuestTemplate quest) =>
        quest != null && (quest.ChapterIdx != 0 || quest.QuestIdx != 0);

    private bool StepPlan(BotRuntime runtime, BotConfig config, DateTimeOffset now)
    {
        var bot = runtime.Bot;
        var npc = bot.ParentWorld?.GetNpc(_plan.NpcObjectId);
        if (!TryValidateNpc(bot, npc, EffectiveScanRadius(config), out var distance, out var reason))
        {
            InvalidatePlan(runtime, reason, now, stopOwnedMovement: true);
            return true;
        }

        var targetPosition = npc.Transform.World.Position;
        if (distance > config.QuestIntakeInteractionRadius)
        {
            if (runtime.MovementState.Destination is { } currentDestination &&
                (!_ownedDestination.HasValue ||
                 Vector3.Distance(currentDestination, _ownedDestination.Value) > DestinationTolerance))
            {
                InvalidatePlan(runtime, "movement_ownership_lost", now, stopOwnedMovement: false);
                return false;
            }

            if (!_ownedDestination.HasValue ||
                runtime.MovementState.Destination == null ||
                Vector3.Distance(_ownedDestination.Value, targetPosition) > DestinationTolerance)
            {
                _setDestination(bot, targetPosition, true);
                _ownedDestination = targetPosition;
                SetState(BotQuestIntakeState.Moving, "moving_to_quest_giver", now);
                Log(bot.Id, "move_requested",
                    $"npc_obj={npc.ObjId} npc_template={npc.TemplateId} distance={distance:F2} " +
                    $"destination=({targetPosition.X:R},{targetPosition.Y:R},{targetPosition.Z:R})");
            }

            return true;
        }

        StopOwnedMovement(runtime);
        SetState(BotQuestIntakeState.Interacting, "within_interaction_radius", now);
        var plannedQuests = _plan.Quests;
        _plan = null;
        foreach (var quest in plannedQuests)
        {
            var key = new QuestCandidateKey(npc.ObjId, quest.Id);
            if (!IsEligible(bot, quest) || IsInBackoff(key, now))
                continue;

            var accepted = false;
            try
            {
                accepted = _acceptQuest(bot, quest.Id, npc.ObjId);
            }
            catch (Exception exception)
            {
                Log(bot.Id, "accept_error",
                    $"npc_obj={npc.ObjId} quest={quest.Id} error={exception.GetType().Name}");
            }
            finally
            {
                if (ReferenceEquals(bot.CurrentTarget, npc))
                    bot.CurrentTarget = null;
            }

            if (accepted && bot.Quests.HasQuest(quest.Id))
            {
                _acceptedCount++;
                _lastAcceptedAt = now;
                _retryAfter.Remove(key);
                Log(bot.Id, "accepted",
                    $"npc_obj={npc.ObjId} npc_template={npc.TemplateId} quest={quest.Id} " +
                    $"main_story={IsMainStory(quest).ToString().ToLowerInvariant()}");
                continue;
            }

            _rejectedCount++;
            var retryAt = now + TimeSpan.FromMilliseconds(config.QuestIntakeRetryBackoffMs);
            _retryAfter[key] = retryAt;
            Log(bot.Id, "rejected",
                $"npc_obj={npc.ObjId} npc_template={npc.TemplateId} quest={quest.Id} retry_at={Timestamp(retryAt)}");
        }

        _retryAt = EarliestRetry();
        SetState(BotQuestIntakeState.Idle, "npc_intake_complete", now);
        return true;
    }

    private List<QuestCandidate> FindCandidates(BotRuntime runtime, BotConfig config, DateTimeOffset now)
    {
        List<uint> nearbyNpcObjectIds;
        try
        {
            if (!runtime.Blackboard.TryGet(
                    BotValues.NearbyNpcIds,
                    now.UtcDateTime,
                    out nearbyNpcObjectIds) ||
                nearbyNpcObjectIds == null || nearbyNpcObjectIds.Count == 0)
            {
                return [];
            }
        }
        catch (Exception exception)
        {
            Log(runtime.Bot.Id, "scan_error", $"error={exception.GetType().Name}");
            return [];
        }

        var bot = runtime.Bot;
        var radius = EffectiveScanRadius(config);
        var candidates = new List<QuestCandidate>();
        foreach (var npcObjectId in nearbyNpcObjectIds.Distinct())
        {
            var npc = bot.ParentWorld?.GetNpc(npcObjectId);
            if (!TryValidateNpc(bot, npc, radius, out var distance, out _))
                continue;

            IReadOnlyList<QuestTemplate> starts;
            try
            {
                starts = _questStarts(npc.TemplateId) ?? [];
            }
            catch
            {
                continue;
            }

            foreach (var quest in starts)
            {
                var key = new QuestCandidateKey(npc.ObjId, quest?.Id ?? 0);
                if (!IsEligible(bot, quest) || IsInBackoff(key, now))
                    continue;

                candidates.Add(new QuestCandidate(npc, quest, IsMainStory(quest), distance));
            }
        }

        candidates.Sort(static (left, right) =>
        {
            var story = (left.MainStory ? 0 : 1).CompareTo(right.MainStory ? 0 : 1);
            if (story != 0)
                return story;
            var distance = left.Distance.CompareTo(right.Distance);
            if (distance != 0)
                return distance;
            var npc = left.Npc.ObjId.CompareTo(right.Npc.ObjId);
            return npc != 0 ? npc : left.Quest.Id.CompareTo(right.Quest.Id);
        });
        return candidates;
    }

    private bool TryValidateNpc(
        Character bot,
        Npc npc,
        float scanRadius,
        out float distance,
        out string reason)
    {
        distance = float.MaxValue;
        reason = "npc_invalid";
        if (npc == null || npc.IsDead || npc.Hp <= 0 || npc.TemplateId == 0 ||
            bot?.ParentWorld == null || !ReferenceEquals(npc.ParentWorld, bot.ParentWorld) ||
            bot.Transform?.World == null || npc.Transform?.World == null)
        {
            return false;
        }

        var botPosition = bot.Transform.World.Position;
        var npcPosition = npc.Transform.World.Position;
        if (!IsFinite(botPosition) || !IsFinite(npcPosition))
        {
            reason = "nonfinite_transform";
            return false;
        }

        float surfaceZ;
        try
        {
            surfaceZ = _heightProvider(bot, npcPosition);
        }
        catch
        {
            reason = "height_unavailable";
            return false;
        }
        if (!float.IsFinite(surfaceZ))
        {
            reason = "nonfinite_height";
            return false;
        }

        distance = Vector3.Distance(botPosition, npcPosition);
        if (!BotCombatTask.IsWithinNavigableQuestTargetVolume(
                botPosition, npcPosition, surfaceZ, scanRadius))
        {
            reason = "outside_navigable_scan_volume";
            return false;
        }

        reason = "valid";
        return true;
    }

    private bool CanOwnTick(BotRuntime runtime, out string reason)
    {
        var bot = runtime.Bot;
        var combat = runtime.CombatState;
        var movement = runtime.MovementState;
        if (runtime.Retired || bot.IsDead || bot.Hp <= 0 || bot.ParentWorld == null ||
            bot.Transform?.World == null || bot.Quests == null || runtime.Brain == null || runtime.Brain.Cancelled ||
            runtime.Mover == null || runtime.Mover.Cancelled)
        {
            reason = "runtime_not_world_ready";
            return false;
        }

        if (runtime.LifeController.ShouldSuspendRuntime ||
            runtime.LifeController.Inspect().Activity != null)
        {
            reason = "lifecycle_busy";
            return false;
        }

        if (combat.IsForced || combat.IsActive || combat.CurrentState != BotCombatStateType.Idle ||
            combat.Target != null || bot.CurrentTarget != null || combat.InDuel ||
            combat.DuelRequestPending || combat.IsResting || combat.RespawnScheduled ||
            combat.ShouldRespawn || combat.IsSearching || combat.LostTarget != null ||
            combat.LastKnownTargetPosition.HasValue || combat.RoamDestination.HasValue ||
            combat.StopAtTargetHpPercent.HasValue || combat.NonlethalFloorReached != null)
        {
            reason = "combat_not_idle";
            return false;
        }

        if (movement.FollowTarget != null || movement.IsFalling || movement.FallVelocity > 0 ||
            movement.JumpRequested || movement.IsJumping)
        {
            reason = "movement_mode_busy";
            return false;
        }

        if (_plan == null && (movement.Destination.HasValue || movement.IsMoving))
        {
            reason = "unowned_movement";
            return false;
        }

        reason = "eligible";
        return true;
    }

    private static bool IsEligible(Character bot, QuestTemplate quest)
    {
        if (bot == null || quest == null || quest.Id == 0 || bot.Quests.HasQuest(quest.Id))
            return false;

        return !bot.Quests.HasQuestCompleted(quest.Id) || quest.Repeatable;
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static float EffectiveScanRadius(BotConfig config) =>
        (float)Math.Min(
            MaximumScanRadius,
            Math.Min(
                double.IsFinite(config.SearchRadius) ? Math.Max(0d, config.SearchRadius) : 0d,
                double.IsFinite(config.QuestIntakeScanRadius)
                    ? Math.Max(0d, config.QuestIntakeScanRadius)
                    : 0d));

    private bool IsInBackoff(QuestCandidateKey key, DateTimeOffset now) =>
        _retryAfter.TryGetValue(key, out var retryAt) && retryAt > now;

    private void RemoveExpiredBackoffs(DateTimeOffset now)
    {
        if (_retryAfter.Count == 0)
            return;

        foreach (var key in _retryAfter
                     .Where(entry => entry.Value <= now)
                     .Select(entry => entry.Key)
                     .ToArray())
        {
            _retryAfter.Remove(key);
        }
    }

    private DateTimeOffset? EarliestRetry() =>
        _retryAfter.Count == 0 ? null : _retryAfter.Values.Min();

    private void Disable(BotRuntime runtime, DateTimeOffset now)
    {
        StopOwnedMovement(runtime);
        _plan = null;
        _retryAfter.Clear();
        _retryAt = null;
        SetState(BotQuestIntakeState.Disabled, "disabled", now);
    }

    private void InvalidatePlan(
        BotRuntime runtime,
        string reason,
        DateTimeOffset now,
        bool stopOwnedMovement)
    {
        if (_plan == null && !_ownedDestination.HasValue)
            return;

        var npcObjectId = _plan?.NpcObjectId;
        if (stopOwnedMovement)
            StopOwnedMovement(runtime);
        _plan = null;
        _ownedDestination = null;
        SetState(BotQuestIntakeState.Blocked, reason, now);
        Log(runtime.Bot.Id, "invalidated", $"npc_obj={npcObjectId?.ToString() ?? "none"} reason={reason}");
    }

    private void StopOwnedMovement(BotRuntime runtime)
    {
        if (_ownedDestination.HasValue &&
            runtime.MovementState.Destination is { } destination &&
            Vector3.Distance(destination, _ownedDestination.Value) <= DestinationTolerance)
        {
            _stopMovement(runtime.Bot);
        }
        _ownedDestination = null;
    }

    private bool SetState(BotQuestIntakeState state, string reason, DateTimeOffset now)
    {
        if (_state == state && string.Equals(_decisionReason, reason, StringComparison.Ordinal))
            return false;

        _state = state;
        _decisionReason = reason;
        _decisionAt = now;
        return true;
    }

    private void Log(uint botId, string eventName, string detail)
    {
        var message = $"BOT id={botId} ev=quest_intake_{eventName} {detail}";
        Logger.Info(message);
        _eventSink?.Invoke(message);
    }

    private static string Timestamp(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O");

    private readonly record struct QuestCandidateKey(uint NpcObjectId, uint QuestId);
    private sealed record NpcPlan(
        uint NpcObjectId,
        uint NpcTemplateId,
        QuestTemplate[] Quests);
    private readonly record struct QuestCandidate(
        Npc Npc,
        QuestTemplate Quest,
        bool MainStory,
        float Distance);
}
