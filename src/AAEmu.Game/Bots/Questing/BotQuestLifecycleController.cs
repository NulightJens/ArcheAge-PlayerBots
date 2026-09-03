using System.Numerics;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Tasks.Bots;
using NLog;

namespace AAEmu.Game.Bots.Questing;

public enum BotQuestLifecycleState
{
    Disabled,
    Idle,
    SelectingTarget,
    MovingToObjective,
    Fighting,
    MovingToLoot,
    WaitingForProgress,
    WaitingForRespawn,
    WaitingForReady,
    MovingToReport,
    Reporting,
    WaitingForCompletion,
    Suspended
}

public readonly record struct BotQuestLifecycleView(
    BotQuestLifecycleState State,
    uint? QuestId,
    uint? ObjectiveTargetTemplateId,
    uint? ObjectiveTargetObjectId,
    uint? ObjectiveItemId,
    int? ObjectiveCurrent,
    int? ObjectiveRequired,
    BotQuestReportKind? ReportKind,
    uint? ReportTemplateId,
    uint? ReportObjectId,
    int? RewardIndex,
    string DecisionReason,
    DateTimeOffset? DecisionAt,
    DateTimeOffset? ProgressObservedAt,
    DateTimeOffset? ReportAttemptedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? RetryAt,
    long CompletedCount,
    long SuspensionCount,
    long ReportAttemptCount);

/// <summary>
/// Executes authoritative, single-objective monster-hunt and item-gather quests. It owns
/// an exact combat filter/target, native corpse-loot interaction, or an exact report destination, observes
/// AAEmu quest state for progress, and delegates reporting to guarded native
/// quest APIs.
/// </summary>
public sealed class BotQuestLifecycleController
{
    internal const float MaximumWorldScanRadius = BotCombatTask.MaximumQuestTargetSearchRadius;
    // Starter and later quest chains can legitimately hand off beyond a local
    // 500 m scan (Nuian quest 2532 reports roughly 646 m away). Static report
    // destinations are authoritative, world-scoped spawns, so allow a regional
    // route while still rejecting accidental cross-world traversal.
    internal const float MaximumReportRouteDistance = 5000f;
    internal const float LocalQuestVicinityRadius = 500f;
    private const float DestinationTolerance = 0.25f;
    private const float ConservativeTravelSpeed = 2f;
    private const double MaximumTravelTimeoutSeconds = 1800d;

    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();

    private readonly object _syncRoot = new();
    private readonly IBotQuestAuthority _authority;
    private readonly Action<BotRuntime, Npc, uint> _beginCombat;
    private readonly Action<BotRuntime, uint, uint?> _endCombat;
    private readonly Action<Character, Vector3, bool> _setDestination;
    private readonly Action<Character> _stopMovement;
    private readonly Action<string> _eventSink;

    private volatile BotQuestLifecycleState _state = BotQuestLifecycleState.Disabled;
    private uint? _questId;
    private uint? _objectiveTargetTemplateId;
    private uint? _objectiveItemId;
    private byte? _objectiveIndex;
    private uint? _objectiveTargetObjectId;
    private Npc _objectiveTarget;
    private int? _objectiveCurrent;
    private int? _objectiveRequired;
    private BotQuestReportEndpoint? _reportEndpoint;
    private uint? _reportObjectId;
    private BotQuestStaticObjectiveDestination? _staticObjectiveDestination;
    private Vector3? _staticReportDestination;
    private int? _rewardIndex;
    private Vector3? _ownedDestination;
    private DateTimeOffset? _decisionAt;
    private DateTimeOffset? _progressObservedAt;
    private DateTimeOffset? _reportAttemptedAt;
    private DateTimeOffset? _completedAt;
    private DateTimeOffset? _retryAt;
    private DateTimeOffset? _selectionDeadline;
    private DateTimeOffset? _respawnWaitStartedAt;
    private DateTimeOffset? _respawnRescanAt;
    private DateTimeOffset? _progressObservationUntil;
    private DateTimeOffset? _lootApproachDeadline;
    private DateTimeOffset? _completionObservationUntil;
    private string _decisionReason = "not_started";
    private long _completedCount;
    private long _suspensionCount;
    private long _reportAttemptCount;

    public BotQuestLifecycleController()
        : this(new BotQuestAuthority(), null, null, null, null, null)
    {
    }

    internal BotQuestLifecycleController(
        IBotQuestAuthority authority,
        Action<BotRuntime, Npc, uint> beginCombat = null,
        Action<BotRuntime, uint, uint?> endCombat = null,
        Action<Character, Vector3, bool> setDestination = null,
        Action<Character> stopMovement = null,
        Action<string> eventSink = null)
    {
        _authority = authority ?? throw new ArgumentNullException(nameof(authority));
        _beginCombat = beginCombat ?? BeginProductionCombat;
        _endCombat = endCombat ?? EndProductionCombat;
        _setDestination = setDestination ??
            ((bot, destination, run) => BotManager.Instance.SetBotTravelDestination(bot, destination, run));
        _stopMovement = stopMovement ?? (bot => BotManager.Instance.StopBot(bot));
        _eventSink = eventSink;
    }

    /// <summary>
    /// Returns true while a supported active quest owns the host tick. A
    /// suspended quest yields during its bounded retry backoff so intake can
    /// pursue another eligible quest instead of being starved indefinitely.
    /// </summary>
    internal bool Step(BotRuntime runtime, BotConfig config, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(config);

        if (!config.QuestCompletionEnabled && _state == BotQuestLifecycleState.Disabled)
            return false;

        lock (_syncRoot)
        {
            if (!config.QuestCompletionEnabled)
            {
                Disable(runtime, now);
                return false;
            }

            if (!IsWorldReady(runtime, out var unavailableReason))
            {
                Suspend(runtime, config, unavailableReason, now);
                return false;
            }

            IReadOnlyList<BotQuestSnapshot> snapshots;
            try
            {
                snapshots = _authority.ReadActiveQuests(runtime.Bot) ?? [];
            }
            catch (Exception exception)
            {
                Suspend(runtime, config, $"quest_read_{exception.GetType().Name}", now);
                return true;
            }

            if (_questId.HasValue && snapshots.All(snapshot => snapshot.QuestId != _questId.Value))
            {
                if (_state == BotQuestLifecycleState.WaitingForCompletion ||
                    runtime.Bot.Quests.HasQuestCompleted(_questId.Value))
                    Complete(runtime, now);
                else
                    ReleaseRemovedQuest(runtime, now);
                return false;
            }

            if (_retryAt.HasValue)
            {
                if (_retryAt.Value > now)
                    return false;

                _retryAt = null;
                _selectionDeadline = now + TimeSpan.FromMilliseconds(config.QuestTargetSelectionTimeoutMs);
                SetState(BotQuestLifecycleState.SelectingTarget, "retry_backoff_elapsed", now);
            }

            var snapshot = SelectSnapshot(runtime, snapshots, config, now);
            if (snapshot == null)
            {
                ResetPlan(runtime, releaseCombat: true, stopMovement: true);
                SetState(BotQuestLifecycleState.Idle, "no_active_quest", now);
                return false;
            }

            // The intake controller may already own a route selected on the
            // previous tick. Release only that owned route before the active
            // quest claims combat or report travel; unrelated movement remains
            // protected by the normal ownership checks below.
            var releasedIntake = runtime.QuestIntakeController.YieldToQuestLifecycle(runtime, now);

            if (!_questId.HasValue || _questId.Value != snapshot.QuestId)
                BeginQuest(runtime, snapshot, config, now);

            // StopBot completes on the mover boundary. Hold priority for one brain
            // tick after cancelling an intake route so transient movement/fall flags
            // cannot suspend the active quest and reopen intake during its backoff.
            if (releasedIntake)
            {
                SetState(BotQuestLifecycleState.SelectingTarget, "intake_movement_released", now);
                return true;
            }

            if (snapshot.Ready)
                return StepReport(runtime, snapshot, config, now);

            return StepObjective(runtime, snapshot, config, now);
        }
    }

