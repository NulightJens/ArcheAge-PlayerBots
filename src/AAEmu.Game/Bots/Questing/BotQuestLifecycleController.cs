using System.Numerics;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Bots.Navigation;
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
    uint[] IgnoredQuestIds,
    uint[] BlockedMainStoryQuestIds,
    long CompletedCount,
    long SuspensionCount,
    long ReportAttemptCount);

/// <summary>
/// Executes authoritative, single-objective monster-hunt and item-gather quests. It owns
/// an exact combat filter/target, native corpse-loot interaction, or an exact report destination, observes
/// AAEmu quest state for progress, and delegates reporting to guarded native
/// quest APIs.
/// </summary>
public sealed partial class BotQuestLifecycleController
{
    internal const float MaximumWorldScanRadius = BotCombatTask.MaximumQuestTargetSearchRadius;
    // Starter and later quest chains can legitimately hand off beyond a local
    // 500 m scan (Nuian quest 2532 reports roughly 646 m away). Static report
    // destinations are authoritative, world-scoped spawns, so allow a regional
    // route while still rejecting accidental cross-world traversal.
    internal const float MaximumReportRouteDistance = 5000f;
    internal const float LocalQuestVicinityRadius = 500f;
    internal const int SideQuestFailureThreshold = 3;
    internal static readonly TimeSpan SideQuestQuarantine = TimeSpan.FromMinutes(5);
    internal static readonly TimeSpan MainStoryFailureCooldown = TimeSpan.FromMinutes(5);
    private const float DestinationTolerance = 0.25f;
    private const float ConservativeTravelSpeed = 2f;
    private const double MaximumTravelTimeoutSeconds = 1800d;

    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();

    private readonly object _syncRoot = new();
    private readonly IBotQuestAuthority _authority;
    private readonly Action<BotRuntime, Npc, uint> _beginCombat;
    private readonly Action<BotRuntime, uint, uint?> _endCombat;
    private readonly Func<Character, Vector3, bool, bool> _setDestination;
    private readonly Func<Character, Vector3, bool, bool> _setInteractionDestination;
    private readonly Action<Character> _stopMovement;
    private readonly Action<string> _eventSink;
    private readonly Dictionary<uint, int> _questFailureCounts = [];
    private readonly Dictionary<uint, DateTimeOffset> _ignoredSideQuests = [];
    private readonly HashSet<uint> _blockedMainStoryQuests = [];
#if !PLAYERBOTS_AAEMU_3_0
    private readonly Dictionary<uint, DateTimeOffset> _mainStoryRetryAfter = [];
#endif
    private readonly List<uint> _dispositionScratch = [];

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
    private bool _selectedQuestMainStory;
    private bool _objectiveAreaEstablished;
    private int _hasSupportedActiveQuest;
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
        _setDestination = setDestination == null
            ? ((bot, destination, run) => BotManager.Instance.SetBotTravelDestination(
                bot,
                destination,
                run,
                BotTravelIntent.QuestObjective,
                BotMovementOwner.QuestLifecycle))
            : ((bot, destination, run) =>
            {
                setDestination(bot, destination, run);
                return true;
            });
        _setInteractionDestination = setDestination == null
            ? ((bot, destination, run) => BotManager.Instance.SetBotTravelDestination(
                bot,
                destination,
                run,
                BotTravelIntent.Interaction,
                BotMovementOwner.QuestLifecycle))
            : ((bot, destination, run) =>
            {
                setDestination(bot, destination, run);
                return true;
            });
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

#if !PLAYERBOTS_AAEMU_3_0
#endif
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

#if !PLAYERBOTS_AAEMU_3_0
            // A forced state is an operator or social ownership boundary. Leave
            // native work and any existing movement cleanup to their owners, but
            // never recreate lifecycle travel while that boundary remains active.
            if (runtime.CombatState.IsForced)
                return false;
#endif

#if !PLAYERBOTS_AAEMU_3_0
            // A native cast or plot owns the tick even when no quest is selected.
            // Do not start a new intake/lifecycle route until it has finished.
            if (runtime.Bot.SkillTask != null || runtime.Bot.ActivePlotState != null)
                return false;
#endif

