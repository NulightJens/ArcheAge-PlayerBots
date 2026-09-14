#if !PLAYERBOTS_AAEMU_3_0
using System.Numerics;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Bots.Questing;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Quests.Templates;
using NLog;

namespace AAEmu.Game.Bots.Social;

internal sealed partial class BotPartyQuestCoordinator
{
    private readonly BotPartyPersonalSpace _personalSpace;
    private readonly Dictionary<uint, string> _holds = [];
    private readonly Dictionary<uint, DateTimeOffset> _waitingSince = [];
    private readonly Dictionary<uint, DateTimeOffset> _nextDecision = [];
    private readonly Dictionary<uint, uint> _decisions = [];
    private DateTimeOffset _nextScan;
    private bool _regrouping;
    private bool _returning;
    private DateTimeOffset? _regroupStartedAt;
    private DateTimeOffset? _unavailableSince;
    private BotRuntime[] _members = [];
    private readonly Dictionary<uint, BotPartyTrail> _trails = [];
    private readonly Dictionary<uint, DateTime?> _teleports = [];
    private readonly Dictionary<uint, (Vector3 Position, DateTimeOffset At)> _progress = [];
    private readonly Dictionary<uint, int> _routeEpochs = [];
    private readonly HashSet<uint> _failedAnchors = [];
    private int _epoch;
    private uint _anchorMember;
    private readonly Func<BotRuntime, Vector3, bool> _route;
    private readonly Func<BotRuntime, IReadOnlyList<Vector3>, bool> _returnRoute;
    private readonly Action<BotRuntime> _stop;
    private readonly Func<BotRuntime, Vector3, float> _height;

    internal BotPartyQuestCoordinator(Func<BotRuntime, Vector3, bool> route = null,
        Func<BotRuntime, IReadOnlyList<Vector3>, bool> returnRoute = null,
        Action<BotRuntime> stop = null, Func<BotRuntime, Vector3, float> height = null)
    {
        _route = route ?? ((r, p) => BotManager.Instance.SetBotTravelDestination(r.Bot, p, true,
            AAEmu.Game.Bots.Navigation.BotTravelIntent.PartyRendezvous, BotMovementOwner.PartyQuest));
        _returnRoute = returnRoute ?? ((r, p) => BotManager.Instance.SetBotPartyReturnRoute(r.Bot, p));
        _stop = stop ?? (r => r.Mover?.StopImmediately(r.Bot));
        _height = height ?? ((r, p) => r.Bot.ParentWorld.GetHeight(p.X, p.Y));
        _personalSpace = new BotPartyPersonalSpace(_route, _stop, _height);
    }
    private Vector3 _anchor;
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    internal static bool IsConfigured(uint id) => BotConfig.Instance.PartyQuestEnabled &&
        BotConfig.Instance.ConfiguredQuestGroups().Any(group => group?.Contains(id) == true);

    internal void Update(BotRuntime[] runtimes, BotConfig config, DateTimeOffset now)
    {
        if (!config.PartyQuestEnabled)
        {
            ClearSupport();
            foreach (var member in _members) _personalSpace.Cancel(member);
            _holds.Clear(); _waitingSince.Clear(); _regrouping = false; _returning = false;
            _trails.Clear(); _teleports.Clear(); _progress.Clear(); _failedAnchors.Clear();
            _nativeWork.Clear(); _regroupStartedAt = null; _supportRecoveryUntil = default;
            _unavailableSince = null;
            _explicitLeaderId = 0; _explicitLeader = null; _explicitUnavailableAt = null;
            _memberScopes.Clear();
            _nextDecision.Clear(); _members = []; return;
        }
        UpdateEnabled(runtimes, config, now);
    }

