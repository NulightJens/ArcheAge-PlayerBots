#if !PLAYERBOTS_AAEMU_3_0
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Quests;
using AAEmu.Game.Models.Game.Quests.Acts;
using AAEmu.Game.Models.Game.Quests.Static;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Bots.Questing;

internal static class BotQuestTalk
{
    internal const float Range = 5f;

    internal static BotInteractionAttempt Execute(Character bot, BotInteractionPlan plan)
    {
        if (bot?.ParentWorld == null || bot.IsDead || bot.IsInBattle || bot.SkillTask != null || !plan.Available)
            return new(false, "talk_actor_unavailable");
        if (BotDrivers.For(bot).Owns(bot))
            return new(false, "connected_client_talk_binding_unavailable");
        if (!bot.Quests.ActiveQuests.TryGetValue(plan.QuestId, out var quest) ||
            quest.Step != QuestComponentKind.Progress || BotQuestInteractions.ReadObjective(quest) != plan.Objective)
            return new(false, "talk_objective_changed");
        var npc = WorldManager.GetAround<Npc>(bot, Range, true)
            .Where(n => n.TemplateId == plan.TalkNpcId && !n.IsDead && n.Hp > 0 &&
                ReferenceEquals(n.ParentWorld, bot.ParentWorld) &&
                BotQuestInteractions.InRange(bot.Transform.World.Position, n.Transform.World.Position, Range))
            .OrderBy(n => n.ObjId).FirstOrDefault();
        if (npc == null) return new(false, "talk_target_not_in_range");
        return Dispatch(bot, quest, npc);
    }

    internal static BotInteractionAttempt Dispatch(Character bot, Quest quest, Npc npc)
    {
        if (bot?.ParentWorld == null || npc == null || bot.IsDead || npc.IsDead || npc.Hp <= 0 ||
            !ReferenceEquals(bot.ParentWorld, npc.ParentWorld) ||
            !BotQuestInteractions.InRange(bot.Transform.World.Position, npc.Transform.World.Position, Range) ||
            quest?.Owner != bot || quest.Step != QuestComponentKind.Progress ||
            !quest.QuestSteps.TryGetValue(QuestComponentKind.Progress, out var progress))
            return new(false, "talk_target_or_quest_invalid");
        var acts = progress.Components.Values.Where(c => c.IsCurrentlyActive).SelectMany(c => c.Acts)
            .Where(a => a.Template.CountsAsAnObjective).ToArray();
        if (acts.Length != 1 || acts[0].Template is not QuestActObjTalk talk || talk.NpcId != npc.TemplateId ||
            !quest.TryGetPlayerBotObjectiveCount(talk.ThisComponentObjectiveIndex, out var current) || current != 0)
            return new(false, "talk_objective_changed");
        var act = acts[0];
        // Use the selected native act, as the existing scoped talk command does.
        // Native evaluation and TeamShare remain authoritative; never set credit.
        act.OnTalkMade(bot, new OnTalkMadeArgs { QuestId = quest.TemplateId, NpcId = npc.TemplateId,
            QuestComponentId = act.QuestComponent.Template.Id, QuestActId = act.Id,
            Transform = npc.Transform, SourcePlayer = bot });
        return new(true, "native_talk_dispatched");
    }
}
#endif