            IReadOnlyList<BotQuestSnapshot> snapshots;
            try
            {
                snapshots = _authority.ReadActiveQuests(runtime.Bot) ?? [];
                ReconcileQuestDispositions(runtime, snapshots, now);
                Volatile.Write(
                    ref _hasSupportedActiveQuest,
                    HasRunnableOrMainStoryQuest(snapshots, now) ? 1 : 0);
            }
            catch (Exception exception)
            {
                // An authority read failure cannot prove that lifecycle work is absent.
                Volatile.Write(ref _hasSupportedActiveQuest, 1);
                Suspend(runtime, config, $"quest_read_{exception.GetType().Name}", now);
                return true;
            }

#if !PLAYERBOTS_AAEMU_3_0
            if (runtime.MovementState.Climb is { } climb)
            {
                var climbingQuest = snapshots.FirstOrDefault(q => q.QuestId == climb.QuestId);
                if (climbingQuest == null || climbingQuest.Ready ||
                    climbingQuest.Interaction is { } progress && progress.Current >= progress.Required)
                    climb.Descending = true;
                if (climb.Descending)
                {
                    SetState(BotQuestLifecycleState.MovingToObjective, "descending_and_disengaging_climb", now);
                    return true;
                }
            }
#endif

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
                var reason = snapshots.Any(candidate => candidate?.QuestId != 0)
                    ? "no_supported_active_quest"
                    : "no_active_quest";
                SetState(BotQuestLifecycleState.Idle, reason, now);
                return false;
            }

            // The intake controller may already own a route selected on the
            // previous tick. Release only that owned route before the active
            // quest claims combat or report travel; unrelated movement remains
            // protected by the normal ownership checks below.
            var releasedIntake = runtime.QuestIntakeController.YieldToQuestLifecycle(runtime, now);

#if !PLAYERBOTS_AAEMU_3_0
            var releasedLifecycleMovement = false;
#endif
            if (!_questId.HasValue || _questId.Value != snapshot.QuestId)
            {
#if !PLAYERBOTS_AAEMU_3_0
                releasedLifecycleMovement = BeginQuest(runtime, snapshot, config, now);
#else
                BeginQuest(runtime, snapshot, config, now);
#endif
            }

            if (ExpireSideQuest(runtime, snapshot, now)) return false;

            // StopBot completes on the mover boundary. Hold priority for one brain
            // tick after cancelling an owned intake or lifecycle route so transient
            // movement/fall flags cannot suspend the active quest and reopen intake.
#if !PLAYERBOTS_AAEMU_3_0
            if (releasedIntake || releasedLifecycleMovement)
            {
                SetState(
                    BotQuestLifecycleState.SelectingTarget,
                    releasedLifecycleMovement ? "lifecycle_movement_released" : "intake_movement_released",
                    now);
                return true;
            }
#else
            if (releasedIntake)
            {
                SetState(BotQuestLifecycleState.SelectingTarget, "intake_movement_released", now);
                return true;
            }
