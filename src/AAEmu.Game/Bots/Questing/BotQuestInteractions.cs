#if !PLAYERBOTS_AAEMU_3_0
using System.Numerics;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Compatibility;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.UnitManagers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.GameData;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.Static;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.DoodadObj;
using AAEmu.Game.Models.Game.DoodadObj.Funcs;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.Quests;
using AAEmu.Game.Models.Game.Quests.Acts;
using AAEmu.Game.Models.Game.Quests.Static;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Effects;
using AAEmu.Game.Models.Game.Skills.Static;
using AAEmu.Game.Models.Game.Skills.Templates;

namespace AAEmu.Game.Bots.Questing;

internal enum BotQuestInteractionKind { ItemUse, DoodadUse, DoodadGather, NpcTalk }

internal readonly record struct BotQuestInteractionObjective(
    BotQuestInteractionKind Kind, uint ItemId, uint DoodadId, int Phase,
    byte Index, int Current, int Required)
{
    internal uint NpcId { get; init; }
}

public sealed record BotInteractionPlan(
    bool Available, string Reason, uint SkillId, ulong ItemInstanceId, uint ItemId,
    uint DoodadObjectId, uint DoodadTemplateId, uint Phase, Vector3 Position,
    float Range, bool SelfTarget, int DurationMs)
{
    internal BotClimbPlan Climb { get; init; }
    internal AAEmu.Game.Models.Game.World.WorldInstance World { get; init; }
    internal uint QuestId { get; init; }
    internal BotQuestInteractionObjective? Objective { get; init; }
    internal bool LocationRequired { get; init; }
    internal uint TalkNpcId { get; init; }
    public static BotInteractionPlan Blocked(string reason) =>
        new(false, reason, 0, 0, 0, 0, 0, 0, default, 0, false, 0);
}

internal readonly record struct BotInteractionAttempt(bool Started, string Reason, IPendingAction Pending = null);

/// <summary>Discovers and executes native actions as the connectionless bot; never grants quest credit.</summary>
internal static class BotQuestInteractions
{
    internal static BotQuestInteractionObjective? ReadObjective(Quest quest)
    {
        if (quest == null || !quest.QuestSteps.TryGetValue(QuestComponentKind.Progress, out var progress))
            return null;
        var acts = progress.Components.Values.Where(c => c.IsCurrentlyActive)
            .SelectMany(c => c.Acts).Select(a => a.Template)
            .Where(a => a.CountsAsAnObjective && a.ThisComponentObjectiveIndex != byte.MaxValue).ToArray();
        if (acts.Length != 1)
            return null;
        var act = acts[0];
        var required = act is QuestActObjTalk ? 1 : act.Count;
        if (required <= 0 || !quest.TryGetPlayerBotObjectiveCount(act.ThisComponentObjectiveIndex, out var current) ||
            current < 0 || current > required)
            return null;
        // UseAlias selects client objective wording in the pinned host. Native
        // item/interaction events still match these exact IDs; it is not a group binding.
        return act switch
        {
            QuestActObjTalk talk when talk.NpcId > 0 =>
                new(BotQuestInteractionKind.NpcTalk, 0, 0, 0,
                    act.ThisComponentObjectiveIndex, current, 1) { NpcId = talk.NpcId },
            QuestActObjItemUse use when use.ItemId > 0 =>
                new(BotQuestInteractionKind.ItemUse, use.ItemId, use.HighlightDoodadId,
                    use.HighlightDoodadPhase, act.ThisComponentObjectiveIndex, current, act.Count),
            QuestActObjInteraction interaction when interaction.DoodadId > 0 && !interaction.TeamShare =>
                new(BotQuestInteractionKind.DoodadUse, 0, interaction.DoodadId,
                    interaction.HighlightDoodadPhase, act.ThisComponentObjectiveIndex, current, act.Count),
            QuestActObjItemGather gather when gather.HighlightDoodadId > 0 && gather.ItemId > 0 =>
                new(BotQuestInteractionKind.DoodadGather, gather.ItemId, gather.HighlightDoodadId,
                    gather.HighlightDoodadPhase, act.ThisComponentObjectiveIndex, current, act.Count),
            _ => null
        };
    }

    internal static uint[] ActionSkills(IEnumerable<DoodadFunc> functions,
        Func<uint, string, object> resolveFunction)
    {
        return functions.SelectMany(function => new[]
            {
                function.SkillId,
                resolveFunction(function.FuncId, function.FuncType) switch
                {
                    DoodadFuncFakeUse fake => fake.FakeSkillId,
                    DoodadFuncUse use => use.SkillId,
                    _ => 0u
                }
            }).Where(id => id > 0).Distinct().OrderBy(id => id).ToArray();
    }

