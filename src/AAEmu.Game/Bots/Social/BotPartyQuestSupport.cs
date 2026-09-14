#if !PLAYERBOTS_AAEMU_3_0
using System.Numerics;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Bots.Questing;
using AAEmu.Game.Models.Game.Bots;

namespace AAEmu.Game.Bots.Social;

internal sealed partial class BotPartyQuestCoordinator
{
    private uint _supportMember;
    private uint _supportQuest;
    private bool _supportAcceptance;
    private Vector3? _supportGround;
    private Vector3 _supportProgressPosition;
    private DateTimeOffset _supportProgressAt;
    private int? _supportObjective;
    private BotQuestLifecycleState _supportPhase;
    private bool _supportBlocked;
    private readonly Dictionary<uint, DateTimeOffset> _lastSupported = [];
    private readonly Dictionary<uint, DateTimeOffset> _supportRetry = [];
    private readonly Dictionary<uint, (Vector3 Position, DateTimeOffset At)> _supportWalk = [];
    private readonly List<Vector3> _supportTrail = [];
    private DateTimeOffset _supportRecoveryUntil;

    private void ClearSupport()
    {
        foreach (var member in _members)
        {
            member.PartyRegroupDetail = "none";
            member.PartyQuestPriorityQuestId = null;
            member.PartyQuestPriorityIsAcceptance = false;
            _personalSpace.Cancel(member);
            if (_supportMember != 0 && member.MovementState.TravelOwner == BotMovementOwner.PartyQuest)
                _stop(member);
        }
        _supportMember = 0; _supportQuest = 0; _supportGround = null;
        _supportAcceptance = false;
        _supportBlocked = false;
        _supportRetry.Clear(); _supportWalk.Clear();
        _supportTrail.Clear();
    }

    private static bool PendingAction(BotQuestLifecycleView view) => view.QuestId.HasValue &&
        view.State is not (BotQuestLifecycleState.Disabled or BotQuestLifecycleState.Idle or BotQuestLifecycleState.Suspended);

    private bool UpdateSupport(BotRuntime[] members, BotConfig config, bool spread, DateTimeOffset now)
    {
        var focus = members.FirstOrDefault(r => r.Bot.Id == _supportMember);
        if (focus != null)
        {
            var view = focus.QuestLifecycleController.Inspect();
            var valid = focus.PartyQuestPriorityIsAcceptance
                ? focus.QuestIntakeController.HasPartyStoryOpportunity(focus, _supportQuest, config, now)
                : NativeReadyWork(focus, _supportQuest, now) || view.QuestId == _supportQuest && PendingAction(view);
            if (focus.Bot.Quests.HasQuestCompleted(_supportQuest) || !valid)
            {
                _lastSupported[focus.Bot.Id] = now;
                ClearSupport(); focus = null;
            }
        }
        if (focus == null)
        {
            var candidates = SupportCandidates(members, config, now)
                .Where(c => !c.Runtime.Bot.Quests.HasQuestCompleted(c.Quest))
                .Where(c => spread || c.FinishedPeers > 0 || c.Acceptance)
                .OrderByDescending(c => c.FinishedPeers)
                .ThenByDescending(c => c.Acceptance)
                .ThenByDescending(c => c.Ready)
                .ThenBy(c => c.View.CompletedCount)
                .ThenBy(c => _lastSupported.GetValueOrDefault(c.Runtime.Bot.Id))
                .ThenBy(c => c.Runtime.Bot.Id).ToArray();
            if (candidates.Length == 0) return false;
            var chosen = candidates[0]; focus = chosen.Runtime;
            _supportMember = focus.Bot.Id; _supportQuest = chosen.Quest;
            _supportAcceptance = chosen.Acceptance;
            focus.PartyQuestPriorityQuestId = _supportQuest;
            focus.PartyQuestPriorityIsAcceptance = chosen.Acceptance;
            _regroupStartedAt ??= now;
            _supportPhase = chosen.View.State; _supportObjective = chosen.View.ObjectiveCurrent;
            _supportProgressPosition = focus.Bot.Transform.World.Position; _supportProgressAt = now;
            _waitingSince.Clear(); _nextDecision.Clear();
            Logger.Info($"BOT ev=party_support_started member={_supportMember} quest={_supportQuest} " +
                $"action={(chosen.Acceptance ? "accept" : chosen.Ready ? "report" : "objective")} reason=unfinished_quest_action");
        }

        var current = focus.QuestLifecycleController.Inspect();
        var position = focus.Bot.Transform.World.Position;
        // Track occupied ground, never the NPC/objective or an elevated climbing position.
        if (IsSafeGround(focus))
        {
            _supportGround = position;
            if (_supportTrail.Count > 0 && Vector3.Distance(_supportTrail[^1], position) > 12f)
                _supportTrail.Clear(); // Never join a teleport, fall, or missing occupied interval.
            if (_supportTrail.Count == 0 || Vector3.Distance(_supportTrail[^1], position) >= 2f)
            {
                _supportTrail.Add(position);
                if (_supportTrail.Count > 256) _supportTrail.RemoveAt(0);
            }
        }
        if (current.ObjectiveCurrent > _supportObjective ||
            current.ProgressObservedAt > _supportProgressAt)
        {
            _supportProgressPosition = position; _supportProgressAt = now;
            _supportObjective = current.ObjectiveCurrent; _supportPhase = current.State;
            _supportBlocked = false;
        }
        if (now - _supportProgressAt >= TimeSpan.FromMinutes(2))
        {
            // Repeated short routes or phase changes are not quest progress.
            // Yield all party movement for a bounded interval so every member
            // can replan its own quest route, finish reporting, or expire a side
            // quest. Preserve native party membership and every main quest.
            Logger.Info($"BOT ev=party_support_recovery member={_supportMember} quest={_supportQuest} reason=no_native_progress");
            ClearSupport();
            _supportRecoveryUntil = now.AddSeconds(60);
            return true;
        }
        return true;
    }

