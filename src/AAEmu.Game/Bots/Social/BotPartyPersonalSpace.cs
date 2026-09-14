#if !PLAYERBOTS_AAEMU_3_0
using System.Numerics;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Models.Game.Bots;

namespace AAEmu.Game.Bots.Social;

internal sealed class BotPartyPersonalSpace(
    Func<BotRuntime, Vector3, bool> route,
    Action<BotRuntime> stop,
    Func<BotRuntime, Vector3, float> height)
{
    private readonly Dictionary<uint, DateTimeOffset> _crowdedSince = [];
    private readonly Dictionary<uint, DateTimeOffset> _retryAfter = [];
    private readonly Dictionary<uint, (Vector3 Target, DateTimeOffset Until)> _active = [];

    internal void Cancel(BotRuntime runtime)
    {
        if (_active.Remove(runtime.Bot.Id) && runtime.MovementState.TravelOwner == BotMovementOwner.PartyQuest)
            stop(runtime);
        _crowdedSince.Remove(runtime.Bot.Id);
    }

    internal bool Step(BotRuntime runtime, BotRuntime[] members, DateTimeOffset now, bool waiting)
    {
        var id = runtime.Bot.Id; var bot = runtime.Bot; var state = runtime.MovementState;
        if (!waiting || runtime.Retired || bot.IsDead || bot.IsInBattle || bot.SkillTask != null ||
            runtime.CombatState.IsForced || runtime.CombatState.Target != null ||
            state.Climb != null || state.FollowTarget != null || state.IsFalling || state.IsJumping ||
            state.JumpRequested || bot.Transform?.Parent != null || bot.Transform?.StickyParent != null)
        { Cancel(runtime); return false; }
        var position = bot.Transform.World.Position;
        if (_active.TryGetValue(id, out var active))
        {
            if (state.TravelOwner != BotMovementOwner.PartyQuest || !state.Destination.HasValue ||
                now >= active.Until || Vector3.Distance(position, active.Target) <= .65f)
            { Cancel(runtime); _retryAfter[id] = now.AddSeconds(15); return false; }
            return true;
        }
        if (state.Destination.HasValue || state.IsMoving ||
            _retryAfter.TryGetValue(id, out var retry) && now < retry) return false;
        var crowded = members.Where(r => !r.Retired && ReferenceEquals(r.Bot.ParentWorld, bot.ParentWorld) &&
            r.Bot.Transform.InstanceId == bot.Transform.InstanceId &&
            Vector3.Distance(r.Bot.Transform.World.Position, position) < 1.2f).ToArray();
        if (crowded.Length < 2) { _crowdedSince.Remove(id); return false; }
        if (!_crowdedSince.TryGetValue(id, out var since)) { _crowdedSince[id] = now; return false; }
        if (now - since < TimeSpan.FromSeconds(2)) return false;
        _crowdedSince.Remove(id); _retryAfter[id] = now.AddSeconds(15);
        var centre = crowded.Aggregate(Vector3.Zero, (sum, r) => sum + r.Bot.Transform.World.Position) / crowded.Length;
        var target = centre + BotPartyQuestCoordinator.SlotOffset(id);
        if (Vector3.Distance(position, target) > 3.25f) return false;
        try
        {
            var originGround = height(runtime, position);
            if (!float.IsFinite(originGround) || MathF.Abs(originGround - position.Z) > .3f) return false;
            for (var i = 1; i <= 12; i++)
            {
                var sample = Vector3.Lerp(position, target, i / 12f);
                var ground = height(runtime, sample);
                if (!float.IsFinite(ground) || MathF.Abs(ground - originGround) > .3f) return false;
            }
            target.Z = height(runtime, target);
            if (!float.IsFinite(target.Z) || !route(runtime, target)) return false;
            // A cosmetic step must never become a long detour.
            if (state.TravelRemainingDistance > 5f) { stop(runtime); return false; }
        }
        catch { return false; }
        _active[id] = (target, now.AddSeconds(5));
        runtime.PartyRegroupDetail = "personal_space_step";
        return true;
    }
}
#endif
