#if !PLAYERBOTS_AAEMU_3_0
using System.Numerics;
using AAEmu.Game.Compatibility;
using AAEmu.Game.Core.Managers.UnitManagers;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.DoodadObj;
using AAEmu.Game.Models.Game.DoodadObj.Funcs;

namespace AAEmu.Game.Bots.Questing;

internal sealed record BotClimbPlan(uint ObjectId, uint TemplateId, uint Phase,
    Vector3 Anchor, Vector3 Approach, float TargetHeight);

internal sealed class BotClimbMotion
{
    internal required BotClimbPlan Plan { get; init; }
    internal required uint QuestId { get; init; }
    internal required DateTimeOffset Deadline { get; init; }
    internal bool Descending { get; set; }
    internal bool AtHeight { get; private set; }
    internal const float Speed = 2f;
    internal const float MaximumHeight = 32f;

    internal static bool Supports(Doodad anchor) => anchor != null &&
        DoodadManager.Instance.GetFuncsForGroup(anchor.FuncGroupId).Any(f =>
            f.FuncType == "DoodadFuncClimb" &&
            DoodadManager.Instance.GetFuncTemplate(f.FuncId, f.FuncType) is DoodadFuncClimb { ClimbTypeId: 6 });

    internal static bool GeometryMatches(Vector3 anchor, Vector3 target, float range) =>
        float.IsFinite(range) && range > 0 &&
        BotQuestInteractions.InRange(new Vector3(anchor.X, anchor.Y, target.Z), target, 1.5f) &&
        target.Z - anchor.Z > range && target.Z - anchor.Z <= MaximumHeight;

    internal static BotClimbPlan Find(Character bot, Doodad target, float range)
    {
        if (bot?.ParentWorld == null || target == null ||
            target.Transform.World.Position.Z - bot.Transform.World.Position.Z <= range)
            return null;
        var targetPosition = target.Transform.World.Position;
        // This first adapter supports native vertical tree/pole climbing only.
        // A nearby wall or arbitrary tall object is not proof of a climbable route.
        var anchors = bot.ParentWorld.GetPlayerBotDoodadsNear(bot, 100)
            .Where(d => Available(bot, d) && GeometryMatches(d.Transform.World.Position, targetPosition, range) && Supports(d))
            .Take(2).ToArray();
        if (anchors.Length != 1)
            return null;
        var anchor = anchors[0];
        var origin = anchor.Transform.World.Position;
        var offset = new Vector2(bot.Transform.World.Position.X - origin.X, bot.Transform.World.Position.Y - origin.Y);
        if (offset.LengthSquared() < .01f) offset = Vector2.UnitX;
        offset = Vector2.Normalize(offset) * .8f;
        var approach = new Vector3(origin.X + offset.X, origin.Y + offset.Y, origin.Z);
        var ground = bot.ParentWorld.GetHeight(approach.X, approach.Y);
        if (!float.IsFinite(ground) || Math.Abs(ground - origin.Z) > 2f) return null;
        approach.Z = ground;
        return new(anchor.ObjId, anchor.TemplateId, anchor.FuncGroupId, origin, approach,
            targetPosition.Z - Math.Min(range * .5f, 1f));
    }

    private static bool Available(Character bot, Doodad d) => d != null &&
        ReferenceEquals(bot.ParentWorld, d.ParentWorld) && d.Despawn == DateTime.MinValue &&
        (d.OwnerId == 0 || d.OwnerId == bot.Id) && d.OwnerDbId == 0;

    internal static bool NativeEntryAvailable(Character bot,Doodad anchor) =>
        bot?.ParentWorld!=null && Available(bot,anchor) &&
        BotQuestInteractions.InRange(bot.Transform.World.Position,anchor.Transform.World.Position,4f) &&
        DoodadManager.Instance.GetFuncsForGroup(anchor.FuncGroupId).Any(f =>
            f.FuncType=="DoodadFuncClimb" &&
            DoodadManager.Instance.GetFuncTemplate(f.FuncId,f.FuncType) is DoodadFuncClimb { ClimbTypeId: 1 or 6 });

