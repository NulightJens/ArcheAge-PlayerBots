using AAEmu.Game.Bots.Body;
using AAEmu.Game.Bots.Body.Positioning;
using AAEmu.Game.Bots.Content.Rotations.Triggers;
using AAEmu.Game.Bots.Content.Rotations.Values;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Static;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.Char;

namespace AAEmu.Game.Bots.Content.Rotations;

public sealed class BotRotationCompiler
{
    private readonly Func<int> _roll;
    private readonly Func<uint, SkillTemplate> _templateResolver;
    private readonly IBotMover _mover;
    private readonly Func<BotCastRequest, SkillResult> _cast;
    private readonly RotationValueResolver _values = new();

    public BotRotationCompiler(Func<int> roll = null, Func<uint, SkillTemplate> templateResolver = null,
        IBotMover mover = null, Func<BotCastRequest, SkillResult> cast = null)
    {
        _roll = roll ?? (() => AAEmu.Game.Bots.Host.BotHost.Instance.Roll());
        _templateResolver = templateResolver ?? (id => SkillManager.Instance.GetSkillTemplate(id));
        _mover = mover ?? BotManagerMover.Instance;
        _cast = cast;
    }

    public RotationStrategy Compile(BotRotationDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var actions = new Dictionary<string, IBotAction>(StringComparer.OrdinalIgnoreCase);
        var fillerRows = new List<WeightedRotationAction>();
        var triggerFactory = new RotationTriggerFactory(
            skill => ResolveSkill(definition, skill),
            _templateResolver, values: _values);

        foreach (var row in definition.Default ?? [])
        {
            var action = CompileRow(definition, row, actions,
                onSuccess: triggerFactory.CreateGroupCooldownSuccessHandler(row.When));
            if (action != null)
            {
                var gate = row.When == null ? null : triggerFactory.Create(row.When, ResolveSkill(definition, row.Skill));
                fillerRows.Add(new(action, row.Weight <= 0 ? 1f : row.Weight, gate == null ? null : gate.IsActive));
            }
        }

        var triggerNodes = new List<BotTriggerNode>();
        foreach (var rule in definition.Rules ?? [])
        {
            var ruleTrigger = triggerFactory.Create(rule.When,
                rule.Then?.Select(row => ResolveSkill(definition, row?.Skill)).FirstOrDefault(id => id.HasValue));
            foreach (var row in rule.Then ?? [])
            {
                var trigger = ruleTrigger;
                if (row.When != null)
                {
                    var rowTrigger = triggerFactory.Create(row.When, ResolveSkill(definition, row.Skill));
                    trigger = new RotationPredicateTrigger(
                        $"{ruleTrigger.Name}:{DefaultActionName(row, ResolveSkill(definition, row.Skill))}",
                        context => ruleTrigger.IsActive(context) && rowTrigger.IsActive(context));
                }

                Func<BotContext, bool> guard = string.Equals(row.Action, "move", StringComparison.OrdinalIgnoreCase) ||
                                               ContainsGroupCooldown(rule.When) || ContainsGroupCooldown(row.When)
                    ? trigger.IsActive
                    : null;
                var action = CompileRow(definition, row, actions, guard,
                    triggerFactory.CreateGroupCooldownSuccessHandler(rule.When, row.When));
                if (action != null)
                {
                    triggerNodes.Add(new BotTriggerNode(trigger, [new BotNextAction(action.Name, row.Relevance)]));
                    if (string.Equals(row.Action, "castHeal", StringComparison.OrdinalIgnoreCase))
                    {
                        var healSkillId = ResolveSkill(definition, row.Skill);
                        if (healSkillId.HasValue)
                        {
                            var position = new HealRecipientRangeAction(
                                ResolveRange(healSkillId),
                                _mover,
                                $"position:heal-recipient:{row.Skill}");
                            if (actions.TryAdd(position.Name, position))
                            {
                                triggerNodes.Add(new BotTriggerNode(
                                    trigger,
                                    [new BotNextAction(position.Name, row.Relevance - 1f)]));
                            }
                        }
                    }
                }
            }
        }

        var homeAnchorSkill = definition.Meta?.HomeAnchorSkill;
        var homeAnchorSkillId = ResolveSkill(definition, homeAnchorSkill);
        MaintainSpellRangeAction homeAnchor = null;
        if (homeAnchorSkillId.HasValue)
        {
            homeAnchor = new MaintainSpellRangeAction(
                ResolveRange(homeAnchorSkillId),
                _mover,
                $"home-range:{homeAnchorSkill}",
                tangentCloseEscape: string.Equals(definition.Id, "primeval.archer",
                    StringComparison.OrdinalIgnoreCase),
                continueAfterMove: true);
            actions.TryAdd(homeAnchor.Name, homeAnchor);
            triggerNodes.Add(new BotTriggerNode(
                new RotationPredicateTrigger(
                    $"outside-home-range:{homeAnchorSkill}",
                    homeAnchor.IsUseful),
                [new BotNextAction(homeAnchor.Name, BotRelevance.Move)]));
        }

        var filler = new WeightedFillerAction(fillerRows, _roll);
        var idle = definition.Rules?.Count > 0 || homeAnchor != null
            ? new RotationIdleAction(homeAnchor?.MaximumRange, _mover)
            : null;
        var strategyActions = idle == null ? actions.Values : actions.Values.Append(idle);
        return new RotationStrategy(definition.Id, filler, triggerNodes, strategyActions);
    }