    // True means this tick belongs to support. The focus keeps its quest route;
    // companions yield their plans and approach its last observed safe ground.
    private bool StepSupport(BotRuntime runtime, DateTimeOffset now, bool busy)
    {
        // The selected member owns the action everyone is waiting for. Distance
        // may redirect companions, but cannot suspend that action's controller.
        if (runtime.Bot.Id == _supportMember) return false;
        if (busy) return false; // Never interrupt a native cast, fight, loot or climb.

        // Members already doing this turn-in should finish their own native
        // route and interaction. Following the moving reporter adds detours and
        // can stop them just before arrival. Missing acceptance still waits.
        var view = runtime.QuestLifecycleController.Inspect();
        if (!_supportAcceptance && (NativeReadyWork(runtime, _supportQuest, now) ||
            view.QuestId == _supportQuest &&
            view.State is BotQuestLifecycleState.MovingToReport or BotQuestLifecycleState.Reporting))
        {
            // Readmission starts with no selected lifecycle action. Native-ready
            // peers still share this report and must be allowed to select it.
            runtime.PartyQuestPriorityQuestId = _supportQuest;
            runtime.PartyQuestPriorityIsAcceptance = false;
            return false;
        }

        // A targetless combat label is not an active fight. The normal brain
        // is about to yield to party movement, so reconcile its temporary state.
        var combat = runtime.CombatState;
        if (combat.CurrentState == BotCombatStateType.Combat && combat.Target == null &&
            runtime.Bot.CurrentTarget == null && !runtime.Bot.IsInBattle && !combat.InDuel)
            combat.RestorePreviousState();
        runtime.PartyQuestSuppressBrain = true;
        runtime.QuestLifecycleController.PauseForParty(runtime, now);
        runtime.QuestIntakeController.YieldToQuestLifecycle(runtime, now);
        SetReason(runtime, _supportBlocked ? "party_blocked_quest_action" : "supporting_quest_member");
        runtime.PartyRegroupDetail = $"support_member={_supportMember} quest={_supportQuest}";
        FollowSupportMember(runtime, now);
        return true;
    }

