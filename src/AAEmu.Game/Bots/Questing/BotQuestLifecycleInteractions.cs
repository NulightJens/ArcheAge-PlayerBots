#if !PLAYERBOTS_AAEMU_3_0
using System.Numerics;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Models.Game.Bots;

namespace AAEmu.Game.Bots.Questing;

public sealed partial class BotQuestLifecycleController
{
    private BotInteractionPlan _interactionPlan;
    private IPendingAction _pendingInteraction;
    private DateTimeOffset? _interactionObservationUntil;

    private bool StepInteraction(BotRuntime runtime, BotQuestSnapshot snapshot,
        BotQuestInteractionObjective objective, BotConfig config, DateTimeOffset now)
    {
        if (objective.Required <= 0 || objective.Current < 0 || objective.Current > objective.Required)
        {
            Suspend(runtime, config, "invalid_interaction_counter", now);
            return true;
        }
        if (runtime.MovementState.Climb == null && !CanOwnObjectiveTravel(runtime, out var conflict))
        {
            Suspend(runtime, config, conflict, now);
            return true;
        }
        if (_objectiveIndex.HasValue && _objectiveIndex.Value != objective.Index)
        {
            Suspend(runtime, config, "interaction_objective_changed", now);
            return true;
        }
        if (_objectiveCurrent.HasValue && objective.Current < _objectiveCurrent.Value)
        {
            Suspend(runtime, config, "objective_progress_regressed", now);
            return true;
        }
        if (_objectiveCurrent.HasValue && objective.Current != _objectiveCurrent.Value)
        {
            _progressObservedAt = now;
            _interactionObservationUntil = null;
            _interactionPlan = null;
            Log(runtime.Bot.Id, "interaction_progress",
                $"quest={snapshot.QuestId} current={objective.Current} required={objective.Required}");
        }
        _objectiveIndex = objective.Index;
        _objectiveCurrent = objective.Current;
        _objectiveRequired = objective.Required;
        _objectiveItemId = objective.ItemId == 0 ? null : objective.ItemId;
        if (objective.Current == objective.Required)
        {
            StopOwnedMovement(runtime);
            return WaitForReady(runtime, config, now);
        }
        if (_pendingInteraction is { } pending)
        {
            if (!pending.Finished || runtime.Bot.SkillTask != null || runtime.Bot.ActivePlotState != null)
            {
                SetState(BotQuestLifecycleState.WaitingForProgress, "awaiting_native_interaction_delivery", now);
                return true;
            }
            _pendingInteraction = null;
            if (pending.Failure != null || pending.ClientAccepted != true ||
                pending.NativeResult != AAEmu.Game.Models.Game.Skills.Static.SkillResult.Success)
            {
                _interactionPlan = null;
                Suspend(runtime, config, pending.Failure ?? "native_interaction_rejected", now);
                return true;
            }
            _lootApproachDeadline = null;
            _interactionObservationUntil = now.AddMilliseconds(Math.Max(config.QuestProgressObservationMs, 1000));
        }
        if (_interactionObservationUntil is { } until)
        {
            if (now < until)
            {
                SetState(BotQuestLifecycleState.WaitingForProgress, "awaiting_native_interaction_progress", now);
                return true;
            }
            _interactionObservationUntil = null;
            _interactionPlan = null;
            Suspend(runtime, config, "interaction_no_native_progress", now);
            return true;
        }
        try
        {
            if (_interactionPlan == null)
            {
                _lootApproachDeadline = null;
                _interactionPlan = _authority.FindInteraction(runtime, objective,
                    EffectiveRadius(config.SearchRadius, config.QuestObjectiveScanRadius)) with
                    { World = runtime.Bot.ParentWorld, QuestId = snapshot.QuestId, Objective = objective };
            }
            if (!_interactionPlan.Available)
            {
                var reason = _interactionPlan.Reason;
                _interactionPlan = null;
                Suspend(runtime, config, reason, now);
                return true;
            }
            var plan = _interactionPlan;
            _objectiveTargetTemplateId = plan.DoodadTemplateId == 0 ? null : plan.DoodadTemplateId;
            _objectiveTargetObjectId = plan.DoodadObjectId == 0 ? null : plan.DoodadObjectId;
            var position = runtime.Bot.Transform.World.Position;
            if (plan.Climb is { } climbPlan)
            {
                if (runtime.MovementState.Climb is { } motion)
                {
                    if (!motion.AtHeight || motion.Descending)
                    {
                        SetState(BotQuestLifecycleState.MovingToObjective, "climbing_to_quest_interaction", now);
                        return true;
                    }
                }
                else
                {
                    _lootApproachDeadline ??= now.AddSeconds(60);
                    if (now >= _lootApproachDeadline)
                    {
                        Suspend(runtime, config, "climb_anchor_approach_timeout", now);
                        return true;
                    }
                    if (!BotQuestInteractions.InRange(position, climbPlan.Approach, 1.25f))
                    {
                        if (!_ownedDestination.HasValue)
                        {
                            if (!_setInteractionDestination(runtime.Bot, climbPlan.Approach, true))
                            {
                                Suspend(runtime, config, "climb_anchor_route_rejected", now);
                                return true;
                            }
                            _ownedDestination = climbPlan.Approach;
                        }
                        SetState(BotQuestLifecycleState.MovingToObjective, "approaching_climb_anchor", now);
                        return true;
                    }
                    if (_ownedDestination.HasValue || runtime.MovementState.IsMoving)
                    {
                        StopOwnedMovement(runtime);
                        return true;
                    }
                    if (!BotClimbMotion.Begin(runtime.Bot, runtime.MovementState, climbPlan, snapshot.QuestId, now))
                    {
                        Suspend(runtime, config, "climb_attachment_rejected", now);
                        return true;
                    }
                    _lootApproachDeadline = null;
                    Log(runtime.Bot.Id, "climb_started", $"quest={snapshot.QuestId} anchor={climbPlan.ObjectId} target_z={climbPlan.TargetHeight:F2}");
                    SetState(BotQuestLifecycleState.MovingToObjective, "climbing_to_quest_interaction", now);
                    return true;
                }
            }

            if ((plan.DoodadObjectId != 0 || plan.LocationRequired) &&
                !BotQuestInteractions.InRange(position, plan.Position, plan.Range))
            {
                if (_ownedDestination.HasValue && CurrentRequestedDestination(runtime) == null)
                {
                    // Movement rejected or abandoned this route. A stale local destination
                    // must not leave an interaction waiting motionless until its old deadline.
                    _interactionPlan = null;
                    Suspend(runtime, config, "interaction_navigation_route_unavailable", now);
                    return true;
                }
                _lootApproachDeadline ??= now + TimeSpan.FromSeconds(
                    Math.Clamp(Vector3.Distance(position, plan.Position) / ConservativeTravelSpeed + 15, 15, 180));
                if (now >= _lootApproachDeadline)
                {
                    _interactionPlan = null;
                    Suspend(runtime, config, "interaction_approach_timeout", now);
                    return true;
                }
                if (!_ownedDestination.HasValue)
                {
                    if (!BotQuestApproachPlanner.TryForWorldObject(position, plan.Position,
                            plan.Range, runtime.Bot.ParentWorld.GetHeight, runtime.Bot.ParentWorld.IsWater,
                            out var destination, config.PartyQuestEnabled ? runtime.Bot.Id : 0) ||
                        !_setInteractionDestination(runtime.Bot, destination, true))
                    {
                        _interactionPlan = null;
                        Suspend(runtime, config, "interaction_route_rejected", now);
                        return true;
                    }
                    _ownedDestination = destination;
                    // A road detour may be much longer than straight-line distance.
                    var remaining = runtime.MovementState.TravelRemainingDistance;
                    if (float.IsFinite(remaining) && remaining > Vector3.Distance(position, plan.Position))
                        _lootApproachDeadline = now.AddSeconds(Math.Clamp(remaining / ConservativeTravelSpeed + 15, 15, 180));
                }
                SetState(BotQuestLifecycleState.MovingToObjective, "moving_to_quest_interaction", now);
                return true;
            }
            if (_ownedDestination.HasValue || runtime.MovementState.IsMoving)
            {
                if (runtime.MovementState.Climb != null)
                {
                    // The mover emits a stationary hanging packet on its next tick.
                    // StopBot would detach us before the elevated interaction.
                    SetState(BotQuestLifecycleState.SelectingTarget, "settling_at_climb_interaction", now);
                    return true;
                }
                StopOwnedMovement(runtime);
                SetState(BotQuestLifecycleState.SelectingTarget, "stopping_for_quest_interaction", now);
                return true;
            }
            var attempt = _authority.ExecuteInteraction(runtime.Bot, plan);
            Log(runtime.Bot.Id, "interaction_dispatched",
                $"quest={snapshot.QuestId} kind={objective.Kind} skill={plan.SkillId} " +
                $"item={plan.ItemId}:{plan.ItemInstanceId} doodad={plan.DoodadTemplateId}:{plan.DoodadObjectId} " +
                $"started={attempt.Started} reason={attempt.Reason}");
            if (attempt.Pending != null)
            {
                _pendingInteraction = attempt.Pending;
                SetState(BotQuestLifecycleState.WaitingForProgress, "awaiting_native_interaction_delivery", now);
                return true;
            }
            if (!attempt.Started)
            {
                _interactionPlan = null;
                Suspend(runtime, config, attempt.Reason, now);
                return true;
            }
            _lootApproachDeadline = null;
            _interactionObservationUntil = now + TimeSpan.FromMilliseconds(
                Math.Max(config.QuestProgressObservationMs, 1000) + plan.DurationMs);
            SetState(BotQuestLifecycleState.WaitingForProgress, "awaiting_native_interaction_progress", now);
            return true;
        }
        catch (Exception exception)
        {
            _interactionPlan = null;
            Suspend(runtime, config, $"interaction_{exception.GetType().Name}", now);
            return true;
        }
    }
}
#endif
