using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.GameData;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Quests;
using AAEmu.Game.Models.Game.Quests.Acts;
using AAEmu.Game.Models.Game.Quests.Static;
using AAEmu.Game.Models.Game.Quests.Templates;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Static;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.Static;
using AAEmu.Game.Models.Tasks.Bots;
using AAEmu.Game.Utils;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands;

/// <summary>
/// Human-facing, deliberately staged quest controls. Scan and inspect are
/// read-only. Accept uses AAEmu's normal quest lifecycle and requires the bot
/// to be beside the exact NPC that starts the quest.
/// </summary>
public sealed class BotQuestCommand : ICommand
{
    internal const float DefaultScanRadius = 35f;
    internal const float MaximumScanRadius = 100f;
    internal const float InteractionRadius = 6f;

    public string[] CommandNames { get; set; } = ["botquest"];

    public void OnLoad() => CommandManager.Instance.Register(CommandNames, this);

    public string GetCommandLineHelp() =>
        "scan <botId> [radius], nearby <botId> <npcTemplateId> [radius], inspect <botId> <questId>, status <botId> <questId>, accept <botId> <questId>, talk <botId> <questId>, use <botId> <questId> <npcObjId>, acquire <botId> <questId> <npcObjId>, report <botId> <questId> [rewardIndex]";

    public string GetCommandHelpText() =>
        "Scans nearby NPC quest relations, explains a structured quest, or accepts/talks/uses supplied quest items/reports through AAEmu's normal quest and skill machinery. " +
        "NPC-group starters, team-shared talk acts, and general autonomous objective execution are intentionally deferred.";

    public void Execute(Character character, string[] args, IMessageOutput messageOutput)
    {
        if (!TryParse(args, out var request))
        {
            CommandManager.SendDefaultHelpText(this, messageOutput);
            return;
        }

        var bot = BotManager.Instance.GetBot(request.BotId);
        if (bot == null)
        {
            BotCommandArgs.SendUnknownBot(this, messageOutput, request.BotId);
            return;
        }

        switch (request.Verb)
        {
            case BotQuestVerb.Scan:
                Scan(bot, request.Radius, messageOutput);
                break;
            case BotQuestVerb.Nearby:
                Nearby(bot, request.NpcTemplateId, request.Radius, messageOutput);
                break;
            case BotQuestVerb.Inspect:
                Inspect(bot, request.QuestId, messageOutput);
                break;
            case BotQuestVerb.Status:
                Status(bot, request.QuestId, messageOutput);
                break;
            case BotQuestVerb.Accept:
                Accept(bot, request.QuestId, messageOutput);
                break;
            case BotQuestVerb.Talk:
                Talk(bot, request.QuestId, messageOutput);
                break;
            case BotQuestVerb.Use:
                Use(bot, request.QuestId, request.TargetObjId, messageOutput);
                break;
            case BotQuestVerb.Acquire:
                Acquire(bot, request.QuestId, request.TargetObjId, messageOutput);
                break;
            case BotQuestVerb.Report:
                Report(bot, request.QuestId, request.SelectedReward, messageOutput);
                break;
        }
    }

    internal static bool TryParse(string[] args, out BotQuestRequest request)
    {
        request = default;
        if (args == null || args.Length < 2 ||
            !Enum.TryParse<BotQuestVerb>(args[0], true, out var verb) ||
            !uint.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out var botId) || botId == 0)
            return false;

