using System.Numerics;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;

namespace AAEmu.Game.Bots.Social;

public static class BotFormation
{
    public static Vector3 PositionFor(Character leader, BotMovementState movement)
    {
        if (leader?.Transform == null || movement?.FormationSlot < 0)
            return leader?.Transform?.World.Position ?? Vector3.Zero;

        return movement.FormationColumns > 0
            ? SpreadPositionFor(
                leader,
                movement.FormationSlot,
                movement.FormationMemberCount,
                movement.FollowDistance,
                movement.FormationColumns,
                movement.FormationSpacing)
            : PositionFor(leader, movement.FormationSlot, movement.FollowDistance);
    }

    public static Vector3 PositionFor(Character leader, int slot, float spacing)
    {
        if (leader?.Transform == null || slot < 0)
            return leader?.Transform?.World.Position ?? Vector3.Zero;

        spacing = MathF.Max(0.5f, spacing);
        var row = slot / 2;
        var side = slot % 2 == 0 ? -1f : 1f;
        var depthVariation = StableSignedVariation(leader.Id, slot, 0xA511E9B3u)
            * MathF.Min(0.7f, spacing * 0.35f);
        var lateralVariation = StableSignedVariation(leader.Id, slot, 0x63D83595u)
            * MathF.Min(0.55f, spacing * 0.3f);
        var yaw = leader.Transform.World.Rotation.Z * MathF.PI / 180f;
        var forward = new Vector3(MathF.Cos(yaw), MathF.Sin(yaw), 0f);
        var right = new Vector3(-forward.Y, forward.X, 0f);
        var behind = MathF.Max(spacing * 0.85f, spacing * (1.15f + row * 0.92f) + depthVariation);
        var lateral = spacing * (0.55f + row * 0.12f) * side + lateralVariation;
        return leader.Transform.World.Position - forward * behind + right * lateral;
    }

    // Party-follow targets are recalculated every movement tick. A stable hash gives
    // each slot organic-looking variance without random sampling or target jitter.
    private static float StableSignedVariation(uint leaderId, int slot, uint salt)
    {
        var value = unchecked(leaderId * 0x9E3779B9u + ((uint)slot + 1u) * 0x85EBCA6Bu + salt);
        value ^= value >> 16;
        value = unchecked(value * 0x7FEB352Du);
        value ^= value >> 15;
        value = unchecked(value * 0x846CA68Bu);
        value ^= value >> 16;
        return (value & 0x00FFFFFFu) / 8388607.5f - 1f;
    }

    public static Vector3 SpreadPositionFor(
        Character leader,
        int slot,
        int memberCount,
        float baseDistance,
        int columns,
        float spacing)
    {
        if (leader?.Transform == null || slot < 0 || memberCount <= 0 || slot >= memberCount)
            return leader?.Transform?.World.Position ?? Vector3.Zero;

        baseDistance = MathF.Max(0.5f, baseDistance);
        spacing = MathF.Max(0.5f, spacing);
        columns = Math.Clamp(columns, 1, memberCount);

        var row = slot / columns;
        var column = slot % columns;
        var membersBeforeRow = row * columns;
        var membersInRow = Math.Min(columns, memberCount - membersBeforeRow);
        var centeredColumn = column - (membersInRow - 1) / 2f;

        var yaw = leader.Transform.World.Rotation.Z * MathF.PI / 180f;
        var forward = new Vector3(MathF.Cos(yaw), MathF.Sin(yaw), 0f);
        var right = new Vector3(-forward.Y, forward.X, 0f);
        var behind = baseDistance + row * spacing;
        var lateral = centeredColumn * spacing;
        return leader.Transform.World.Position - forward * behind + right * lateral;
    }
}