    private void UpdateEnabled(BotRuntime[] runtimes, BotConfig config, DateTimeOffset now)
    {
        if (HasScopeChanged(runtimes)) InvalidateRecoveryScope();
        var requestedLeaders = (config.PartyQuestSupportLeaders ?? [])
            .Where(id => config.PartyQuestBotIds?.Contains(id) == true).Distinct().ToArray();
        var explicitLeader = requestedLeaders.Length == 1 ? requestedLeaders[0] : 0;
        if (_explicitLeaderId != explicitLeader)
        {
            InvalidateRecoveryScope();
            _explicitLeaderId = explicitLeader; _explicitLeader = null; _explicitUnavailableAt = null;
            _supportRecoveryUntil = default; _unavailableSince = null;
        }
        if (now < _nextScan) return;
        _nextScan = now.AddMilliseconds(500);
        _holds.Clear();
        var ids = config.PartyQuestBotIds ?? [];
        var members = runtimes.Where(r => ids.Contains(r.Bot.Id) && !r.Retired).ToArray();
        CaptureScopes(members);
        if (_explicitLeaderId != 0)
        {
            _members = members;
            UpdateExplicitSupport(members, now);
            return;
        }
        if (ids.Length != 3 || ids.Distinct().Count() != 3 || members.Length != 3 ||
            members.Any(r => r.Bot.Transform == null || r.Bot.IsDead || r.Social.TeamId == 0 || r.CombatState.ForcedState != null) ||
            members.Select(r => r.Social.TeamId).Distinct().Count() != 1 ||
            members.Any(r => !ReferenceEquals(r.Bot.ParentWorld, members[0].Bot.ParentWorld) ||
                r.Bot.Transform.InstanceId != members[0].Bot.Transform.InstanceId))
        {
            ClearSupport();
            _unavailableSince ??= _regroupStartedAt ?? now;
            var expired = now - _unavailableSince.Value >= TimeSpan.FromMinutes(2);
            foreach (var member in members)
                _holds[member.Bot.Id] = expired && member.Bot.Transform != null &&
                    !member.Bot.IsDead && member.CombatState.ForcedState == null
                    ? "party_member_unavailable_replan" : "party_member_unavailable";
            foreach (var trail in _trails.Values) trail.Invalidate();
            _regrouping = false; _returning = false;
            return;
        }

        _unavailableSince = null;
        _members = members;
        foreach (var member in members)
            member.QuestLifecycleController.ReconcileNativeCompletion(member, now);
        if (!RefreshNativeWork(members, now)) return;
        if (now < _supportRecoveryUntil)
        {
            _regrouping = false; _returning = false;
            return;
        }
        var positions = members.Select(r => r.Bot.Transform.World.Position).ToArray();
        var closeTogether = positions.All(p => positions.All(q => Vector3.Distance(p, q) <= 12f));
        var safe = members.Select(IsSafeGround).ToArray();
        for (var i = 0; i < members.Length; i++)
        {
            var id = members[i].Bot.Id;
            if (!_trails.TryGetValue(id, out var trail)) _trails[id] = trail = new BotPartyTrail();
            var teleported = _teleports.TryGetValue(id, out var last) && last != members[i].MovementState.SafeRecovery.LastTeleportAt;
            _teleports[id] = members[i].MovementState.SafeRecovery.LastTeleportAt;
            if (closeTogether && safe.All(value => value)) trail.Reset(positions[i]);
            else if (!_returning) trail.Observe(positions[i], safe[i] && !teleported);
        }
        var shouldRegroup = NeedsRegroup(positions, _regrouping);
        // One episode survives harmless fence crossings and changes of action.
        // Only native acceptance/objective/report progress refreshes its budget.
        if (shouldRegroup || _supportMember != 0) _regroupStartedAt ??= now;
        if (_regroupStartedAt is { } sinceRegroup && now - sinceRegroup >= TimeSpan.FromMinutes(2))
        {
            // Alternate anchors must not reset the escape budget indefinitely.
            ClearSupport(); _holds.Clear(); _regrouping = false; _returning = false;
            foreach (var member in members)
                if (member.MovementState.TravelOwner == BotMovementOwner.PartyQuest) _stop(member);
            _regroupStartedAt = null;
            _supportRecoveryUntil = now.AddSeconds(60);
            Logger.Warn("BOT ev=party_regroup_recovery reason=regroup_budget_exhausted");
            return;
        }
        if (UpdateSupport(members, config, shouldRegroup, now))
        {
            _regrouping = false; _returning = false;
            return;
        }
        if (shouldRegroup && !_regrouping)
        {
            // Freeze a real member's occupied position, not a moving centroid.
            var index = Enumerable.Range(0, positions.Length).MinBy(i => positions.Sum(q => Vector3.Distance(positions[i], q)));
            _anchor = positions[index]; _anchorMember = members[index].Bot.Id;
            _failedAnchors.Clear(); _returning = false; NewRouteEpoch();
        }
        _regrouping = shouldRegroup;
        if (!_regrouping) _returning = false;

        var activeStory = members.ToDictionary(r => r.Bot.Id, r => r.Bot.Quests.ActiveQuests
            .Where(p => p.Value?.Template is QuestTemplate template && BotQuestIntakeController.IsMainStory(template))
            .Select(p => p.Key).ToHashSet());
        var sharedStory = activeStory.Values.SelectMany(q => q).Distinct().ToArray();
        foreach (var member in members)
        {
            var id = member.Bot.Id;
            var ahead = sharedStory.Any(q => StoryMustWait(activeStory[id].Contains(q), member.Bot.Quests.HasQuestCompleted(q),
                members.Where(other => other.Bot.Id != id).Select(other =>
                    (activeStory[other.Bot.Id].Contains(q), other.Bot.Quests.HasQuestCompleted(q))).ToArray()));
            var reason = _regrouping ? "regrouping" : ahead ? "waiting_for_story_peers" : null;
            if (reason != null)
            {
                _regroupStartedAt ??= now;
                _holds[id] = reason;
                if (!_waitingSince.TryGetValue(id, out var since)) _waitingSince[id] = now;
                else if (now - since >= TimeSpan.FromMinutes(2))
                    _holds[id] = "party_blocked_" + reason;
            }
            else _waitingSince.Remove(id);
        }
    }

