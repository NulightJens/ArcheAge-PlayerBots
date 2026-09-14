#if !PLAYERBOTS_AAEMU_3_0
using AAEmu.Game.Bots.Host;

namespace AAEmu.Game.Bots.Questing;

public sealed partial class BotQuestLifecycleController
{
    private DateTimeOffset? _partyPausedAt;
    internal IReadOnlyList<BotQuestSnapshot> ReadPartyWork(BotRuntime runtime)
    {
        lock (_syncRoot)
            return _authority.ReadActiveQuests(runtime.Bot) ?? [];
    }

    internal bool CanSupportQuest(BotQuestSnapshot quest, DateTimeOffset now)
    {
        lock (_syncRoot) return IsSelectable(quest, now);
    }

    internal bool HasPartyPriorityWork(BotRuntime runtime, DateTimeOffset now)
    {
        lock (_syncRoot)
        {
            if (runtime.PartyQuestPriorityQuestId is not { } questId)
                return false;

            var quests = _authority.ReadActiveQuests(runtime.Bot) ?? [];
            for (var index = 0; index < quests.Count; index++)
                if (quests[index].QuestId == questId && IsSelectable(quests[index], now))
                    return true;
            return false;
        }
    }

    private bool CanChangeWork(BotRuntime runtime) =>
        runtime.Bot.SkillTask == null && runtime.Bot.ActivePlotState == null && !runtime.Bot.IsInBattle &&
        !runtime.CombatState.IsForced && !runtime.CombatState.InDuel && !runtime.CombatState.IsActive && !runtime.CombatState.IsSearching &&
        runtime.CombatState.Target == null && runtime.Bot.CurrentTarget == null &&
        runtime.MovementState.FollowTarget == null && !HasUnownedMovement(runtime) &&
        (!runtime.MovementState.IsMoving || OwnsCurrentMovement(runtime)) &&
        runtime.MovementState.Climb == null && !runtime.MovementState.IsFalling &&
        !runtime.MovementState.IsJumping &&
        _state is not (BotQuestLifecycleState.Fighting or BotQuestLifecycleState.MovingToLoot or
            BotQuestLifecycleState.WaitingForProgress or BotQuestLifecycleState.Reporting or
            BotQuestLifecycleState.WaitingForCompletion);
    // Native auto-completion can happen while the party owns the brain tick.
    // Observe it before deciding who still needs support; never grant credit here.
    internal void ReconcileNativeCompletion(BotRuntime runtime, DateTimeOffset now)
    {
        lock (_syncRoot)
        {
            if (_questId is { } quest && runtime.Bot.Quests.HasQuestCompleted(quest) &&
                runtime.MovementState.Climb == null)
                Complete(runtime, now);
        }
    }

    internal void PauseForParty(BotRuntime runtime, DateTimeOffset now)
    {
        lock (_syncRoot)
        {
            _partyPausedAt ??= now;
            StopOwnedMovement(runtime);
        }
    }

    internal void ResumeAfterParty(DateTimeOffset now)
    {
        lock (_syncRoot)
        {
            if (!_partyPausedAt.HasValue) return;
            var elapsed = now > _partyPausedAt.Value ? now - _partyPausedAt.Value : TimeSpan.Zero;
            if (_selectionDeadline.HasValue) _selectionDeadline += elapsed;
            if (_lootApproachDeadline.HasValue) _lootApproachDeadline += elapsed;
            if (_respawnWaitStartedAt.HasValue) _respawnWaitStartedAt += elapsed;
            if (_respawnRescanAt.HasValue) _respawnRescanAt += elapsed;
            if (_questId is { } quest && _sideProgress.TryGetValue(quest, out var progress))
                _sideProgress[quest] = (progress.Count, progress.Since + elapsed);
            _partyPausedAt = null;
        }
    }
}
#endif
