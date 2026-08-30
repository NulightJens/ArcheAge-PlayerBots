using System.Numerics;

namespace AAEmu.Game.Models.Game.Bots;

public interface IBotMovementBroadcaster
{
    void SendMove(Vector3 position, Vector3 velocity, bool isInBattle);

    void SendStop(Vector3 position, bool isInBattle);

    void SendFall(Vector3 position, float fallVelocity, bool isInBattle);

    /// <summary>
    /// Broadcasts an airborne jump using world-space velocity (positive Z is upward).
    /// The ArcheAge movement packet uses the opposite sign for its vertical velocity.
    /// </summary>
    void SendJump(Vector3 position, Vector3 velocity, bool isInBattle);

    void SendTeleport(Vector3 position, bool isInBattle);

    void SendFaceTarget(Vector3 position, float rotationZ, bool isInBattle);

    void SendRelaxedStance(Vector3 position);
}