    public BotQuestLifecycleView Inspect()
    {
        lock (_syncRoot)
        {
            return new BotQuestLifecycleView(
                _state,
                _questId,
                _objectiveTargetTemplateId,
                _objectiveTargetObjectId,
                _objectiveItemId,
                _objectiveCurrent,
                _objectiveRequired,
                _reportEndpoint?.Kind,
                _reportEndpoint?.TemplateId,
                _reportObjectId,
                _rewardIndex,
                _decisionReason,
                _decisionAt,
                _progressObservedAt,
                _reportAttemptedAt,
                _completedAt,
                _retryAt,
                _completedCount,
                _suspensionCount,
                _reportAttemptCount);
        }
    }

    private bool StepObjective(
        BotRuntime runtime,
        BotQuestSnapshot snapshot,
        BotConfig config,
        DateTimeOffset now)
    {
        if (snapshot.ObjectiveShape == BotQuestObjectiveShape.MonsterHunt && snapshot.MonsterHunt.HasValue)
            return StepMonsterHunt(runtime, snapshot, snapshot.MonsterHunt.Value, config, now);
        if (snapshot.ObjectiveShape == BotQuestObjectiveShape.ItemGather && snapshot.ItemGather.HasValue)
            return StepItemGather(runtime, snapshot, snapshot.ItemGather.Value, config, now);

        Suspend(runtime, config, snapshot.Reason, now);
        return true;
    }

    private bool StepMonsterHunt(
        BotRuntime runtime,
        BotQuestSnapshot snapshot,
        BotQuestMonsterHuntObjective objective,
        BotConfig config,
        DateTimeOffset now)
    {
        if (objective.TargetNpcTemplateId == 0 || objective.Required <= 0 ||
            objective.Current < 0 || objective.Current > objective.Required)
        {
            Suspend(runtime, config, "invalid_monster_hunt_state", now);
            return true;
        }

        if (_objectiveIndex.HasValue &&
            (_objectiveIndex.Value != objective.ObjectiveIndex ||
             _objectiveTargetTemplateId != objective.TargetNpcTemplateId ||
             _objectiveItemId.HasValue))
        {
            Suspend(runtime, config, "objective_changed", now);
            return true;
        }

        _objectiveIndex = objective.ObjectiveIndex;
        _objectiveTargetTemplateId = objective.TargetNpcTemplateId;
        _objectiveRequired = objective.Required;
        if (!ObserveProgress(runtime, snapshot.QuestId, objective.ObjectiveIndex,
                objective.Current, objective.Required, config, now, clearGatherTemplate: false))
        {
            return true;
        }

        if (objective.Current >= objective.Required)
        {
            ReleaseObjectiveCombat(runtime);
            return WaitForReady(runtime, config, now);
        }

        if (_objectiveTarget != null)
        {
            if (IsLiveOwnedTarget(runtime, objective.TargetNpcTemplateId))
            {
                SetState(BotQuestLifecycleState.Fighting, "normal_combat_active", now);
                return true;
            }

            ReleaseObjectiveCombat(runtime);
            _progressObservationUntil ??=
                now + TimeSpan.FromMilliseconds(config.QuestProgressObservationMs);
            SetState(BotQuestLifecycleState.WaitingForProgress, "awaiting_authoritative_credit", now);
            return true;
        }

        if (WaitForProgressObservation(runtime, snapshot, objective.Current, objective.Required, now))
            return true;

        if (WaitForObjectiveRespawn(runtime, snapshot, objective.Current, objective.Required, config, now))
            return true;

        IReadOnlyList<Npc> targets;
        try
        {
            targets = _authority.FindMonsterTargets(
                runtime,
                objective.TargetNpcTemplateId,
                EffectiveRadius(config.SearchRadius, config.QuestObjectiveScanRadius),
                now) ?? [];
        }
        catch (Exception exception)
        {
            Suspend(runtime, config, $"target_scan_{exception.GetType().Name}", now);
            return true;
        }

        var target = targets.FirstOrDefault(candidate =>
            IsValidTarget(runtime, candidate, objective.TargetNpcTemplateId));
        if (target == null)
            return StepObjectiveTravel(
                runtime,
                snapshot,
                objective.Current,
                objective.Required,
                config,
                now,
                () => _authority.FindStaticMonsterDestinations(
                    runtime,
                    objective,
                    MaximumReportRouteDistance));

        if (HasUnownedMovement(runtime))
        {
            Suspend(runtime, config, "objective_movement_ownership_lost", now);
            return false;
        }
        StopOwnedMovement(runtime);
        if (!CanBeginObjectiveCombat(runtime, out var combatReason))
        {
            Suspend(runtime, config, combatReason, now);
            return false;
        }

        ClearRespawnWait();
        _objectiveTarget = target;
        _objectiveTargetObjectId = target.ObjId;
        _beginCombat(runtime, target, objective.TargetNpcTemplateId);
        SetState(BotQuestLifecycleState.Fighting, "objective_target_selected", now);
        Log(runtime.Bot.Id, "target_selected",
            $"quest={snapshot.QuestId} target_template={objective.TargetNpcTemplateId} " +
            $"target_obj={target.ObjId} current={objective.Current} required={objective.Required}");
        return true;
    }