    internal bool Step(BotRuntime runtime, BotConfig config, DateTimeOffset now)
    {
        runtime.PartyQuestSuppressBrain = false;
        if (!config.PartyQuestEnabled || !(config.PartyQuestBotIds?.Contains(runtime.Bot.Id) ?? false))
        { _personalSpace.Cancel(runtime); runtime.PartyQuestReason = "disabled"; return true; }
        if (_explicitLeaderId != 0) return StepExplicitSupport(runtime, now);
        if (now < _supportRecoveryUntil && !_holds.ContainsKey(runtime.Bot.Id))
        {
            runtime.QuestLifecycleController.ResumeAfterParty(now);
            SetReason(runtime, "party_support_recovery_replan");
            return true;
        }
        var view = runtime.QuestLifecycleController.Inspect();
        var busy = runtime.Bot.SkillTask != null || runtime.Bot.IsInBattle || runtime.CombatState.Target != null || runtime.MovementState.Climb != null ||
            runtime.MovementState.IsFalling || runtime.MovementState.IsJumping ||
            view.State is BotQuestLifecycleState.MovingToLoot or BotQuestLifecycleState.WaitingForProgress or
                BotQuestLifecycleState.WaitingForCompletion;
        if (_holds.GetValueOrDefault(runtime.Bot.Id) == "party_member_unavailable_replan")
        {
            // An absent peer cannot suspend native progress forever. Release only
            // this controller's travel; manual orders and active actions keep ownership.
            if (!busy && runtime.MovementState.TravelOwner == BotMovementOwner.PartyQuest)
                _stop(runtime);
            runtime.QuestLifecycleController.ResumeAfterParty(now);
            SetReason(runtime, "party_member_unavailable_replan");
            return true;
        }
        if (_supportMember != 0 && StepSupport(runtime, now, busy)) return false;
        if (_holds.TryGetValue(runtime.Bot.Id, out var reason) && !busy)
        {
            SetReason(runtime, reason.EndsWith("regrouping", StringComparison.Ordinal) && _failedAnchors.Count >= 3
                ? "party_blocked_no_regroup_route" : reason);
            runtime.PartyQuestSuppressBrain = true;
            runtime.QuestLifecycleController.PauseForParty(runtime, now);
            runtime.QuestIntakeController.YieldToQuestLifecycle(runtime, now);
            var state = runtime.MovementState;
            if (reason.EndsWith("regrouping", StringComparison.Ordinal))
            {
                // Let waiting members spread; an active regroup route keeps priority.
                if (_personalSpace.Step(runtime, _members, now, true)) return false;
                Regroup(runtime, now);
            }
            else if (state.TravelOwner == BotMovementOwner.PartyQuest)
                _stop(runtime);
            return false;
        }
        var waiting = !busy && view.State is BotQuestLifecycleState.WaitingForRespawn or BotQuestLifecycleState.Idle;
        if (_personalSpace.Step(runtime, _members, now, waiting))
        {
            runtime.PartyQuestSuppressBrain = true;
            runtime.QuestLifecycleController.PauseForParty(runtime, now);
            SetReason(runtime, "making_personal_space");
            return false;
        }
        if (runtime.MovementState.TravelOwner == BotMovementOwner.PartyQuest)
            _stop(runtime);
        runtime.QuestLifecycleController.ResumeAfterParty(now);
        if (runtime.PartyQuestReason is not ("questing" or "finishing_quest_action" or "recovering_quest_action"))
            _nextDecision.Remove(runtime.Bot.Id);
        runtime.PartyRegroupDetail = _supportMember == runtime.Bot.Id
            ? $"support_member={_supportMember} quest={_supportQuest}" : "none";
        SetReason(runtime, _supportMember == runtime.Bot.Id
            ? _supportBlocked ? "recovering_quest_action" : "finishing_quest_action"
            : "questing");
        if (_nextDecision.TryGetValue(runtime.Bot.Id, out var next) && now < next) return false;
        var sequence = _decisions.GetValueOrDefault(runtime.Bot.Id) + 1;
        _decisions[runtime.Bot.Id] = sequence;
        _nextDecision[runtime.Bot.Id] = now.AddMilliseconds(DecisionDelay(runtime.Bot.Id, sequence));
        return true;
    }

