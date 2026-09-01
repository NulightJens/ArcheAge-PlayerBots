using AAEmu.Game.Bots.Blackboard;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Units;
using NLog;

namespace AAEmu.Game.Bots.Life;

public readonly record struct BotLifeControllerView(
    BotLifeSnapshot Life,
    string ProfileId,
    string Activity,
    string DecisionReason,
    DateTimeOffset? DecisionAt,
    BotLifeTransition? LastTransition,
    DateTimeOffset? LogoutRequestedAt,
    DateTimeOffset? LogoutCallbackAt,
    DateTimeOffset? LogoutCompletedAt,
    bool? LogoutSucceeded);

/// <summary>
/// Owns the bounded production lifecycle for one runtime. Population eligibility
/// remains a host concern, while every life transition is accepted by the shared
/// deterministic state machine before combat or logout state changes are applied.
/// </summary>
public sealed class BotLifeController
{
    private const string GrindActivity = "grind";
    private const string NearbyMortalReason = "nearby_mortal";

    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();
    private static BotBehaviorProfile DefaultProfile { get; } = new(
        "single-bot-one-kill",
        TimeSpan.Zero,
        TimeSpan.MaxValue,
        TimeSpan.Zero,
        TimeSpan.MaxValue);

    private readonly object _syncRoot = new();
    private readonly BotBehaviorProfile _profile;
    private BotLifeSnapshot _life = new(BotLifeState.Offline, DateTimeOffset.MinValue);
    private BotLifeTransition? _lastTransition;
    private string _activity;
    private string _decisionReason;
    private DateTimeOffset? _decisionAt;
    private DateTimeOffset? _logoutRequestedAt;
    private DateTimeOffset? _logoutCallbackAt;
    private DateTimeOffset? _logoutCompletedAt;
    private bool? _logoutSucceeded;
    private bool _logoutQueued;

    public BotLifeController(BotBehaviorProfile profile = null)
    {
        _profile = profile ?? DefaultProfile;
    }

    internal void ResetPostSpawn(uint botId, DateTimeOffset now)
    {
        lock (_syncRoot)
        {
            _life = new BotLifeSnapshot(BotLifeState.Idle, now);
            _lastTransition = null;
            _activity = null;
            _decisionReason = null;
            _decisionAt = null;
            _logoutRequestedAt = null;
            _logoutCallbackAt = null;
            _logoutCompletedAt = null;
            _logoutSucceeded = null;
            _logoutQueued = false;
        }

        Logger.Info($"BOT id={botId} ev=life_registered state=Idle entered_at={Timestamp(now)} profile={_profile.Id}");
    }

    internal bool Step(BotRuntime runtime, bool isSoleRuntime, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        lock (_syncRoot)
        {
            if (!isSoleRuntime || runtime.Retired || _logoutQueued)
                return false;

            if (_activity == null)
                return TryActivate(runtime, now);

            return TryRequestLogout(runtime, now);
        }
    }

    internal bool TryBeginLogoutCallback(DateTimeOffset now)
    {
        lock (_syncRoot)
        {
            if (!_logoutQueued || _logoutCallbackAt.HasValue)
                return false;

            _logoutCallbackAt = now;
            return true;
        }
    }

    internal bool ShouldSuspendRuntime
    {
        get
        {
            lock (_syncRoot)
                return _logoutQueued;
        }
    }

    internal void RecordLogoutResult(uint botId, bool succeeded, DateTimeOffset now)
    {
        BotLifeTransition? completion = null;
        lock (_syncRoot)
        {
            if (!_logoutQueued || !_logoutCallbackAt.HasValue || _logoutSucceeded.HasValue)
                return;

            _logoutSucceeded = succeeded;
            _logoutCompletedAt = now;
            if (succeeded)
            {
                completion = BotLifeStateMachine.Transition(
                    _life,
                    new BotLifeEvent(BotLifeEventKind.DespawnCompleted, now),
                    _profile);
                _lastTransition = completion;
                _life = completion.Value.After;
            }
        }

        Logger.Info(
            $"BOT id={botId} ev=life_logout_result activity={_activity} reason={_decisionReason} " +
            $"success={succeeded.ToString().ToLowerInvariant()} requested_at={Timestamp(_logoutRequestedAt)} " +
            $"callback_at={Timestamp(_logoutCallbackAt)} completed_at={Timestamp(now)} state={_life.State} " +
            $"transition_outcome={completion?.Outcome.ToString() ?? "retained"} " +
            $"transition_reason={completion?.Reason.ToString() ?? "callback_failed"}");
    }

    public BotLifeControllerView Inspect()
    {
        lock (_syncRoot)
        {
            return new BotLifeControllerView(
                _life,
                _profile.Id,
                _activity,
                _decisionReason,
                _decisionAt,
                _lastTransition,
                _logoutRequestedAt,
                _logoutCallbackAt,
                _logoutCompletedAt,
                _logoutSucceeded);
        }
    }