    private bool StepItemGather(
        BotRuntime runtime,
        BotQuestSnapshot snapshot,
        BotQuestItemGatherObjective objective,
        BotConfig config,
        DateTimeOffset now)
    {
        if (objective.ItemId == 0 || objective.Required <= 0 ||
            objective.Current < 0 || objective.Current > objective.Required)
        {
            Suspend(runtime, config, "invalid_item_gather_state", now);
            return true;
        }

        if (_objectiveIndex.HasValue &&
            (_objectiveIndex.Value != objective.ObjectiveIndex ||
             _objectiveItemId != objective.ItemId))
        {
            Suspend(runtime, config, "objective_changed", now);
            return true;
        }

        _objectiveIndex = objective.ObjectiveIndex;
        _objectiveItemId = objective.ItemId;
        _objectiveRequired = objective.Required;
        if (!ObserveProgress(runtime, snapshot.QuestId, objective.ObjectiveIndex,
                objective.Current, objective.Required, config, now, clearGatherTemplate: true))
        {
            return true;
        }

        if (objective.Current >= objective.Required)
        {
            ReleaseGatherCombat(runtime);
            StopOwnedMovement(runtime);
            return WaitForReady(runtime, config, now);
        }

        if (_objectiveTarget != null)
        {
            if (!_objectiveTarget.IsDead && _objectiveTarget.Hp > 0 &&
                _objectiveTargetTemplateId.HasValue &&
                IsLiveOwnedTarget(runtime, _objectiveTargetTemplateId.Value))
            {
                SetState(BotQuestLifecycleState.Fighting, "gather_combat_active", now);
                return true;
            }

            if (_objectiveTarget.IsDead || _objectiveTarget.Hp <= 0)
                return StepGatherCorpse(runtime, snapshot, objective, config, now);

            Log(runtime.Bot.Id, "gather_target_lost",
                $"quest={snapshot.QuestId} item={objective.ItemId} target_obj={_objectiveTarget.ObjId}");
            ReleaseGatherCombat(runtime);
            _selectionDeadline = now + TimeSpan.FromMilliseconds(config.QuestTargetSelectionTimeoutMs);
            SetState(BotQuestLifecycleState.SelectingTarget, "gather_target_lost", now);
            return true;
        }

        if (WaitForProgressObservation(runtime, snapshot, objective.Current, objective.Required, now))
            return true;

        if (WaitForObjectiveRespawn(runtime, snapshot, objective.Current, objective.Required, config, now))
            return true;

        IReadOnlyList<Npc> targets;
        try
        {
            targets = _authority.FindItemGatherTargets(
                runtime,
                snapshot.QuestId,
                objective.ItemId,
                EffectiveRadius(config.SearchRadius, config.QuestObjectiveScanRadius),
                now) ?? [];
        }
        catch (Exception exception)
        {
            Suspend(runtime, config, $"gather_target_scan_{exception.GetType().Name}", now);
            return true;
        }

        var target = targets.FirstOrDefault(candidate => IsValidGatherTarget(runtime, candidate));
        if (target == null)
            return StepObjectiveTravel(
                runtime,
                snapshot,
                objective.Current,
                objective.Required,
                config,
                now,
                () => _authority.FindStaticItemGatherDestinations(
                    runtime,
                    snapshot.QuestId,
                    objective,
                    MaximumReportRouteDistance));

        if (HasUnownedMovement(runtime))
        {
            Suspend(runtime, config, "objective_movement_ownership_lost", now);
            return false;
        }
        StopOwnedMovement(runtime);
        if (!CanBeginObjectiveCombat(runtime, out var combatReason))
        {
            Suspend(runtime, config, combatReason, now);
            return false;
        }

        ClearRespawnWait();
        _objectiveTargetTemplateId = target.TemplateId;
        _objectiveTarget = target;
        _objectiveTargetObjectId = target.ObjId;
        _beginCombat(runtime, target, target.TemplateId);
        SetState(BotQuestLifecycleState.Fighting, "gather_target_selected", now);
        Log(runtime.Bot.Id, "gather_target_selected",
            $"quest={snapshot.QuestId} item={objective.ItemId} target_template={target.TemplateId} " +
            $"target_obj={target.ObjId} current={objective.Current} required={objective.Required}");
        return true;
    }

    private bool StepObjectiveTravel(
        BotRuntime runtime,
        BotQuestSnapshot snapshot,
        int current,
        int required,
        BotConfig config,
        DateTimeOffset now,
        Func<IReadOnlyList<BotQuestStaticObjectiveDestination>> findDestinations)
    {
        if (!_staticObjectiveDestination.HasValue)
        {
            IReadOnlyList<BotQuestStaticObjectiveDestination> destinations;
            try
            {
                destinations = findDestinations() ?? [];
            }
            catch (Exception exception)
            {
                Suspend(runtime, config, $"objective_route_scan_{exception.GetType().Name}", now);
                return true;
            }

            var selected = destinations
                .Where(candidate => IsFinite(candidate.Position) &&
                                    float.IsFinite(candidate.Distance) && candidate.Distance >= 0f &&
                                    candidate.Distance <= MaximumReportRouteDistance)
                .OrderBy(candidate => candidate.MapMarked && candidate.NpcTemplateId != 0 ? 0 :
                    candidate.MapMarked ? 1 : 2)
                .ThenBy(candidate => candidate.Distance)
                .Select(candidate => (BotQuestStaticObjectiveDestination?)candidate)
                .FirstOrDefault();
            if (selected.HasValue)
            {
                _staticObjectiveDestination = selected.Value;
                ExtendTravelSelectionDeadline(config, now, selected.Value.Distance);
                Log(runtime.Bot.Id, "objective_route_selected",
                    $"quest={snapshot.QuestId} target_template={selected.Value.NpcTemplateId} " +
                    $"map_marked={selected.Value.MapMarked.ToString().ToLowerInvariant()} " +
                    $"distance={selected.Value.Distance:F2} radius={selected.Value.Radius:F2} " +
                    $"destination=({selected.Value.Position.X:F2},{selected.Value.Position.Y:F2},{selected.Value.Position.Z:F2})");
            }
        }

        if (_staticObjectiveDestination.HasValue)
        {
            if (!CanOwnObjectiveTravel(runtime, out var travelReason))
            {
                Suspend(runtime, config, travelReason, now);
                return false;
            }

            var selected = _staticObjectiveDestination.Value;
            var distance = Vector3.Distance(runtime.Bot.Transform.World.Position, selected.Position);
            var scanRadius = EffectiveRadius(config.SearchRadius, config.QuestObjectiveScanRadius);
            var arrivalRadius = Math.Clamp(
                selected.Radius > 0f ? selected.Radius * 0.25f : 4f,
                4f,
                Math.Max(4f, scanRadius * 0.5f));
            if (float.IsFinite(distance) && distance <= arrivalRadius)
            {
                StopOwnedMovement(runtime);
                BeginRespawnWait(runtime, snapshot, current, required, config, now);
                return true;
            }

            if (_selectionDeadline.HasValue && now >= _selectionDeadline.Value)
            {
                Suspend(runtime, config, "objective_route_timeout", now);
                return true;
            }

            if (!_ownedDestination.HasValue ||
                CurrentRequestedDestination(runtime) == null ||
                Vector3.Distance(_ownedDestination.Value, selected.Position) > DestinationTolerance)
            {
                _setDestination(runtime.Bot, selected.Position, true);
                _ownedDestination = selected.Position;
                Log(runtime.Bot.Id, "objective_route_move_requested",
                    $"quest={snapshot.QuestId} target_template={selected.NpcTemplateId} " +
                    $"destination=({selected.Position.X:F2},{selected.Position.Y:F2},{selected.Position.Z:F2})");
            }

            SetState(BotQuestLifecycleState.MovingToObjective, "moving_to_static_objective", now);
            return true;
        }

        if (_selectionDeadline.HasValue && now >= _selectionDeadline.Value)
        {
            if (current > 0)
                BeginRespawnWait(runtime, snapshot, current, required, config, now);
            else
                Suspend(runtime, config, "target_selection_timeout", now);
            return true;
        }

        SetState(BotQuestLifecycleState.SelectingTarget, "no_valid_objective_target", now);
        return true;
    }