#endif

            if (snapshot.Ready)
            {
                if (_objectiveRequired.HasValue)
                    _objectiveCurrent = _objectiveRequired;
                return StepReport(runtime, snapshot, config, now);
            }

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
                _objectiveTarget?.TemplateId ?? _objectiveTargetTemplateId,
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
                _ignoredSideQuests.Keys.OrderBy(questId => questId).ToArray(),
                _blockedMainStoryQuests.OrderBy(questId => questId).ToArray(),
                _completedCount,
                _suspensionCount,
                _reportAttemptCount);
        }
    }

    internal bool HasSelectedQuest
    {
        get
        {
            lock (_syncRoot)
                return _questId.HasValue;
        }
    }

    internal bool HasSupportedActiveQuest =>
        Volatile.Read(ref _hasSupportedActiveQuest) != 0;

    internal bool CanYieldForNearbyIntake(BotRuntime runtime, bool includeSideReport = false)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        lock (_syncRoot)
            return CanYieldForNearbyIntakeCore(runtime, includeSideReport);
    }

    internal bool YieldForNearbyIntake(BotRuntime runtime, DateTimeOffset now, bool includeSideReport = false)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        lock (_syncRoot)
        {
            if (!CanYieldForNearbyIntakeCore(runtime, includeSideReport))
                return false;

            var pausedQuestId = _questId;
            ResetPlan(runtime, releaseCombat: true, stopMovement: true);
            SetState(BotQuestLifecycleState.Idle, "nearby_quest_intake_preempted_lifecycle", now);
            Log(runtime.Bot.Id, "lifecycle_yielded",
                $"quest={pausedQuestId} reason=nearby_eligible_quest");
            return true;
        }
    }

    private bool CanYieldForNearbyIntakeCore(BotRuntime runtime, bool includeSideReport)
    {
#if !PLAYERBOTS_AAEMU_3_0
        if (!CanChangeWork(runtime)) return false;
#endif
        if (!_questId.HasValue || runtime.Bot?.Transform?.World == null)
            return false;

        var canYield = _state == BotQuestLifecycleState.SelectingTarget ||
                       _state == BotQuestLifecycleState.MovingToObjective ||
                       (_state == BotQuestLifecycleState.MovingToReport && (_selectedQuestMainStory || includeSideReport));
        if (!canYield || runtime.CombatState.IsActive || runtime.CombatState.Target != null ||
            runtime.Bot.CurrentTarget != null)
        {
            return false;
        }

        if (HasUnownedMovement(runtime) ||
            (!_ownedDestination.HasValue && runtime.MovementState.IsMoving))
            return false;

        return !_ownedDestination.HasValue ||
               !CurrentRequestedDestination(runtime).HasValue ||
               OwnsCurrentMovement(runtime);
    }

    private bool StepObjective(
        BotRuntime runtime,
        BotQuestSnapshot snapshot,
        BotConfig config,
        DateTimeOffset now)
    {
#if !PLAYERBOTS_AAEMU_3_0
        if (snapshot.ObjectiveShape == BotQuestObjectiveShape.Interaction && snapshot.Interaction is { } interaction)
            return StepInteraction(runtime, snapshot, interaction, config, now);
#endif
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
            if (objective.Matches(_objectiveTarget.TemplateId) && IsLiveOwnedTarget(runtime, _objectiveTarget.TemplateId))
            {
                if (CombatMadeNoProgress(runtime, config, now)) return true;
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
                objective,
                EffectiveRadius(config.SearchRadius, config.QuestObjectiveScanRadius),
                now) ?? [];
        }
        catch (Exception exception)
        {
            Suspend(runtime, config, $"target_scan_{exception.GetType().Name}", now);
            return true;
        }

        var target = targets.FirstOrDefault(candidate =>
            candidate != null && !TargetCoolingDown(candidate, now) && objective.Matches(candidate.TemplateId) && IsValidTarget(runtime, candidate, candidate.TemplateId));
        if (target == null)
        {
            if (_objectiveAreaEstablished || objective.Current > 0)
            {
                StopOwnedMovement(runtime);
                BeginRespawnWait(runtime, snapshot, objective.Current, objective.Required, config, now);
                return true;
            }

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
        }

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
        _objectiveAreaEstablished = true;
        _objectiveTarget = target;
        _objectiveTargetObjectId = target.ObjId;
        _beginCombat(runtime, target, target.TemplateId);
        SetState(BotQuestLifecycleState.Fighting, "objective_target_selected", now);
        Log(runtime.Bot.Id, "target_selected",
            $"quest={snapshot.QuestId} target_template={target.TemplateId} group={objective.MonsterGroupId} " +
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
                if (CombatMadeNoProgress(runtime, config, now)) return true;
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

        var target = targets.FirstOrDefault(candidate => !TargetCoolingDown(candidate, now) && IsValidGatherTarget(runtime, candidate));
        if (target == null)
        {
            if (_objectiveAreaEstablished || objective.Current > 0)
            {
                StopOwnedMovement(runtime);
                BeginRespawnWait(runtime, snapshot, objective.Current, objective.Required, config, now);
                return true;
            }

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
        }

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
        _objectiveAreaEstablished = true;
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
                _objectiveAreaEstablished = true;
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
                if (!_setDestination(runtime.Bot, selected.Position, true))
                {
                    Suspend(runtime, config, "objective_navigation_route_unavailable", now);
                    return true;
                }
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

        if (attempt.ConsumedByNativeDistribution)
        {
            var corpseObjectId = _objectiveTarget.ObjId;
            ReleaseGatherCombat(runtime);
            StopOwnedMovement(runtime);
            _lootApproachDeadline = null;
            _selectionDeadline = now + TimeSpan.FromMilliseconds(config.QuestTargetSelectionTimeoutMs);
            SetState(BotQuestLifecycleState.SelectingTarget, "native_party_loot_distributed", now);
            Log(runtime.Bot.Id, "gather_loot_distributed",
                $"quest={snapshot.QuestId} item={objective.ItemId} corpse={corpseObjectId} self_credit=unconfirmed");
            return true;
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
                if (!_setInteractionDestination(runtime.Bot, destination, true))
                {
                    Suspend(runtime, config, "gather_navigation_route_unavailable", now);
                    return true;
                }
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
        if (current > 0)
            _objectiveAreaEstablished = true;

        if (current < _objectiveCurrent)
        {
            Suspend(runtime, config, "objective_progress_regressed", now);
            return false;
        }

        if (current <= _objectiveCurrent)
            return true;

        var previous = _objectiveCurrent.Value;
        _objectiveCurrent = current;
        ClearQuestFailure(questId);
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
        if (now - _respawnWaitStartedAt.Value >= TimeSpan.FromMinutes(3))
        {
            Suspend(runtime, config, "objective_respawn_timeout", now);
            return;
        }
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
        _rewardIndex =
#if !PLAYERBOTS_AAEMU_3_0
            snapshot.PreferredRewardIndex ??
#endif
            BotQuestAuthority.SelectRewardIndex(snapshot.RewardIndices);
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
#if !PLAYERBOTS_AAEMU_3_0
#endif
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
#if PLAYERBOTS_AAEMU_3_0
                    _staticReportDestination = staticDestination.Position;
#else
                    if (!BotQuestApproachPlanner.TryForWorldObject(runtime.Bot.Transform.World.Position,
                            staticDestination.Position, (float)config.QuestReportInteractionRadius,
                            runtime.Bot.ParentWorld.GetHeight, runtime.Bot.ParentWorld.IsWater,
                            out var approach, config.PartyQuestEnabled ? runtime.Bot.Id : 0))
                    {
                        Suspend(runtime, config, "report_navigation_route_unavailable", now);
                        return true;
                    }
                    _staticReportDestination = approach;
#endif
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

                if (ReportTravelFailed(runtime, config, now))
                    return true;

                var destination = _staticReportDestination.Value;
                if (!_ownedDestination.HasValue ||
                    CurrentRequestedDestination(runtime) == null ||
                    Vector3.Distance(_ownedDestination.Value, destination) > DestinationTolerance)
                {
                    if (!_setInteractionDestination(runtime.Bot, destination, true))
                    {
                        Suspend(runtime, config, "report_navigation_route_unavailable", now);
                        return true;
                    }
                    _ownedDestination = destination;
                    ExtendTravelSelectionDeadline(config, now, Math.Max(Vector3.Distance(runtime.Bot.Transform.World.Position, destination),
                        runtime.MovementState.TravelRemainingDistance));
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

            if (_state != BotQuestLifecycleState.MovingToReport)
                ExtendTravelSelectionDeadline(config, now, reportObject.Distance);
            if (ReportTravelFailed(runtime, config, now))
                return true;

            // Keep following the accepted route while it is still ours. Recomputing
            // an off-mesh NPC approach point from the bot's changing position makes
            // that point drift every brain tick. The travel manager intentionally
            // deduplicates sub-0.5 m changes, so claiming the drifted point as owned
            // could make our ownership value disagree with the actual route.
            if (!_ownedDestination.HasValue || CurrentRequestedDestination(runtime) == null)
            {
                var targetPosition = reportObject.Object.Transform.World.Position;
#if PLAYERBOTS_AAEMU_3_0
                var destination = BotQuestApproachPlanner.ForWorldObject(
                    runtime.Bot.Transform.World.Position,
                    targetPosition,
                    (float)config.QuestReportInteractionRadius,
                    runtime.Bot.ParentWorld.GetHeight
#if !PLAYERBOTS_AAEMU_3_0
                    , config.PartyQuestEnabled ? runtime.Bot.Id : 0
#endif
                    );
                if (!_setInteractionDestination(runtime.Bot, destination, true))
#else
                if (!BotQuestApproachPlanner.TryForWorldObject(runtime.Bot.Transform.World.Position,
                        targetPosition, (float)config.QuestReportInteractionRadius,
                        runtime.Bot.ParentWorld.GetHeight, runtime.Bot.ParentWorld.IsWater,
                        out var destination, config.PartyQuestEnabled ? runtime.Bot.Id : 0) ||
                    !_setInteractionDestination(runtime.Bot, destination, true))
#endif
                {
                    Suspend(runtime, config, "report_navigation_route_unavailable", now);
                    return true;
                }
                _ownedDestination = destination;
                ExtendTravelSelectionDeadline(config, now, Math.Max(reportObject.Distance,
                    runtime.MovementState.TravelRemainingDistance));
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
#if !PLAYERBOTS_AAEMU_3_0
#endif
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

    private bool BeginQuest(
        BotRuntime runtime,
        BotQuestSnapshot snapshot,
        BotConfig config,
        DateTimeOffset now)
    {
        var releasedMovement = ResetPlan(runtime, releaseCombat: true, stopMovement: true);
        _questId = snapshot.QuestId;
        _selectedQuestMainStory = snapshot.MainStory;
        _selectionDeadline = now + TimeSpan.FromMilliseconds(config.QuestTargetSelectionTimeoutMs);
        SetState(BotQuestLifecycleState.SelectingTarget, "active_quest_selected", now);
        Log(runtime.Bot.Id, "objective_selected",
            $"quest={snapshot.QuestId} shape={snapshot.ObjectiveShape.ToString().ToLowerInvariant()} " +
            $"main_story={snapshot.MainStory.ToString().ToLowerInvariant()} ready={snapshot.Ready.ToString().ToLowerInvariant()}");
        return releasedMovement;
    }

    private BotQuestSnapshot SelectSnapshot(
        BotRuntime runtime,
        IReadOnlyList<BotQuestSnapshot> snapshots,
        BotConfig config,
        DateTimeOffset now)
    {
        var eligible = snapshots?
            .Where(snapshot => IsSelectable(snapshot, now))
            .ToArray() ?? [];

#if !PLAYERBOTS_AAEMU_3_0
        // Party support is explicit ownership. It only applies when this member
        // can run the hinted native quest, never as a global story shortcut.
        if (CanChangeWork(runtime))
        {
            if (!runtime.PartyQuestPriorityIsAcceptance && runtime.PartyQuestPriorityQuestId is { } partyQuest)
            {
                var supported = eligible.FirstOrDefault(q => q.QuestId == partyQuest);
                if (supported != null) return supported;
            }
        }

        // Do not replace work while native combat, casts, reporting, loot, or
        // movement recovery owns the bot. The selected native snapshot remains
        // authoritative until a safe boundary is reached.
        if (!CanChangeWork(runtime) && _questId.HasValue)
        {
            var current = eligible.FirstOrDefault(snapshot => snapshot.QuestId == _questId.Value);
            if (current != null)
                return current;
        }
#endif

#if !PLAYERBOTS_AAEMU_3_0
        var candidates = eligible
            .Select(snapshot => new
            {
                Snapshot = snapshot,
                Distance = EstimateQuestTravelDistance(runtime, snapshot, config, now)
            })
            .ToArray();

        // Clear supported local optional work before beginning a regional story
        // route. A selected local quest stays put within this tier, which keeps
        // discovery from repeatedly changing an otherwise feasible next step.
        var localSides = candidates
            .Where(candidate => !candidate.Snapshot.MainStory &&
                                float.IsFinite(candidate.Distance) &&
                                candidate.Distance >= 0f &&
                                candidate.Distance <= LocalQuestVicinityRadius)
            .ToArray();
        if (localSides.Length > 0)
        {
            var selectedLocalSide = localSides.FirstOrDefault(candidate =>
                _questId.HasValue && candidate.Snapshot.QuestId == _questId.Value);
            if (selectedLocalSide != null)
                return selectedLocalSide.Snapshot;

            return localSides
                .OrderBy(candidate => candidate.Distance)
                .ThenBy(candidate => candidate.Snapshot.Ready ? 0 : 1)
                .ThenBy(candidate => candidate.Snapshot.QuestId)
                .Select(candidate => candidate.Snapshot)
                .First();
        }
        // Finish the selected quest before reconsidering the route. This mirrors
        // the current-target bias used by mature playerbot travel systems and
        // prevents a newly accepted quest from causing cross-zone thrashing.
        if (_questId.HasValue)
        {
            var current = candidates.FirstOrDefault(candidate => candidate.Snapshot.QuestId == _questId.Value);
            if (current != null && (current.Snapshot.MainStory ||
                                    !candidates.Any(candidate => candidate.Snapshot.MainStory)))
                return current.Snapshot;
        }

        if (_preferStoryAfterStall)
        {
            var story = candidates.Where(q => q.Snapshot.MainStory)
                .OrderBy(q => q.Distance).ThenBy(q => q.Snapshot.QuestId)
                .Select(q => q.Snapshot).FirstOrDefault();
            if (story != null) { _preferStoryAfterStall = false; return story; }
        }

        return candidates
            // Local side work has already been handled above. Do not let an
            // unknown or regional optional route outrank available story work.
            .OrderBy(candidate => candidate.Snapshot.MainStory ? 0 : 1)
            .ThenBy(candidate => candidate.Distance)
            .ThenBy(candidate => candidate.Snapshot.Ready ? 0 : 1)
            .ThenBy(candidate => candidate.Snapshot.MainStory ? 0 : 1)
            .ThenBy(candidate => candidate.Snapshot.QuestId)
            .Select(candidate => candidate.Snapshot)
            .FirstOrDefault();
#else
        // Preserve the released 3.0 selection semantics.
        if (_questId.HasValue)
        {
            var current = eligible.FirstOrDefault(snapshot => snapshot.QuestId == _questId.Value);
            if (current != null)
                return current;
        }

        if (_preferStoryAfterStall)
        {
            var story = eligible.Where(q => q.MainStory)
                .OrderBy(q => EstimateQuestTravelDistance(runtime, q, config, now)).FirstOrDefault();
            if (story != null) { _preferStoryAfterStall = false; return story; }
        }

        return eligible
            .Select(snapshot => new
            {
                Snapshot = snapshot,
                Distance = EstimateQuestTravelDistance(runtime, snapshot, config, now)
            })
            .OrderBy(candidate => candidate.Distance <= LocalQuestVicinityRadius ? 0 : 1)
            .ThenBy(candidate => candidate.Distance)
            .ThenBy(candidate => candidate.Snapshot.Ready ? 0 : 1)
            .ThenBy(candidate => candidate.Snapshot.MainStory ? 0 : 1)
            .ThenBy(candidate => candidate.Snapshot.QuestId)
            .Select(candidate => candidate.Snapshot)
            .FirstOrDefault();
#endif
    }

    internal static bool CanExecuteSnapshot(BotQuestSnapshot snapshot)
    {
        if (snapshot == null || snapshot.QuestId == 0)
            return false;

        if (snapshot.Ready)
            return snapshot.ReportEndpoints?.Length == 1;

        return snapshot.ObjectiveShape switch
        {
            BotQuestObjectiveShape.MonsterHunt => snapshot.MonsterHunt.HasValue,
            BotQuestObjectiveShape.ItemGather => snapshot.ItemGather.HasValue,
#if !PLAYERBOTS_AAEMU_3_0
            BotQuestObjectiveShape.Interaction => snapshot.Interaction.HasValue,
#endif
            _ => false
        };
    }

    private bool IsSelectable(BotQuestSnapshot snapshot, DateTimeOffset now)
    {
        if (!CanExecuteSnapshot(snapshot))
            return false;
#if !PLAYERBOTS_AAEMU_3_0
        if (_mainStoryRetryAfter.TryGetValue(snapshot.QuestId, out var retryAfter) && retryAfter > now)
            return false;
#endif
        if (snapshot.MainStory)
            return true;
        return !_ignoredSideQuests.TryGetValue(snapshot.QuestId, out var until) || until <= now;
    }

    private bool HasRunnableOrMainStoryQuest(
        IReadOnlyList<BotQuestSnapshot> snapshots,
        DateTimeOffset now)
    {
        for (var index = 0; index < snapshots.Count; index++)
        {
            var snapshot = snapshots[index];
            if (snapshot is { QuestId: not 0 } &&
                (snapshot.MainStory || IsSelectable(snapshot, now)))
            {
                return true;
            }
        }
        return false;
    }

    private void ReconcileQuestDispositions(
        BotRuntime runtime,
        IReadOnlyList<BotQuestSnapshot> snapshots,
        DateTimeOffset now)
    {
#if !PLAYERBOTS_AAEMU_3_0
        foreach (var entry in _mainStoryRetryAfter.ToArray())
        {
            if (!ContainsQuest(snapshots, entry.Key))
                _mainStoryRetryAfter.Remove(entry.Key);
            else if (entry.Value <= now)
            {
                _mainStoryRetryAfter.Remove(entry.Key);
                _preferStoryAfterStall = true;
                Log(runtime.Bot.Id, "reconsidered", $"quest={entry.Key} reason=main_story_cooldown_elapsed");
            }
        }
#endif
        foreach (var quest in _sideProgress.Keys.Where(id => !ContainsQuest(snapshots, id)).ToArray())
            _sideProgress.Remove(quest);
        if (_questFailureCounts.Count > 0)
        {
            _dispositionScratch.Clear();
            foreach (var questId in _questFailureCounts.Keys)
            {
                if (!ContainsQuest(snapshots, questId))
                    _dispositionScratch.Add(questId);
            }
            for (var index = 0; index < _dispositionScratch.Count; index++)
                _questFailureCounts.Remove(_dispositionScratch[index]);
        }

        if (_ignoredSideQuests.Count > 0)
        {
            _dispositionScratch.Clear();
            foreach (var questId in _ignoredSideQuests.Keys)
            {
                if (!ContainsQuest(snapshots, questId))
                    _dispositionScratch.Add(questId);
            }
            for (var index = 0; index < _dispositionScratch.Count; index++)
                _ignoredSideQuests.Remove(_dispositionScratch[index]);
        }

        if (_blockedMainStoryQuests.Count > 0)
        {
            _dispositionScratch.Clear();
            foreach (var questId in _blockedMainStoryQuests)
            {
                if (!ContainsQuest(snapshots, questId))
                    _dispositionScratch.Add(questId);
            }
            for (var index = 0; index < _dispositionScratch.Count; index++)
                _blockedMainStoryQuests.Remove(_dispositionScratch[index]);
        }

        _dispositionScratch.Clear();
        foreach (var entry in _ignoredSideQuests)
        {
            if (entry.Value != DateTimeOffset.MaxValue && entry.Value <= now)
                _dispositionScratch.Add(entry.Key);
        }
        for (var index = 0; index < _dispositionScratch.Count; index++)
        {
            var expired = _dispositionScratch[index];
            _ignoredSideQuests.Remove(expired);
            _questFailureCounts.Remove(expired);
            Log(runtime.Bot.Id, "reconsidered", $"quest={expired} reason=quarantine_elapsed");
        }

        for (var index = 0; index < snapshots.Count; index++)
        {
            var snapshot = snapshots[index];
            if (snapshot is not { QuestId: not 0 })
                continue;

            if (CanExecuteSnapshot(snapshot))
            {
                if (_ignoredSideQuests.TryGetValue(snapshot.QuestId, out var until) &&
                    until == DateTimeOffset.MaxValue)
                {
                    _ignoredSideQuests.Remove(snapshot.QuestId);
                    _questFailureCounts.Remove(snapshot.QuestId);
                    Log(runtime.Bot.Id, "reconsidered",
                        $"quest={snapshot.QuestId} reason=objective_became_supported");
                }
                continue;
            }

            if (snapshot.MainStory)
            {
                if (_blockedMainStoryQuests.Add(snapshot.QuestId))
                {
                    Log(runtime.Bot.Id, "main_story_blocked",
                        $"quest={snapshot.QuestId} reason={snapshot.Reason}");
                }
                continue;
            }

            if (!_ignoredSideQuests.ContainsKey(snapshot.QuestId))
            {
                _ignoredSideQuests[snapshot.QuestId] = DateTimeOffset.MaxValue;
                Log(runtime.Bot.Id, "ignored",
                    $"quest={snapshot.QuestId} reason={snapshot.Reason} until=objective_supported");
            }
        }
    }

    private static bool ContainsQuest(
        IReadOnlyList<BotQuestSnapshot> snapshots,
        uint questId)
    {
        for (var index = 0; index < snapshots.Count; index++)
        {
            if (snapshots[index]?.QuestId == questId)
                return true;
        }
        return false;
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

#if !PLAYERBOTS_AAEMU_3_0
            if (snapshot.Interaction is { } interaction)
            {
                var plan = _authority.FindInteraction(runtime, interaction,
                    EffectiveRadius(config.SearchRadius, config.QuestObjectiveScanRadius));
                return plan.Available ? Vector3.Distance(botPosition, plan.Position) : float.PositiveInfinity;
            }
#endif
            IReadOnlyList<Npc> targets = snapshot.ObjectiveShape switch
            {
                BotQuestObjectiveShape.MonsterHunt when snapshot.MonsterHunt.HasValue =>
                    _authority.FindMonsterTargets(
                        runtime,
                        snapshot.MonsterHunt.Value,
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
        CurrentRequestedDestination(runtime).HasValue && !OwnsCurrentMovement(runtime);

    private static float EffectiveRadius(double generalRadius, double configuredRadius) =>
        (float)Math.Min(
            MaximumWorldScanRadius,
            Math.Min(
                double.IsFinite(generalRadius) ? Math.Max(0d, generalRadius) : 0d,
                double.IsFinite(configuredRadius) ? Math.Max(0d, configuredRadius) : 0d));

    private bool ReportTravelFailed(BotRuntime runtime, BotConfig config, DateTimeOffset now)
    {
        // A mover can accept a route, then reject a later waypoint and clear it.
        // Losing that route before arrival is a failed attempt, not permission
        // to resubmit it every brain tick. Do not use LastNavigationDecision:
        // it can belong to an earlier route or another movement owner.
        if (_ownedDestination is { } destination &&
            CurrentRequestedDestination(runtime) == null &&
            Vector3.Distance(runtime.Bot.Transform.World.Position, destination) >
                config.QuestReportInteractionRadius)
        {
            Suspend(runtime, config, "report_navigation_route_unavailable", now);
            return true;
        }

        if (_selectionDeadline.HasValue && now >= _selectionDeadline.Value)
        {
            Suspend(runtime, config, "report_route_timeout", now);
            return true;
        }

        return false;
    }

    private void Suspend(BotRuntime runtime, BotConfig config, string reason, DateTimeOffset now)
    {
#if !PLAYERBOTS_AAEMU_3_0
        if (reason is not "report_navigation_route_unavailable" and not "report_route_timeout")
#endif
#if !PLAYERBOTS_AAEMU_3_0
        if (runtime.MovementState.Climb is { } climb) climb.Descending = true;
#endif
#if !PLAYERBOTS_AAEMU_3_0
        _interactionPlan = null;
        _pendingInteraction = null;
        // Keep an in-flight observation window across temporary ownership conflicts.
#endif
        ReleaseObjectiveCombat(runtime);
        StopOwnedMovement(runtime);
        _reportObjectId = null;
        // Refresh the authoritative endpoint and travel budget on a later attempt.
        _staticReportDestination = null;
        _progressObservationUntil = null;
        _completionObservationUntil = null;
        _selectionDeadline = now + TimeSpan.FromMilliseconds(config.QuestTargetSelectionTimeoutMs);
        _retryAt = now + TimeSpan.FromMilliseconds(config.QuestCompletionRetryBackoffMs);
        if (SetState(BotQuestLifecycleState.Suspended, reason, now))
        {
            _suspensionCount++;
            Log(runtime.Bot.Id, "suspended",
                $"quest={_questId?.ToString() ?? "none"} reason={reason} retry_at={Timestamp(_retryAt.Value)}");
            ApplyQuestFailurePolicy(runtime, reason, now);
#if !PLAYERBOTS_AAEMU_3_0
            if (reason is "report_navigation_route_unavailable" or "report_route_timeout" or
                "objective_navigation_route_unavailable" or "objective_route_timeout")
                runtime.MovementState.SafeRecovery.FailedRoute(runtime, config, now.UtcDateTime,
                    $"quest_route_{_questId}", runtime.Mover ?? (AAEmu.Game.Bots.Body.IBotMover)AAEmu.Game.Bots.Body.BotManagerMover.Instance);
#endif
        }
    }

    private void ApplyQuestFailurePolicy(BotRuntime runtime, string reason, DateTimeOffset now)
    {
        if (!_questId.HasValue || !CountsTowardQuestQuarantine(reason))
            return;

        var questId = _questId.Value;
        var failures = _questFailureCounts.GetValueOrDefault(questId) + 1;
        _questFailureCounts[questId] = failures;
        if (failures < SideQuestFailureThreshold)
            return;

        if (_selectedQuestMainStory)
        {
            if (_blockedMainStoryQuests.Add(questId))
            {
                Log(runtime.Bot.Id, "main_story_blocked",
                    $"quest={questId} reason={reason} failures={failures}");
            }
            var cooldownUntil = now + MainStoryFailureCooldown;
#if !PLAYERBOTS_AAEMU_3_0
            // Back off this quest, not every accepted quest. Keep the story
            // visible as blocked and eligible again after its own cooldown.
            _mainStoryRetryAfter[questId] = cooldownUntil;
            Log(runtime.Bot.Id, "deferred", $"quest={questId} reason=main_story_cooldown until={Timestamp(cooldownUntil)}");
            ResetPlan(runtime, releaseCombat: true, stopMovement: true);
            SetState(BotQuestLifecycleState.Idle, "main_story_cooldown", now);
#else
            if (!_retryAt.HasValue || _retryAt.Value < cooldownUntil)
                _retryAt = cooldownUntil;
#endif
            return;
        }

        var until = now + SideQuestQuarantine;
        _ignoredSideQuests[questId] = until;
        Log(runtime.Bot.Id, "ignored",
            $"quest={questId} reason={reason} failures={failures} until={Timestamp(until)}");
        ResetPlan(runtime, releaseCombat: true, stopMovement: true);
        SetState(BotQuestLifecycleState.Idle, "side_quest_quarantined", now);
    }

    private static bool CountsTowardQuestQuarantine(string reason) =>
        reason != null &&
        (reason.EndsWith("_timeout", StringComparison.Ordinal) ||
         reason.EndsWith("_route_unavailable", StringComparison.Ordinal) ||
         reason.Contains("_scan_", StringComparison.Ordinal) ||
         reason is "authoritative_report_rejected" or
             "completion_not_observed" or
             "ready_state_not_observed" or
             "invalid_report_endpoint" or
             "invalid_reward_set" or
             "invalid_monster_hunt_state" or
             "invalid_item_gather_state" or
             "objective_progress_regressed" or
             "invalid_interaction_counter" or
             "interaction_no_native_progress" or
             "interaction_route_rejected" ||
         reason.StartsWith("quest_doodad_", StringComparison.Ordinal) ||
         reason.StartsWith("quest_item_", StringComparison.Ordinal) ||
         reason.StartsWith("native_skill_", StringComparison.Ordinal));

    private void ClearQuestFailure(uint questId)
    {
        _questFailureCounts.Remove(questId);
        _blockedMainStoryQuests.Remove(questId);
#if !PLAYERBOTS_AAEMU_3_0
        _mainStoryRetryAfter.Remove(questId);
#endif
    }

    private void Complete(BotRuntime runtime, DateTimeOffset now)
    {
        var completedQuestId = _questId;
        if (completedQuestId.HasValue)
        {
            ClearQuestFailure(completedQuestId.Value);
            _ignoredSideQuests.Remove(completedQuestId.Value);
        }
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
        if (removedQuestId.HasValue)
        {
            ClearQuestFailure(removedQuestId.Value);
            _ignoredSideQuests.Remove(removedQuestId.Value);
        }
        ResetPlan(runtime, releaseCombat: true, stopMovement: true);
        SetState(BotQuestLifecycleState.Idle, "active_quest_removed", now);
        Log(runtime.Bot.Id, "released",
            $"quest={removedQuestId?.ToString() ?? "none"} reason=active_quest_removed");
    }

    private void Disable(BotRuntime runtime, DateTimeOffset now)
    {
        Volatile.Write(ref _hasSupportedActiveQuest, 0);
        _questFailureCounts.Clear();
        _ignoredSideQuests.Clear();
        _blockedMainStoryQuests.Clear();
#if !PLAYERBOTS_AAEMU_3_0
        _mainStoryRetryAfter.Clear();
#endif
        ResetPlan(runtime, releaseCombat: true, stopMovement: true);
        SetState(BotQuestLifecycleState.Disabled, "disabled", now);
    }

    private bool ResetPlan(BotRuntime runtime, bool releaseCombat, bool stopMovement)
    {
#if !PLAYERBOTS_AAEMU_3_0
        _interactionPlan = null;
        _pendingInteraction = null;
        _interactionObservationUntil = null;
#endif
        if (releaseCombat)
            ReleaseObjectiveCombat(runtime);
        var releasedMovement = stopMovement && StopOwnedMovement(runtime);
        _questId = null;
        _selectedQuestMainStory = false;
        _objectiveAreaEstablished = false;
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
        return releasedMovement;
    }

    private void ReleaseObjectiveCombat(BotRuntime runtime)
    {
        if (!_objectiveTargetTemplateId.HasValue && !_objectiveTargetObjectId.HasValue)
            return;

        _endCombat(runtime, _objectiveTarget?.TemplateId ?? _objectiveTargetTemplateId ?? 0, _objectiveTargetObjectId);
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

        _endCombat(runtime, _objectiveTarget?.TemplateId ?? _objectiveTargetTemplateId ?? 0, _objectiveTargetObjectId);
    }

    private bool StopOwnedMovement(BotRuntime runtime)
    {
        var stopped = false;
        if (OwnsCurrentMovement(runtime))
        {
            _stopMovement(runtime.Bot);
            stopped = true;
        }
        _ownedDestination = null;
        return stopped;
    }

    private bool OwnsCurrentMovement(BotRuntime runtime)
    {
        if (!_ownedDestination.HasValue ||
            CurrentRequestedDestination(runtime) is not { } destination ||
            Vector3.Distance(destination, _ownedDestination.Value) > DestinationTolerance)
        {
            return false;
        }

        return runtime.MovementState.TravelOwner is
            BotMovementOwner.None or BotMovementOwner.QuestLifecycle;
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
