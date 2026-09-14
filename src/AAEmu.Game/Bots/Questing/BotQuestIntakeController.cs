using System.Numerics;
using AAEmu.Game.Bots.Blackboard;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Bots.Navigation;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.GameData;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.DoodadObj;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Quests.Static;
using AAEmu.Game.Models.Game.Quests.Templates;
using AAEmu.Game.Models.Game.Units;
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
    BotQuestGiverKind? GiverKind,
    uint? GiverObjectId,
    uint? GiverTemplateId,
    uint? QuestId,
    bool? MainStory,
    string DecisionReason,
    DateTimeOffset? DecisionAt,
    DateTimeOffset? LastAcceptedAt,
    DateTimeOffset? RetryAt,
    long AcceptedCount,
    long RejectedCount)
{
    public uint? NpcObjectId =>
        GiverKind == BotQuestGiverKind.Npc ? GiverObjectId : null;

    public uint? NpcTemplateId =>
        GiverKind == BotQuestGiverKind.Npc ? GiverTemplateId : null;

    public uint? DoodadObjectId =>
        GiverKind == BotQuestGiverKind.Doodad ? GiverObjectId : null;

    public uint? DoodadTemplateId =>
        GiverKind == BotQuestGiverKind.Doodad ? GiverTemplateId : null;
}

/// <summary>
/// Owns the bounded, opt-in path from nearby quest discovery to normal AAEmu
/// NPC or doodad acceptance. Objective execution and quest reporting are owned
/// by <see cref="BotQuestLifecycleController"/>.
/// </summary>
public sealed partial class BotQuestIntakeController
{
    internal const float MaximumScanRadius = BotCombatTask.MaximumQuestTargetSearchRadius;
    internal const float MaximumStaticStartRouteDistance = 500f;
    internal static readonly TimeSpan StaticStartDiscoveryGrace = TimeSpan.FromSeconds(2.5);
    private const float DestinationTolerance = 0.25f;
    private const float SelectedGiverLeash = 20f;
    private static readonly TimeSpan OpportunityScanInterval = TimeSpan.FromSeconds(1);

    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();

    private readonly object _syncRoot = new();
    private readonly Func<uint, IReadOnlyList<QuestTemplate>> _questStarts;
    private readonly Func<BotRuntime, float, DateTimeOffset, IReadOnlyList<BotQuestStartCandidate>> _doodadStarts;
    private readonly Func<BotRuntime, float, IReadOnlyList<BotQuestStaticStartDestination>> _staticNpcStarts;
    private readonly Func<Character, BotQuestGiverKind, uint, uint, bool> _acceptQuest;
    private readonly Func<Character, BotQuestGiverKind, uint, uint, bool> _validateGiverQuest;
    private readonly Func<Character, Vector3, bool, bool> _setDestination;
    private readonly Action<Character> _stopMovement;
    private readonly Func<Character, Vector3, float> _heightProvider;
    private readonly Action<string> _eventSink;
    private readonly Dictionary<QuestCandidateKey, DateTimeOffset> _retryAfter = [];

    private GiverPlan _plan;
    private StaticStartPlan _pendingStaticStartPlan;
    private StaticStartPlan _staticStartPlan;
    private Vector3? _ownedDestination;
    private volatile BotQuestIntakeState _state = BotQuestIntakeState.Disabled;
    private string _decisionReason = "not_started";
    private DateTimeOffset? _decisionAt;
    private DateTimeOffset? _lastAcceptedAt;
    private DateTimeOffset? _retryAt;
    private DateTimeOffset _nextOpportunityScanAt;
    private DateTimeOffset? _staticStartEligibleAt;
    private bool _lastOpportunityScanResult;
    private long _acceptedCount;
    private long _rejectedCount;

    public BotQuestIntakeController()
        : this(new BotQuestAuthority(), null, null, null, null)
    {
    }

    private BotQuestIntakeController(
        IBotQuestAuthority authority,
        Action<Character, Vector3, bool> setDestination,
        Action<Character> stopMovement,
        Func<Character, Vector3, float> heightProvider,
        Action<string> eventSink)
        : this(
            npcTemplateId => QuestManager.Instance.GetPlayerBotNpcQuestStarts(npcTemplateId),
            (runtime, radius, now) => authority.FindDoodadQuestStarts(runtime, radius, now),
            authority.AcceptQuest,
            ValidateProductionGiverQuest,
            setDestination,
            stopMovement,
            heightProvider,
            eventSink,
            authority.FindStaticNpcQuestStarts)
    {
    }

    internal BotQuestIntakeController(
        Func<uint, IReadOnlyList<QuestTemplate>> questStarts,
        Func<Character, uint, uint, bool> acceptQuest,
        Action<Character, Vector3, bool> setDestination,
        Action<Character> stopMovement,
        Func<Character, Vector3, float> heightProvider,
        Action<string> eventSink)
        : this(
            questStarts,
            (_, _, _) => [],
            (bot, kind, questId, objectId) =>
                kind == BotQuestGiverKind.Npc && acceptQuest(bot, questId, objectId),
            (_, kind, _, _) => kind == BotQuestGiverKind.Npc,
            setDestination,
            stopMovement,
            heightProvider,
            eventSink,
            null)
    {
    }