    internal static uint[] ActionSkills(Doodad doodad) => ActionSkills(
        DoodadManager.Instance.GetFuncsForGroup(doodad.FuncGroupId),
        (id, type) => DoodadManager.Instance.GetFuncTemplate(id, type));

    internal static BotInteractionPlan Find(Character bot, BotQuestInteractionObjective objective, float radius)
    {
        if (bot?.ParentWorld == null || !float.IsFinite(radius) || radius <= 0)
            return BotInteractionPlan.Blocked("interaction_world_unavailable");
        SkillTemplate itemSkill = null;
        Item item = null;
        var doodadId = objective.DoodadId;
        float? scopedRange = null;
        if (objective.Kind == BotQuestInteractionKind.ItemUse)
        {
            item = FindItem(bot, objective.ItemId, 0);
            if (item == null)
                return BotInteractionPlan.Blocked("quest_item_missing");
            itemSkill = SkillManager.Instance.GetSkillTemplate(item.Template.UseSkillId);
            if (itemSkill == null)
                return BotInteractionPlan.Blocked("quest_item_skill_missing");
            if (itemSkill.TargetType is not SkillTargetType.Self and not SkillTargetType.Doodad)
                return BotInteractionPlan.Blocked("quest_item_target_not_supported");
            var scoped = itemSkill.Effects.Select(e => e.Template).OfType<ScopedFEffect>().ToArray();
            if (scoped.Length > 1 || (scoped.Length == 1 && doodadId != 0 && doodadId != scoped[0].DoodadId))
                return BotInteractionPlan.Blocked("ambiguous_item_doodad_binding");
            if (scoped.Length == 1)
            {
                doodadId = scoped[0].DoodadId;
                scopedRange = scoped[0].Range / 1000f;
                if (doodadId == 0 || scopedRange <= 0)
                    return BotInteractionPlan.Blocked("invalid_item_doodad_binding");
            }
            var requirements = UnitRequirementsGameData.Instance.GetSkillRequirements(itemSkill.Id);
            var binding = ResolveDoodadRequirement(requirements, doodadId, scopedRange, itemSkill.OrUnitReqs);
            if (binding.Error != null)
                return BotInteractionPlan.Blocked(binding.Error);
            doodadId = binding.DoodadId;
            scopedRange = binding.Range;
            if (doodadId == 0)
                return itemSkill.TargetType == SkillTargetType.Self
                    ? FindAreaApproach(bot, itemSkill, item, requirements, radius)
                    : BotInteractionPlan.Blocked("quest_item_doodad_binding_missing");
        }

        var candidates = bot.ParentWorld.GetPlayerBotDoodadsNear(bot, Math.Min(radius, 500f))
            .Where(d => d.TemplateId == doodadId && AllowedDoodad(bot, d) &&
                        (objective.Phase <= 0 || d.FuncGroupId == (uint)objective.Phase))
            .OrderBy(d => Vector3.Distance(bot.Transform.World.Position, d.Transform.World.Position))
            .ThenBy(d => d.ObjId).Take(32).ToArray();
        foreach (var doodad in candidates)
        {
            var skill = itemSkill;
            if (skill == null)
            {
                var skills = ActionSkills(doodad);
                // Never guess between harvest, uproot, or another action in a multi-action phase.
                if (skills.Length != 1)
                    continue;
                skill = SkillManager.Instance.GetSkillTemplate(skills[0]);
                if (skill?.TargetType != SkillTargetType.Doodad)
                    continue;
            }
            var range = scopedRange ?? Math.Min(skill.MaxRange, 6);
            if (range <= 0 || skill.MinRange > 0)
                continue;
            return Plan(bot, doodad, skill, item, range) with { Climb = BotClimbMotion.Find(bot, doodad, range) };
        }
        return BotInteractionPlan.Blocked(candidates.Length == 0
            ? "quest_doodad_not_available" : "quest_doodad_action_ambiguous_or_unsupported");
    }

    internal static (uint DoodadId, float? Range, string Error) ResolveDoodadRequirement(
        IEnumerable<UnitReqs> requirements, uint doodadId, float? range, bool alternatives)
    {
        var nearby = requirements.Where(r => r.KindType == UnitReqsKindType.DoodadRange).ToArray();
        if (nearby.Length > 0 && alternatives)
            return (0, null, "alternative_item_location_requirements_unsupported");
        foreach (var req in nearby)
        {
            if (req.Value1 == 0 || req.Value2 == 0 || doodadId != 0 && doodadId != req.Value1)
                return (0, null, "ambiguous_item_doodad_requirement");
            doodadId = req.Value1;
            var requiredRange = req.Value2 / 1000f;
            range = range.HasValue ? Math.Min(range.Value, requiredRange) : requiredRange;
        }
        return (doodadId, range, null);
    }

