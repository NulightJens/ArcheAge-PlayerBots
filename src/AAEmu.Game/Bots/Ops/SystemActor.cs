using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Units;
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
        try
        {
            spawnPosition = WorldManager.Instance.MainWorld?.Template?.SpawnPosition;
        }
        catch (Exception)
        {
            // Unit tests and early headless callers may not have a WorldManager yet.
        }

        return new SystemActor(spawnPosition);
    }
}