    private bool StepGatherCorpse(
        BotRuntime runtime,
        BotQuestSnapshot snapshot,
        BotQuestItemGatherObjective objective,
        BotConfig config,
        DateTimeOffset now)
    {
        BotQuestLootAttempt attempt;
        try
        {
            attempt = _authority.TryLootGatherItem(
                runtime.Bot,
                snapshot.QuestId,
                objective.ItemId,
                _objectiveTarget,
                (float)config.QuestReportInteractionRadius);
        }
        catch (Exception exception)
        {
            attempt = new BotQuestLootAttempt(false, $"loot_{exception.GetType().Name}", 0, 0);
        }

        if (attempt.Looted)
        {
            var corpseObjectId = _objectiveTarget.ObjId;
            ReleaseGatherCombat(runtime);
            StopOwnedMovement(runtime);
            _lootApproachDeadline = null;
            _progressObservationUntil =
                now + TimeSpan.FromMilliseconds(config.QuestProgressObservationMs);
            SetState(BotQuestLifecycleState.WaitingForProgress, "awaiting_authoritative_gather_credit", now);
            Log(runtime.Bot.Id, "gather_loot_taken",
                $"quest={snapshot.QuestId} item={objective.ItemId} corpse={corpseObjectId} " +
                $"matches={attempt.MatchingItems} remaining={attempt.RemainingCorpseItems}");
            return true;
        }

        if (string.Equals(attempt.Reason, "corpse_out_of_range", StringComparison.Ordinal))
        {
            EndObjectiveCombatRetainingTarget(runtime);
            _lootApproachDeadline ??= now + TimeSpan.FromSeconds(Math.Max(
                5d,
                config.QuestTargetSelectionTimeoutMs / 1000d));
            if (now >= _lootApproachDeadline.Value)
            {
                Log(runtime.Bot.Id, "gather_loot_timeout",
                    $"quest={snapshot.QuestId} item={objective.ItemId} corpse={_objectiveTarget.ObjId}");
                ReleaseGatherCombat(runtime);
                StopOwnedMovement(runtime);
                _lootApproachDeadline = null;
                _selectionDeadline = now + TimeSpan.FromMilliseconds(config.QuestTargetSelectionTimeoutMs);
                SetState(BotQuestLifecycleState.SelectingTarget, "gather_loot_approach_timeout", now);
                return true;
            }

            if (HasUnownedMovement(runtime))
            {
                Suspend(runtime, config, "gather_loot_movement_ownership_lost", now);
                return false;
            }

            var destination = _objectiveTarget.Transform.World.Position;
            if (!_ownedDestination.HasValue || CurrentRequestedDestination(runtime) == null ||
                Vector3.Distance(_ownedDestination.Value, destination) > DestinationTolerance)
            {
                _setDestination(runtime.Bot, destination, true);
                _ownedDestination = destination;
                Log(runtime.Bot.Id, "gather_loot_move_requested",
                    $"quest={snapshot.QuestId} item={objective.ItemId} corpse={_objectiveTarget.ObjId} " +
                    $"destination=({destination.X:F2},{destination.Y:F2},{destination.Z:F2})");
            }

            SetState(BotQuestLifecycleState.MovingToLoot, "moving_to_gather_corpse", now);
            return true;
        }

        Log(runtime.Bot.Id, "gather_loot_unavailable",
            $"quest={snapshot.QuestId} item={objective.ItemId} corpse={_objectiveTarget.ObjId} " +
            $"reason={attempt.Reason} matches={attempt.MatchingItems} remaining={attempt.RemainingCorpseItems}");
        ReleaseGatherCombat(runtime);
        StopOwnedMovement(runtime);
        _lootApproachDeadline = null;
        _selectionDeadline = now + TimeSpan.FromMilliseconds(config.QuestTargetSelectionTimeoutMs);
        SetState(BotQuestLifecycleState.SelectingTarget, attempt.Reason, now);
        return true;
    }

    private bool ObserveProgress(
        BotRuntime runtime,
        uint questId,
        byte objectiveIndex,
        int current,
        int required,
        BotConfig config,
        DateTimeOffset now,
        bool clearGatherTemplate)
    {
        if (!_objectiveCurrent.HasValue)
            _objectiveCurrent = current;

        if (current < _objectiveCurrent)
        {
            Suspend(runtime, config, "objective_progress_regressed", now);
            return false;
        }

        if (current <= _objectiveCurrent)
            return true;

        var previous = _objectiveCurrent.Value;
        _objectiveCurrent = current;
        _progressObservedAt = now;
        _selectionDeadline = now + TimeSpan.FromMilliseconds(config.QuestTargetSelectionTimeoutMs);
        _progressObservationUntil = null;
        _lootApproachDeadline = null;
        ClearRespawnWait();
        if (clearGatherTemplate)
            ReleaseGatherCombat(runtime);
        else
            ReleaseObjectiveCombat(runtime);
        StopOwnedMovement(runtime);
        Log(runtime.Bot.Id, "progress_observed",
            $"quest={questId} objective={objectiveIndex} from={previous} to={current} required={required}");
        return true;
    }

    private bool WaitForProgressObservation(
        BotRuntime runtime,
        BotQuestSnapshot snapshot,
        int current,
        int required,
        DateTimeOffset now)
    {
        if (!_progressObservationUntil.HasValue)
            return false;
        if (now < _progressObservationUntil.Value)
        {
            SetState(BotQuestLifecycleState.WaitingForProgress, "awaiting_authoritative_credit", now);
            return true;
        }

        Log(runtime.Bot.Id, "no_credit",
            $"quest={snapshot.QuestId} current={current} required={required}");
        _progressObservationUntil = null;
        return false;
    }

    private bool WaitForObjectiveRespawn(
        BotRuntime runtime,
        BotQuestSnapshot snapshot,
        int current,
        int required,
        BotConfig config,
        DateTimeOffset now)
    {
        if (!_respawnRescanAt.HasValue)
            return false;

        if (now < _respawnRescanAt.Value)
        {
            SetState(BotQuestLifecycleState.WaitingForRespawn, "waiting_for_objective_respawn", now);
            return true;
        }

        var waitedSince = _respawnWaitStartedAt ?? now;
        _respawnRescanAt = null;
        _selectionDeadline = null;
        SetState(BotQuestLifecycleState.SelectingTarget, "objective_respawn_rescan", now);
        Log(runtime.Bot.Id, "respawn_rescan",
            $"quest={snapshot.QuestId} current={current} required={required} " +
            $"waited_ms={(long)Math.Max(0d, (now - waitedSince).TotalMilliseconds)}");
        return false;
    }

