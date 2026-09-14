#if !PLAYERBOTS_AAEMU_3_0
using System.Numerics;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Bots.Questing;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Bots.Social;

internal sealed partial class BotPartyQuestCoordinator
{
    private uint _explicitLeaderId;
    private BotRuntime _explicitLeader;
    private DateTimeOffset? _explicitUnavailableAt;

    private static bool Present(BotRuntime runtime) => runtime is { Retired: false } &&
        runtime.Bot.Transform != null && !runtime.Bot.IsDead &&
        (runtime.OwnerHost == null || ReferenceEquals(runtime.OwnerHost.GetRuntime(runtime.Bot.Id), runtime));

    private void UpdateExplicitSupport(BotRuntime[] members, DateTimeOffset now)
    {
        _explicitLeader = members.FirstOrDefault(r => r.Bot.Id == _explicitLeaderId && Present(r) &&
            r.Social.TeamId != 0 && r.CombatState.ForcedState == null);
        if (_explicitLeader == null)
        {
            ClearSupport();
            _explicitUnavailableAt ??= now;
            return;
        }
        _explicitUnavailableAt = null;
        _supportMember = _explicitLeaderId;
        if (!IsSafeGround(_explicitLeader)) return;
        var position = _explicitLeader.Bot.Transform.World.Position;
        _supportGround = position;
        if (_supportTrail.Count > 0 && Vector3.Distance(_supportTrail[^1], position) > 12f)
            _supportTrail.Clear();
        if (_supportTrail.Count == 0 || Vector3.Distance(_supportTrail[^1], position) >= 2f)
        {
            _supportTrail.Add(position);
            if (_supportTrail.Count > 256) _supportTrail.RemoveAt(0);
        }
    }

    private bool StepExplicitSupport(BotRuntime runtime, DateTimeOffset now)
    {
        var movement = runtime.MovementState;
        var combat = runtime.CombatState;
        runtime.PartyRegroupDetail = $"mode=support leader={_explicitLeaderId}";
        if (!Present(runtime) || combat.ForcedState != null)
        {
            // A manual Follow/Attack still owns its brain; a manual Idle stays paused.
            runtime.PartyQuestSuppressBrain = combat.ForcedState is null or BotCombatStateType.Idle;
            SetReason(runtime, "support_member_unavailable_or_ordered");
            return false;
        }
        var view = runtime.QuestLifecycleController.Inspect();
        var busy = runtime.Bot.SkillTask != null || runtime.Bot.IsInBattle || combat.Target != null ||
            movement.Climb != null || movement.IsFalling || movement.IsJumping ||
            view.State is BotQuestLifecycleState.MovingToLoot or BotQuestLifecycleState.WaitingForProgress or
                BotQuestLifecycleState.WaitingForCompletion;
        if (busy)
        {
            runtime.QuestLifecycleController.ResumeAfterParty(now);
            SetReason(runtime, "support_finishing_native_action");
            return true;
        }
        var leader = _explicitLeader;
        var sameParty = Present(leader) && leader.CombatState.ForcedState == null && leader.Social.TeamId == runtime.Social.TeamId &&
            ReferenceEquals(leader.Bot.ParentWorld, runtime.Bot.ParentWorld) &&
            leader.Bot.Transform.InstanceId == runtime.Bot.Transform.InstanceId;
        if (sameParty && runtime.Bot.Id == _explicitLeaderId)
        {
            if (movement.TravelOwner == BotMovementOwner.PartyQuest) _stop(runtime);
            runtime.QuestLifecycleController.ResumeAfterParty(now);
            SetReason(runtime, "support_leader_questing");
            return true;
        }
        if (movement.FollowTarget != null || movement.TravelOwner == BotMovementOwner.External &&
            (movement.Destination.HasValue || movement.TravelDestination.HasValue || movement.IsMoving))
        {
            SetReason(runtime, "support_preserving_manual_order");
            return false;
        }
        runtime.QuestLifecycleController.PauseForParty(runtime, now);
        runtime.QuestIntakeController.YieldToQuestLifecycle(runtime, now);
        runtime.PartyQuestSuppressBrain = true;
        if (!sameParty)
        {
            if (movement.TravelOwner == BotMovementOwner.PartyQuest) _stop(runtime);
            SetReason(runtime, _explicitUnavailableAt is { } since && now - since >= TimeSpan.FromMinutes(2)
                ? "support_blocked_leader_unavailable" : "support_leader_unavailable");
            return false;
        }
        if (runtime.Social.CombatOrder == BotCombatOrder.Assist && leader.Bot.IsInBattle &&
            leader.Bot.CurrentTarget is Unit target && !target.IsDead && target.Transform != null &&
            ReferenceEquals(target.ParentWorld, runtime.Bot.ParentWorld) &&
            target.Transform.InstanceId == runtime.Bot.Transform.InstanceId &&
            Vector3.Distance(runtime.Bot.Transform.World.Position, target.Transform.World.Position) <= 45f &&
            runtime.Bot.CanAttack(target))
        {
            if (movement.TravelOwner == BotMovementOwner.PartyQuest) _stop(runtime);
            runtime.Social.ApplyAttack(target);
            runtime.PartyQuestSuppressBrain = false;
            SetReason(runtime, "support_assisting_leader");
            return false;
        }
        SetReason(runtime, "support_following_leader");
        FollowSupportMember(runtime, now);
        return false;
    }
}
#endif
