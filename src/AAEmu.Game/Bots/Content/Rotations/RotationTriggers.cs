using System.Numerics;
using AAEmu.Game.Bots.Body;
using AAEmu.Game.Bots.Body.Positioning;
using AAEmu.Game.Bots.Content.Rotations.Values;
using AAEmu.Game.Bots.Content.Triggers;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Bots.Content.Rotations.Triggers;

public sealed class RotationPredicateTrigger(string name, Func<BotContext, bool> predicate, int checkIntervalMs = 100) : IBotTrigger
{
    public string Name { get; } = name;
    public int CheckIntervalMs { get; } = checkIntervalMs;
    public BotEvent Event => new(Name);
    public bool IsActive(BotContext context) => predicate(context);
}

public sealed class RotationTimerTrigger(string name, TimeSpan every, float probability, Func<int> roll) : IBotTrigger
{
    private DateTime _lastFired = DateTime.MinValue;
    private readonly object _lock = new();

    public string Name { get; } = name;
    public int CheckIntervalMs => (int)Math.Min(Math.Max(every.TotalMilliseconds, 1), int.MaxValue);
    public BotEvent Event => new(Name);

    public bool IsActive(BotContext context)
    {
        lock (_lock)
        {
            if (_lastFired != DateTime.MinValue && context.Now - _lastFired < every)
                return false;
            if (probability < 1f && Math.Abs(roll()) % 1000 >= probability * 1000f)
                return false;
            _lastFired = context.Now;
            return true;
        }
    }
}

public sealed class RotationTriggerFactory
{
    private readonly Func<string, uint?> _skillResolver;
    private readonly Func<uint, SkillTemplate> _templateResolver;
    private readonly Func<int> _roll;
    private readonly RotationValueResolver _values;
    private readonly Dictionary<string, DateTime> _groupCooldowns = new(StringComparer.OrdinalIgnoreCase);

    public RotationTriggerFactory(Func<string, uint?> skillResolver, Func<uint, SkillTemplate> templateResolver,
        Func<int> roll = null, RotationValueResolver values = null)
    {
        _skillResolver = skillResolver;
        _templateResolver = templateResolver;
        _roll = roll ?? (() => 0);
        _values = values ?? new RotationValueResolver();
    }

    public IBotTrigger Create(BotRotationWhen when, uint? ruleSkillId = null)
    {
        ArgumentNullException.ThrowIfNull(when);
        var arguments = new ParsedArguments(when);
        var skillId = arguments.Skill == null ? ruleSkillId : _skillResolver(arguments.Skill);
        var children = when.Children?.Select(child => Create(child)).ToArray() ?? [];
        var kind = when.Kind?.ToLowerInvariant();
        var canCast = skillId.HasValue
            ? new BotCastSkillAction(skillId.Value, templateResolver: _templateResolver)
            : null;
        return kind switch
        {
            "cancast" => new RotationPredicateTrigger(when.Kind, context => canCast?.IsPossible(context) == true),
            "cooldownready" => new RotationPredicateTrigger(when.Kind, context => skillId.HasValue &&
                IsReady(context, skillId.Value)),
            "range" => new RotationPredicateTrigger(when.Kind, context =>
                (skillId.HasValue || arguments.Min.HasValue || arguments.Max.HasValue) &&
                IsInRange(context, skillId.GetValueOrDefault(), arguments)),
            "healthband" => new RotationPredicateTrigger(when.Kind, context => InHealthBand(context, arguments)),
            "partylowest" => new RotationPredicateTrigger(when.Kind, context => PartyLowestInHealthBand(context, arguments)),
            "hastarget" => new RotationPredicateTrigger(when.Kind, context =>
                PositioningHelpers.IsValidTarget(context.Runtime.CombatState.Target)),
            "pvp" => new RotationPredicateTrigger(when.Kind, context => context.Runtime.CombatState.Target is Character),
            "stunned" => new RotationPredicateTrigger(when.Kind, context => IsStunned(context.Bot)),
            "not" => new RotationPredicateTrigger(when.Kind, context => children.Length == 1 && !children[0].IsActive(context)),
            "groupcooldown" => new RotationPredicateTrigger(when.Kind, context =>
                IsGroupCooldownReady(arguments.Group, arguments.Milliseconds ?? 0, context.Now)),
            "comboactive" => new RotationPredicateTrigger(when.Kind, context =>
                _values.ComboState(context) &&
                (!skillId.HasValue || context.Runtime.CombatState.LastComboSkill == skillId.Value)),
            "chainstep" => new RotationPredicateTrigger(when.Kind, context =>
                context.Runtime.CombatState.TripleSlashStage == arguments.Steps &&
                (arguments.StepDelayMs is not { } delayMs ||
                 context.Now - context.Runtime.CombatState.LastTripleSlashTime >= TimeSpan.FromMilliseconds(delayMs))),
            "controlled" => new RotationPredicateTrigger(when.Kind, context => IsControlled(context.Bot)),
            "all" => new RotationPredicateTrigger(when.Kind, context => children.All(trigger => trigger.IsActive(context))),
            "any" => new RotationPredicateTrigger(when.Kind, context => children.Any(trigger => trigger.IsActive(context))),
            "timer" => new RotationTimerTrigger(when.Kind, TimeSpan.FromMilliseconds(Math.Max(1, arguments.EveryMs ?? 1000)),
                Math.Clamp(arguments.Probability ?? 1f, 0f, 1f), _roll),
            "resource" => new RotationPredicateTrigger(when.Kind, context => ResourceInRange(context, arguments)),
            "enemycount" => new RotationPredicateTrigger(when.Kind, context => EnemyCountAtLeast(context, arguments)),
            "targetcasting" => new RotationPredicateTrigger(when.Kind, context =>
                context.Runtime.CombatState.Target?.SkillTask != null),
            "buffmissing" or "hasnoaura" => new RotationPredicateTrigger(when.Kind, context =>
                BuffMissing(context.Bot, skillId, context.Now, arguments, refresh: true)),
            "buffpresent" or "hasaura" => new RotationPredicateTrigger(when.Kind, context =>
                HasBuff(context.Bot, skillId, context.Now)),
            "debuffmissing" => new RotationPredicateTrigger(when.Kind, context =>
                BuffMissing(ResolveOnTarget(context, arguments), skillId, context.Now, arguments, refresh: false)),
            "hascleansabledebuff" => new RotationPredicateTrigger(when.Kind, context => HasCleansableDebuff(context.Bot)),
            _ => throw new ArgumentException($"Unknown trigger kind '{when.Kind}'.", nameof(when))
        };
    }

