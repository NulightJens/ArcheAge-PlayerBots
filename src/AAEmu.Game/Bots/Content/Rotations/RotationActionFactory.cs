using System.Numerics;
using AAEmu.Game.Bots.Body;
using AAEmu.Game.Bots.Body.Positioning;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Bots.Social;
using AAEmu.Game.Models.Game.AI.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.Skills.Static;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Bots.Content.Rotations;

public sealed class RotationIdleAction : IBotAction
{
    private readonly bool _holdsSpellRange;
    private readonly float _minimumRange;
    private readonly float _maximumRange;
    private readonly IBotMover _mover;

    public RotationIdleAction(float? maximumRange = null, IBotMover mover = null)
    {
        _holdsSpellRange = maximumRange.HasValue && maximumRange.Value > 2f;
        _maximumRange = Math.Max(0f, maximumRange ?? 0f);
        _minimumRange = Math.Max(0f, _maximumRange - 2f);
        _mover = mover ?? BotManagerMover.Instance;
    }

    public string Name => _holdsSpellRange ? "rotation:hold-range" : "rotation:idle";
    public bool IsUseful(BotContext context)
    {
        var state = context.Runtime.CombatState;
        if (_holdsSpellRange)
        {
            var target = PositioningHelpers.Target(context);
            if (PositioningHelpers.IsValidTarget(target))
                return true;
        }

        var lastSkillTime = state.LastSkillTime;
        return state.Target != null &&
               (state.TripleSlashStage != 0 ||
                lastSkillTime != DateTime.MinValue && context.Now - lastSkillTime <
                TimeSpan.FromMilliseconds(Math.Max(0, context.Config.GlobalSkillDelayMs)));
    }
    public bool IsPossible(BotContext context) => IsUseful(context);
    public BotActionResult Execute(BotContext context, BotEvent ev)
    {
        if (_holdsSpellRange)
        {
            var target = PositioningHelpers.Target(context);
            if (PositioningHelpers.IsValidTarget(target))
            {
                var distance = PositioningHelpers.Distance(context.Bot, target);
                if (distance >= _minimumRange && distance <= _maximumRange ||
                    !PositioningHelpers.CanMoveForCombat(context))
                    (context.Mover ?? _mover).StopIfMoving(context.Bot);
            }
        }
        return BotActionResult.Success;
    }
}

public sealed class RotationGlobalDelayAction : IBotAction
{
    private readonly IBotAction _inner;
    private readonly bool _ignoreGlobalSkillDelay;

    public RotationGlobalDelayAction(IBotAction inner, bool ignoreGlobalSkillDelay)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _ignoreGlobalSkillDelay = ignoreGlobalSkillDelay;
    }

    public string Name => _inner.Name;
    public IReadOnlyList<BotNextAction> Prerequisites => _inner.Prerequisites;
    public IReadOnlyList<BotNextAction> Alternatives => _inner.Alternatives;
    public IReadOnlyList<BotNextAction> Continuers => _inner.Continuers;
    public bool IsUseful(BotContext context) => _inner.IsUseful(context);
    public bool IsPossible(BotContext context) => IsGlobalDelayReady(context) && _inner.IsPossible(context);
    public BotActionResult Execute(BotContext context, BotEvent ev)
    {
        if (!IsGlobalDelayReady(context))
            return BotActionResult.Impossible;

        return _inner.Execute(context, ev);
    }

    private bool IsGlobalDelayReady(BotContext context)
    {
        if (_ignoreGlobalSkillDelay || context.Runtime.CombatState.LastSkillTime == DateTime.MinValue)
            return true;

        return context.Now - context.Runtime.CombatState.LastSkillTime >=
               TimeSpan.FromMilliseconds(Math.Max(0, context.Config.GlobalSkillDelayMs));
    }
}

public sealed class RotationCastAction : IBotAction
{
    private readonly BotCastSkillAction _inner;
    private readonly Action<BotContext> _onSuccess;
    private readonly Func<BotContext, bool> _guard;

    public RotationCastAction(uint skillId, string name, TargetSource targetSource,
        Func<uint, SkillTemplate> templateResolver, IReadOnlyList<BotNextAction> alternatives = null,
        Func<BotCastRequest, SkillResult> cast = null, bool castWhileControlled = false,
        Action<BotContext> onSuccess = null,
        Func<BotContext, bool> guard = null,
        bool requireKnownSkill = false)
    {
        _inner = new BotCastSkillAction(skillId, targetSource, templateResolver, cast, name, castWhileControlled,
            requireKnownSkill);
        _onSuccess = onSuccess;
        _guard = guard;
        Name = name;
        Alternatives = alternatives ?? [];
    }