    private bool IsSafeGround(BotRuntime runtime)
    {
        var bot = runtime.Bot; var state = runtime.MovementState;
        if (bot.Transform?.Parent != null || bot.Transform?.StickyParent != null || state.Climb != null ||
            state.IsFalling || state.IsJumping || state.JumpRequested) return false;
        try
        {
            var point = bot.Transform.World.Position; var height = _height(runtime, point);
            return float.IsFinite(height) && MathF.Abs(point.Z - height) <= 0.5f;
        }
        catch { return false; }
    }

    private void NewRouteEpoch()
    {
        _epoch++; _nextDecision.Clear(); _progress.Clear();
    }

    private void FailedRoute(BotRuntime runtime, DateTimeOffset now, bool physicalStall = false)
    {
        runtime.PartyRegroupDetail = _returning ? "occupied_trail_unavailable" : "anchor_route_unavailable_or_stalled";
        Logger.Warn($"BOT id={runtime.Bot.Id} ev=party_regroup_route_failed returning={_returning} " +
            $"navigation={runtime.MovementState.LastNavigationDecision?.Reason} anchor={_anchorMember}");
        if (!_returning && _members.All(r => _trails.TryGetValue(r.Bot.Id, out var t) && t.Valid))
        {
            _returning = true; NewRouteEpoch();
            Logger.Info("BOT ev=party_regroup_replan mode=occupied_return_trails");
            return;
        }
        if (_returning) _trails[runtime.Bot.Id].Invalidate();
        _returning = false;
        _failedAnchors.Add(_anchorMember);
        var alternative = _members.FirstOrDefault(r => !_failedAnchors.Contains(r.Bot.Id) && IsSafeGround(r));
        if (alternative != null)
        {
            _anchor = alternative.Bot.Transform.World.Position; _anchorMember = alternative.Bot.Id;
            NewRouteEpoch();
            Logger.Info($"BOT ev=party_regroup_replan mode=alternate_occupied_anchor anchor={_anchorMember}");
        }
        else
        {
            // Invalid navigation cannot be repaired by teleporting the actor. Keep the
            // existing narrow rollback only after real movement stalls exhaust normal routes.
            if (physicalStall && runtime.MovementState.LastNavigationDecision?.IsAccepted == true)
                runtime.MovementState.SafeRecovery.FailedRoute(runtime, BotConfig.Instance, now.UtcDateTime,
                    "party_regroup_physical_stall", runtime.Mover ??
                    (AAEmu.Game.Bots.Body.IBotMover)AAEmu.Game.Bots.Body.BotManagerMover.Instance);
            // No claimed successful recovery: keep the group visible and retry infrequently.
            _nextDecision[runtime.Bot.Id] = now.AddMinutes(2);
            SetReason(runtime, "party_blocked_no_regroup_route");
        }
    }