    public void ClaimGroupCooldown(BotRotationWhen when, DateTime now)
    {
        if (when == null)
            return;

        if (string.Equals(when.Kind, "groupCooldown", StringComparison.OrdinalIgnoreCase))
        {
            var arguments = new ParsedArguments(when);
            if (!string.IsNullOrWhiteSpace(arguments.Group))
                _groupCooldowns[arguments.Group] = now;
        }

        foreach (var child in when.Children ?? [])
            ClaimGroupCooldown(child, now);
    }

    public Action<BotContext> CreateGroupCooldownSuccessHandler(params BotRotationWhen[] whens)
    {
        var groupCooldowns = new List<BotRotationWhen>();
        foreach (var when in whens ?? [])
            CollectGroupCooldowns(when, groupCooldowns);
        if (groupCooldowns.Count == 0)
            return null;

        return context =>
        {
            foreach (var when in groupCooldowns)
                ClaimGroupCooldown(when, context.Now);
        };
    }

    private static void CollectGroupCooldowns(BotRotationWhen when, List<BotRotationWhen> groupCooldowns)
    {
        if (when == null)
            return;
        if (string.Equals(when.Kind, "groupCooldown", StringComparison.OrdinalIgnoreCase))
            groupCooldowns.Add(when);
        foreach (var child in when.Children ?? [])
            CollectGroupCooldowns(child, groupCooldowns);
    }

    private bool IsReady(BotContext context, uint skillId)
    {
        var target = context.Runtime.CombatState.Target;
        var distance = Distance(context.Bot, target);
        return BotSkillGate.Check(context.Bot, _templateResolver(skillId), target, distance, context.Now, context.Config).IsAllowed;
    }

    private bool IsInRange(BotContext context, uint skillId, ParsedArguments arguments)
    {
        var target = context.Runtime.CombatState.Target;
        if (target?.Transform == null)
            return false;
        var template = skillId == 0 ? null : _templateResolver(skillId);
        var distance = _values.Distance(context);
        var min = arguments.Min ?? template?.MinRange ?? 0;
        var max = arguments.Max ?? template?.MaxRange ?? float.MaxValue;
        return distance >= min && distance <= max;
    }

    private static bool InHealthBand(BotContext context, ParsedArguments arguments)
    {
        var percent = context.Bot.MaxHp <= 0 ? 0f : context.Bot.Hp * 100f / context.Bot.MaxHp;
        var min = arguments.Min ?? 0;
        var max = arguments.Max ?? 100;
        return percent >= min && percent <= max;
    }

    private static bool PartyLowestInHealthBand(BotContext context, ParsedArguments arguments)
    {
        return context.Runtime.Social.CommitLowestHealthMember(
            arguments.Radius ?? 30f,
            arguments.Min ?? 0f,
            arguments.Max ?? 100f) != null;
    }

    private bool ResourceInRange(BotContext context, ParsedArguments arguments)
    {
        var value = Convert.ToSingle(_values.Stat(context, arguments.Stat, true));
        return value >= (arguments.Min ?? float.MinValue) && value <= (arguments.Max ?? float.MaxValue);
    }