    private IBotAction CompileRow(BotRotationDefinition definition, BotRotationRow row,
        Dictionary<string, IBotAction> actions, Func<BotContext, bool> guard = null,
        Action<BotContext> onSuccess = null)
    {
        if (row == null || string.IsNullOrWhiteSpace(row.Action))
            return null;

        var skillId = ResolveSkill(definition, row.Skill);
        var name = string.IsNullOrWhiteSpace(row.As)
            ? DefaultActionName(row, skillId)
            : row.As;
        if (actions.TryGetValue(name, out var existing))
            return existing;

        IBotAction action = row.Action switch
        {
            "autoAttack" => new RotationAutoAttackAction(name),
            "maintainRange" when skillId.HasValue =>
                new MaintainSpellRangeAction(ResolveRange(skillId), _mover, name),
            "move" => new RotationMoveAction(name, row.Skill, _mover, ResolveRange(skillId), guard),
            "reachAndCast" when skillId.HasValue => CompileReachAndCast(name, skillId.Value, definition, actions,
                row, onSuccess, guard),
            "cast" or "castMelee" or "castBuff" or "castDebuff" or "castAoe" or "castHeal" when skillId.HasValue =>
                new RotationGlobalDelayAction(new RotationCastAction(skillId.Value, name,
                    row.Action switch
                    {
                        "castAoe" => TargetSource.Position,
                        "castHeal" => TargetSource.PartyLowest,
                        _ => TargetSource.CurrentTarget
                    },
                    _templateResolver, cast: _cast, castWhileControlled: row.CastWhileControlled,
                    onSuccess: BuildRowSuccessHandler(definition, row, skillId.Value, onSuccess), guard: guard,
                    requireKnownSkill: true),
                    row.IgnoreGlobalDelay),
            _ => null
        };

        if (action != null)
            actions[name] = action;
        return action;
    }

    private IBotAction CompileReachAndCast(string name, uint skillId, BotRotationDefinition definition,
        Dictionary<string, IBotAction> actions, BotRotationRow row, Action<BotContext> onSuccess = null,
        Func<BotContext, bool> guard = null)
    {
        var reachName = $"reach:{ResolveSkillKey(definition, skillId)}";
        var reach = new ReachSpellRangeAction(ResolveRange(skillId), _mover, reachName);
        actions.TryAdd(reach.Name, reach);
        var cast = new RotationGlobalDelayAction(new RotationCastAction(skillId, name, TargetSource.CurrentTarget,
            _templateResolver, [new BotNextAction(reach.Name, BotRelevance.Move)], _cast,
            castWhileControlled: row.CastWhileControlled,
            onSuccess: BuildRowSuccessHandler(definition, row, skillId, onSuccess), guard: guard,
            requireKnownSkill: true),
            row.IgnoreGlobalDelay);
        return cast;
    }

    private static Action<BotContext> BuildRowSuccessHandler(BotRotationDefinition definition, BotRotationRow row,
        uint skillId, Action<BotContext> onSuccess = null)
    {
        var combo = row.Combo;
        var comboOpener = combo == null ? null : ResolveSkill(definition, combo.Opener);
        var comboFollowUp = combo == null ? null : ResolveSkill(definition, combo.FollowUp);
        var chain = row.Chain;
        return context =>
        {
            var combatState = context.Runtime.CombatState;
            var consumesCombo = combatState.IsComboLocked && combatState.PendingComboFollowUp == skillId;
            if (consumesCombo)
            {
                combatState.ClearCombo();
                combatState.EndCombo();
            }
            if (comboOpener.HasValue && comboFollowUp.HasValue)
                combatState.BeginCombo(comboOpener.Value, comboFollowUp.Value, now: context.Now);
            if (chain != null)
                combatState.TripleSlashStage = chain.Stage;
            if (chain != null)
                combatState.LastTripleSlashTime = context.Now;
            if (!consumesCombo && !comboOpener.HasValue &&
                chain == null)
                combatState.EndCombo();
            if (string.Equals(row.Action, "castHeal", StringComparison.OrdinalIgnoreCase))
                context.Runtime.Social.ClearCommittedHealRecipient();
            onSuccess?.Invoke(context);
        };
    }

    private static bool ContainsGroupCooldown(BotRotationWhen when)
    {
        if (when == null)
            return false;
        if (string.Equals(when.Kind, "groupCooldown", StringComparison.OrdinalIgnoreCase))
            return true;
        return when.Children?.Any(ContainsGroupCooldown) == true;
    }

    private float ResolveRange(uint? skillId)
    {
        return skillId.HasValue ? _templateResolver(skillId.Value)?.MaxRange ?? 20f : 20f;
    }

    private static string DefaultActionName(BotRotationRow row, uint? skillId)
    {
        if (row.Action == "autoAttack")
            return skillId.HasValue ? $"autoattack:{skillId.Value}" : "autoattack";
        if (row.Action == "maintainRange")
            return skillId.HasValue ? $"maintain-range:{skillId.Value}" : "maintain-range";
        return skillId.HasValue ? $"cast:{row.Skill}" : row.Action;
    }

    private static uint? ResolveSkill(BotRotationDefinition definition, string key)
    {
        return !string.IsNullOrWhiteSpace(key) && definition.Skills.TryGetValue(key, out var id) ? id : null;
    }

    private static string ResolveSkillKey(BotRotationDefinition definition, uint skillId)
    {
        return definition.Skills.FirstOrDefault(pair => pair.Value == skillId).Key ?? skillId.ToString();
    }
}