    private void Regroup(BotRuntime runtime, DateTimeOffset now)
    {
        var id = runtime.Bot.Id; var state = runtime.MovementState;
        if (state.TravelOwner != BotMovementOwner.PartyQuest && state.Destination.HasValue) return;
        if (state.TravelOwner == BotMovementOwner.PartyQuest && _routeEpochs.GetValueOrDefault(id, -1) != _epoch)
            _stop(runtime);
        var position = runtime.Bot.Transform.World.Position;
        var goal = _returning ? _trails[id].Origin : _anchor;
        if (Vector3.Distance(position, goal) <= (_returning ? 1f : 7f))
        {
            if (state.TravelOwner == BotMovementOwner.PartyQuest) _stop(runtime);
            _progress.Remove(id); return;
        }
        if (_progress.TryGetValue(id, out var progress))
        {
            if (Vector3.Distance(position, progress.Position) >= 1f)
                _progress[id] = (position, now);
            else if (now - progress.At >= TimeSpan.FromSeconds(30))
            {
                var physicalStall = state.TravelOwner == BotMovementOwner.PartyQuest && state.Destination.HasValue;
                if (state.TravelOwner == BotMovementOwner.PartyQuest) _stop(runtime);
                _progress.Remove(id); FailedRoute(runtime, now, physicalStall); return;
            }
        }
        if (state.Destination.HasValue || _nextDecision.TryGetValue(id, out var retry) && now < retry) return;
        _nextDecision[id] = now.AddSeconds(30);
        _routeEpochs[id] = _epoch;
        var selected = _returning
            ? _returnRoute(runtime, _trails[id].ReturnPath(position, (x, y) => _height(runtime, new(x, y, 0))))
            : _route(runtime, goal);
        if (selected)
        {
            _progress[id] = (position, now);
            runtime.PartyRegroupDetail = _returning ? "walking_occupied_return_trail" : "walking_to_occupied_anchor";
        }
        else FailedRoute(runtime, now);
    }

    internal static bool StoryMustWait(bool active, bool completed, (bool Active, bool Completed)[] peers) =>
        completed && peers.Any(p => p.Active && !p.Completed) ||
        active && peers.Any(p => !p.Active && !p.Completed);

    internal static bool NeedsRegroup(Vector3[] positions, bool regrouping)
    {
        var spread = positions.SelectMany(p => positions.Select(q => Vector2.Distance(new(p.X, p.Y), new(q.X, q.Y)))).DefaultIfEmpty().Max();
        return spread > (regrouping ? 20f : 40f);
    }

    internal static int DecisionDelay(uint id, uint sequence) => 350 + (int)(unchecked(id * 747796405u + sequence * 2891336453u) % 700);
    internal static Vector3 SlotOffset(uint id)
    {
        var angle = (id % 3) * MathF.Tau / 3;
        return new Vector3(MathF.Cos(angle), MathF.Sin(angle), 0) * 2f;
    }

    private static void SetReason(BotRuntime runtime, string reason)
    {
        if (runtime.PartyQuestReason == reason) return;
        runtime.PartyQuestReason = reason;
        Logger.Info($"BOT id={runtime.Bot.Id} ev=party_quest state={reason}");
    }
}
#endif