    private void BeginRespawnWait(
        BotRuntime runtime,
        BotQuestSnapshot snapshot,
        int current,
        int required,
        BotConfig config,
        DateTimeOffset now)
    {
        _respawnWaitStartedAt ??= now;
        var rescanDelayMs = Math.Clamp(config.QuestProgressObservationMs, 250, 5000);
        _respawnRescanAt = now + TimeSpan.FromMilliseconds(rescanDelayMs);
        _selectionDeadline = null;
        SetState(BotQuestLifecycleState.WaitingForRespawn, "waiting_for_objective_respawn", now);
        Log(runtime.Bot.Id, "respawn_wait",
            $"quest={snapshot.QuestId} current={current} required={required} " +
            $"rescan_at={Timestamp(_respawnRescanAt.Value)}");
    }

    private void ClearRespawnWait()
    {
        _respawnWaitStartedAt = null;
        _respawnRescanAt = null;
    }

    private bool WaitForReady(BotRuntime runtime, BotConfig config, DateTimeOffset now)
    {
        _completionObservationUntil ??=
            now + TimeSpan.FromMilliseconds(config.QuestCompletionObservationMs);
        if (now >= _completionObservationUntil.Value)
            Suspend(runtime, config, "ready_state_not_observed", now);
        else
            SetState(BotQuestLifecycleState.WaitingForReady, "awaiting_authoritative_ready", now);
        return true;
    }

    private bool StepReport(
        BotRuntime runtime,
        BotQuestSnapshot snapshot,
        BotConfig config,
        DateTimeOffset now)
    {
        ReleaseObjectiveCombat(runtime);

        if (_state == BotQuestLifecycleState.WaitingForCompletion)
        {
            if (!_reportAttemptedAt.HasValue)
            {
                Suspend(runtime, config, "missing_report_dispatch_time", now);
                return true;
            }
            if (!_completionObservationUntil.HasValue)
                _completionObservationUntil =
                    _reportAttemptedAt.Value + TimeSpan.FromMilliseconds(config.QuestCompletionObservationMs);
            if (now >= _completionObservationUntil.Value)
                Suspend(runtime, config, "completion_not_observed", now);
            return true;
        }

        _completionObservationUntil = null;

        if (snapshot.ReportEndpoints == null || snapshot.ReportEndpoints.Length != 1)
        {
            Suspend(runtime, config,
                snapshot.ReportEndpoints?.Length > 1 ? "ambiguous_report_endpoint" : "missing_report_endpoint",
                now);
            return true;
        }

        var endpoint = snapshot.ReportEndpoints[0];
        if (_reportEndpoint.HasValue && _reportEndpoint.Value != endpoint)
        {
            Suspend(runtime, config, "report_endpoint_changed", now);
            return true;
        }

        _reportEndpoint = endpoint;
        _rewardIndex = BotQuestAuthority.SelectRewardIndex(snapshot.RewardIndices);
        if (_rewardIndex < 0)
        {
            Suspend(runtime, config, "invalid_reward_set", now);
            return true;
        }

        if (endpoint.Kind == BotQuestReportKind.Journal)
            return AttemptReport(runtime, snapshot.QuestId, endpoint.Kind, 0, config, now);

        if (endpoint.Kind is not BotQuestReportKind.Npc and not BotQuestReportKind.Doodad ||
            endpoint.TemplateId == 0)
        {
            Suspend(runtime, config, "invalid_report_endpoint", now);
            return true;
        }

        IReadOnlyList<BotQuestWorldObject> candidates;
        try
        {
            candidates = _authority.FindReportObjects(
                runtime,
                endpoint,
                EffectiveRadius(config.SearchRadius, config.QuestReportScanRadius),
                now) ?? [];
        }
        catch (Exception exception)
        {
            Suspend(runtime, config, $"report_scan_{exception.GetType().Name}", now);
            return true;
        }

        var reportObject = candidates.FirstOrDefault(candidate =>
            candidate.Kind == endpoint.Kind &&
            candidate.Object != null &&
            candidate.Object.TemplateId == endpoint.TemplateId &&
            float.IsFinite(candidate.Distance) &&
            candidate.Distance >= 0 &&
            IsSameWorld(runtime.Bot, candidate.Object));
        if (reportObject.Object == null)
        {
            _reportObjectId = null;
            if (!_staticReportDestination.HasValue)
            {
                IReadOnlyList<BotQuestStaticReportDestination> staticDestinations;
                try
                {
                    staticDestinations = _authority.FindStaticReportDestinations(
                        runtime,
                        endpoint,
                        MaximumReportRouteDistance) ?? [];
                }
                catch (Exception exception)
                {
                    Suspend(runtime, config, $"report_route_scan_{exception.GetType().Name}", now);
                    return true;
                }

                var staticDestination = staticDestinations.FirstOrDefault(candidate =>
                    candidate.Kind == endpoint.Kind &&
                    candidate.TemplateId == endpoint.TemplateId &&
                    IsFinite(candidate.Position) &&
                    float.IsFinite(candidate.Distance) &&
                    candidate.Distance >= 0f &&
                    candidate.Distance <= MaximumReportRouteDistance);
                if (staticDestination.TemplateId != 0)
                {
                    _staticReportDestination = staticDestination.Position;
                    ExtendTravelSelectionDeadline(config, now, staticDestination.Distance);
                    Log(runtime.Bot.Id, "report_route_selected",
                        $"quest={snapshot.QuestId} kind={EndpointName(endpoint.Kind)} " +
                        $"template={endpoint.TemplateId} distance={staticDestination.Distance:F2} " +
                        $"destination=({staticDestination.Position.X:F2},{staticDestination.Position.Y:F2},{staticDestination.Position.Z:F2})");
                }
            }

            if (_staticReportDestination.HasValue)
            {
                if (HasUnownedMovement(runtime))
                {
                    Suspend(runtime, config, "report_movement_ownership_lost", now);
                    return false;
                }

                var destination = _staticReportDestination.Value;
                if (!_ownedDestination.HasValue ||
                    CurrentRequestedDestination(runtime) == null ||
                    Vector3.Distance(_ownedDestination.Value, destination) > DestinationTolerance)
                {
                    _setDestination(runtime.Bot, destination, true);
                    _ownedDestination = destination;
                    Log(runtime.Bot.Id, "report_route_move_requested",
                        $"quest={snapshot.QuestId} kind={EndpointName(endpoint.Kind)} " +
                        $"template={endpoint.TemplateId} destination=({destination.X:F2},{destination.Y:F2},{destination.Z:F2})");
                }

                SetState(BotQuestLifecycleState.MovingToReport, "moving_to_static_report_endpoint", now);
                return true;
            }

            StopOwnedMovement(runtime);
            if (_selectionDeadline.HasValue && now >= _selectionDeadline.Value)
                Suspend(runtime, config, "report_endpoint_timeout", now);
            else
                SetState(BotQuestLifecycleState.MovingToReport, "report_endpoint_not_nearby", now);
            return true;
        }

        _staticReportDestination = null;
        _reportObjectId = reportObject.Object.ObjId;
        if (reportObject.Distance > config.QuestReportInteractionRadius)
        {
            if (HasUnownedMovement(runtime))
            {
                Suspend(runtime, config, "report_movement_ownership_lost", now);
                return false;
            }

            // Keep following the accepted route while it is still ours. Recomputing
            // an off-mesh NPC approach point from the bot's changing position makes
            // that point drift every brain tick. The travel manager intentionally
            // deduplicates sub-0.5 m changes, so claiming the drifted point as owned
            // could make our ownership value disagree with the actual route.
            if (!_ownedDestination.HasValue || CurrentRequestedDestination(runtime) == null)
            {
                var targetPosition = reportObject.Object.Transform.World.Position;
                var destination = BotQuestApproachPlanner.ForWorldObject(
                    runtime.Bot.Transform.World.Position,
                    targetPosition,
                    (float)config.QuestReportInteractionRadius,
                    runtime.Bot.ParentWorld.GetHeight);
                _setDestination(runtime.Bot, destination, true);
                _ownedDestination = destination;
                Log(runtime.Bot.Id, "report_move_requested",
                    $"quest={snapshot.QuestId} kind={EndpointName(endpoint.Kind)} " +
                    $"template={endpoint.TemplateId} object={reportObject.Object.ObjId} " +
                    $"distance={reportObject.Distance:F2} " +
                    $"destination=({destination.X:F2},{destination.Y:F2},{destination.Z:F2})");
            }

            SetState(BotQuestLifecycleState.MovingToReport, "moving_to_report_endpoint", now);
            return true;
        }

        StopOwnedMovement(runtime);
        SetState(BotQuestLifecycleState.Reporting, "report_endpoint_revalidated", now);
        return AttemptReport(
            runtime,
            snapshot.QuestId,
            endpoint.Kind,
            reportObject.Object.ObjId,
            config,
            now);
    }