    internal static bool Begin(Character bot, BotMovementState state, BotClimbPlan plan, uint questId, DateTimeOffset now)
    {
        // Connected climbing must be entered by the owning client's native
        // interaction/controller. The headless transform executor cannot do it.
        if (AAEmu.Game.Bots.Host.BotDrivers.For(bot).Owns(bot)) return false;
        var anchor = bot.ParentWorld?.GetDoodad(plan.ObjectId);
        if (state.Climb != null || bot.Transform.Parent != null || bot.Transform.StickyParent != null ||
            state.FollowTarget != null || state.Destination.HasValue || state.IsMoving || state.IsFalling ||
            bot.IsDead || bot.IsInBattle || !Available(bot, anchor) || !Supports(anchor) ||
            anchor.TemplateId != plan.TemplateId || anchor.FuncGroupId != plan.Phase ||
            !BotQuestInteractions.InRange(anchor.Transform.World.Position, plan.Anchor, .1f) ||
            !BotQuestInteractions.InRange(bot.Transform.World.Position, plan.Approach, 1.25f))
            return false;
        // Same host operations as CSHangPacket, with stricter bot-owned range/phase checks.
        bot.Transform.StickyParent = anchor.Transform;
        bot.BroadcastPacket(new SCHungPacket(bot.ObjId, anchor.ObjId), true);
        anchor.Use(bot);
        state.Climb = new() { Plan = plan, QuestId = questId, Deadline = now.AddSeconds(90) };
        state.TravelOwner = BotMovementOwner.QuestLifecycle;
        return true;
    }

    internal static float AdvanceHeight(float current, float target, float elapsed) =>
        current + Math.Clamp(target - current, -Speed * Math.Clamp(elapsed, 0, .25f), Speed * Math.Clamp(elapsed, 0, .25f));

    internal static bool Tick(Character bot, BotMovementState state, IBotMovementBroadcaster broadcaster, float elapsed, DateTimeOffset now)
    {
        if (state.Climb is not { } climb) return false;
        var anchor = bot.ParentWorld?.GetDoodad(climb.Plan.ObjectId);
        if (!Available(bot, anchor) || anchor.FuncGroupId != climb.Plan.Phase ||
            !ReferenceEquals(bot.Transform.StickyParent, anchor.Transform) ||
            !BotQuestInteractions.InRange(anchor.Transform.World.Position, climb.Plan.Anchor, .1f) ||
            state.FollowTarget != null || state.Destination.HasValue || bot.IsDead || bot.IsInBattle)
        {
            Cancel(bot, state, broadcaster);
            return false;
        }
        if (now >= climb.Deadline) climb.Descending = true;
        var position = bot.Transform.World.Position;
        if (!BotQuestInteractions.InRange(new Vector3(position.X, position.Y, climb.Plan.Anchor.Z), climb.Plan.Anchor, 2.25f) ||
            position.Z < climb.Plan.Approach.Z - .5f || position.Z > climb.Plan.Anchor.Z + MaximumHeight)
        {
            Cancel(bot, state, broadcaster);
            return false;
        }
        var destinationZ = climb.Descending ? climb.Plan.Approach.Z : climb.Plan.TargetHeight;
        var nextZ = bot.SkillTask == null ? AdvanceHeight(position.Z, destinationZ, elapsed) : position.Z;
        var velocity = new Vector3(0, 0, elapsed > 0 ? (nextZ - position.Z) / elapsed : 0);
        position.Z = nextZ;
        // StickyParent is a real transform parent in this host. Preserve world-space
        // climbing speed while storing the native unscaled parent-local position.
        var localPosition = Vector3.Transform(position - anchor.Transform.World.Position,
            Quaternion.Inverse(anchor.Transform.World.ToQuaternion())) / anchor.Scale;
        bot.Transform.Local.SetPosition(localPosition);
        bot.Transform.FinalizeTransform();
        state.IsMoving = Math.Abs(velocity.Z) > .001f;
        state.IsFalling = false;
        state.FallVelocity = 0;
        climb.AtHeight = Math.Abs(nextZ - destinationZ) < .02f;
        broadcaster.SendClimb(position, velocity, anchor.ObjId, Math.Max(0, nextZ - climb.Plan.Anchor.Z));
        if (climb.Descending && climb.AtHeight) Cancel(bot, state, broadcaster, 0);
        return true;
    }

    internal static void Cancel(Character bot, BotMovementState state, IBotMovementBroadcaster broadcaster, uint reason = 7)
    {
        if (state.Climb is not { } climb) return;
        if (bot.Transform.StickyParent?.GameObject?.ObjId == climb.Plan.ObjectId)
        {
            bot.Transform.StickyParent = null;
            bot.BroadcastPacket(new SCUnhungPacket(bot.ObjId, climb.Plan.ObjectId, reason), true);
        }
        state.Climb = null;
        state.IsMoving = false;
        state.TravelOwner = BotMovementOwner.None;
        broadcaster.SendStop(bot.Transform.World.Position, bot.IsInBattle);
    }
}
#endif