    private void FollowSupportMember(BotRuntime runtime, DateTimeOffset now)
    {
        var state = runtime.MovementState; var id = runtime.Bot.Id;
        if (state.FollowTarget != null) return;
        if (state.TravelOwner == BotMovementOwner.External)
        {
            // Preserve a real manual order, but not an orphaned ownership label
            // left after a combat/direct move has already ended.
            if (state.Destination.HasValue || state.TravelDestination.HasValue ||
                state.TravelWaypointCount > 0 || state.IsMoving) return;
            state.TravelOwner = BotMovementOwner.None;
        }
        if (!_supportGround.HasValue)
        {
            runtime.PartyRegroupDetail = "support_waiting_for_safe_ground";
            if (state.TravelOwner == BotMovementOwner.PartyQuest) _stop(runtime);
            return;
        }
        var position = runtime.Bot.Transform.World.Position;
        var occupied = _supportGround.Value;
        if (Vector3.Distance(position, occupied) <= 10f)
        {
            if (_supportWalk.Remove(id) && state.TravelOwner == BotMovementOwner.PartyQuest) _stop(runtime);
            _personalSpace.Step(runtime, _members, now, true);
            return;
        }
        _personalSpace.Cancel(runtime);
        if (_supportWalk.TryGetValue(id, out var progress))
        {
            if (Vector3.Distance(position, progress.Position) >= 1f) _supportWalk[id] = (position, now);
            else if (now - progress.At >= TimeSpan.FromSeconds(30))
            {
                if (state.TravelOwner == BotMovementOwner.PartyQuest) _stop(runtime);
                _supportWalk.Remove(id); _supportRetry[id] = now.AddSeconds(30);
                runtime.PartyRegroupDetail = "support_route_stalled";
                return;
            }
        }
        var trailTarget = SelectOccupiedTrailTarget(position, _supportTrail);
        var followPoint = trailTarget ?? occupied;
        if (state.TravelOwner == BotMovementOwner.PartyQuest && state.Destination.HasValue &&
            state.TravelDestination is { } destination && Vector3.Distance(destination, followPoint) < 4f) return;
        if (_supportRetry.TryGetValue(id, out var retry) && now < retry) return;
        if (state.TravelOwner == BotMovementOwner.PartyQuest) _stop(runtime);
        // Intermediate breadcrumbs are already occupied corridor points. Keep
        // cosmetic spacing at the final rendezvous, not across corridor corners.
        var target = trailTarget ?? occupied + SlotOffset(id);
        try
        {
            // Cosmetic offsets must stay on the occupied surface. Fall back to
            // occupied ground if a shoulder is not suitable; native routing validates travel.
            var ground = _height(runtime, target);
            if (!float.IsFinite(ground) || MathF.Abs(ground - followPoint.Z) > .3f) target = followPoint;
            else target.Z = ground;
            _supportRetry[id] = now.AddSeconds(3);
            if (_route(runtime, target)) _supportWalk.TryAdd(id, (position, now));
            else { _supportRetry[id] = now.AddSeconds(30); runtime.PartyRegroupDetail = "support_route_unavailable"; }
        }
        catch
        {
            _supportRetry[id] = now.AddSeconds(30);
            runtime.PartyRegroupDetail = "support_route_unavailable";
        }
    }

    internal static Vector3? SelectOccupiedTrailTarget(Vector3 position, IReadOnlyList<Vector3> trail)
    {
        if (trail.Count < 2) return null;
        var closest = -1; var best = 12f;
        for (var i = 0; i < trail.Count; i++)
        {
            var distance = Vector3.Distance(position, trail[i]);
            if (distance < best) { closest = i; best = distance; }
        }
        if (closest < 0) return null;
        var selected = closest;
        var length = best;
        for (var i = closest + 1; i < trail.Count; i++)
        {
            var segment = Vector3.Distance(trail[i - 1], trail[i]);
            if (segment > 12f || length + segment > 12f) break;
            length += segment; selected = i;
        }
        return trail[selected];
    }
}
#endif
