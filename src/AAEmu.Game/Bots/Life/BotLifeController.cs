using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AAEmu.Game.Bots.Blackboard;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Units;
using NLog;

namespace AAEmu.Game.Bots.Life;

public readonly record struct BotLifeProgressionSnapshot(
    DateTimeOffset CapturedAt,
    long? Level,
    long? Experience,
    long? Hp,
    long? MaxHp,
    long? Mp,
    long? MaxMp,
    long? OccupiedBagSlots,
    long? BagItemUnits,
    bool InventoryAvailable,
    string InventorySummary,
    string InventoryFingerprint);

public readonly record struct BotLifeProgressionDelta(
    long? Level,
    long? Experience,
    long? Hp,
    long? MaxHp,
    long? Mp,
    long? MaxMp,
    long? OccupiedBagSlots,
    long? BagItemUnits,
    bool? InventoryChanged);

public enum BotLifeRecoveryState
{
    NotRequired,
    Pending,
    Completed
}

public readonly record struct BotLifeRecoveryView(
    BotLifeRecoveryState State,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? ObservedAt,
    bool? ResourcesAvailable,
    long? Hp,
    long? MaxHp,
    long? Mp,
    long? MaxMp);

public readonly record struct BotLifeControllerView(
    BotLifeSnapshot Life,
    string ProfileId,
    string Activity,
    string DecisionReason,
    DateTimeOffset? DecisionAt,
    BotLifeTransition? LastTransition,
    BotLifeRecoveryView Recovery,
    DateTimeOffset? LogoutRequestedAt,
    DateTimeOffset? LogoutCallbackAt,
    DateTimeOffset? LogoutCompletedAt,
    bool? LogoutSucceeded,
    BotLifeProgressionSnapshot? ProgressionBaseline,
    BotLifeProgressionSnapshot? ProgressionCompletion,
    BotLifeProgressionDelta? ProgressionDelta);

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
    private DateTimeOffset? _recoveryStartedAt;
    private DateTimeOffset? _recoveryCompletedAt;
    private ResourceObservation? _resourceObservation;
    private BotLifeProgressionSnapshot? _progressionBaseline;
    private BotLifeProgressionSnapshot? _progressionCompletion;
    private BotLifeProgressionDelta? _progressionDelta;

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
            _recoveryStartedAt = null;
            _recoveryCompletedAt = null;
            _resourceObservation = null;
            _progressionBaseline = null;
            _progressionCompletion = null;
            _progressionDelta = null;
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
                return _logoutQueued ||
                    (_recoveryStartedAt.HasValue && !_recoveryCompletedAt.HasValue);
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
                RecoveryView(),
                _logoutRequestedAt,
                _logoutCallbackAt,
                _logoutCompletedAt,
                _logoutSucceeded,
                _progressionBaseline,
                _progressionCompletion,
                _progressionDelta);
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

        _progressionBaseline ??= CaptureProgression(runtime.Bot, now);
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
        if (_life.State != BotLifeState.Active || combat.KillCount < 1)
        {
            return false;
        }

        var previousAvailability = _resourceObservation?.Available;
        var resources = ObserveResources(bot, now);
        _resourceObservation = resources;
        if (!IsCompletionContext(runtime, resources))
            return false;

        // The legacy combat task returns to Idle after consuming its kill goal but
        // can retain a reference to the defeated object. Clearing only that dead
        // reference establishes the required targetless boundary; this controller
        // never chooses or assigns a target.
        if (combat.Target != null && ReadBoolean(() => combat.Target.IsDead) == true)
        {
            var defeated = combat.Target;
            combat.Target = null;
            if (ReferenceEquals(bot.CurrentTarget, defeated))
                bot.CurrentTarget = null;
        }
        if (bot.CurrentTarget is Unit currentTarget && ReadBoolean(() => currentTarget.IsDead) == true)
            bot.CurrentTarget = null;

        if (combat.Target != null || bot.CurrentTarget != null)
            return false;

        if (!resources.IsDebtFree)
        {
            BeginOrContinueRecovery(runtime.Bot.Id, now, resources, previousAvailability);
            return false;
        }

        var completion = CaptureProgression(runtime.Bot, now, resources);
        var transition = BotLifeStateMachine.Transition(
            _life,
            new BotLifeEvent(BotLifeEventKind.LogoutRequested, now),
            _profile);
        _lastTransition = transition;
        _life = transition.After;
        if (!transition.Accepted || !transition.Changed)
        {
            LogTransition(runtime.Bot.Id, transition, _activity, _decisionReason);
            return false;
        }

        CompleteRecovery(runtime.Bot.Id, now, resources);
        LogTransition(runtime.Bot.Id, transition, _activity, _decisionReason);
        _progressionCompletion ??= completion;
        if (_progressionBaseline.HasValue && !_progressionDelta.HasValue)
        {
            _progressionDelta = CalculateDelta(
                _progressionBaseline.Value,
                _progressionCompletion.Value);
        }
        _logoutRequestedAt = now;
        _logoutQueued = true;
        LogProgression(runtime.Bot.Id);
        return true;
    }

    private static bool IsCompletionContext(BotRuntime runtime, ResourceObservation resources)
    {
        var bot = runtime.Bot;
        var combat = runtime.CombatState;
        var movement = runtime.MovementState;
        return ReadBoolean(() => bot.IsDead) == false && resources.Hp is > 0 &&
            bot.Transform?.World != null && bot.ParentWorld != null &&
            !combat.IsForced && !combat.IsActive && combat.CurrentState == BotCombatStateType.Idle &&
            !combat.InDuel && !combat.DuelRequestPending && !combat.IsResting &&
            !combat.RespawnScheduled && !combat.ShouldRespawn && !combat.IsSearching &&
            combat.StopAtTargetHpPercent == null && combat.NonlethalFloorReached == null &&
            combat.LostTarget == null && !combat.LastKnownTargetPosition.HasValue &&
            !combat.RoamDestination.HasValue &&
            !movement.Destination.HasValue && !movement.ApprovedNavigationDestination.HasValue &&
            movement.FollowTarget == null && !movement.IsMoving && !movement.IsFalling &&
            !movement.JumpRequested && !movement.IsJumping;
    }

    private void BeginOrContinueRecovery(
        uint botId,
        DateTimeOffset now,
        ResourceObservation resources,
        bool? previousAvailability)
    {
        _resourceObservation = resources;
        if (!_recoveryStartedAt.HasValue)
        {
            _recoveryStartedAt = now;
            LogRecovery(botId, "pending", now, resources);
            return;
        }

        if (previousAvailability != resources.Available)
            LogRecovery(botId, "pending", now, resources);
    }

    private void CompleteRecovery(uint botId, DateTimeOffset now, ResourceObservation resources)
    {
        _resourceObservation = resources;
        if (!_recoveryStartedAt.HasValue || _recoveryCompletedAt.HasValue)
            return;

        _recoveryCompletedAt = now;
        LogRecovery(botId, "completed", now, resources);
    }

    private BotLifeRecoveryView RecoveryView()
    {
        var resources = _resourceObservation;
        var state = _recoveryCompletedAt.HasValue
            ? BotLifeRecoveryState.Completed
            : _recoveryStartedAt.HasValue
                ? BotLifeRecoveryState.Pending
                : BotLifeRecoveryState.NotRequired;
        return new BotLifeRecoveryView(
            state,
            _recoveryStartedAt,
            _recoveryCompletedAt,
            resources?.ObservedAt,
            resources?.Available,
            resources?.Hp,
            resources?.MaxHp,
            resources?.Mp,
            resources?.MaxMp);
    }

    private static ResourceObservation ObserveResources(Character bot, DateTimeOffset now) =>
        new(
            now,
            ReadValue(() => bot.Hp),
            ReadValue(() => bot.MaxHp),
            ReadValue(() => bot.Mp),
            ReadValue(() => bot.MaxMp));

    private static BotLifeProgressionSnapshot CaptureProgression(
        Character bot,
        DateTimeOffset now,
        ResourceObservation? resources = null)
    {
        var inventory = CaptureInventory(bot);
        return new BotLifeProgressionSnapshot(
            now,
            ReadValue(() => bot.Level),
            ReadValue(() => bot.Experience),
            resources?.Hp ?? ReadValue(() => bot.Hp),
            resources?.MaxHp ?? ReadValue(() => bot.MaxHp),
            resources?.Mp ?? ReadValue(() => bot.Mp),
            resources?.MaxMp ?? ReadValue(() => bot.MaxMp),
            inventory.OccupiedSlots,
            inventory.ItemUnits,
            inventory.Available,
            inventory.Summary,
            inventory.Fingerprint);
    }

    private static InventoryObservation CaptureInventory(Character bot)
    {
        try
        {
            var items = bot.Inventory?.Bag?.Items;
            if (items == null)
                return InventoryObservation.Unavailable;

            var snapshot = items.ToArray();
            var occupiedSlots = new HashSet<int>();
            foreach (var item in snapshot)
            {
                if (item == null || item.Slot < 0 || item.TemplateId == 0 || item.Count <= 0 ||
                    !occupiedSlots.Add(item.Slot))
                {
                    return InventoryObservation.Unavailable;
                }
            }

            Array.Sort(snapshot, static (left, right) =>
            {
                var slot = left.Slot.CompareTo(right.Slot);
                if (slot != 0)
                    return slot;
                var template = left.TemplateId.CompareTo(right.TemplateId);
                return template != 0 ? template : left.Count.CompareTo(right.Count);
            });

            var summary = snapshot.Length == 0
                ? "empty"
                : string.Join(",", snapshot.Select(item => string.Concat(
                    item.Slot.ToString(CultureInfo.InvariantCulture), ":",
                    item.TemplateId.ToString(CultureInfo.InvariantCulture), ":",
                    item.Count.ToString(CultureInfo.InvariantCulture))));
            var units = snapshot.Sum(item => (long)item.Count);
            var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(summary)))
                .ToLowerInvariant();
            return new InventoryObservation(true, snapshot.LongLength, units, summary, fingerprint);
        }
        catch
        {
            return InventoryObservation.Unavailable;
        }
    }

    private static BotLifeProgressionDelta CalculateDelta(
        BotLifeProgressionSnapshot baseline,
        BotLifeProgressionSnapshot completion) =>
        new(
            Difference(baseline.Level, completion.Level),
            Difference(baseline.Experience, completion.Experience),
            Difference(baseline.Hp, completion.Hp),
            Difference(baseline.MaxHp, completion.MaxHp),
            Difference(baseline.Mp, completion.Mp),
            Difference(baseline.MaxMp, completion.MaxMp),
            Difference(baseline.OccupiedBagSlots, completion.OccupiedBagSlots),
            Difference(baseline.BagItemUnits, completion.BagItemUnits),
            baseline.InventoryAvailable && completion.InventoryAvailable
                ? !string.Equals(
                    baseline.InventoryFingerprint,
                    completion.InventoryFingerprint,
                    StringComparison.Ordinal)
                : null);

    private void LogProgression(uint botId)
    {
        if (!_progressionBaseline.HasValue || !_progressionCompletion.HasValue || !_progressionDelta.HasValue)
            return;

        var baseline = _progressionBaseline.Value;
        var completion = _progressionCompletion.Value;
        var delta = _progressionDelta.Value;
        try
        {
            Logger.Info(
                $"BOT id={botId} ev=life_progression activity={_activity ?? "none"} reason={_decisionReason ?? "none"} " +
                $"baseline_at={Timestamp(baseline.CapturedAt)} completion_at={Timestamp(completion.CapturedAt)} " +
                $"level_before={Value(baseline.Level)} level_after={Value(completion.Level)} level_delta={Signed(delta.Level)} " +
                $"experience_before={Value(baseline.Experience)} experience_after={Value(completion.Experience)} experience_delta={Signed(delta.Experience)} " +
                $"hp_before={Value(baseline.Hp)} hp_after={Value(completion.Hp)} hp_delta={Signed(delta.Hp)} " +
                $"max_hp_before={Value(baseline.MaxHp)} max_hp_after={Value(completion.MaxHp)} max_hp_delta={Signed(delta.MaxHp)} " +
                $"mp_before={Value(baseline.Mp)} mp_after={Value(completion.Mp)} mp_delta={Signed(delta.Mp)} " +
                $"max_mp_before={Value(baseline.MaxMp)} max_mp_after={Value(completion.MaxMp)} max_mp_delta={Signed(delta.MaxMp)} " +
                $"bag_slots_before={Value(baseline.OccupiedBagSlots)} bag_slots_after={Value(completion.OccupiedBagSlots)} bag_slots_delta={Signed(delta.OccupiedBagSlots)} " +
                $"bag_units_before={Value(baseline.BagItemUnits)} bag_units_after={Value(completion.BagItemUnits)} bag_units_delta={Signed(delta.BagItemUnits)} " +
                $"inventory_before={(baseline.InventoryAvailable ? "available" : "unavailable")} inventory_after={(completion.InventoryAvailable ? "available" : "unavailable")} " +
                $"inventory_changed={Boolean(delta.InventoryChanged)} inventory_summary_before={baseline.InventorySummary} inventory_summary_after={completion.InventorySummary} " +
                $"inventory_fingerprint_before={baseline.InventoryFingerprint} inventory_fingerprint_after={completion.InventoryFingerprint}");
        }
        catch
        {
            // Observability must never reject or delay the accepted logout.
        }
    }

    private static long? ReadValue(Func<long> read)
    {
        try
        {
            return read();
        }
        catch
        {
            return null;
        }
    }

    private static bool? ReadBoolean(Func<bool> read)
    {
        try
        {
            return read();
        }
        catch
        {
            return null;
        }
    }

    private static long? Difference(long? before, long? after) =>
        before.HasValue && after.HasValue ? after.Value - before.Value : null;

    private static string Value(long? value) =>
        value?.ToString(CultureInfo.InvariantCulture) ?? "unavailable";

    private static string Signed(long? value) =>
        value?.ToString("+0;-0;+0", CultureInfo.InvariantCulture) ?? "unavailable";

    private static string Boolean(bool? value) =>
        value.HasValue ? value.Value.ToString().ToLowerInvariant() : "unavailable";

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

    private void LogRecovery(
        uint botId,
        string state,
        DateTimeOffset now,
        ResourceObservation resources)
    {
        try
        {
            Logger.Info(
                $"BOT id={botId} ev=life_recovery state={state} activity={_activity ?? "none"} " +
                $"reason={_decisionReason ?? "none"} started_at={Timestamp(_recoveryStartedAt)} " +
                $"completed_at={Timestamp(_recoveryCompletedAt)} observed_at={Timestamp(now)} " +
                $"resources={(resources.Available ? "available" : "unavailable")} " +
                $"hp={Value(resources.Hp)} max_hp={Value(resources.MaxHp)} " +
                $"mp={Value(resources.Mp)} max_mp={Value(resources.MaxMp)}");
        }
        catch
        {
            // Recovery observability is fail-closed and must never escape the host tick.
        }
    }

    private static string Timestamp(DateTimeOffset? value) =>
        value?.ToUniversalTime().ToString("O") ?? "none";

    private readonly record struct InventoryObservation(
        bool Available,
        long? OccupiedSlots,
        long? ItemUnits,
        string Summary,
        string Fingerprint)
    {
        internal static InventoryObservation Unavailable { get; } =
            new(false, null, null, "unavailable", "unavailable");
    }

    private readonly record struct ResourceObservation(
        DateTimeOffset ObservedAt,
        long? Hp,
        long? MaxHp,
        long? Mp,
        long? MaxMp)
    {
        internal bool Available =>
            Hp.HasValue && MaxHp is > 0 && Mp.HasValue && MaxMp is > 0 &&
            Hp.Value >= 0 && Hp.Value <= MaxHp.Value &&
            Mp.Value >= 0 && Mp.Value <= MaxMp.Value;

        internal bool IsDebtFree =>
            Available && Hp == MaxHp && Mp == MaxMp;
    }
}