    private static BotInteractionPlan FindAreaApproach(Character bot, SkillTemplate skill, Item item,
        IReadOnlyList<UnitReqs> requirements, float radius)
    {
        var plan = Plan(bot, null, skill, item, 0);
        var areas = requirements.Where(r => r.KindType == UnitReqsKindType.AreaSphere).ToArray();
        if (areas.Length == 0) return plan;
        if (areas.Length != 1 || skill.OrUnitReqs)
            return BotInteractionPlan.Blocked("ambiguous_item_area_requirement");
        var area = areas[0];
        var components = bot.Quests.GetActiveActsWithUseItem(item.TemplateId)
            .Select(a => a.Template.ParentComponent).DistinctBy(c => c.Id).Take(32).ToArray();
        bool Valid(Vector3 position) => components.Any(c =>
            SphereGameData.Instance.IsInsideAreaSphere(area.Value1, area.Value2, position, c.Id) != null);
        if (Valid(bot.Transform.World.Position)) return plan;
        var spheres = components.Select(c => c.ParentQuestTemplate.Id).Distinct()
            .SelectMany(SphereQuestManager.GetSpheresForQuest)
            .Where(s => s.WorldId == bot.ParentWorld.Template.Name && s.Radius > .5f &&
                Vector3.Distance(bot.Transform.World.Position, s.Xyz) <= Math.Min(radius, 500f))
            .OrderBy(s => Vector3.Distance(bot.Transform.World.Position, s.Xyz)).Take(32);
        foreach (var sphere in spheres)
        {
            var position = sphere.Xyz;
            position.Z = bot.ParentWorld.GetHeight(position.X, position.Y);
            if (!float.IsFinite(position.Z) || !Valid(position)) continue;
            return plan with { Position = position, Range = .75f, LocationRequired = true };
        }
        return BotInteractionPlan.Blocked($"quest_item_area_unavailable_{area.Value1}");
    }

    internal static BotInteractionPlan ForDoodad(Character bot, Doodad doodad, uint skillId = 0)
    {
        if (!AllowedDoodad(bot, doodad))
            return BotInteractionPlan.Blocked("doodad_unavailable_or_not_owned");
        var skills = ActionSkills(doodad);
        if ((skillId == 0 && skills.Length != 1) || (skillId != 0 && !skills.Contains(skillId)))
            return BotInteractionPlan.Blocked("doodad_action_ambiguous_or_not_offered");
        var skill = SkillManager.Instance.GetSkillTemplate(skillId == 0 ? skills[0] : skillId);
        if (skill?.TargetType != SkillTargetType.Doodad || skill.MaxRange <= 0 || skill.MinRange > 0)
            return BotInteractionPlan.Blocked("doodad_skill_target_or_range_unsupported");
        return Plan(bot, doodad, skill, null, Math.Min(skill.MaxRange, 6));
    }

    private static BotInteractionPlan Plan(Character bot, Doodad doodad, SkillTemplate skill, Item item, float range) =>
        new(true, "native_action_available", skill.Id, item?.Id ?? 0, item?.TemplateId ?? 0,
            doodad?.ObjId ?? 0, doodad?.TemplateId ?? 0, doodad?.FuncGroupId ?? 0,
            doodad?.Transform.World.Position ?? bot.Transform.World.Position,
            range, skill.TargetType == SkillTargetType.Self,
            (int)Math.Clamp((long)skill.CastingTime + skill.ChannelingTime + skill.EffectDelay, 0, 120000));

    internal static bool InRange(Vector3 position, Vector3 target, float range) =>
        float.IsFinite(range) && range >= 0 &&
        float.IsFinite(position.X) && float.IsFinite(position.Y) && float.IsFinite(position.Z) &&
        float.IsFinite(target.X) && float.IsFinite(target.Y) && float.IsFinite(target.Z) &&
        Vector3.Distance(position, target) <= range;

    internal static bool AllowedDoodad(Character bot, Doodad doodad) =>
        bot?.ParentWorld != null && doodad != null && ReferenceEquals(bot.ParentWorld, doodad.ParentWorld) &&
        doodad.Despawn == DateTime.MinValue && (doodad.OwnerId == 0 || doodad.OwnerId == bot.Id) &&
        doodad.OwnerDbId == 0;

    private static Item FindItem(Character bot, uint templateId, ulong instanceId)
    {
        if (templateId == 0 || bot.Inventory == null ||
            !bot.Inventory.GetAllItemsByTemplate([SlotType.Inventory], templateId, -1, out var items, out _))
            return null;
        return items.Where(i => i != null && i.Count > 0 && (instanceId == 0 || i.Id == instanceId))
            .OrderBy(i => i.Id).FirstOrDefault();
    }

