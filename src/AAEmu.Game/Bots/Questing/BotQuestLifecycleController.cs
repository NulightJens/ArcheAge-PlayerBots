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
    Fighting,
    WaitingForProgress,
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
/// Executes only authoritative, single-objective monster-hunt quests. It owns
/// an exact combat filter/target or an exact report destination, observes
/// AAEmu quest state for progress, and delegates reporting to guarded native
/// quest APIs.
/// </summary>
public sealed class BotQuestLifecycleController
{
    internal const float MaximumWorldScanRadius = BotCombatTask.MaximumQuestTargetSearchRadius;
    private const float DestinationTolerance = 0.25f;

    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();

    private readonly object _syncRoot = new();
    private readonly IBotQuestAuthority _authority;
    private readonly Action<BotRuntime, Npc, uint> _beginCombat;
    private readonly Action<BotRuntime, uint, uint?> _endCombat;
    private readonly Action<Character, Vector3, bool> _setDestination;
    private readonly Action<Character> _stopMovement;
    private readonly Action<string> _eventSink;

    private BotQuestLifecycleState _state = BotQuestLifecycleState.Disabled;
    private uint? _questId;
    private uint? _objectiveTargetTemplateId;
    private byte? _objectiveIndex;
    private uint? _objectiveTargetObjectId;
    private Npc _objectiveTarget;
    private int? _objectiveCurrent;
    private int? _objectiveRequired;
    private BotQuestReportEndpoint? _reportEndpoint;
    private uint? _reportObjectId;
    private int? _rewardIndex;
    private Vector3? _ownedDestination;
    private DateTimeOffset? _decisionAt;
    private DateTimeOffset? _progressObservedAt;
    private DateTimeOffset? _reportAttemptedAt;
    private DateTimeOffset? _completedAt;
    private DateTimeOffset? _retryAt;
    private DateTimeOffset? _selectionDeadline;
    private DateTimeOffset? _progressObservationUntil;
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
            ((bot, destination, run) => BotManager.Instance.SetBotDestination(
                bot, destination.X, destination.Y, destination.Z, run));
        _stopMovement = stopMovement ?? (bot => BotManager.Instance.StopBot(bot));
        _eventSink = eventSink;
    }

    /// <summary>
    /// Returns true while a supported or safely suspended active quest owns
    /// the host tick. False yields immediately to intake/life behavior.
    /// </summary>
    internal bool Step(BotRuntime runtime, BotConfig config, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(config);

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
                if (_state == BotQuestLifecycleState.WaitingForCompletion)
                    Complete(runtime, now);
                else
                    ReleaseRemovedQuest(runtime, now);
                return false;
            }

            if (_retryAt.HasValue && _retryAt.Value > now)
                return snapshots.Count > 0;
            _retryAt = null;

            var snapshot = SelectSnapshot(snapshots);
            if (snapshot == null)
            {
                ResetPlan(runtime, releaseCombat: true, stopMovement: true);
                SetState(BotQuestLifecycleState.Idle, "no_active_quest", now);
                return false;
            }

            if (!_questId.HasValue || _questId.Value != snapshot.QuestId)
                BeginQuest(runtime, snapshot, config, now);

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
        if (snapshot.ObjectiveShape != BotQuestObjectiveShape.MonsterHunt ||
            !snapshot.MonsterHunt.HasValue)
        {
            Suspend(runtime, config, snapshot.Reason, now);
            return true;
        }

        var objective = snapshot.MonsterHunt.Value;
        if (objective.TargetNpcTemplateId == 0 || objective.Required <= 0 ||
            objective.Current < 0 || objective.Current > objective.Required)
        {
            Suspend(runtime, config, "invalid_monster_hunt_state", now);
            return true;
        }

        if (_objectiveIndex.HasValue &&
            (_objectiveIndex.Value != objective.ObjectiveIndex ||
             _objectiveTargetTemplateId != objective.TargetNpcTemplateId))
        {
            Suspend(runtime, config, "objective_changed", now);
            return true;
        }

        _objectiveIndex = objective.ObjectiveIndex;
        _objectiveTargetTemplateId = objective.TargetNpcTemplateId;
        _objectiveRequired = objective.Required;
        if (!_objectiveCurrent.HasValue)
            _objectiveCurrent = objective.Current;

        if (objective.Current < _objectiveCurrent)
        {
            Suspend(runtime, config, "objective_progress_regressed", now);
            return true;
        }

        if (objective.Current > _objectiveCurrent)
        {
            var previous = _objectiveCurrent.Value;
            _objectiveCurrent = objective.Current;
            _progressObservedAt = now;
            _selectionDeadline = now + TimeSpan.FromMilliseconds(config.QuestTargetSelectionTimeoutMs);
            _progressObservationUntil = null;
            ReleaseObjectiveCombat(runtime);
            Log(runtime.Bot.Id, "progress_observed",
                $"quest={snapshot.QuestId} objective={objective.ObjectiveIndex} " +
                $"from={previous} to={objective.Current} required={objective.Required}");
        }

        if (objective.Current >= objective.Required)
        {
            ReleaseObjectiveCombat(runtime);
            _completionObservationUntil ??=
                now + TimeSpan.FromMilliseconds(config.QuestCompletionObservationMs);
            if (now >= _completionObservationUntil.Value)
                Suspend(runtime, config, "ready_state_not_observed", now);
            else
                SetState(BotQuestLifecycleState.WaitingForReady, "awaiting_authoritative_ready", now);
            return true;
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

        if (_progressObservationUntil.HasValue)
        {
            if (now < _progressObservationUntil.Value)
            {
                SetState(BotQuestLifecycleState.WaitingForProgress, "awaiting_authoritative_credit", now);
                return true;
            }

            Log(runtime.Bot.Id, "no_credit",
                $"quest={snapshot.QuestId} current={objective.Current} required={objective.Required}");
            _progressObservationUntil = null;
        }

        if (_selectionDeadline.HasValue && now >= _selectionDeadline.Value)
        {
            Suspend(runtime, config, "target_selection_timeout", now);
            return true;
        }

        if (!CanBeginObjectiveCombat(runtime, out var combatReason))
        {
            Suspend(runtime, config, combatReason, now);
            return false;
        }

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

        var target = targets.FirstOrDefault(candidate => IsValidTarget(runtime, candidate, objective.TargetNpcTemplateId));
        if (target == null)
        {
            SetState(BotQuestLifecycleState.SelectingTarget, "no_valid_objective_target", now);
            return true;
        }

        _objectiveTarget = target;
        _objectiveTargetObjectId = target.ObjId;
        _beginCombat(runtime, target, objective.TargetNpcTemplateId);
        SetState(BotQuestLifecycleState.Fighting, "objective_target_selected", now);
        Log(runtime.Bot.Id, "target_selected",
            $"quest={snapshot.QuestId} target_template={objective.TargetNpcTemplateId} " +
            $"target_obj={target.ObjId} current={objective.Current} required={objective.Required}");
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
            StopOwnedMovement(runtime);
            _reportObjectId = null;
            if (_selectionDeadline.HasValue && now >= _selectionDeadline.Value)
                Suspend(runtime, config, "report_endpoint_timeout", now);
            else
                SetState(BotQuestLifecycleState.MovingToReport, "report_endpoint_not_nearby", now);
            return true;
        }

        _reportObjectId = reportObject.Object.ObjId;
        if (reportObject.Distance > config.QuestReportInteractionRadius)
        {
            if (HasUnownedMovement(runtime))
            {
                Suspend(runtime, config, "report_movement_ownership_lost", now);
                return false;
            }

            var destination = reportObject.Object.Transform.World.Position;
            if (!_ownedDestination.HasValue ||
                runtime.MovementState.Destination == null ||
                Vector3.Distance(_ownedDestination.Value, destination) > DestinationTolerance)
            {
                _setDestination(runtime.Bot, destination, true);
                _ownedDestination = destination;
                Log(runtime.Bot.Id, "report_move_requested",
                    $"quest={snapshot.QuestId} kind={EndpointName(endpoint.Kind)} " +
                    $"template={endpoint.TemplateId} object={reportObject.Object.ObjId} " +
                    $"distance={reportObject.Distance:F2}");
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

    private static BotQuestSnapshot SelectSnapshot(IReadOnlyList<BotQuestSnapshot> snapshots) =>
        snapshots?
            .Where(snapshot => snapshot != null && snapshot.QuestId != 0)
            .OrderBy(snapshot => snapshot.Ready ? 0 : 1)
            .ThenBy(snapshot => snapshot.MainStory ? 0 : 1)
            .ThenBy(snapshot => snapshot.QuestId)
            .FirstOrDefault();

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

    private static bool IsSameWorld(Character bot, BaseUnit target) =>
        bot?.ParentWorld != null && target?.ParentWorld != null &&
        ReferenceEquals(bot.ParentWorld, target.ParentWorld) &&
        bot.Transform?.World != null && target.Transform?.World != null;

    private bool HasUnownedMovement(BotRuntime runtime) =>
        runtime.MovementState.Destination is { } destination &&
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
        _objectiveIndex = null;
        _objectiveTargetObjectId = null;
        _objectiveTarget = null;
        _objectiveCurrent = null;
        _objectiveRequired = null;
        _reportEndpoint = null;
        _reportObjectId = null;
        _rewardIndex = null;
        _retryAt = null;
        _selectionDeadline = null;
        _progressObservationUntil = null;
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
}