    internal BotQuestIntakeController(
        Func<uint, IReadOnlyList<QuestTemplate>> questStarts,
        Func<BotRuntime, float, DateTimeOffset, IReadOnlyList<BotQuestStartCandidate>> doodadStarts,
        Func<Character, BotQuestGiverKind, uint, uint, bool> acceptQuest,
        Func<Character, BotQuestGiverKind, uint, uint, bool> validateGiverQuest,
        Action<Character, Vector3, bool> setDestination,
        Action<Character> stopMovement,
        Func<Character, Vector3, float> heightProvider,
        Action<string> eventSink,
        Func<BotRuntime, float, IReadOnlyList<BotQuestStaticStartDestination>> staticNpcStarts = null)
    {
        _questStarts = questStarts ??
            (npcTemplateId => QuestManager.Instance.GetPlayerBotNpcQuestStarts(npcTemplateId));
        _acceptQuest = acceptQuest ??
            ((bot, kind, questId, objectId) => kind switch
            {
                BotQuestGiverKind.Npc => bot.Quests.AddQuestFromNpc(questId, objectId),
                BotQuestGiverKind.Doodad => bot.Quests.AddQuestFromDoodad(questId, objectId),
                _ => false
            });
        _doodadStarts = doodadStarts ?? ((_, _, _) => []);
        _staticNpcStarts = staticNpcStarts ?? ((_, _) => []);
        _validateGiverQuest = validateGiverQuest ?? ((_, _, _, _) => false);
        _setDestination = setDestination == null
            ? ((bot, destination, run) => BotManager.Instance.SetBotTravelDestination(
                bot,
                destination,
                run,
                BotTravelIntent.Interaction,
                BotMovementOwner.QuestIntake))
            : ((bot, destination, run) =>
            {
                setDestination(bot, destination, run);
                return true;
            });
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

#if !PLAYERBOTS_AAEMU_3_0
#endif
        if (!config.QuestIntakeEnabled && _state == BotQuestIntakeState.Disabled)
            return false;

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

#if !PLAYERBOTS_AAEMU_3_0
            if (runtime.PartyQuestPriorityIsAcceptance && runtime.PartyQuestPriorityQuestId is { } partyQuest)
                return StepPartyStoryAcceptance(runtime, config, now, partyQuest);
            // Notice a board before a committed NPC/story route carries it out of range.
            // Combat and external movement ownership have already been checked above.
            if (TrySelectPassingBoard(runtime, config, now))
                return StepPlan(runtime, config, now);
#endif

            if (_plan != null)
                return StepPlan(runtime, config, now);

            if (_staticStartPlan != null)
                return StepStaticStartPlan(runtime, config, now);

            var candidates = FindCandidates(runtime, config, now);
            var hasNearbyMainStory = candidates.Count > 0 && candidates[0].MainStory;
            // When nearby intake preempts accepted work, select that actual nearby
            // opportunity. Substituting a remote anchor causes an endless handoff loop.
            var hasStaticMainStory = !hasNearbyMainStory &&
#if PLAYERBOTS_AAEMU_3_0
                (candidates.Count == 0 || runtime.Bot.Quests.ActiveQuests.Count == 0) &&
#else
                candidates.Count == 0 &&
#endif
                EnsurePendingStaticMainStoryRoute(runtime);
            CacheOpportunityResult(candidates.Count > 0 ||
                (runtime.Bot.Quests.ActiveQuests.Count == 0 && hasStaticMainStory), now);
            if (hasStaticMainStory)
            {
                // With local intake exhausted, retain the story destination
                // while allowing the initial world scan to discover nearby work.
                _retryAt = null;
                _staticStartEligibleAt ??= now + StaticStartDiscoveryGrace;
                if (now < _staticStartEligibleAt.Value)
                {
                    SetState(BotQuestIntakeState.Idle, "awaiting_local_quest_discovery", now);
                    return true;
                }

                if (BeginPendingStaticMainStoryRoute(runtime, config, now))
                    return true;
            }
            else
            {
                _staticStartEligibleAt = null;
                _pendingStaticStartPlan = null;
            }

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
#if !PLAYERBOTS_AAEMU_3_0
            // Collect an eligible quest already within reach before departing.
            // Story priority still decides between givers that require travel.
            foreach (var candidate in candidates)
            {
                if (candidate.Distance > config.QuestIntakeInteractionRadius) continue;
                selected = candidate;
                break;
            }
#endif
            var quests = candidates
                .Where(candidate => candidate.Kind == selected.Kind &&
                                    candidate.Giver.ObjId == selected.Giver.ObjId)
                .OrderBy(candidate => candidate.MainStory ? 0 : 1)
                .ThenBy(candidate => candidate.Quest.Id)
                .Select(candidate => candidate.Quest)
                .ToArray();
            _plan = new GiverPlan(
                selected.Kind,
                selected.Giver.ObjId,
                selected.Giver.TemplateId,
                quests);
            _retryAt = null;
            SetState(BotQuestIntakeState.Interacting, "candidate_selected", now);
            Log(runtime.Bot.Id, "selected",
                $"giver={GiverName(selected.Kind)} giver_obj={selected.Giver.ObjId} " +
                $"giver_template={selected.Giver.TemplateId} " +
                $"quest={selected.Quest.Id} main_story={selected.MainStory.ToString().ToLowerInvariant()} " +
                $"distance={selected.Distance:F2} planned={quests.Length}");
            return StepPlan(runtime, config, now);
        }
    }

    internal bool HasPriorityIntakeOpportunity(
        BotRuntime runtime,
        BotConfig config,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(config);

        lock (_syncRoot)
        {
            if (!config.QuestIntakeEnabled)
                return false;
            if (_plan != null)
                return true;
            if (_staticStartPlan != null)
                return false;
            if (now < _nextOpportunityScanAt)
                return _lastOpportunityScanResult;

            try
            {
                // A remote story anchor is fallback work, not a reason to abandon
                // a hunt already accepted from a passing board.
                var hasPriorityOpportunity = FindCandidates(runtime, config, now).Count > 0 ||
                    (runtime.Bot.Quests.ActiveQuests.Count == 0 && EnsurePendingStaticMainStoryRoute(runtime));
                CacheOpportunityResult(hasPriorityOpportunity, now);
                return hasPriorityOpportunity;
            }
            catch
            {
                CacheOpportunityResult(false, now);
                return false;
            }
        }
    }

