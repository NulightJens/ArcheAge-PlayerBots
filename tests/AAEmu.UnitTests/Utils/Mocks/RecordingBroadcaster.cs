using System.Numerics;
using AAEmu.Game.Models.Game.Bots;

namespace AAEmu.UnitTests.Utils.Mocks;

public sealed class RecordingBroadcaster : IBotMovementBroadcaster
{
    public List<(string Kind, Vector3 Position, Vector3 Velocity, bool InBattle)> Calls { get; } = [];

    public void SendMove(Vector3 position, Vector3 velocity, bool isInBattle)
    {
        Calls.Add(("Move", position, velocity, isInBattle));
    }

    public void SendStop(Vector3 position, bool isInBattle)
    {
        Calls.Add(("Stop", position, Vector3.Zero, isInBattle));
    }

    public void SendFall(Vector3 position, float fallVelocity, bool isInBattle)
    {
        Calls.Add(("Fall", position, new Vector3(0, 0, fallVelocity), isInBattle));
    }

    public void SendJump(Vector3 position, Vector3 velocity, bool isInBattle)
    {
        Calls.Add(("Jump", position, velocity, isInBattle));
    }

    public void SendTeleport(Vector3 position, bool isInBattle)
    {
        Calls.Add(("Teleport", position, Vector3.Zero, isInBattle));
    }

    public void SendFaceTarget(Vector3 position, float rotationZ, bool isInBattle)
    {
        Calls.Add(("FaceTarget", position, new Vector3(0, 0, rotationZ), isInBattle));
    }

    public void SendRelaxedStance(Vector3 position)
    {
        Calls.Add(("RelaxedStance", position, Vector3.Zero, false));
    }
}
