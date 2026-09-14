using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.World;
using AAEmu.Game.Models.Game.World.Transform;

namespace AAEmu.Game.Bots.Ops;

internal sealed class SystemActor : Character
{
    public const string ActorName = "@system";

    private SystemActor(WorldSpawnPosition spawnPosition)
        : base(new UnitCustomModelParams())
    {
        Name = ActorName;
        AccessLevel = 100;
        Connection = null;
        Transform.ApplyWorldSpawnPosition(spawnPosition ?? new WorldSpawnPosition());
    }

    public static SystemActor Create()
    {
        WorldSpawnPosition spawnPosition = null;
#if !PLAYERBOTS_AAEMU_3_0
        WorldInstance world = null;
        var instanceId = 0u;
#endif
        try
        {
#if PLAYERBOTS_AAEMU_3_0
            spawnPosition = WorldManager.Instance.GetWorld(WorldManager.DefaultInstanceId)?.SpawnPosition;
#else
            TryGetQualifiedBotAnchor(out world, out spawnPosition, out instanceId);
#endif
        }
        catch (Exception)
        {
            // Unit tests and early headless callers may not have the managers yet.
        }

        var actor = new SystemActor(spawnPosition);
#if !PLAYERBOTS_AAEMU_3_0
        if (world != null)
        {
            actor.Transform.InstanceId = instanceId;
            actor.ParentWorld = world;
        }
#endif
        return actor;
    }

#if !PLAYERBOTS_AAEMU_3_0
    private static bool TryGetQualifiedBotAnchor(
        out WorldInstance world,
        out WorldSpawnPosition spawnPosition,
        out uint instanceId)
    {
        world = null;
        spawnPosition = null;
        instanceId = 0;

        foreach (var bot in BotManager.Instance.GetAllBots().OrderBy(candidate => candidate.Id))
        {
            try
            {
                if (!TryCaptureQualifiedAnchor(
                        bot,
                        out var candidateWorld,
                        out var candidateSpawnPosition,
                        out var candidateInstanceId))
                    continue;

                world = candidateWorld;
                spawnPosition = candidateSpawnPosition;
                instanceId = candidateInstanceId;
                return true;
            }
            catch (Exception)
            {
                // A concurrently departing bot is not a usable operational anchor.
            }
        }

        return false;
    }

    private static bool TryCaptureQualifiedAnchor(
        Character bot,
        out WorldInstance world,
        out WorldSpawnPosition spawnPosition,
        out uint instanceId)
    {
        world = bot?.ParentWorld;
        spawnPosition = null;
        instanceId = 0;
        var transform = bot?.Transform;
        if (bot?.Id == 0 || world == null || transform == null || transform.ZoneId == 0)
            return false;

        using var transformSnapshot = transform.CloneDetached();
        var positionSnapshot = transformSnapshot.CloneAsSpawnPosition();
        var capturedInstanceId = transformSnapshot.InstanceId;
        if (!ReferenceEquals(bot.ParentWorld, world) ||
            transform.InstanceId != capturedInstanceId ||
            transform.ZoneId != positionSnapshot.ZoneId ||
            world.Id != capturedInstanceId ||
            positionSnapshot.ZoneId == 0 ||
            !HasFiniteTransform(positionSnapshot))
            return false;

        spawnPosition = positionSnapshot;
        instanceId = capturedInstanceId;
        return true;
    }

    private static bool HasFiniteTransform(WorldSpawnPosition position)
    {
        return float.IsFinite(position.X) &&
               float.IsFinite(position.Y) &&
               float.IsFinite(position.Z) &&
               float.IsFinite(position.Roll) &&
               float.IsFinite(position.Pitch) &&
               float.IsFinite(position.Yaw);
    }
#endif
}