    internal static BotInteractionAttempt Execute(Character bot, BotInteractionPlan plan)
    {
        if (plan == null) return new(false, "interaction_plan_unavailable");
        if (plan.TalkNpcId != 0) return BotQuestTalk.Execute(bot, plan);
        // The owning client still needs ordinary item/doodad intent bindings.
        // Do not fall through to a duplicate server cast on an attached actor.
        if (BotDrivers.For(bot).Owns(bot))
        {
            if (!plan.Available) return new(false, "interaction_plan_unavailable");
            var runtime = BotDrivers.Runtime(bot);
            var pending = runtime?.Driver.TryEnqueue(runtime,
                new BotClientRequest(plan.SkillId, null, plan.DoodadObjectId, "interaction") { Interaction = plan });
            return pending == null
                ? new(false, "driver_interaction_unavailable")
                : new(true, "driver_interaction_pending", pending);
        }
        if (bot?.ParentWorld == null || bot.IsDead || !plan.Available)
            return new(false, "interaction_actor_unavailable");
        // Quest items have native requirements; being in battle is not a universal
        // prohibition (some story items are explicitly usable during combat).
        if (plan.ItemId == 0 && bot.IsInBattle)
            return new(false, "interaction_actor_in_combat");
        if (bot.SkillTask != null)
            return new(false, "interaction_actor_casting");
        var doodad = plan.DoodadObjectId == 0 ? null : bot.ParentWorld.GetDoodad(plan.DoodadObjectId);
        if (plan.DoodadObjectId != 0)
        {
            if (!AllowedDoodad(bot, doodad) || doodad.TemplateId != plan.DoodadTemplateId || doodad.FuncGroupId != plan.Phase)
                return new(false, "interaction_target_or_phase_changed");
            if (!InRange(bot.Transform.World.Position, doodad.Transform.World.Position, plan.Range))
                return new(false, "interaction_out_of_range");
            if (plan.ItemId == 0 && !ActionSkills(doodad).Contains(plan.SkillId))
                return new(false, "interaction_skill_no_longer_offered");
        }
        SkillCaster caster = new SkillCasterUnit(bot.ObjId);
        if (plan.ItemId != 0)
        {
            var item = FindItem(bot, plan.ItemId, plan.ItemInstanceId);
            if (item == null || item.Template.UseSkillId != plan.SkillId)
                return new(false, "interaction_item_missing_or_changed");
            caster = new SkillItem(bot.ObjId, item.Id, item.TemplateId);
        }
        var skill = SkillManager.Instance.GetSkillTemplate(plan.SkillId);
        if (skill == null)
            return new(false, "interaction_skill_missing");
        var requirement = UnitRequirementsGameData.Instance.CanUseSkill(skill, bot, caster);
        if (requirement.ResultKey != SkillResultKeys.ok)
            return new(false, RequirementFailure(requirement));
        SkillCastTarget target = plan.SelfTarget ? new SkillCastUnitTarget(bot.ObjId)
            : new SkillCastDoodadTarget { Type = SkillCastTargetType.Doodad, ObjId = plan.DoodadObjectId };
        var result = new Skill(skill).Use(bot, caster, target, null, false, out var error);
        return new(result == SkillResult.Success, $"native_skill_{result}_error_{error}");
    }

    internal static string RequirementFailure(UnitReqsValidationResult result) =>
        $"native_requirement_{result.ResultKey}_value_{result.ResultUInt}";
}

internal sealed partial class BotQuestAuthority
{
    public BotInteractionPlan FindInteraction(BotRuntime runtime, BotQuestInteractionObjective objective, float radius)
    {
        if (objective.Kind != BotQuestInteractionKind.NpcTalk)
            return BotQuestInteractions.Find(runtime.Bot, objective, radius);
        var endpoint = new BotQuestReportEndpoint(BotQuestReportKind.Npc, objective.NpcId);
        var live = FindReportObjects(runtime, endpoint, radius, DateTimeOffset.UtcNow).FirstOrDefault();
        var destination = live.Object?.Transform.World.Position;
        if (!destination.HasValue)
        {
            var spawns = FindStaticReportDestinations(runtime, endpoint, 4000);
            if (spawns.Count > 0) destination = spawns[0].Position;
        }
        return destination.HasValue
            ? new BotInteractionPlan(true, "native_talk_approach", 0, 0, 0, 0, 0, 0,
                destination.Value, BotQuestTalk.Range, false, 0) { TalkNpcId = objective.NpcId, LocationRequired = true }
            : BotInteractionPlan.Blocked("quest_talk_npc_unavailable");
    }
    public BotInteractionAttempt ExecuteInteraction(Character bot, BotInteractionPlan plan) =>
        BotQuestInteractions.Execute(bot, plan);
}
#endif
