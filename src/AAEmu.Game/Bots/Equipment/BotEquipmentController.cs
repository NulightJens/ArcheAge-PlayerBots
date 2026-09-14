#if !PLAYERBOTS_AAEMU_3_0
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Bots.Questing;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using NLog;

namespace AAEmu.Game.Bots.Equipment;

internal sealed record BotGearOpenCandidate(BotInteractionPlan Plan, int SourceCount,
    Dictionary<uint, int> BeforeCounts);
internal enum BotGearOpenResult { Pending, Opened, SourceMissing }
internal interface IBotEquipmentAuthority
{
    void Equip(Character bot);
    BotGearOpenCandidate Find(Character bot, IReadOnlyDictionary<ulong, DateTimeOffset> retries, DateTimeOffset now);
    bool Open(Character bot, BotGearOpenCandidate candidate);
    BotGearOpenResult Observe(Character bot, BotGearOpenCandidate candidate);
}

/// <summary>A quiet inventory action in the quest decision loop, with native-result confirmation.</summary>
internal sealed class BotEquipmentController
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly IBotEquipmentAuthority _authority;
    private readonly Dictionary<ulong, DateTimeOffset> _retries = new();
    private BotGearOpenCandidate _pending;
    private DateTimeOffset _deadline;
    private DateTimeOffset _nextScan;
    internal string Reason { get; private set; } = "not_checked";

    internal BotEquipmentController(IBotEquipmentAuthority authority = null) =>
        _authority = authority ?? new BotEquipmentAuthority();

    internal bool Step(BotRuntime runtime, BotConfig config, DateTimeOffset now)
    {
        if (runtime.Driver.Owns(runtime.Bot))
        { Reason = "connected_native_equipment_not_bound"; return false; }
        if ((!config.QuestIntakeEnabled && !config.QuestCompletionEnabled) ||
            runtime.CombatState.ForcedState != null)
        { Reason = "held"; return false; }
        var bot = runtime.Bot;
        if (bot.IsDead || bot.IsInBattle || runtime.CombatState.Target != null ||
            runtime.CombatState.CurrentState is BotCombatStateType.Combat or BotCombatStateType.Dueling ||
            runtime.MovementState.Climb != null || runtime.MovementState.IsFalling ||
            runtime.MovementState.IsJumping || bot.Transform?.Parent != null || bot.Transform?.StickyParent != null)
        { Reason = "actor_busy"; return false; }
        if (_pending != null)
        {
            var result = _authority.Observe(bot, _pending);
            if (result == BotGearOpenResult.Opened && bot.SkillTask == null)
            {
                Logger.Info($"BOT id={bot.Id} ev=gear_container_opened item={_pending.Plan.ItemId}");
                _pending = null;
                _authority.Equip(bot);
                Reason = "opened_and_checked_equipment";
                return true;
            }
            if (now < _deadline) { Reason = "waiting_for_native_inventory"; return true; }
            Logger.Warn($"BOT id={bot.Id} ev=gear_container_unconfirmed item={_pending.Plan.ItemId} result={result}");
            _retries[_pending.Plan.ItemInstanceId] = now.AddMinutes(5);
            _pending = null;
            Reason = "native_result_timeout";
            return false;
        }
        if (now < _nextScan || bot.SkillTask != null || runtime.MovementState.IsMoving ||
            runtime.MovementState.Destination.HasValue || runtime.MovementState.FollowTarget != null)
            return false;
        _nextScan = now.AddSeconds(2);
        // Do not capture now in a lambda: its closure would allocate on held ticks too.
        foreach (var pair in _retries.ToArray())
            if (pair.Value <= now) _retries.Remove(pair.Key);
        if (_retries.Count >= 64) { Reason = "retry_limit"; return false; }
        _authority.Equip(bot);
        var candidate = _authority.Find(bot, _retries, now);
        if (candidate == null) { Reason = "no_available_gear_container"; return false; }
        // Backoff is installed before the native call, including rejected attempts.
        _retries[candidate.Plan.ItemInstanceId] = now.AddMinutes(5);
        if (!_authority.Open(bot, candidate)) { Reason = "native_item_use_rejected"; return false; }
        _pending = candidate;
        _deadline = now.AddMilliseconds(Math.Clamp(candidate.Plan.DurationMs + 5000, 10000, 30000));
        Reason = "opening_gear_container";
        Logger.Info($"BOT id={bot.Id} ev=gear_container_use item={candidate.Plan.ItemId} instance={candidate.Plan.ItemInstanceId}");
        return true;
    }
}

internal sealed class BotEquipmentAuthority : IBotEquipmentAuthority
{
    public void Equip(Character bot)
    {
        var manager = BotArchetypeManager.Instance;
        var state = manager.GetState(bot);
        if (state != null) manager.EquipBestGear(bot, state);
    }

    public BotGearOpenCandidate Find(Character bot, IReadOnlyDictionary<ulong, DateTimeOffset> retries, DateTimeOffset now)
    {
        if (bot.Inventory?.Bag == null) return null;
        foreach (var item in bot.Inventory.Bag.Items.Where(item => item != null).OrderBy(item => item.Slot))
        {
            if (retries.TryGetValue(item.Id, out var until) && now < until ||
                !BotEquipmentPolicy.Usable(item.Template, bot.Level)) continue;
            var container = BotGearContainers.Read(item.Template);
            if (container == null || bot.Inventory.Bag.FreeSlotCount < container.RequiredFreeSlots ||
                container.Contents.Any(output => BotEquipmentPolicy.Rank(bot.Ability1, output) < 0) ||
                !container.Contents.Any(output => BotEquipmentPolicy.Rank(bot.Ability1, output) > 0)) continue;
            // The pinned native effect removes its source instance, so never open a stack.
            if (item.Count != 1) continue;
            var counts = container.Contents.Select(output => output.Id).Distinct()
                .ToDictionary(id => id, id => Count(bot, id));
            var plan = new BotInteractionPlan(true, "gear_container", container.Skill.Id, item.Id,
                item.TemplateId, 0, 0, 0, default, 0, true,
                container.Skill.CastingTime + container.Skill.EffectDelay);
            return new(plan, item.Count, counts);
        }
        return null;
    }

    public bool Open(Character bot, BotGearOpenCandidate candidate)
    {
        var source = bot.Inventory?.Bag?.Items.FirstOrDefault(item => item?.Id == candidate.Plan.ItemInstanceId);
        var container = BotGearContainers.Read(source?.Template);
        if (source == null || source.Count != candidate.SourceCount || source.Count != 1 || container == null ||
            bot.Inventory.Bag.FreeSlotCount < container.RequiredFreeSlots) return false;
        return BotQuestInteractions.Execute(bot, candidate.Plan).Started;
    }

    public BotGearOpenResult Observe(Character bot, BotGearOpenCandidate candidate)
    {
        var source = bot.Inventory.Bag.Items.FirstOrDefault(item => item?.Id == candidate.Plan.ItemInstanceId);
        if (source != null && source.Count >= candidate.SourceCount) return BotGearOpenResult.Pending;
        return candidate.BeforeCounts.Any(pair => Count(bot, pair.Key) > pair.Value)
            ? BotGearOpenResult.Opened : BotGearOpenResult.SourceMissing;
    }

    private static int Count(Character bot, uint templateId) => bot.Inventory.Bag.Items
        .Concat(bot.Inventory.Equipment.Items).Where(item => item?.TemplateId == templateId).Sum(item => item.Count);
}
#endif
