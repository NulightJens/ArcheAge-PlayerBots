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
#endif
        try
        {
#if PLAYERBOTS_AAEMU_3_0
            spawnPosition = WorldManager.Instance.GetWorld(WorldManager.DefaultInstanceId)?.SpawnPosition;
#else
            world = WorldManager.Instance.MainWorld;
            spawnPosition = world?.Template?.SpawnPosition;
#endif
        }
        catch (Exception)
        {
            // Unit tests and early headless callers may not have a WorldManager yet.
        }

        var actor = new SystemActor(spawnPosition);
#if !PLAYERBOTS_AAEMU_3_0
        actor.ParentWorld = world;
#endif
        return actor;
    }
}