    private bool TryActivate(BotRuntime runtime, DateTimeOffset now)
    {
        var bot = runtime.Bot;
        var combat = runtime.CombatState;
        if (_life.State != BotLifeState.Idle || bot.IsDead || bot.Hp <= 0 ||
            bot.Transform?.World == null || bot.ParentWorld == null ||
            runtime.Brain == null || runtime.Brain.Cancelled ||
            runtime.Mover == null || runtime.Mover.Cancelled ||
            combat.IsForced || combat.IsActive || combat.CurrentState != BotCombatStateType.Idle ||
            combat.Target != null || bot.CurrentTarget != null || combat.InDuel || combat.IsResting ||
            combat.RespawnScheduled || combat.ShouldRespawn || combat.StopAtTargetHpPercent.HasValue ||
            combat.NonlethalFloorReached != null || combat.IsSearching || combat.LostTarget != null ||
            combat.LastKnownTargetPosition.HasValue || combat.RoamDestination.HasValue ||
            runtime.MovementState.Destination.HasValue || runtime.MovementState.FollowTarget != null)
        {
            return false;
        }

        if (!runtime.Blackboard.TryGet(BotValues.NearbyHostileNpcIds, now.UtcDateTime, out List<uint> opportunities) ||
            opportunities == null || opportunities.Count == 0)
        {
            return false;
        }

        var hasLivingOpportunity = false;
        foreach (var npcId in opportunities)
        {
            var npc = bot.ParentWorld.GetNpc(npcId);
            if (npc != null && !npc.IsDead)
            {
                hasLivingOpportunity = true;
                break;
            }
        }
        if (!hasLivingOpportunity)
            return false;

        var transition = BotLifeStateMachine.Transition(
            _life,
            new BotLifeEvent(BotLifeEventKind.ActivityRequested, now),
            _profile);
        _lastTransition = transition;
        _life = transition.After;
        LogTransition(runtime.Bot.Id, transition, GrindActivity, NearbyMortalReason);
        if (!transition.Accepted || !transition.Changed)
            return false;

        combat.KillCount = 0;
        combat.KillGoal = 1;
        combat.TargetTypeFilter = null;
        combat.TransitionTo(BotCombatStateType.Grinding);
        _activity = GrindActivity;
        _decisionReason = NearbyMortalReason;
        _decisionAt = now;
        return false;
    }

    private bool TryRequestLogout(BotRuntime runtime, DateTimeOffset now)
    {
        var bot = runtime.Bot;
        var combat = runtime.CombatState;
        if (_life.State != BotLifeState.Active || combat.KillCount < 1 || bot.IsDead || bot.Hp <= 0 ||
            bot.Transform?.World == null || bot.ParentWorld == null || combat.IsForced || combat.IsActive ||
            combat.CurrentState != BotCombatStateType.Idle || combat.InDuel || combat.IsResting ||
            combat.IsSearching)
        {
            return false;
        }

        // The legacy combat task returns to Idle after consuming its kill goal but
        // can retain a reference to the defeated object. Clearing only that dead
        // reference establishes the required targetless boundary; this controller
        // never chooses or assigns a target.
        if (combat.Target?.IsDead == true)
        {
            var defeated = combat.Target;
            combat.Target = null;
            if (ReferenceEquals(bot.CurrentTarget, defeated))
                bot.CurrentTarget = null;
        }
        if (bot.CurrentTarget is Unit currentTarget && currentTarget.IsDead)
            bot.CurrentTarget = null;

        if (combat.Target != null || bot.CurrentTarget != null)
            return false;

        var transition = BotLifeStateMachine.Transition(
            _life,
            new BotLifeEvent(BotLifeEventKind.LogoutRequested, now),
            _profile);
        _lastTransition = transition;
        _life = transition.After;
        LogTransition(runtime.Bot.Id, transition, _activity, _decisionReason);
        if (!transition.Accepted || !transition.Changed)
            return false;

        _logoutRequestedAt = now;
        _logoutQueued = true;
        return true;
    }

    private static void LogTransition(
        uint botId,
        BotLifeTransition transition,
        string activity,
        string decisionReason)
    {
        Logger.Info(
            $"BOT id={botId} ev=life_transition event={transition.Event.Kind} before={transition.Before.State} " +
            $"after={transition.After.State} outcome={transition.Outcome} transition_reason={transition.Reason} " +
            $"at={Timestamp(transition.Event.At)} entered_at={Timestamp(transition.After.EnteredAt)} " +
            $"activity={activity ?? "none"} reason={decisionReason ?? "none"}");
    }

    private static string Timestamp(DateTimeOffset? value) =>
        value?.ToUniversalTime().ToString("O") ?? "none";
}
