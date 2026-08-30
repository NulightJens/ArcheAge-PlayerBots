#if PLAYERBOTS_AAEMU_3_0
using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;

namespace AAEmu.Game.Core.Packets.G2C;

/// <summary>
/// Tells a 3.0 client whether another character permits remote equipment inspection.
/// The matching client request carries only the active character's open flag; the
/// server broadcast adds the subject object's compact id.
/// </summary>
public sealed class SCUnitOpenEquipInfoPacket : GamePacket
{
    private readonly uint _objectId;
    private readonly bool _open;

    public SCUnitOpenEquipInfoPacket(uint objectId, bool open)
        : base(SCOffsets.SCUnitOpenEquipInfoPacket, 5)
    {
        _objectId = objectId;
        _open = open;
    }

    public override PacketStream Write(PacketStream stream)
    {
        stream.WriteBc(_objectId);
        stream.Write(_open);
        return stream;
    }
}
#endif
