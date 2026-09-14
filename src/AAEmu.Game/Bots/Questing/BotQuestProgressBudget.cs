using AAEmu.Game.Bots.Host;
using AAEmu.Game.Models.Game.Bots;

namespace AAEmu.Game.Bots.Questing;

public sealed partial class BotQuestLifecycleController
{
    internal static readonly TimeSpan SideQuestNoProgressBudget = TimeSpan.FromMinutes(5);
    private readonly Dictionary<uint, (int Count, DateTimeOffset Since)> _sideProgress = [];
    private bool _preferStoryAfterStall;

    // Native objective progress owns this clock. Kills, retries, movement and
    // target respawns cannot continually renew an unsuccessful optional quest.
    private bool ExpireSideQuest(BotRuntime runtime, BotQuestSnapshot snapshot, DateTimeOffset now)
    {
        if (snapshot.MainStory || snapshot.Ready) return false;
        var count = snapshot.MonsterHunt?.Current ?? snapshot.ItemGather?.Current ?? 0;
#if !PLAYERBOTS_AAEMU_3_0
        count = snapshot.Interaction?.Current ?? count;
#endif
        if (!_sideProgress.TryGetValue(snapshot.QuestId, out var progress) || count > progress.Count)
        {
            _sideProgress[snapshot.QuestId] = (count, now);
            return false;
        }
        if (now - progress.Since < SideQuestNoProgressBudget) return false;
#if !PLAYERBOTS_AAEMU_3_0
        // Let native traversal finish safely before releasing its owner.
        if (runtime.MovementState.Climb != null) return false;
#endif
        if (runtime.Bot.IsInBattle || runtime.Bot.SkillTask != null || runtime.Bot.ActivePlotState != null)
            return false;
        var until = now + SideQuestQuarantine;
        _ignoredSideQuests[snapshot.QuestId] = until;
        _sideProgress.Remove(snapshot.QuestId);
        _preferStoryAfterStall = true;
        Log(runtime.Bot.Id, "ignored", $"quest={snapshot.QuestId} reason=side_quest_no_native_progress until={Timestamp(until)}");
        ResetPlan(runtime, releaseCombat: true, stopMovement: true);
        SetState(BotQuestLifecycleState.Idle, "side_quest_no_native_progress", now);
        return true;
    }
}
