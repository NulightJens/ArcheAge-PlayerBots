#if !PLAYERBOTS_AAEMU_3_0
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Models.Game.Bots;

namespace AAEmu.Game.Bots.Questing;

public sealed partial class BotQuestIntakeController
{
    internal bool HasPartyStoryOpportunity(BotRuntime runtime, uint questId, BotConfig config, DateTimeOffset now)
    {
        lock (_syncRoot)
        {
            if (!config.QuestIntakeEnabled || runtime.Bot.Quests.HasQuest(questId) ||
                runtime.Bot.Quests.HasQuestCompleted(questId) || _retryAt > now) return false;
            try
            {
                return FindCandidates(runtime, config, now).Any(c => c.Quest.Id == questId && c.MainStory) ||
                    FindPartyStoryRoute(runtime, questId).Quest != null;
            }
            catch { return false; }
        }
    }

    private BotQuestStaticStartDestination FindPartyStoryRoute(BotRuntime runtime, uint questId) =>
        (_staticNpcStarts(runtime, MaximumStaticStartRouteDistance) ?? [])
            .Where(r => r.Quest?.Id == questId && r.NpcTemplateId != 0 && IsMainStory(r.Quest) &&
                IsEligible(runtime.Bot, r.Quest) && IsFinite(r.Position) && float.IsFinite(r.Distance) &&
                r.Distance >= 0 && r.Distance <= MaximumStaticStartRouteDistance)
            .OrderBy(r => r.Distance).ThenBy(r => r.NpcTemplateId).FirstOrDefault();

    private bool StepPartyStoryAcceptance(BotRuntime runtime, BotConfig config, DateTimeOffset now, uint questId)
    {
        if (_plan?.Quests.Any(q => q.Id == questId) == true)
            return StepPlan(runtime, config, now);
        if (_staticStartPlan?.Quest.Id == questId)
            return StepStaticStartPlan(runtime, config, now);
        if (_retryAt > now) return false;
        var local = FindCandidates(runtime, config, now).FirstOrDefault(c => c.Quest.Id == questId && c.MainStory);
        if (local.Giver != null)
        {
            InvalidatePlan(runtime, "party_story_acceptance_selected", now, stopOwnedMovement: true);
            _plan = new GiverPlan(local.Kind, local.Giver.ObjId, local.Giver.TemplateId, [local.Quest]);
            SetState(BotQuestIntakeState.Interacting, "party_story_acceptance_selected", now);
            return StepPlan(runtime, config, now);
        }
        var route = FindPartyStoryRoute(runtime, questId);
        if (route.Quest == null)
        {
            SetState(BotQuestIntakeState.Blocked, "party_story_acceptance_unavailable", now);
            return false;
        }
        InvalidatePlan(runtime, "party_story_acceptance_selected", now, stopOwnedMovement: true);
        _pendingStaticStartPlan = new StaticStartPlan(route.NpcTemplateId, route.Quest, route.Position);
        return BeginPendingStaticMainStoryRoute(runtime, config, now);
    }
}
#endif