    private bool AttemptReport(
        BotRuntime runtime,
        uint questId,
        BotQuestReportKind kind,
        uint objectId,
        BotConfig config,
        DateTimeOffset now)
    {
        _reportAttemptCount++;
        _reportAttemptedAt = now;
        var reported = false;
        try
        {
            reported = _authority.ReportQuest(runtime.Bot, questId, kind, objectId, _rewardIndex ?? 0);
        }
        catch (Exception exception)
        {
            Log(runtime.Bot.Id, "report_error",
                $"quest={questId} kind={EndpointName(kind)} error={exception.GetType().Name}");
        }

        Log(runtime.Bot.Id, reported ? "report_dispatched" : "report_rejected",
            $"quest={questId} kind={EndpointName(kind)} object={objectId} reward={_rewardIndex ?? 0}");
        if (!reported)
        {
            Suspend(runtime, config, "authoritative_report_rejected", now);
            return true;
        }

        _completionObservationUntil =
            now + TimeSpan.FromMilliseconds(config.QuestCompletionObservationMs);
        SetState(BotQuestLifecycleState.WaitingForCompletion, "awaiting_authoritative_completion", now);
        return true;
    }

    private void BeginQuest(
        BotRuntime runtime,
        BotQuestSnapshot snapshot,
        BotConfig config,
        DateTimeOffset now)
    {
        ResetPlan(runtime, releaseCombat: true, stopMovement: true);
        _questId = snapshot.QuestId;
        _selectionDeadline = now + TimeSpan.FromMilliseconds(config.QuestTargetSelectionTimeoutMs);
        SetState(BotQuestLifecycleState.SelectingTarget, "active_quest_selected", now);
        Log(runtime.Bot.Id, "objective_selected",
            $"quest={snapshot.QuestId} shape={snapshot.ObjectiveShape.ToString().ToLowerInvariant()} " +
            $"main_story={snapshot.MainStory.ToString().ToLowerInvariant()} ready={snapshot.Ready.ToString().ToLowerInvariant()}");
    }

    private BotQuestSnapshot SelectSnapshot(
        BotRuntime runtime,
        IReadOnlyList<BotQuestSnapshot> snapshots,
        BotConfig config,
        DateTimeOffset now)
    {
        var eligible = snapshots?
            .Where(snapshot => snapshot != null && snapshot.QuestId != 0)
            .ToArray() ?? [];

        // Finish the selected quest before reconsidering the route. This mirrors
        // the current-target bias used by mature playerbot travel systems and
        // prevents a newly accepted quest from causing cross-zone thrashing.
        if (_questId.HasValue)
        {
            var current = eligible.FirstOrDefault(snapshot => snapshot.QuestId == _questId.Value);
            if (current != null)
                return current;
        }

        return eligible
            .Select(snapshot => new
            {
                Snapshot = snapshot,
                Distance = EstimateQuestTravelDistance(runtime, snapshot, config, now)
            })
            // Clear nearby work as a cluster before committing to a regional
            // handoff. Within the cluster, minimize actual travel distance.
            .OrderBy(candidate => candidate.Distance <= LocalQuestVicinityRadius ? 0 : 1)
            .ThenBy(candidate => candidate.Distance)
            .ThenBy(candidate => candidate.Snapshot.Ready ? 0 : 1)
            .ThenBy(candidate => candidate.Snapshot.MainStory ? 0 : 1)
            .ThenBy(candidate => candidate.Snapshot.QuestId)
            .Select(candidate => candidate.Snapshot)
            .FirstOrDefault();
    }