    private bool EnemyCountAtLeast(BotContext context, ParsedArguments arguments)
    {
        return _values.EnemyCount(context, arguments.Radius ?? 10f) >= (arguments.Minimum ?? 1);
    }

    private static Unit ResolveOnTarget(BotContext context, ParsedArguments arguments) =>
        string.Equals(arguments.On, "target", StringComparison.OrdinalIgnoreCase)
            ? context.Runtime.CombatState.Target
            : context.Bot;

    private static bool BuffMissing(Unit unit, uint? skillId, DateTime now, ParsedArguments arguments, bool refresh)
    {
        if (unit?.Buffs == null || !skillId.HasValue)
            return true;
        var threshold = refresh ? arguments.RefreshBeforeMs ?? 0 : arguments.MinLifetimeMs ?? 0;
        return !unit.Buffs.HasEffectsMatchingCondition(buff => buff?.Template?.Id == skillId.Value &&
            Remaining(buff, now) > threshold);
    }

    private static bool HasBuff(Unit unit, uint? skillId, DateTime now)
    {
        if (unit?.Buffs == null || !skillId.HasValue)
            return false;
        return unit.Buffs.HasEffectsMatchingCondition(buff => buff?.Template?.Id == skillId.Value &&
            Remaining(buff, now) > 0);
    }

    private static double Remaining(Buff buff, DateTime now) =>
        buff.EndTime == DateTime.MinValue || buff.Duration <= 0 ? double.PositiveInfinity : (buff.EndTime - now).TotalMilliseconds;

    private static bool IsControlled(Character bot) => bot?.Buffs?.HasEffectsMatchingCondition(static buff =>
        buff?.Template is { Stun: true } or { Root: true } or { Sleep: true } or { Knockdown: true }) == true;

    private static bool IsStunned(Character bot) => bot?.Buffs?.HasEffectsMatchingCondition(static buff =>
        buff?.Template is { Stun: true } or { Knockdown: true }) == true;

    private static bool HasCleansableDebuff(Character bot) => bot?.Buffs?.HasEffectsMatchingCondition(static buff =>
        buff?.Template is { Kind: BuffKind.Bad } template &&
        (template.Root || template.Stun || template.Knockdown || template.Sleep || template.Cripled || template.Psychokinesis)) == true;

    private static float Distance(Unit first, Unit second) => first?.Transform == null || second?.Transform == null
        ? 0f
        : Vector3.Distance(first.Transform.World.Position, second.Transform.World.Position);

    private bool IsGroupCooldownReady(string group, double milliseconds, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(group))
            return false;
        if (_groupCooldowns.TryGetValue(group, out var lastFired) &&
            now - lastFired < TimeSpan.FromMilliseconds(Math.Max(0, milliseconds)))
            return false;
        return true;
    }

    private sealed class ParsedArguments
    {
        public ParsedArguments(BotRotationWhen when)
        {
            Skill = StringArgument(when, "skill") ?? StringArgument(when, "spell") ?? StringArgument(when, "opener");
            On = StringArgument(when, "on");
            Stat = StringArgument(when, "stat");
            Min = NumberArgument<float>(when, "min");
            Max = NumberArgument<float>(when, "max");
            Radius = NumberArgument<float>(when, "radius");
            Minimum = NumberArgument<int>(when, "min");
            Steps = NumberArgument<int>(when, "steps") ?? 0;
            StepDelayMs = NumberArgument<double>(when, "stepDelayMs");
            EveryMs = NumberArgument<double>(when, "every");
            Probability = NumberArgument<float>(when, "probability");
            RefreshBeforeMs = NumberArgument<double>(when, "refreshBefore");
            MinLifetimeMs = NumberArgument<double>(when, "minLifetime");
            Group = StringArgument(when, "group");
            Milliseconds = NumberArgument<double>(when, "ms") ?? NumberArgument<double>(when, "cooldownMs");
        }

        public string Skill { get; }
        public string On { get; }
        public string Stat { get; }
        public float? Min { get; }
        public float? Max { get; }
        public float? Radius { get; }
        public int? Minimum { get; }
        public int Steps { get; }
        public double? StepDelayMs { get; }
        public double? EveryMs { get; }
        public float? Probability { get; }
        public double? RefreshBeforeMs { get; }
        public double? MinLifetimeMs { get; }
        public string Group { get; }
        public double? Milliseconds { get; }

        private static string StringArgument(BotRotationWhen when, string name) =>
            when.Arguments.TryGetValue(name, out var token) ? token.ToObject<string>() : null;

        private static T? NumberArgument<T>(BotRotationWhen when, string name) where T : struct =>
            when.Arguments.TryGetValue(name, out var token) ? token.ToObject<T>() : null;
    }
}
