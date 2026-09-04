using System.Numerics;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Static;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Bots.Body;

public enum TargetSource
{
    CurrentTarget,
    Self,
    Position,
    PartyLowest
}

public sealed record BotCastRequest(
    Skill Skill,
    SkillCaster Caster,
    SkillCastTarget Target);

public sealed class BotCastSkillAction : IBotAction
{
    private readonly Func<uint, SkillTemplate> _templateResolver;
    private readonly Func<BotCastRequest, SkillResult> _cast;
    private readonly bool _requireKnownSkill;
    private bool _gateCached;
    private DateTime _gateAt;
    private Unit _gateTarget;

    public BotCastSkillAction(
        uint skillId,
        TargetSource targetSource = TargetSource.CurrentTarget,
        Func<uint, SkillTemplate> templateResolver = null,
        Func<BotCastRequest, SkillResult> cast = null,
        string name = null,
        bool castWhileControlled = false,
        bool requireKnownSkill = false)
    {
        SkillId = skillId;
        TargetSource = targetSource;
        _templateResolver = templateResolver ?? (id => SkillManager.Instance.GetSkillTemplate(id));
        _cast = cast;
        _requireKnownSkill = requireKnownSkill;
        CastWhileControlled = castWhileControlled;
        Name = string.IsNullOrWhiteSpace(name) ? $"cast:{skillId}" : name;
    }

    public string Name { get; }
    public uint SkillId { get; }
    public TargetSource TargetSource { get; }
    public GateResult LastGate { get; private set; }
    public bool CastWhileControlled { get; }

    public bool IsUseful(BotContext context)
    {
        var template = ResolveTemplate();
        return template != null && (TargetSource == TargetSource.Position || ResolveTarget(context) != null);
    }

    public bool IsPossible(BotContext context)
    {
        if (!_gateCached || _gateAt != context.Now)
        {
            LastGate = CheckGate(context);
            _gateCached = true;
            _gateAt = context.Now;
        }

        return LastGate.IsAllowed;
    }

    public BotActionResult Execute(BotContext context, BotEvent ev)
    {
        if (!_gateCached || _gateAt != context.Now)
            LastGate = CheckGate(context);
        var unitTarget = _gateTarget;
        _gateCached = false;
        _gateTarget = null;
        if (!LastGate.IsAllowed)
            return BotActionResult.Impossible;

        var template = ResolveTemplate();
        var target = BuildTarget(context, ev, unitTarget);
        if (target == null)
        {
            LastGate = new GateResult(GateReason.WrongRelation, "position target is missing");
            return BotActionResult.Impossible;
        }
        if (target is SkillCastPositionTarget positionTarget)
        {
            LastGate = CheckPositionRange(context.Bot, template, positionTarget);
            if (!LastGate.IsAllowed)
                return BotActionResult.Impossible;
        }

        var skill = new Skill(template, context.Bot);
        var request = new BotCastRequest(skill, new SkillCasterUnit(context.Bot.ObjId), target);
        var result = _cast != null
            ? _cast(request)
            : skill.Use(context.Bot, request.Caster, request.Target, null, false, out _);
        context.Runtime.HostMetrics?.RecordCast(result == SkillResult.Success);
        if (result != SkillResult.Success)
            return BotActionResult.Failure;

        context.Runtime.CombatState.LastSkillTime = context.Now;
        context.Bot.IsInBattle = true;
        if (unitTarget != null)
            unitTarget.IsInBattle = true;
        return BotActionResult.Success;
    }

    private GateResult CheckGate(BotContext context)
    {
        var template = ResolveTemplate();
        _gateTarget = ResolveTarget(context);
        if (_requireKnownSkill && context.Bot?.Skills?.Skills?.ContainsKey(SkillId) != true)
            return new GateResult(GateReason.Unlearned, $"skill {SkillId} is not learned");
        var distance = _gateTarget == null ? 0f : Distance(context.Bot, _gateTarget);
        var gate = BotSkillGate.Check(context.Bot, template, _gateTarget, distance, context.Now, context.Config,
            CastWhileControlled);
        if (!gate.IsAllowed)
            return gate;

        if (template is { TargetType: SkillTargetType.Self, TargetRelation: SkillTargetRelation.Hostile,
                TargetAreaRadius: > 0 })
        {
            var effectTarget = ResolveCurrentTarget(context);
            if (effectTarget == null)
                return new GateResult(GateReason.WrongRelation, "hostile effect target is missing");
            var effectDistance = ReferenceEquals(effectTarget, _gateTarget)
                ? distance
                : Distance(context.Bot, effectTarget);
            if (effectDistance > template.TargetAreaRadius)
                return OutOfRange(effectDistance, 0f, template.TargetAreaRadius);
        }

        if (template?.TargetType == SkillTargetType.Pos && _gateTarget != null &&
            (distance < template.MinRange || distance > template.MaxRange))
            return OutOfRange(distance, template.MinRange, template.MaxRange);

        return gate;
    }

    private SkillTemplate ResolveTemplate() => _templateResolver(SkillId);

    private static float Distance(Unit first, Unit second)
    {
        if (first.Transform == null || second.Transform == null)
            return 0f;
        return Vector3.Distance(first.Transform.World.Position, second.Transform.World.Position);
    }

    private Unit ResolveTarget(BotContext context)
    {
        return TargetSource switch
        {
            TargetSource.Self => context.Bot,
            TargetSource.CurrentTarget => ResolveCurrentTarget(context),
            TargetSource.Position => ResolveCurrentTarget(context),
            TargetSource.PartyLowest => context.Runtime.Social.ResolveCommittedHealRecipient(),
            _ => null
        };
    }

    private static Unit ResolveCurrentTarget(BotContext context) =>
        context.Runtime.CombatState.Target as Unit ?? context.Bot.CurrentTarget as Unit;

    private SkillCastTarget BuildTarget(BotContext context, BotEvent ev, Unit unitTarget)
    {
        if (TargetSource == TargetSource.Position)
        {
            var position = ev.Payload is Vector3 payload ? payload : unitTarget?.Transform?.World.Position;
            if (!position.HasValue)
                return null;
            return new SkillCastPositionTarget
            {
                Type = SkillCastTargetType.Position,
                PosX = position.Value.X,
                PosY = position.Value.Y,
                PosZ = position.Value.Z,
                PosRot = context.Bot.Transform?.Local.Rotation.Z ?? 0f
            };
        }

        var target = unitTarget ?? context.Bot;
        return new SkillCastUnitTarget(target.ObjId)
        {
            Type = SkillCastTargetType.Unit
        };
    }

    private static GateResult CheckPositionRange(Character bot, SkillTemplate template,
        SkillCastPositionTarget target)
    {
        if (template?.TargetType != SkillTargetType.Pos || bot?.Transform == null)
            return new GateResult(GateReason.Ok);

        var position = new Vector3(target.PosX, target.PosY, target.PosZ);
        var distance = Vector3.Distance(bot.Transform.World.Position, position);
        return distance < template.MinRange || distance > template.MaxRange
            ? OutOfRange(distance, template.MinRange, template.MaxRange)
            : new GateResult(GateReason.Ok);
    }

    private static GateResult OutOfRange(float distance, float minimum, float maximum) =>
        new(GateReason.OutOfRange, $"distance {distance:F2} is outside [{minimum}, {maximum}]");

}