        switch (verb)
        {
            case BotQuestVerb.Scan:
                if (args.Length > 3)
                    return false;
                var radius = DefaultScanRadius;
                if (args.Length == 3 &&
                    (!float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out radius) ||
                     radius <= 0f || radius > MaximumScanRadius))
                    return false;
                request = new BotQuestRequest(verb, botId, 0, 0, 0, radius, 0);
                return true;
            case BotQuestVerb.Nearby:
                if (args.Length is < 3 or > 4 ||
                    !uint.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out var npcTemplateId) ||
                    npcTemplateId == 0)
                    return false;
                radius = DefaultScanRadius;
                if (args.Length == 4 &&
                    (!float.TryParse(args[3], NumberStyles.Float, CultureInfo.InvariantCulture, out radius) ||
                     radius <= 0f || radius > MaximumScanRadius))
                    return false;
                request = new BotQuestRequest(verb, botId, 0, npcTemplateId, 0, radius, 0);
                return true;
            case BotQuestVerb.Inspect:
            case BotQuestVerb.Status:
            case BotQuestVerb.Accept:
            case BotQuestVerb.Talk:
                if (args.Length != 3 ||
                    !uint.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out var questId) || questId == 0)
                    return false;
                request = new BotQuestRequest(verb, botId, questId, 0, 0, 0f, 0);
                return true;
            case BotQuestVerb.Use:
            case BotQuestVerb.Acquire:
                if (args.Length != 4 ||
                    !uint.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out questId) || questId == 0 ||
                    !uint.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out var targetObjId) || targetObjId == 0)
                    return false;
                request = new BotQuestRequest(verb, botId, questId, 0, targetObjId, 0f, 0);
                return true;
            case BotQuestVerb.Report:
                if (args.Length is < 3 or > 4 ||
                    !uint.TryParse(args[2], NumberStyles.None, CultureInfo.InvariantCulture, out questId) || questId == 0)
                    return false;
                var selectedReward = 0;
                if (args.Length == 4 &&
                    (!int.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out selectedReward) || selectedReward < 0))
                    return false;
                request = new BotQuestRequest(verb, botId, questId, 0, 0, 0f, selectedReward);
                return true;
            default:
                return false;
        }
    }

    private void Scan(Character bot, float radius, IMessageOutput messageOutput)
    {
        var relations = WorldManager.GetAround<Npc>(bot, radius, true)
            .Select(npc => new NearbyQuestNpc(
                npc,
                Distance(bot, npc),
                QuestManager.Instance.GetPlayerBotNpcQuestStarts(npc.TemplateId),
                QuestManager.Instance.GetPlayerBotNpcQuestReports(npc.TemplateId)))
            .Where(result => result.Starts.Count > 0 || result.Reports.Count > 0)
            .OrderBy(result => result.Distance)
            .ThenBy(result => result.Npc.ObjId)
            .ToList();

        CommandManager.SendNormalText(this, messageOutput,
            $"Bot '{bot.Name}' quest scan: radius={radius:F1}m, quest_npcs={relations.Count}.");
        foreach (var relation in relations)
        {
            CommandManager.SendNormalText(this, messageOutput,
                $"npc='{LocalizedNpcName(relation.Npc)}' template={relation.Npc.TemplateId} obj={relation.Npc.ObjId} " +
                $"distance={relation.Distance:F1}m position=({relation.Npc.Transform.World.Position.X:F1}," +
                $"{relation.Npc.Transform.World.Position.Y:F1},{relation.Npc.Transform.World.Position.Z:F1})");
            foreach (var quest in relation.Starts)
                CommandManager.SendNormalText(this, messageOutput,
                    $"  START quest={quest.Id} name='{LocalizedQuestName(quest.Id)}' level={quest.Level} status={GetAvailability(bot, quest)} objective={DescribeObjectiveShape(quest)}");
            foreach (var quest in relation.Reports.Where(quest => bot.Quests.HasQuest(quest.Id)))
                CommandManager.SendNormalText(this, messageOutput,
                    $"  REPORT quest={quest.Id} name='{LocalizedQuestName(quest.Id)}' active=true objective={DescribeObjectiveShape(quest)}");
        }
    }

    private void Nearby(Character bot, uint npcTemplateId, float radius, IMessageOutput messageOutput)
    {
        var matches = WorldManager.GetAround<Npc>(bot, radius, true)
            .Where(npc => npc.TemplateId == npcTemplateId)
            .Select(npc => new { Npc = npc, Distance = Distance(bot, npc) })
            .OrderBy(candidate => candidate.Distance)
            .ThenBy(candidate => candidate.Npc.ObjId)
            .ToList();

        CommandManager.SendNormalText(this, messageOutput,
            $"Bot '{bot.Name}' exact NPC scan: template={npcTemplateId}, radius={radius:F1}m, matches={matches.Count}.");
        foreach (var match in matches)
        {
            var position = match.Npc.Transform.World.Position;
            CommandManager.SendNormalText(this, messageOutput,
                $"npc='{LocalizedNpcName(match.Npc)}' template={match.Npc.TemplateId} obj={match.Npc.ObjId} " +
                $"hp={match.Npc.Hp}/{match.Npc.MaxHp} dead={match.Npc.IsDead.ToString().ToLowerInvariant()} " +
                $"distance={match.Distance:F1}m position=({position.X:F1},{position.Y:F1},{position.Z:F1})");
        }
    }

    private void Inspect(Character bot, uint questId, IMessageOutput messageOutput)
    {
        var quest = QuestManager.Instance.GetTemplate(questId);
        if (quest == null)
        {
            CommandManager.SendErrorText(this, messageOutput, $"Unknown quest id {questId}.");
            return;
        }

        CommandManager.SendNormalText(this, messageOutput,
            $"Quest {quest.Id} '{LocalizedQuestName(quest.Id)}': level={quest.Level}, zone={quest.ZoneId}, repeatable={quest.Repeatable}, status={GetAvailability(bot, quest)}.");
        foreach (var component in quest.Components.Values.OrderBy(component => component.KindId).ThenBy(component => component.Id))
        {
            var acts = component.ActTemplates.Count == 0
                ? "none"
                : string.Join(", ", component.ActTemplates.Select(DescribeAct));
            CommandManager.SendNormalText(this, messageOutput,
                $"component={component.Id} step={component.KindId}: {acts}");
        }
    }

    private void Status(Character bot, uint questId, IMessageOutput messageOutput)
    {
        if (bot.Quests.ActiveQuests.TryGetValue(questId, out var activeQuest))
        {
            CommandManager.SendNormalText(this, messageOutput,
                $"Bot '{bot.Name}' quest {questId}: lifecycle=active step={activeQuest.Step} status={activeQuest.Status} " +
                $"objective={activeQuest.GetQuestObjectiveStatus()} acceptor={activeQuest.QuestAcceptorType}:{activeQuest.AcceptorId}.");
            if (activeQuest.QuestSteps.TryGetValue(activeQuest.Step, out var currentStep))
            {
                foreach (var gatherAct in currentStep.Components.Values
                             .Where(component => component.IsCurrentlyActive)
                             .SelectMany(component => component.Acts)
                             .Where(act => act.Template is QuestActObjItemGather))
                {
                    var gather = (QuestActObjItemGather)gatherAct.Template;
                    CommandManager.SendNormalText(this, messageOutput,
                        $"item_gather act={gatherAct.Id} item={gather.ItemId} " +
                        $"objective={gather.GetObjective(activeQuest)}/{gather.Count} " +
                        $"inventory={bot.Inventory.GetItemsCount(gather.ItemId)} cleanup={gather.Cleanup.ToString().ToLowerInvariant()}.");
                }
            }
            return;
        }

        var lifecycle = bot.Quests.HasQuestCompleted(questId) ? "completed" : "inactive";
        CommandManager.SendNormalText(this, messageOutput,
            $"Bot '{bot.Name}' quest {questId}: lifecycle={lifecycle}.");
    }

    private void Accept(Character bot, uint questId, IMessageOutput messageOutput)
    {
        var quest = QuestManager.Instance.GetTemplate(questId);
        if (quest == null)
        {
            CommandManager.SendErrorText(this, messageOutput, $"Unknown quest id {questId}.");
            return;
        }

        var starterNpcIds = quest.Components.Values
            .SelectMany(component => component.ActTemplates)
            .Select(GetExactStarterNpcId)
            .Where(npcId => npcId != 0)
            .ToHashSet();
        if (starterNpcIds.Count == 0)
        {
            CommandManager.SendErrorText(this, messageOutput,
                $"Quest {questId} has no supported exact-NPC starter. NPC groups are not accepted by this milestone.");
            return;
        }

        var starter = WorldManager.GetAround<Npc>(bot, InteractionRadius, true)
            .Where(npc => starterNpcIds.Contains(npc.TemplateId))
            .Select(npc => new { Npc = npc, Distance = Distance(bot, npc) })
            .Where(candidate => candidate.Distance <= InteractionRadius)
            .OrderBy(candidate => candidate.Distance)
            .FirstOrDefault();
        if (starter == null)
        {
            CommandManager.SendErrorText(this, messageOutput,
                $"Bot '{bot.Name}' is not within {InteractionRadius:F1}m of the exact NPC starter for quest {questId}.");
            return;
        }

        var accepted = bot.Quests.AddQuestFromNpc(questId, starter.Npc.ObjId);
        if (!accepted)
        {
            CommandManager.SendErrorText(this, messageOutput,
                $"AAEmu rejected quest {questId} for bot '{bot.Name}'; no quest state was fabricated.");
            return;
        }

        CommandManager.SendNormalText(this, messageOutput,
            $"Bot '{bot.Name}' accepted quest {questId} from npc template={starter.Npc.TemplateId} obj={starter.Npc.ObjId} distance={starter.Distance:F1}m.");
    }

    private void Talk(Character bot, uint questId, IMessageOutput messageOutput)
    {
        if (!bot.Quests.ActiveQuests.TryGetValue(questId, out var activeQuest))
        {
            CommandManager.SendErrorText(this, messageOutput,
                $"Quest {questId} is not active for bot '{bot.Name}'.");
            return;
        }

        if (activeQuest.Step != QuestComponentKind.Progress ||
            !activeQuest.QuestSteps.TryGetValue(QuestComponentKind.Progress, out var progressStep))
        {
            CommandManager.SendErrorText(this, messageOutput,
                $"Quest {questId} is not at an active talk-objective step.");
            return;
        }

        var talkActs = progressStep.Components.Values
            .Where(component => component.IsCurrentlyActive)
            .SelectMany(component => component.Acts)
            .Where(act => act.Template is QuestActObjTalk)
            .ToList();
        if (talkActs.Count == 0)
        {
            CommandManager.SendErrorText(this, messageOutput,
                $"Quest {questId} has no active exact-NPC talk objective.");
            return;
        }

        var target = WorldManager.GetAround<Npc>(bot, InteractionRadius, true)
            .Select(npc => new { Npc = npc, Distance = Distance(bot, npc) })
            .Where(candidate => candidate.Distance <= InteractionRadius && talkActs.Any(act =>
                act.Template is QuestActObjTalk talk && talk.NpcId == candidate.Npc.TemplateId))
            .OrderBy(candidate => candidate.Distance)
            .ThenBy(candidate => candidate.Npc.ObjId)
            .FirstOrDefault();
        if (target == null)
        {
            CommandManager.SendErrorText(this, messageOutput,
                $"Bot '{bot.Name}' is not within {InteractionRadius:F1}m of the exact NPC talk target for quest {questId}.");
            return;
        }

        var matchingTalkActs = talkActs.Where(act =>
                act.Template is QuestActObjTalk talk && talk.NpcId == target.Npc.TemplateId)
            .ToArray();
        if (matchingTalkActs.Any(act => ((QuestActObjTalk)act.Template).TeamShare))
        {
            CommandManager.SendErrorText(this, messageOutput,
                $"Quest {questId} uses a team-shared talk act at npc template={target.Npc.TemplateId}; " +
                "the scoped talk milestone refuses cross-character quest broadcasts.");
            return;
        }

        var objectivesBefore = matchingTalkActs
            .Select(act => act.Template.GetObjective(activeQuest))
            .ToArray();

        // AAEmu's generic talk method broadcasts to every active quest. Invoke only
        // this selected quest's exact live acts so unrelated quests cannot advance.
        foreach (var talkAct in matchingTalkActs)
        {
            talkAct.OnTalkMade(bot, new OnTalkMadeArgs
            {
                QuestId = questId,
                NpcId = target.Npc.TemplateId,
                QuestComponentId = talkAct.QuestComponent.Template.Id,
                QuestActId = talkAct.Id,
                Transform = target.Npc.Transform,
                SourcePlayer = bot
            });
        }

        var objectivesAfter = matchingTalkActs
            .Select(act => act.Template.GetObjective(activeQuest))
            .ToArray();
        if (!AnyObjectiveAdvanced(objectivesBefore, objectivesAfter))
        {
            CommandManager.SendErrorText(this, messageOutput,
                $"AAEmu rejected quest {questId} talk progress for bot '{bot.Name}'; no objective increase was claimed.");
            return;
        }

        CommandManager.SendNormalText(this, messageOutput,
            $"Bot '{bot.Name}' talked for quest {questId} to npc template={target.Npc.TemplateId} " +
            $"obj={target.Npc.ObjId} distance={target.Distance:F1}m; advanced_acts=" +
            $"{objectivesAfter.Where((value, index) => value > objectivesBefore[index]).Count()}, native evaluation requested.");
    }

    private void Acquire(Character bot, uint questId, uint targetObjId, IMessageOutput messageOutput)
    {
        if (!TryResolveQuestItemUse(bot, questId, out _, out _, out _, out var skillTemplate, out var error))
        {
            CommandManager.SendErrorText(this, messageOutput, error);
            return;
        }

        if (!TryGetNativeAcquisitionContract(skillTemplate, questId,
                out var targetNpcTemplateId, out var healthFloor, out error))
        {
            CommandManager.SendErrorText(this, messageOutput, error);
            return;
        }

        var target = bot.ParentWorld?.GetNpc(targetObjId);
        if (target == null || target.IsDead || !ReferenceEquals(target.ParentWorld, bot.ParentWorld))
        {
            CommandManager.SendErrorText(this, messageOutput,
                $"Living NPC object {targetObjId} was not found in bot '{bot.Name}'s world.");
            return;
        }

        if (target.TemplateId != targetNpcTemplateId)
        {
            CommandManager.SendErrorText(this, messageOutput,
                $"Quest {questId} item skill {skillTemplate.Id} requires npc template={targetNpcTemplateId}; " +
                $"selected object {target.ObjId} is template={target.TemplateId}.");
            return;
        }

        if (BotCombatTask.HasReachedHpFloor(target, healthFloor))
        {
            Use(bot, questId, targetObjId, messageOutput);
            return;
        }

        if (!BotAttackObjectCommand.TryStartContainedAttack(
                bot,
                target,
                healthFloor,
                () => Use(bot, questId, targetObjId, messageOutput),
                out error))
        {
            CommandManager.SendErrorText(this, messageOutput, error);
            return;
        }

        CommandManager.SendNormalText(this, messageOutput,
            $"Bot '{bot.Name}' started native acquisition for quest {questId}: npc={target.TemplateId}:{target.ObjId} " +
            $"hp={target.Hp}/{target.MaxHp}, derived_floor={healthFloor}%, item_skill={skillTemplate.Id}. " +
            $"Contained combat will disengage and launch the selected quest item once AAEmu's target-health requirement is met; " +
            $"verify the delayed native result with /botquest status {bot.Id} {questId}.");
    }

    private void Use(Character bot, uint questId, uint targetObjId, IMessageOutput messageOutput)
    {
        if (!TryResolveQuestItemUse(bot, questId, out var activeQuest, out var gather,
                out var sourceItem, out var skillTemplate, out var error))
        {
            CommandManager.SendErrorText(this, messageOutput, error);
            return;
        }

        var target = bot.ParentWorld?.GetNpc(targetObjId);
        if (target == null || target.IsDead || !ReferenceEquals(target.ParentWorld, bot.ParentWorld))
        {
            CommandManager.SendErrorText(this, messageOutput,
                $"Living NPC object {targetObjId} was not found in bot '{bot.Name}'s world.");
            return;
        }

        var distance = Distance(bot, target);
        var objectiveBefore = gather.GetObjective(activeQuest);
        var inventoryBefore = bot.Inventory.GetItemsCount(gather.ItemId);
        var caster = new SkillItem(bot.ObjId, sourceItem.Id, sourceItem.TemplateId);
        uint resultErrorValue = 0;
        var result = UseWithSelectedTarget(bot, target, () =>
            new Skill(skillTemplate).Use(
                bot,
                caster,
                new SkillCastUnitTarget(target.ObjId),
                null,
                false,
                out resultErrorValue));
        if (result != SkillResult.Success)
        {
            CommandManager.SendErrorText(this, messageOutput,
                $"AAEmu rejected quest {questId} item skill {skillTemplate.Id} for bot '{bot.Name}': " +
                $"result={result} error={resultErrorValue} target={target.TemplateId}:{target.ObjId} distance={distance:F1}m; no progress was claimed.");
            return;
        }

        CommandManager.SendNormalText(this, messageOutput,
            $"Bot '{bot.Name}' started native quest-item skill {skillTemplate.Id} from item={sourceItem.TemplateId}:{sourceItem.Id} " +
            $"for quest {questId} against npc={target.TemplateId}:{target.ObjId} distance={distance:F1}m " +
            $"target_hp={target.Hp}/{target.MaxHp} gather_item={gather.ItemId} objective_before={objectiveBefore}/{gather.Count} " +
            $"inventory_before={inventoryBefore} channel_ms={skillTemplate.ChannelingTime}; verify completion with /botquest status {bot.Id} {questId}.");
    }

    private void Report(Character bot, uint questId, int selectedReward, IMessageOutput messageOutput)
    {
        if (!bot.Quests.ActiveQuests.TryGetValue(questId, out var activeQuest))
        {
            CommandManager.SendErrorText(this, messageOutput,
                $"Quest {questId} is not active for bot '{bot.Name}'.");
            return;
        }

        if (!activeQuest.QuestSteps.TryGetValue(QuestComponentKind.Ready, out var readyStep))
        {
            CommandManager.SendErrorText(this, messageOutput,
                $"Quest {questId} has no supported ready/report step.");
            return;
        }

        var reportActs = readyStep.Components.Values
            .SelectMany(component => component.Acts)
            .Where(act => act.Template is QuestActConReportNpc)
            .ToList();
        if (reportActs.Count == 0)
        {
            CommandManager.SendErrorText(this, messageOutput,
                $"Quest {questId} is not at a supported exact-NPC report step.");
            return;
        }

        var selectiveRewardIndexes = activeQuest.QuestSteps
            .GetValueOrDefault(QuestComponentKind.Reward)?.Components.Values
            .SelectMany(component => component.Acts)
            .Where(act => act.Template is QuestActSupplySelectiveItem)
            .Select(act => act.Template.ThisSelectiveIndex)
            .Distinct()
            .OrderBy(index => index)
            .ToArray() ?? [];
        if (!IsValidSelectedReward(selectiveRewardIndexes, selectedReward))
        {
            var expected = selectiveRewardIndexes.Length == 0
                ? "0 (this quest has no selective reward)"
                : string.Join(", ", selectiveRewardIndexes);
            CommandManager.SendErrorText(this, messageOutput,
                $"Quest {questId} does not accept reward index {selectedReward}; expected {expected}.");
            return;
        }

        var reporter = WorldManager.GetAround<Npc>(bot, InteractionRadius, true)
            .Select(npc => new { Npc = npc, Distance = Distance(bot, npc) })
            .Where(candidate => candidate.Distance <= InteractionRadius && reportActs.Any(act =>
                act.Template is QuestActConReportNpc report && report.NpcId == candidate.Npc.TemplateId))
            .OrderBy(candidate => candidate.Distance)
            .FirstOrDefault();
        if (reporter == null)
        {
            CommandManager.SendErrorText(this, messageOutput,
                $"Bot '{bot.Name}' is not within {InteractionRadius:F1}m of the exact NPC reporter for quest {questId}.");
            return;
        }

        var matchingReportActs = reportActs.Where(act =>
                act.Template is QuestActConReportNpc report && report.NpcId == reporter.Npc.TemplateId)
            .ToArray();
        var completionBefore = matchingReportActs
            .Select(act => act.OverrideObjectiveCompleted)
            .ToArray();

        // AAEmu's generic report method broadcasts to all active quests at this NPC.
        // Invoke only the selected quest's live act so native readiness, rewards, and
        // evaluation remain intact without advancing unrelated quests.
        var previousTarget = bot.CurrentTarget;
        bot.CurrentTarget = reporter.Npc;
        try
        {
            var reportArgs = new OnReportNpcArgs
            {
                QuestId = questId,
                NpcId = reporter.Npc.TemplateId,
                Selected = selectedReward,
                Transform = reporter.Npc.Transform
            };
            foreach (var reportAct in matchingReportActs)
                reportAct.OnReportNpc(bot, reportArgs);
        }
        finally
        {
            bot.CurrentTarget = previousTarget;
        }

        var accepted = matchingReportActs
            .Select((act, index) => act.OverrideObjectiveCompleted && !completionBefore[index])
            .Any(changed => changed);
        if (!accepted)
        {
            CommandManager.SendErrorText(this, messageOutput,
                $"AAEmu rejected quest {questId} reporting for bot '{bot.Name}'; no completion was claimed.");
            return;
        }

        CommandManager.SendNormalText(this, messageOutput,
            $"Bot '{bot.Name}' submitted quest {questId} to npc template={reporter.Npc.TemplateId} " +
            $"obj={reporter.Npc.ObjId} distance={reporter.Distance:F1}m reward_index={selectedReward}; " +
            $"accepted_acts={matchingReportActs.Length}, native evaluation requested.");
    }

    internal static bool IsValidSelectedReward(IReadOnlyCollection<int> selectiveRewardIndexes, int selectedReward) =>
        selectiveRewardIndexes.Count == 0 ? selectedReward == 0 : selectiveRewardIndexes.Contains(selectedReward);

    internal static bool AnyObjectiveAdvanced(IReadOnlyList<int> before, IReadOnlyList<int> after) =>
        before.Count == after.Count && before.Where((value, index) => after[index] > value).Any();

    internal static bool IsSupportedQuestUseSource(QuestActSupplyItem supply, Item item, uint questId) =>
        supply != null && item?.Template != null &&
        item.TemplateId == supply.ItemId &&
        item.Template.LootQuestId == questId &&
        item.Template.UseSkillId != 0;

    private static bool TryResolveQuestItemUse(
        Character bot,
        uint questId,
        out Quest activeQuest,
        out QuestActObjItemGather gather,
        out Item sourceItem,
        out SkillTemplate skillTemplate,
        out string error)
    {
        activeQuest = null;
        gather = null;
        sourceItem = null;
        skillTemplate = null;
        error = null;

        if (!bot.Quests.ActiveQuests.TryGetValue(questId, out activeQuest))
        {
            error = $"Quest {questId} is not active for bot '{bot.Name}'.";
            return false;
        }

        if (activeQuest.Step != QuestComponentKind.Progress ||
            !activeQuest.QuestSteps.TryGetValue(QuestComponentKind.Progress, out var progressStep))
        {
            error = $"Quest {questId} is not at an active item-gather objective step.";
            return false;
        }

        var gatherActs = progressStep.Components.Values
            .Where(component => component.IsCurrentlyActive)
            .SelectMany(component => component.Acts)
            .Where(act => act.Template is QuestActObjItemGather)
            .ToArray();
        if (gatherActs.Length != 1)
        {
            error = $"Quest {questId} requires exactly one active item-gather act for scoped item use; found {gatherActs.Length}.";
            return false;
        }

        var supplyActs = activeQuest.QuestSteps
            .GetValueOrDefault(QuestComponentKind.Supply)?.Components.Values
            .SelectMany(component => component.Acts)
            .Where(act => act.Template is QuestActSupplyItem)
            .ToArray() ?? [];
        var sourceItems = new List<Item>();
        foreach (var supplyAct in supplyActs)
        {
            var supply = (QuestActSupplyItem)supplyAct.Template;
            if (!bot.Inventory.GetAllItemsByTemplate([SlotType.Inventory], supply.ItemId, -1,
                    out var matchingItems, out _))
                continue;
            sourceItems.AddRange(matchingItems.Where(item => IsSupportedQuestUseSource(supply, item, questId)));
        }

        sourceItems = sourceItems
            .Where(item => item != null)
            .DistinctBy(item => item.Id)
            .ToList();
        if (sourceItems.Count != 1)
        {
            error = $"Quest {questId} requires exactly one carried quest-linked supply item with a native use skill; found {sourceItems.Count}.";
            return false;
        }

        sourceItem = sourceItems[0];
        skillTemplate = SkillManager.Instance.GetSkillTemplate(sourceItem.Template.UseSkillId);
        if (skillTemplate == null)
        {
            error = $"Quest {questId} supply item {sourceItem.TemplateId} references missing skill {sourceItem.Template.UseSkillId}.";
            return false;
        }

        gather = (QuestActObjItemGather)gatherActs[0].Template;
        return true;
    }

    internal static bool TryGetNativeAcquisitionContract(
        SkillTemplate skillTemplate,
        uint questId,
        out uint targetNpcTemplateId,
        out byte healthFloor,
        out string error)
    {
        var requirements = skillTemplate == null
            ? []
            : UnitRequirementsGameData.Instance.GetSkillRequirements(skillTemplate.Id);
        return TryGetNativeAcquisitionContract(
            skillTemplate, questId, requirements, out targetNpcTemplateId, out healthFloor, out error);
    }

    internal static bool TryGetNativeAcquisitionContract(
        SkillTemplate skillTemplate,
        uint questId,
        IReadOnlyList<UnitReqs> requirements,
        out uint targetNpcTemplateId,
        out byte healthFloor,
        out string error)
    {
        targetNpcTemplateId = 0;
        healthFloor = 0;
        error = null;
        if (skillTemplate == null)
        {
            error = "A native skill template is required for item acquisition.";
            return false;
        }

        if (skillTemplate.OrUnitReqs)
        {
            error = $"Skill {skillTemplate.Id} uses alternative unit-requirement branches; automatic acquisition refuses an ambiguous target contract.";
            return false;
        }

        var targetNpc = requirements.Where(requirement => requirement.KindType == UnitReqsKindType.TargetNpc).ToArray();
        var targetHealth = requirements.Where(requirement => requirement.KindType == UnitReqsKindType.TargetHealthLessThan).ToArray();
        var questContexts = requirements.Where(requirement => requirement.KindType == UnitReqsKindType.ProgressQuestContext).ToArray();
        if (targetNpc.Length != 1 || targetNpc[0].Value1 == 0 || targetHealth.Length != 1 ||
            questContexts.Length != 1 || questContexts[0].Value1 != questId ||
            targetHealth[0].Value1 > targetHealth[0].Value2 ||
            targetHealth[0].Value2 is < 1 or > 99)
        {
            error = $"Skill {skillTemplate.Id} does not expose one exact NPC, one 1-99% target-health ceiling, and one matching progress-quest context for quest {questId}.";
            return false;
        }

        targetNpcTemplateId = targetNpc[0].Value1;
        healthFloor = (byte)targetHealth[0].Value2;
        return true;
    }

    internal static SkillResult UseWithSelectedTarget(Character bot, Npc target, Func<SkillResult> useSkill)
    {
        // AAEmu validates UnitReqs against CurrentTarget before it resolves the
        // explicit SkillCastUnitTarget. A normal client has already selected the
        // unit, so reproduce that prerequisite for connectionless characters.
        var previousTarget = bot.CurrentTarget;
        bot.CurrentTarget = target;
        try
        {
            var result = useSkill();
            if (result != SkillResult.Success)
                bot.CurrentTarget = previousTarget;
            return result;
        }
        catch
        {
            bot.CurrentTarget = previousTarget;
            throw;
        }
    }

    private static string GetAvailability(Character bot, QuestTemplate quest)
    {
        if (bot.Quests.HasQuest(quest.Id))
            return "active";
        if (bot.Quests.HasQuestCompleted(quest.Id) && !quest.Repeatable)
            return "completed";
        return quest.GetComponents(QuestComponentKind.Start)
            .All(component => UnitRequirementsGameData.Instance.CanComponentRun(component, bot))
            ? "eligible"
            : "blocked";
    }

    private static string DescribeObjectiveShape(QuestTemplate quest)
    {
        var types = quest.Components.Values
            .Where(component => component.KindId == QuestComponentKind.Progress)
            .SelectMany(component => component.ActTemplates)
            .Select(act => act.GetType().Name)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return types.Length == 0 ? "travel/report" : string.Join('+', types);
    }

    private static string LocalizedNpcName(Npc npc) =>
        LocalizationManager.Instance.Get("npcs", "name", npc.TemplateId, npc.Template?.Name ?? $"NPC {npc.TemplateId}");

    private static string LocalizedQuestName(uint questId) =>
        LocalizationManager.Instance.Get("quest_contexts", "name", questId, $"Quest {questId}");

    internal static string DescribeAct(QuestActTemplate act)
    {
        var target = act switch
        {
            QuestActConAcceptNpc accept => $" npc={accept.NpcId}",
            QuestActConAcceptNpcEmotion emotion => $" npc={emotion.NpcId}",
            QuestActConAcceptNpcKill kill => $" npc={kill.NpcId}",
            QuestActConReportNpc report => $" npc={report.NpcId}",
            QuestActConReportDoodad report => $" doodad={report.DoodadId}",
            QuestActObjMonsterHunt hunt =>
                $" npc={hunt.NpcId}{OptionalField("highlight_doodad", hunt.HighlightDoodadId)}",
            QuestActObjMonsterGroupHunt groupHunt =>
                $" npc_group={groupHunt.QuestMonsterGroupId}{OptionalField("highlight_doodad", groupHunt.HighlightDoodadId)}",
            QuestActObjTalk talk =>
                $" npc={talk.NpcId}{OptionalField("item", talk.ItemId)} team_share={talk.TeamShare.ToString().ToLowerInvariant()}",
            QuestActObjTalkNpcGroup groupTalk => $" npc_group={groupTalk.NpcGroupId}",
            QuestActObjItemGather gather =>
                $" item={gather.ItemId}{OptionalField("highlight_doodad", gather.HighlightDoodadId)} cleanup={gather.Cleanup.ToString().ToLowerInvariant()}",
            QuestActObjItemUse use =>
                $" item={use.ItemId}{OptionalField("highlight_doodad", use.HighlightDoodadId)}",
            QuestActObjInteraction interaction =>
                $" doodad={interaction.DoodadId} world_interaction={interaction.WorldInteractionId}" +
                OptionalField("highlight_doodad", interaction.HighlightDoodadId),
            QuestActObjDistance distance =>
                $" npc={distance.NpcId} distance={distance.Distance} within={distance.WithIn.ToString().ToLowerInvariant()}" +
                OptionalField("highlight_doodad", distance.HighlightDoodadId),
            QuestActObjSphere sphere =>
                $" sphere={sphere.SphereId}{OptionalField("npc", sphere.NpcId)}" +
                OptionalField("highlight_doodad", sphere.HighlightDoodadId),
            QuestActSupplyItem supply => $" item={supply.ItemId} grade={supply.GradeId}",
            QuestActSupplyRemoveItem remove => $" item={remove.ItemId}",
            QuestActSupplySelectiveItem selective =>
                $" item={selective.ItemId} grade={selective.GradeId} selection={selective.ThisSelectiveIndex}",
            _ => string.Empty
        };
        var count = act.Count > 0 ? $" count={act.Count}" : string.Empty;
        return $"{act.GetType().Name}{target}{count}";
    }

    private static string OptionalField(string name, uint value) => value == 0 ? string.Empty : $" {name}={value}";

    private static uint GetExactStarterNpcId(QuestActTemplate act) => act switch
    {
        QuestActConAcceptNpc accept => accept.NpcId,
        _ => 0
    };

    private static float Distance(Character bot, Npc npc) =>
        (float)MathUtil.CalculateDistance(bot.Transform.World.Position, npc.Transform.World.Position);

    private sealed record NearbyQuestNpc(
        Npc Npc,
        float Distance,
        IReadOnlyList<QuestTemplate> Starts,
        IReadOnlyList<QuestTemplate> Reports);
}

internal enum BotQuestVerb
{
    Scan,
    Nearby,
    Inspect,
    Status,
    Accept,
    Talk,
    Use,
    Acquire,
    Report
}

internal readonly record struct BotQuestRequest(
    BotQuestVerb Verb,
    uint BotId,
    uint QuestId,
    uint NpcTemplateId,
    uint TargetObjId,
    float Radius,
    int SelectedReward);