    public string Name { get; }
    public IReadOnlyList<BotNextAction> Alternatives { get; }
    public bool IsUseful(BotContext context) => (_guard?.Invoke(context) ?? true) &&
        (_inner.TargetSource == TargetSource.PartyLowest || context.Runtime.CombatState.Target != null) &&
        _inner.IsUseful(context);
    public bool IsPossible(BotContext context) => (_guard?.Invoke(context) ?? true) && _inner.IsPossible(context);
    public BotActionResult Execute(BotContext context, BotEvent ev)
    {
        if (!(_guard?.Invoke(context) ?? true))
            return BotActionResult.Impossible;

        var result = _inner.Execute(context, ev);
        if (result == BotActionResult.Success)
            _onSuccess?.Invoke(context);
        return result;
    }
}

public sealed class RotationAutoAttackAction : IBotAction
{
    public RotationAutoAttackAction(string name = "autoattack")
    {
        Name = name;
    }

    public string Name { get; }
    public bool IsUseful(BotContext context) =>
        (context.Bot.IsAutoAttack ? IsRotationIdleWindow(context) : context.Runtime.CombatState.Target != null);
    public bool IsPossible(BotContext context) => IsUseful(context);
    public BotActionResult Execute(BotContext context, BotEvent ev)
    {
        if (context.Runtime.CombatState.Target != null && IsPastReengageRange(context))
        {
            var state = context.Runtime.CombatState;
            state.Target = null;
            (context.Mover ?? BotManagerMover.Instance).StopImmediately(context.Bot);
            state.IsStalking = false;
            state.TripleSlashStage = 0;
            state.EndCombo();
            return BotActionResult.Success;
        }

        if (context.Bot.IsAutoAttack)
            return BotActionResult.Success;

        var target = context.Runtime.CombatState.Target;
        return BasicCombat.Execute(context.Bot, context.Runtime.CombatState, target)
            ? BotActionResult.Success
            : BotActionResult.Impossible;
    }

    private static bool IsRotationIdleWindow(BotContext context)
    {
        var target = context.Runtime.CombatState.Target;
        if (target == null)
            return true;
        if (IsPastReengageRange(context))
            return true;

        var state = context.Runtime.CombatState;
        if (state.IsComboLocked || state.TripleSlashStage != 0)
            return true;
        return state.LastSkillTime != DateTime.MinValue &&
               context.Now - state.LastSkillTime < TimeSpan.FromMilliseconds(Math.Max(0, context.Config.GlobalSkillDelayMs));
    }

    private static bool IsPastReengageRange(BotContext context)
    {
        var target = context.Runtime.CombatState.Target;
        return target?.Transform != null && context.Bot.Transform != null &&
               Vector3.Distance(context.Bot.Transform.World.Position, target.Transform.World.Position) >
               (float)context.Config.ReengageRange;
    }

}

public sealed class RotationMoveAction : IBotAction
{
    private readonly IBotAction _inner;
    private readonly Func<BotContext, bool> _guard;

    public RotationMoveAction(string name, string mode, IBotMover mover, float spellRange = 20f,
        Func<BotContext, bool> guard = null)
    {
        Name = name;
        _guard = guard;
        _inner = mode?.ToLowerInvariant() switch
        {
            "melee" => new ReachMeleeAction(mover),
            "spellrange" or "spell-range" => new ReachSpellRangeAction(spellRange, mover, name),
            "behind" => new RearFlankAction(mover),
            "facing" => new SetFacingAction(mover),
            "away" => new FleeAction(mover, null, jitter: true),
            "flee" => new FleeAction(mover),
            _ => new ReachMeleeAction(mover)
        };
    }

    public string Name { get; }
    public bool IsUseful(BotContext context) => (_guard?.Invoke(context) ?? true) && _inner.IsUseful(context);
    public bool IsPossible(BotContext context) => (_guard?.Invoke(context) ?? true) && _inner.IsPossible(context);
    public BotActionResult Execute(BotContext context, BotEvent ev)
    {
        if (!(_guard?.Invoke(context) ?? true))
            return BotActionResult.Impossible;

        var result = _inner.Execute(context, ev);
        return result == BotActionResult.Success ? BotActionResult.Impossible : result;
    }
}