    private float EstimateQuestTravelDistance(
        BotRuntime runtime,
        BotQuestSnapshot snapshot,
        BotConfig config,
        DateTimeOffset now)
    {
        var botPosition = runtime.Bot.Transform.World.Position;
        if (!IsFinite(botPosition))
            return float.PositiveInfinity;

        try
        {
            if (snapshot.Ready && snapshot.ReportEndpoints?.Length == 1)
            {
                var endpoint = snapshot.ReportEndpoints[0];
                if (endpoint.Kind == BotQuestReportKind.Journal)
                    return 0f;

                var nearby = _authority.FindReportObjects(
                    runtime,
                    endpoint,
                    EffectiveRadius(config.SearchRadius, config.QuestReportScanRadius),
                    now) ?? [];
                var liveDistance = nearby
                    .Where(candidate => candidate.Kind == endpoint.Kind &&
                                        candidate.Object?.TemplateId == endpoint.TemplateId &&
                                        float.IsFinite(candidate.Distance) && candidate.Distance >= 0f)
                    .Select(candidate => candidate.Distance)
                    .DefaultIfEmpty(float.PositiveInfinity)
                    .Min();
                if (float.IsFinite(liveDistance))
                    return liveDistance;

                return (_authority.FindStaticReportDestinations(
                            runtime,
                            endpoint,
                            MaximumReportRouteDistance) ?? [])
                    .Where(candidate => candidate.Kind == endpoint.Kind &&
                                        candidate.TemplateId == endpoint.TemplateId &&
                                        float.IsFinite(candidate.Distance) && candidate.Distance >= 0f)
                    .Select(candidate => candidate.Distance)
                    .DefaultIfEmpty(float.PositiveInfinity)
                    .Min();
            }

            IReadOnlyList<Npc> targets = snapshot.ObjectiveShape switch
            {
                BotQuestObjectiveShape.MonsterHunt when snapshot.MonsterHunt.HasValue =>
                    _authority.FindMonsterTargets(
                        runtime,
                        snapshot.MonsterHunt.Value.TargetNpcTemplateId,
                        EffectiveRadius(config.SearchRadius, config.QuestObjectiveScanRadius),
                        now),
                BotQuestObjectiveShape.ItemGather when snapshot.ItemGather.HasValue =>
                    _authority.FindItemGatherTargets(
                        runtime,
                        snapshot.QuestId,
                        snapshot.ItemGather.Value.ItemId,
                        EffectiveRadius(config.SearchRadius, config.QuestObjectiveScanRadius),
                        now),
                _ => []
            };

            var objectiveLiveDistance = (targets ?? [])
                .Where(target => target?.Transform?.World != null && IsSameWorld(runtime.Bot, target))
                .Select(target => Vector3.Distance(botPosition, target.Transform.World.Position))
                .Where(distance => float.IsFinite(distance) && distance >= 0f)
                .DefaultIfEmpty(float.PositiveInfinity)
                .Min();
            if (float.IsFinite(objectiveLiveDistance))
                return objectiveLiveDistance;

            IReadOnlyList<BotQuestStaticObjectiveDestination> staticDestinations =
                snapshot.ObjectiveShape switch
                {
                    BotQuestObjectiveShape.MonsterHunt when snapshot.MonsterHunt.HasValue =>
                        _authority.FindStaticMonsterDestinations(
                            runtime,
                            snapshot.MonsterHunt.Value,
                            MaximumReportRouteDistance),
                    BotQuestObjectiveShape.ItemGather when snapshot.ItemGather.HasValue =>
                        _authority.FindStaticItemGatherDestinations(
                            runtime,
                            snapshot.QuestId,
                            snapshot.ItemGather.Value,
                            MaximumReportRouteDistance),
                    _ => []
                };

            return (staticDestinations ?? [])
                .Where(candidate => IsFinite(candidate.Position) &&
                                    float.IsFinite(candidate.Distance) && candidate.Distance >= 0f)
                .OrderBy(candidate => candidate.MapMarked && candidate.NpcTemplateId != 0 ? 0 :
                    candidate.MapMarked ? 1 : 2)
                .ThenBy(candidate => candidate.Distance)
                .Select(candidate => candidate.Distance)
                .DefaultIfEmpty(float.PositiveInfinity)
                .First();
        }
        catch
        {
            // Selection scoring is advisory. The selected quest's normal step
            // retains the detailed guarded error and bounded retry behavior.
            return float.PositiveInfinity;
        }
    }

    private static bool IsWorldReady(BotRuntime runtime, out string reason)
    {
        var bot = runtime.Bot;
        if (runtime.Retired || bot.IsDead || bot.Hp <= 0 || bot.ParentWorld == null ||
            bot.Transform?.World == null || bot.Quests == null || runtime.Brain == null ||
            runtime.Brain.Cancelled || runtime.Mover == null || runtime.Mover.Cancelled)
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

        reason = "ready";
        return true;
    }

    private bool CanBeginObjectiveCombat(BotRuntime runtime, out string reason)
    {
        var combat = runtime.CombatState;
        if (combat.IsForced || combat.InDuel || combat.DuelRequestPending || combat.IsResting ||
            combat.RespawnScheduled || combat.ShouldRespawn || combat.IsSearching ||
            combat.StopAtTargetHpPercent.HasValue || combat.NonlethalFloorReached != null)
        {
            reason = "combat_not_available";
            return false;
        }

        if (combat.Target != null || runtime.Bot.CurrentTarget != null ||
            combat.CurrentState != BotCombatStateType.Idle || combat.IsActive ||
            combat.TargetTypeFilter.HasValue)
        {
            reason = "combat_ownership_conflict";
            return false;
        }

        if (runtime.MovementState.FollowTarget != null || runtime.MovementState.IsFalling ||
            runtime.MovementState.JumpRequested || runtime.MovementState.IsJumping ||
            runtime.MovementState.Destination.HasValue || runtime.MovementState.IsMoving)
        {
            reason = "movement_not_available";
            return false;
        }

        reason = "available";
        return true;
    }

    private bool CanOwnObjectiveTravel(BotRuntime runtime, out string reason)
    {
        var combat = runtime.CombatState;
        if (combat.IsForced || combat.InDuel || combat.DuelRequestPending || combat.IsResting ||
            combat.RespawnScheduled || combat.ShouldRespawn || combat.IsSearching ||
            combat.StopAtTargetHpPercent.HasValue || combat.NonlethalFloorReached != null)
        {
            reason = "combat_not_available";
            return false;
        }

        if (combat.Target != null || runtime.Bot.CurrentTarget != null ||
            combat.CurrentState != BotCombatStateType.Idle || combat.IsActive ||
            combat.TargetTypeFilter.HasValue)
        {
            reason = "combat_ownership_conflict";
            return false;
        }

        if (runtime.MovementState.FollowTarget != null || runtime.MovementState.IsFalling ||
            runtime.MovementState.JumpRequested || runtime.MovementState.IsJumping)
        {
            reason = "movement_not_available";
            return false;
        }

        if (HasUnownedMovement(runtime) ||
            (!_ownedDestination.HasValue && runtime.MovementState.IsMoving))
        {
            reason = "objective_movement_ownership_lost";
            return false;
        }

        reason = "available";
        return true;
    }

    private bool IsLiveOwnedTarget(BotRuntime runtime, uint targetTemplateId)
    {
        var combat = runtime.CombatState;
        return IsValidTarget(runtime, _objectiveTarget, targetTemplateId) &&
               combat.TargetTypeFilter == targetTemplateId &&
               combat.IsActive &&
               combat.Target is Npc combatTarget &&
               combatTarget.ObjId == _objectiveTarget.ObjId &&
               runtime.Bot.CurrentTarget is Npc currentTarget &&
               currentTarget.ObjId == _objectiveTarget.ObjId;
    }

    private static bool IsValidTarget(BotRuntime runtime, Npc target, uint targetTemplateId) =>
        target != null && target.ObjId != 0 && target.TemplateId == targetTemplateId &&
        !target.IsDead && target.Hp > 0 && IsSameWorld(runtime.Bot, target) &&
        runtime.Bot.CanAttack(target);

    private static bool IsValidGatherTarget(BotRuntime runtime, Npc target) =>
        target != null && target.ObjId != 0 && target.TemplateId != 0 &&
        !target.IsDead && target.Hp > 0 && IsSameWorld(runtime.Bot, target) &&
        runtime.Bot.CanAttack(target);

    private static bool IsSameWorld(Character bot, BaseUnit target) =>
        bot?.ParentWorld != null && target?.ParentWorld != null &&
        ReferenceEquals(bot.ParentWorld, target.ParentWorld) &&
        bot.Transform?.World != null && target.Transform?.World != null;

    private bool HasUnownedMovement(BotRuntime runtime) =>
        CurrentRequestedDestination(runtime) is { } destination &&
        (!_ownedDestination.HasValue ||
         Vector3.Distance(destination, _ownedDestination.Value) > DestinationTolerance);

