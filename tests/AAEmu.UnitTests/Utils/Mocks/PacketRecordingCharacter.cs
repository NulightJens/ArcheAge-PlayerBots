using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Models.Game.Char;

namespace AAEmu.UnitTests.Utils.Mocks;

public class PacketRecordingCharacter : Character
{
    public List<GamePacket> Sent { get; } = [];

    public PacketRecordingCharacter() : base(null)
    {
    }

    public override void BroadcastPacket(GamePacket packet, bool self)
    {
        Sent.Add(packet);
    }
}