#if !PLAYERBOTS_AAEMU_3_0
    internal bool HasSideReportIntakeOpportunity(
        BotRuntime runtime,
        BotConfig config,
        DateTimeOffset now,
        out bool nearbyStory)
    {
        lock (_syncRoot)
        {
            nearbyStory = false;
            if (!config.QuestIntakeEnabled)
                return false;

            var boardRadius = Math.Max(config.QuestIntakeInteractionRadius, 20d);
            var candidates = FindCandidates(runtime, config, now);
            nearbyStory = candidates.Any(candidate => candidate.MainStory &&
                candidate.Distance <= config.QuestIntakeInteractionRadius);
            return nearbyStory || candidates.Any(candidate =>
                candidate.Kind == BotQuestGiverKind.Doodad && candidate.Distance <= boardRadius);
        }
    }
#endif

    public BotQuestIntakeView Inspect()
    {
        lock (_syncRoot)
        {
            var quest = _plan?.Quests.FirstOrDefault() ??
                        _staticStartPlan?.Quest ??
                        _pendingStaticStartPlan?.Quest;
            var staticPlan = _staticStartPlan ?? _pendingStaticStartPlan;
            return new BotQuestIntakeView(
                _state,
                _plan?.Kind ?? (staticPlan == null ? null : BotQuestGiverKind.Npc),
                _plan?.ObjectId,
                _plan?.TemplateId ?? staticPlan?.NpcTemplateId,
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

    /// <summary>
    /// Relinquishes only movement owned by quest intake when an already-active
    /// quest needs the bot. Completing and reporting accepted work always
    /// outranks travelling to collect more work.
    /// </summary>
    internal bool YieldToQuestLifecycle(BotRuntime runtime, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        lock (_syncRoot)
        {
            if (_plan == null && _pendingStaticStartPlan == null &&
                _staticStartPlan == null && !_ownedDestination.HasValue)
                return false;

            InvalidatePlan(runtime, "quest_lifecycle_priority", now, stopOwnedMovement: true);
            return true;
        }
    }

    internal static bool IsMainStory(QuestTemplate quest) =>
        quest != null && (quest.ChapterIdx != 0 || quest.QuestIdx != 0);

    private bool StepPlan(BotRuntime runtime, BotConfig config, DateTimeOffset now)
    {
        var bot = runtime.Bot;
        var giver = ResolveGiver(bot, _plan.Kind, _plan.ObjectId);
        var selectedGiverRadius = Math.Max(
            EffectiveScanRadius(config),
            (float)config.QuestIntakeInteractionRadius) + SelectedGiverLeash;
        if (!TryValidateGiver(bot, _plan.Kind, giver, selectedGiverRadius, out var distance, out var reason))
        {
            InvalidatePlan(runtime, reason, now, stopOwnedMovement: true);
            return true;
        }

        var targetPosition = giver.Transform.World.Position;
        if (distance > config.QuestIntakeInteractionRadius)
        {
            if (CurrentRequestedDestination(runtime).HasValue && !OwnsCurrentMovement(runtime))
            {
                InvalidatePlan(runtime, "movement_ownership_lost", now, stopOwnedMovement: false);
                return false;
            }

            // A terrain-valid approach point is relative to the bot. Preserve the
            // route already accepted for this giver instead of recalculating a
            // slightly different endpoint on every movement tick.
            if (!_ownedDestination.HasValue || CurrentRequestedDestination(runtime) == null)
            {
#if PLAYERBOTS_AAEMU_3_0
                var approachPosition = BotQuestApproachPlanner.ForWorldObject(
                    bot.Transform.World.Position,
                    targetPosition,
                    (float)config.QuestIntakeInteractionRadius,
                    bot.ParentWorld.GetHeight
#if !PLAYERBOTS_AAEMU_3_0
                    , config.PartyQuestEnabled ? bot.Id : 0
#endif
                    );
                if (!_setDestination(bot, approachPosition, true))
#else
                if (!BotQuestApproachPlanner.TryForWorldObject(bot.Transform.World.Position,
                        targetPosition, (float)config.QuestIntakeInteractionRadius,
                        bot.ParentWorld.GetHeight, bot.ParentWorld.IsWater,
                        out var approachPosition, config.PartyQuestEnabled ? bot.Id : 0) ||
                    !_setDestination(bot, approachPosition, true))
#endif
                {
                    BackoffCurrentPlan(runtime, config, now, "navigation_route_unavailable");
                    return true;
                }
                _ownedDestination = approachPosition;
                SetState(BotQuestIntakeState.Moving, "moving_to_quest_giver", now);
                Log(bot.Id, "move_requested",
                    $"giver={GiverName(_plan.Kind)} giver_obj={giver.ObjId} " +
                    $"giver_template={giver.TemplateId} distance={distance:F2} " +
                    $"destination=({approachPosition.X:R},{approachPosition.Y:R},{approachPosition.Z:R})");
            }

            return true;
        }

        StopOwnedMovement(runtime);
#if !PLAYERBOTS_AAEMU_3_0
#endif
        SetState(BotQuestIntakeState.Interacting, "within_interaction_radius", now);
        var plannedQuests = _plan.Quests;
        var giverKind = _plan.Kind;
        _plan = null;
        foreach (var quest in plannedQuests)
        {
            var key = new QuestCandidateKey(giverKind, giver.ObjId, quest.Id);
            if (!IsEligible(bot, quest) || IsInBackoff(key, now))
                continue;

            if (!_validateGiverQuest(bot, giverKind, giver.ObjId, quest.Id))
            {
                _rejectedCount++;
                var validationRetryAt =
                    now + TimeSpan.FromMilliseconds(config.QuestIntakeRetryBackoffMs);
                _retryAfter[key] = validationRetryAt;
                Log(bot.Id, "validation_rejected",
                    $"giver={GiverName(giverKind)} giver_obj={giver.ObjId} " +
                    $"giver_template={giver.TemplateId} quest={quest.Id} " +
                    $"retry_at={Timestamp(validationRetryAt)}");
                continue;
            }

            var accepted = false;
            try
            {
                accepted = _acceptQuest(bot, giverKind, quest.Id, giver.ObjId);
            }
            catch (Exception exception)
            {
                Log(bot.Id, "accept_error",
                    $"giver={GiverName(giverKind)} giver_obj={giver.ObjId} " +
                    $"quest={quest.Id} error={exception.GetType().Name}");
            }
            finally
            {
                if (ReferenceEquals(bot.CurrentTarget, giver))
                    bot.CurrentTarget = null;
            }

            if (accepted && bot.Quests.HasQuest(quest.Id))
            {
                _acceptedCount++;
                _lastAcceptedAt = now;
                _retryAfter.Remove(key);
                Log(bot.Id, "accepted",
                    $"giver={GiverName(giverKind)} giver_obj={giver.ObjId} " +
                    $"giver_template={giver.TemplateId} quest={quest.Id} " +
                    $"main_story={IsMainStory(quest).ToString().ToLowerInvariant()}");
                continue;
            }

            _rejectedCount++;
            var retryAt = now + TimeSpan.FromMilliseconds(config.QuestIntakeRetryBackoffMs);
            _retryAfter[key] = retryAt;
            Log(bot.Id, "rejected",
                $"giver={GiverName(giverKind)} giver_obj={giver.ObjId} giver_template={giver.TemplateId} " +
                $"quest={quest.Id} retry_at={Timestamp(retryAt)}");
        }

        _retryAt = EarliestRetry();
        SetState(BotQuestIntakeState.Idle, "giver_intake_complete", now);
        return true;
    }

#if !PLAYERBOTS_AAEMU_3_0
    private bool TrySelectPassingBoard(BotRuntime runtime, BotConfig config, DateTimeOffset now)
    {
        var candidates = FindCandidates(runtime, config, now);
        if (CurrentRequestedDestination(runtime).HasValue && !OwnsCurrentMovement(runtime))
            return false;
        var story = candidates.FirstOrDefault(candidate => candidate.MainStory &&
            candidate.Distance <= config.QuestIntakeInteractionRadius);
        if (story.Giver != null)
        {
            // An offered story already in native interaction range supersedes an
            // unaccepted side route, including a board plan selected last tick.
            StopOwnedMovement(runtime);
            _pendingStaticStartPlan ??= _staticStartPlan;
            _staticStartPlan = null;
            _plan = new GiverPlan(story.Kind, story.Giver.ObjId, story.Giver.TemplateId,
                candidates.Where(candidate => candidate.Kind == story.Kind &&
                                              candidate.Giver.ObjId == story.Giver.ObjId)
                    .OrderBy(candidate => candidate.MainStory ? 0 : 1)
                    .ThenBy(candidate => candidate.Quest.Id)
                    .Select(candidate => candidate.Quest).ToArray());
            SetState(BotQuestIntakeState.Interacting, "nearby_story_giver_selected", now);
            Log(runtime.Bot.Id, "nearby_story_selected",
                $"quest={story.Quest.Id} giver={GiverName(story.Kind)} giver_obj={story.Giver.ObjId} " +
                $"distance={story.Distance:F2}");
            return true;
        }

        if (_staticStartPlan is { } staticStory)
        {
            var side = candidates.FirstOrDefault(candidate =>
                candidate.Kind == BotQuestGiverKind.Npc && !candidate.MainStory);
            if (side.Giver != null)
            {
                // Keep the validated story anchor, but clear nearby NPC work
                // before its regional route carries the bot out of range.
                StopOwnedMovement(runtime);
                _pendingStaticStartPlan ??= staticStory;
                _staticStartPlan = null;
                _plan = new GiverPlan(side.Kind, side.Giver.ObjId, side.Giver.TemplateId,
                    candidates.Where(candidate => candidate.Kind == side.Kind &&
                                                  candidate.Giver.ObjId == side.Giver.ObjId)
                        .OrderBy(candidate => candidate.MainStory ? 0 : 1)
                        .ThenBy(candidate => candidate.Quest.Id)
                        .Select(candidate => candidate.Quest).ToArray());
                SetState(BotQuestIntakeState.Interacting, "nearby_side_giver_selected", now);
                Log(runtime.Bot.Id, "nearby_side_selected",
                    $"quest={side.Quest.Id} giver_obj={side.Giver.ObjId} distance={side.Distance:F2}");
                return true;
            }
        }

        if (_plan?.Kind == BotQuestGiverKind.Doodad)
            return false;
        var board = candidates.Where(candidate => candidate.Kind == BotQuestGiverKind.Doodad &&
                candidate.Distance <= Math.Max(config.QuestIntakeInteractionRadius, 20d))
            .OrderBy(candidate => candidate.Distance).ThenBy(candidate => candidate.Quest.Id)
            .FirstOrDefault();
        if (board.Giver == null)
            return false;

        StopOwnedMovement(runtime);
        // Keep the eligible story anchor. Native eligibility will recover an interrupted
        // live NPC giver after accepted work completes; no quest order is recorded.
        _pendingStaticStartPlan ??= _staticStartPlan;
        _staticStartPlan = null;
        _plan = new GiverPlan(BotQuestGiverKind.Doodad, board.Giver.ObjId,
            board.Giver.TemplateId, candidates.Where(candidate =>
                candidate.Kind == BotQuestGiverKind.Doodad && candidate.Giver.ObjId == board.Giver.ObjId)
                .Select(candidate => candidate.Quest).DistinctBy(quest => quest.Id).ToArray());
        Log(runtime.Bot.Id, "passing_board_selected",
            $"quest={board.Quest.Id} giver_template={board.Giver.TemplateId} distance={board.Distance:F2}");
        return true;
    }

#endif

    private bool StepStaticStartPlan(BotRuntime runtime, BotConfig config, DateTimeOffset now)
    {
        var plan = _staticStartPlan;
        var bot = runtime.Bot;
        if (!IsEligible(bot, plan.Quest) || !IsMainStory(plan.Quest))
        {
            InvalidatePlan(runtime, "static_start_no_longer_eligible", now, stopOwnedMovement: true);
            return true;
        }

        var localMatch = FindCandidates(runtime, config, now)
            .FirstOrDefault(candidate =>
                candidate.Kind == BotQuestGiverKind.Npc &&
                candidate.Giver.TemplateId == plan.NpcTemplateId &&
                candidate.Quest.Id == plan.Quest.Id);
        if (localMatch.Giver != null)
        {
            StopOwnedMovement(runtime);
            _staticStartPlan = null;
            _plan = new GiverPlan(
                BotQuestGiverKind.Npc,
                localMatch.Giver.ObjId,
                localMatch.Giver.TemplateId,
                [localMatch.Quest]);
            SetState(BotQuestIntakeState.Interacting, "static_start_giver_acquired", now);
            Log(bot.Id, "static_route_arrived",
                $"giver=npc giver_obj={localMatch.Giver.ObjId} " +
                $"giver_template={localMatch.Giver.TemplateId} quest={localMatch.Quest.Id} " +
                $"distance={localMatch.Distance:F2}");
            return StepPlan(runtime, config, now);
        }

        var botPosition = bot.Transform.World.Position;
        var distance = Vector3.Distance(botPosition, plan.Position);
        if (!IsFinite(botPosition) || !IsFinite(plan.Position) || !float.IsFinite(distance) ||
            distance > MaximumStaticStartRouteDistance)
        {
            InvalidatePlan(runtime, "static_start_route_invalid", now, stopOwnedMovement: true);
            return true;
        }

        if (CurrentRequestedDestination(runtime).HasValue && !OwnsCurrentMovement(runtime))
        {
            InvalidatePlan(runtime, "movement_ownership_lost", now, stopOwnedMovement: false);
            return false;
        }

        if (!_ownedDestination.HasValue || runtime.MovementState.Destination == null
#if PLAYERBOTS_AAEMU_3_0
            || Vector3.Distance(_ownedDestination.Value, plan.Position) > DestinationTolerance
#endif
            )
        {
#if PLAYERBOTS_AAEMU_3_0
            var approachPosition = plan.Position;
            if (!_setDestination(bot, plan.Position, true))
#else
            if (!BotQuestApproachPlanner.TryForWorldObject(botPosition, plan.Position,
                    (float)config.QuestIntakeInteractionRadius, bot.ParentWorld.GetHeight,
                    bot.ParentWorld.IsWater, out var approachPosition,
                    config.PartyQuestEnabled ? bot.Id : 0) ||
                !_setDestination(bot, approachPosition, true))
#endif
            {
                _retryAt = now + TimeSpan.FromMilliseconds(config.QuestIntakeRetryBackoffMs);
                InvalidatePlan(runtime, "static_navigation_route_unavailable", now, stopOwnedMovement: true);
                return true;
            }
            _ownedDestination = approachPosition;
            SetState(BotQuestIntakeState.Moving, "moving_to_static_main_story_giver", now);
            Log(bot.Id, "static_route_move_requested",
                $"giver=npc giver_template={plan.NpcTemplateId} quest={plan.Quest.Id} " +
                $"distance={distance:F2} destination=({plan.Position.X:R},{plan.Position.Y:R},{plan.Position.Z:R})");
        }

        return true;
    }

    private bool EnsurePendingStaticMainStoryRoute(BotRuntime runtime)
    {
        if (_pendingStaticStartPlan is { } pending &&
            IsMainStory(pending.Quest) &&
            IsEligible(runtime.Bot, pending.Quest) &&
            IsFinite(pending.Position))
        {
            return true;
        }

        _pendingStaticStartPlan = null;
        IReadOnlyList<BotQuestStaticStartDestination> routes;
        try
        {
            routes = _staticNpcStarts(runtime, MaximumStaticStartRouteDistance) ?? [];
        }
        catch (Exception exception)
        {
            Log(runtime.Bot.Id, "static_route_scan_error", $"error={exception.GetType().Name}");
            return false;
        }

        var selected = routes
            .Where(route =>
                route.NpcTemplateId != 0 &&
                IsMainStory(route.Quest) &&
                IsEligible(runtime.Bot, route.Quest) &&
                IsFinite(route.Position) &&
                float.IsFinite(route.Distance) &&
                route.Distance <= MaximumStaticStartRouteDistance)
            .OrderBy(route => route.Distance)
            .ThenBy(route => route.NpcTemplateId)
            .ThenBy(route => route.Quest.Id)
            .FirstOrDefault();
        if (selected.Quest == null)
            return false;

        _pendingStaticStartPlan = new StaticStartPlan(
            selected.NpcTemplateId,
            selected.Quest,
            selected.Position);
        return true;
    }

    private bool BeginPendingStaticMainStoryRoute(
        BotRuntime runtime,
        BotConfig config,
        DateTimeOffset now)
    {
        if (_pendingStaticStartPlan is not { } selected ||
            !IsMainStory(selected.Quest) ||
            !IsEligible(runtime.Bot, selected.Quest) ||
            !IsFinite(selected.Position))
        {
            _pendingStaticStartPlan = null;
            return false;
        }

        _staticStartPlan = selected;
        _pendingStaticStartPlan = null;
        _staticStartEligibleAt = null;
        _retryAt = null;
        SetState(BotQuestIntakeState.Moving, "static_main_story_route_selected", now);
        var distance = Vector3.Distance(runtime.Bot.Transform.World.Position, selected.Position);
        Log(runtime.Bot.Id, "static_route_selected",
            $"giver=npc giver_template={selected.NpcTemplateId} quest={selected.Quest.Id} " +
            $"main_story=true distance={distance:F2} " +
            $"destination=({selected.Position.X:R},{selected.Position.Y:R},{selected.Position.Z:R})");
        return StepStaticStartPlan(runtime, config, now);
    }

    private List<QuestCandidate> FindCandidates(BotRuntime runtime, BotConfig config, DateTimeOffset now)
    {
        List<uint> nearbyNpcObjectIds = [];
        try
        {
            if (!runtime.Blackboard.TryGet(
                    BotValues.NearbyNpcIds,
                    now.UtcDateTime,
                    out nearbyNpcObjectIds) ||
                nearbyNpcObjectIds == null)
            {
                nearbyNpcObjectIds = [];
            }
        }
        catch (Exception exception)
        {
            Log(runtime.Bot.Id, "scan_error", $"error={exception.GetType().Name}");
            nearbyNpcObjectIds = [];
        }

        var bot = runtime.Bot;
        var radius = EffectiveScanRadius(config);
        var candidates = new List<QuestCandidate>();
        foreach (var npcObjectId in nearbyNpcObjectIds.Distinct())
        {
            var npc = bot.ParentWorld?.GetNpc(npcObjectId);
            if (!TryValidateGiver(bot, BotQuestGiverKind.Npc, npc, radius, out var distance, out _))
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
                var key = new QuestCandidateKey(BotQuestGiverKind.Npc, npc.ObjId, quest?.Id ?? 0);
                if (!IsEligible(bot, quest) || IsInBackoff(key, now))
                    continue;

                candidates.Add(new QuestCandidate(
                    BotQuestGiverKind.Npc,
                    npc,
                    quest,
                    IsMainStory(quest),
                    distance));
            }
        }

        try
        {
            foreach (var candidate in _doodadStarts(runtime, radius, now) ?? [])
            {
                var key = new QuestCandidateKey(
                    BotQuestGiverKind.Doodad,
                    candidate.Giver?.ObjId ?? 0,
                    candidate.Quest?.Id ?? 0);
                if (candidate.Kind != BotQuestGiverKind.Doodad ||
                    !TryValidateGiver(
                        bot,
                        BotQuestGiverKind.Doodad,
                        candidate.Giver,
                        radius,
                        out var distance,
                        out _) ||
                    !IsEligible(bot, candidate.Quest) ||
                    IsInBackoff(key, now))
                {
                    continue;
                }

                candidates.Add(new QuestCandidate(
                    BotQuestGiverKind.Doodad,
                    candidate.Giver,
                    candidate.Quest,
                    IsMainStory(candidate.Quest),
                    distance));
            }
        }
        catch (Exception exception)
        {
            Log(runtime.Bot.Id, "doodad_scan_error", $"error={exception.GetType().Name}");
        }

        candidates.Sort(static (left, right) =>
        {
            var story = (left.MainStory ? 0 : 1).CompareTo(right.MainStory ? 0 : 1);
            if (story != 0)
                return story;
            var distance = left.Distance.CompareTo(right.Distance);
            if (distance != 0)
                return distance;
            var kind = left.Kind.CompareTo(right.Kind);
            if (kind != 0)
                return kind;
            var giver = left.Giver.ObjId.CompareTo(right.Giver.ObjId);
            return giver != 0 ? giver : left.Quest.Id.CompareTo(right.Quest.Id);
        });
        return candidates;
    }

    private bool TryValidateGiver(
        Character bot,
        BotQuestGiverKind kind,
        BaseUnit giver,
        float scanRadius,
        out float distance,
        out string reason)
    {
        distance = float.MaxValue;
        reason = kind == BotQuestGiverKind.Npc ? "npc_invalid" : "doodad_invalid";
        if (giver == null || giver.TemplateId == 0 ||
            (kind == BotQuestGiverKind.Npc && (giver is not Npc npc || npc.IsDead || npc.Hp <= 0)) ||
            (kind == BotQuestGiverKind.Doodad &&
             (giver is not Doodad || giver.Despawn > DateTime.MinValue)) ||
            bot?.ParentWorld == null || !ReferenceEquals(giver.ParentWorld, bot.ParentWorld) ||
            bot.Transform?.World == null || giver.Transform?.World == null)
        {
            return false;
        }

        var botPosition = bot.Transform.World.Position;
        var giverPosition = giver.Transform.World.Position;
        if (!IsFinite(botPosition) || !IsFinite(giverPosition))
        {
            reason = "nonfinite_transform";
            return false;
        }

        float surfaceZ;
        try
        {
            surfaceZ = _heightProvider(bot, giverPosition);
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

        distance = Vector3.Distance(botPosition, giverPosition);
        if (!BotCombatTask.IsWithinNavigableQuestTargetVolume(
                botPosition, giverPosition, surfaceZ, scanRadius))
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

#if !PLAYERBOTS_AAEMU_3_0
        if (bot.SkillTask != null || bot.ActivePlotState != null)
        {
            reason = "native_action_active";
            return false;
        }
#endif

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

        if (_plan == null && _staticStartPlan == null &&
            (movement.Destination.HasValue || movement.IsMoving))
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

        if (bot.Quests.HasQuestCompleted(quest.Id) && !quest.Repeatable)
            return false;

        return quest.GetComponents(QuestComponentKind.Start)
            .All(component => UnitRequirementsGameData.Instance.CanComponentRun(component, bot));
    }

    private static BaseUnit ResolveGiver(
        Character bot,
        BotQuestGiverKind kind,
        uint objectId) =>
        kind switch
        {
            BotQuestGiverKind.Npc => bot?.ParentWorld?.GetNpc(objectId),
            BotQuestGiverKind.Doodad => bot?.ParentWorld?.GetDoodad(objectId),
            _ => null
        };

    private static bool ValidateProductionGiverQuest(
        Character bot,
        BotQuestGiverKind kind,
        uint objectId,
        uint questId)
    {
        if (bot?.ParentWorld == null || objectId == 0 || questId == 0)
            return false;

        return kind switch
        {
            BotQuestGiverKind.Npc =>
                bot.ParentWorld.GetNpc(objectId) is { } npc &&
                QuestManager.Instance.GetPlayerBotNpcQuestStarts(npc.TemplateId)
                    .Any(quest => quest.Id == questId),
            BotQuestGiverKind.Doodad =>
                bot.ParentWorld.GetDoodad(objectId) is { } doodad &&
                doodad.TryGetPlayerBotCurrentQuest(out var currentQuestId) &&
                currentQuestId == questId,
            _ => false
        };
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

    private void BackoffCurrentPlan(
        BotRuntime runtime,
        BotConfig config,
        DateTimeOffset now,
        string reason)
    {
        if (_plan is { } plan)
        {
            var retryAt = now + TimeSpan.FromMilliseconds(config.QuestIntakeRetryBackoffMs);
            foreach (var quest in plan.Quests)
                _retryAfter[new QuestCandidateKey(plan.Kind, plan.ObjectId, quest.Id)] = retryAt;
        }

        InvalidatePlan(runtime, reason, now, stopOwnedMovement: true);
        _retryAt = EarliestRetry();
    }

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
#if !PLAYERBOTS_AAEMU_3_0
#endif
        StopOwnedMovement(runtime);
        _plan = null;
        _pendingStaticStartPlan = null;
        _staticStartPlan = null;
        _retryAfter.Clear();
        _retryAt = null;
        _staticStartEligibleAt = null;
        _nextOpportunityScanAt = DateTimeOffset.MinValue;
        _lastOpportunityScanResult = false;
        SetState(BotQuestIntakeState.Disabled, "disabled", now);
    }

    private void InvalidatePlan(
        BotRuntime runtime,
        string reason,
        DateTimeOffset now,
        bool stopOwnedMovement)
    {
#if !PLAYERBOTS_AAEMU_3_0
        if (reason != "navigation_route_unavailable")
#endif
        if (_plan == null && _pendingStaticStartPlan == null &&
            _staticStartPlan == null && !_ownedDestination.HasValue)
            return;

        var kind = _plan?.Kind;
        var objectId = _plan?.ObjectId;
        if (stopOwnedMovement)
            StopOwnedMovement(runtime);
        _plan = null;
        _pendingStaticStartPlan = null;
        _staticStartPlan = null;
        _ownedDestination = null;
        _staticStartEligibleAt = null;
        _nextOpportunityScanAt = DateTimeOffset.MinValue;
        _lastOpportunityScanResult = false;
        SetState(BotQuestIntakeState.Blocked, reason, now);
        Log(runtime.Bot.Id, "invalidated",
            $"giver={(kind.HasValue ? GiverName(kind.Value) : "none")} " +
            $"giver_obj={objectId?.ToString() ?? "none"} reason={reason}");
    }

    private void StopOwnedMovement(BotRuntime runtime)
    {
        if (OwnsCurrentMovement(runtime))
        {
            _stopMovement(runtime.Bot);
        }
        _ownedDestination = null;
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
            BotMovementOwner.None or BotMovementOwner.QuestIntake;
    }

    private void CacheOpportunityResult(bool result, DateTimeOffset now)
    {
        _lastOpportunityScanResult = result;
        _nextOpportunityScanAt = now + OpportunityScanInterval;
    }

    private static Vector3? CurrentRequestedDestination(BotRuntime runtime) =>
        runtime.MovementState.TravelDestination ?? runtime.MovementState.Destination;

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

    private static string GiverName(BotQuestGiverKind kind) =>
        kind.ToString().ToLowerInvariant();

    private readonly record struct QuestCandidateKey(
        BotQuestGiverKind Kind,
        uint ObjectId,
        uint QuestId);
    private sealed record GiverPlan(
        BotQuestGiverKind Kind,
        uint ObjectId,
        uint TemplateId,
        QuestTemplate[] Quests);
    private sealed record StaticStartPlan(
        uint NpcTemplateId,
        QuestTemplate Quest,
        Vector3 Position);
    private readonly record struct QuestCandidate(
        BotQuestGiverKind Kind,
        BaseUnit Giver,
        QuestTemplate Quest,
        bool MainStory,
        float Distance);
}