    private static float EffectiveRadius(double generalRadius, double configuredRadius) =>
        (float)Math.Min(
            MaximumWorldScanRadius,
            Math.Min(
                double.IsFinite(generalRadius) ? Math.Max(0d, generalRadius) : 0d,
                double.IsFinite(configuredRadius) ? Math.Max(0d, configuredRadius) : 0d));

    private void Suspend(BotRuntime runtime, BotConfig config, string reason, DateTimeOffset now)
    {
        ReleaseObjectiveCombat(runtime);
        StopOwnedMovement(runtime);
        _reportObjectId = null;
        _progressObservationUntil = null;
        _completionObservationUntil = null;
        _selectionDeadline = now + TimeSpan.FromMilliseconds(config.QuestTargetSelectionTimeoutMs);
        _retryAt = now + TimeSpan.FromMilliseconds(config.QuestCompletionRetryBackoffMs);
        if (SetState(BotQuestLifecycleState.Suspended, reason, now))
        {
            _suspensionCount++;
            Log(runtime.Bot.Id, "suspended",
                $"quest={_questId?.ToString() ?? "none"} reason={reason} retry_at={Timestamp(_retryAt.Value)}");
        }
    }

    private void Complete(BotRuntime runtime, DateTimeOffset now)
    {
        var completedQuestId = _questId;
        ResetPlan(runtime, releaseCombat: true, stopMovement: true);
        _completedCount++;
        _completedAt = now;
        SetState(BotQuestLifecycleState.Idle, "quest_completed_rescan", now);
        Log(runtime.Bot.Id, "completed",
            $"quest={completedQuestId?.ToString() ?? "none"} completed={_completedCount}");
        Log(runtime.Bot.Id, "rescan", "reason=quest_completed");
    }

    private void ReleaseRemovedQuest(BotRuntime runtime, DateTimeOffset now)
    {
        var removedQuestId = _questId;
        ResetPlan(runtime, releaseCombat: true, stopMovement: true);
        SetState(BotQuestLifecycleState.Idle, "active_quest_removed", now);
        Log(runtime.Bot.Id, "released",
            $"quest={removedQuestId?.ToString() ?? "none"} reason=active_quest_removed");
    }

    private void Disable(BotRuntime runtime, DateTimeOffset now)
    {
        ResetPlan(runtime, releaseCombat: true, stopMovement: true);
        SetState(BotQuestLifecycleState.Disabled, "disabled", now);
    }

    private void ResetPlan(BotRuntime runtime, bool releaseCombat, bool stopMovement)
    {
        if (releaseCombat)
            ReleaseObjectiveCombat(runtime);
        if (stopMovement)
            StopOwnedMovement(runtime);
        _questId = null;
        _objectiveTargetTemplateId = null;
        _objectiveItemId = null;
        _objectiveIndex = null;
        _objectiveTargetObjectId = null;
        _objectiveTarget = null;
        _objectiveCurrent = null;
        _objectiveRequired = null;
        _reportEndpoint = null;
        _reportObjectId = null;
        _staticObjectiveDestination = null;
        _staticReportDestination = null;
        _rewardIndex = null;
        _retryAt = null;
        _selectionDeadline = null;
        ClearRespawnWait();
        _progressObservationUntil = null;
        _lootApproachDeadline = null;
        _completionObservationUntil = null;
        _reportAttemptedAt = null;
    }

    private void ReleaseObjectiveCombat(BotRuntime runtime)
    {
        if (!_objectiveTargetTemplateId.HasValue && !_objectiveTargetObjectId.HasValue)
            return;

        _endCombat(runtime, _objectiveTargetTemplateId ?? 0, _objectiveTargetObjectId);
        _objectiveTarget = null;
        _objectiveTargetObjectId = null;
    }

    private void ReleaseGatherCombat(BotRuntime runtime)
    {
        ReleaseObjectiveCombat(runtime);
        _objectiveTargetTemplateId = null;
    }

    private void EndObjectiveCombatRetainingTarget(BotRuntime runtime)
    {
        if (!_objectiveTargetTemplateId.HasValue && !_objectiveTargetObjectId.HasValue)
            return;

        _endCombat(runtime, _objectiveTargetTemplateId ?? 0, _objectiveTargetObjectId);
    }

    private void StopOwnedMovement(BotRuntime runtime)
    {
        if (_ownedDestination.HasValue &&
            CurrentRequestedDestination(runtime) is { } destination &&
            Vector3.Distance(destination, _ownedDestination.Value) <= DestinationTolerance)
        {
            _stopMovement(runtime.Bot);
        }
        _ownedDestination = null;
    }

    private static Vector3? CurrentRequestedDestination(BotRuntime runtime) =>
        runtime.MovementState.TravelDestination ?? runtime.MovementState.Destination;

    private static void BeginProductionCombat(BotRuntime runtime, Npc target, uint targetTemplateId)
    {
        var combat = runtime.CombatState;
        combat.TargetTypeFilter = targetTemplateId;
        combat.Target = target;
        runtime.Bot.CurrentTarget = target;
        combat.IsActive = true;
        combat.TransitionTo(BotCombatStateType.Combat);
    }

    private static void EndProductionCombat(
        BotRuntime runtime,
        uint targetTemplateId,
        uint? targetObjectId)
    {
        var combat = runtime.CombatState;
        if (combat.TargetTypeFilter != targetTemplateId)
            return;

        if ((!targetObjectId.HasValue || combat.Target?.ObjId == targetObjectId.Value) ||
            combat.Target?.TemplateId == targetTemplateId)
            combat.Target = null;
        if ((!targetObjectId.HasValue || runtime.Bot.CurrentTarget?.ObjId == targetObjectId.Value) ||
            runtime.Bot.CurrentTarget?.TemplateId == targetTemplateId)
            runtime.Bot.CurrentTarget = null;
        combat.TargetTypeFilter = null;
        combat.IsActive = false;
        if (!combat.IsForced && combat.CurrentState is
                BotCombatStateType.Combat or
                BotCombatStateType.Questing or
                BotCombatStateType.Searching)
        {
            combat.TransitionTo(BotCombatStateType.Idle);
        }
        BotManager.Instance.StopBot(runtime.Bot);
    }

    private bool SetState(BotQuestLifecycleState state, string reason, DateTimeOffset now)
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
        var message = $"BOT id={botId} ev=quest_lifecycle_{eventName} {detail}";
        Logger.Info(message);
        _eventSink?.Invoke(message);
    }

    private static string EndpointName(BotQuestReportKind kind) =>
        kind.ToString().ToLowerInvariant();

    private static string Timestamp(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("O");

    private void ExtendTravelSelectionDeadline(
        BotConfig config,
        DateTimeOffset now,
        float distance)
    {
        var configuredSeconds = Math.Max(0d, config.QuestTargetSelectionTimeoutMs / 1000d);
        var travelSeconds = Math.Max(configuredSeconds, distance / ConservativeTravelSpeed + configuredSeconds);
        _selectionDeadline = now + TimeSpan.FromSeconds(Math.Min(MaximumTravelTimeoutSeconds, travelSeconds));
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
