#if !PLAYERBOTS_AAEMU_3_0
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Bots.Questing;
using AAEmu.Game.Models.Game.Bots;

namespace AAEmu.Game.Bots.Social;

internal sealed partial class BotPartyQuestCoordinator
{
    private Dictionary<uint, IReadOnlyList<BotQuestSnapshot>> _nativeWork = [];
    private bool _nativeReadFailed;

    private bool RefreshNativeWork(BotRuntime[] members, DateTimeOffset now)
    {
        Dictionary<uint, IReadOnlyList<BotQuestSnapshot>> next;
        try
        {
            next = members.ToDictionary(r => r.Bot.Id, r => (IReadOnlyList<BotQuestSnapshot>)
                r.QuestLifecycleController.ReadPartyWork(r).Where(q => !r.Bot.Quests.HasQuestCompleted(q.QuestId)).ToArray());
            _nativeReadFailed = false;
        }
        catch (Exception exception)
        {
            if (!_nativeReadFailed) Logger.Warn(exception, "BOT ev=party_native_work_unavailable");
            _nativeReadFailed = true;
            ClearSupport();
            foreach (var member in members) _holds[member.Bot.Id] = "party_native_work_unavailable";
            return false;
        }
        var progressed = false;
        foreach (var member in members)
        {
            if (!_nativeWork.TryGetValue(member.Bot.Id, out var before)) continue;
            var after = next[member.Bot.Id];
            progressed |= after.Any(q =>
            {
                var prior = before.FirstOrDefault(p => p.QuestId == q.QuestId);
                return prior == null || q.Ready && !prior.Ready || ObjectiveCount(q) > ObjectiveCount(prior);
            });
            progressed |= before.Any(q => member.Bot.Quests.HasQuestCompleted(q.QuestId));
        }
        _nativeWork = next;
        if (progressed)
        {
            _waitingSince.Clear();
            if (_regroupStartedAt.HasValue) _regroupStartedAt = now;
            _supportProgressAt = now;
        }
        return true;
    }

    private static int ObjectiveCount(BotQuestSnapshot quest) =>
        quest.MonsterHunt?.Current ?? quest.ItemGather?.Current ?? quest.Interaction?.Current ?? 0;

    private bool NativeReadyWork(BotRuntime member, uint quest, DateTimeOffset now) =>
        _nativeWork.GetValueOrDefault(member.Bot.Id, []).Any(q => q.QuestId == quest && q.Ready &&
            member.QuestLifecycleController.CanSupportQuest(q, now));

    private sealed record SupportWork(BotRuntime Runtime, BotQuestLifecycleView View, uint Quest,
        bool Ready, bool Acceptance, int FinishedPeers);

    private IEnumerable<SupportWork> SupportCandidates(BotRuntime[] members, BotConfig config, DateTimeOffset now)
    {
        var sharedStory = _nativeWork.Values.SelectMany(q => q).Where(q => q.MainStory)
            .Select(q => q.QuestId).Distinct().Order().ToArray();
        foreach (var member in members)
        {
            var view = member.QuestLifecycleController.Inspect();
            var ready = _nativeWork.GetValueOrDefault(member.Bot.Id, [])
                .Where(q => q.Ready && member.QuestLifecycleController.CanSupportQuest(q, now)).ToArray();
            foreach (var quest in ready) yield return Work(quest.QuestId, true, false);
            if (PendingAction(view) && ready.All(q => q.QuestId != view.QuestId))
                yield return Work(view.QuestId.Value, false, false);
            foreach (var quest in sharedStory)
                if (!_nativeWork.GetValueOrDefault(member.Bot.Id, []).Any(q => q.QuestId == quest) &&
                    member.QuestIntakeController.HasPartyStoryOpportunity(member, quest, config, now))
                    yield return Work(quest, false, true);

            SupportWork Work(uint quest, bool isReady, bool acceptance) => new(member, view, quest, isReady, acceptance,
                members.Count(other => other.Bot.Id != member.Bot.Id &&
                    (other.Bot.Quests.HasQuestCompleted(quest) ||
                     _nativeWork.GetValueOrDefault(other.Bot.Id, []).Any(q => q.QuestId == quest && q.Ready))));
        }
    }
}
#endif
