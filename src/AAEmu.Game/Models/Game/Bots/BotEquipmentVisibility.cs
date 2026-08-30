using AAEmu.Game.Models.Game.Char;

#if PLAYERBOTS_AAEMU_3_0
using AAEmu.Game.Core.Packets.G2C;
#endif

namespace AAEmu.Game.Models.Game.Bots;

/// <summary>
/// Enforces the PlayerBots equipment-inspection policy.
/// Connectionless bots cannot toggle a client preference, so their gear is always public.
/// </summary>
public static class BotEquipmentVisibility
{
    public const bool IsPublic = true;

    public static void PublishPublic(Character bot)
    {
#if PLAYERBOTS_AAEMU_3_0
        if (bot == null)
            return;

        bot.BroadcastPacket(new SCUnitOpenEquipInfoPacket(bot.ObjId, IsPublic), false);
#endif
    }

    public static void SendPublicTo(Character bot, Character observer)
    {
#if PLAYERBOTS_AAEMU_3_0
        if (bot == null || observer == null)
            return;

        observer.SendPacket(new SCUnitOpenEquipInfoPacket(bot.ObjId, IsPublic));
#endif
    }
}
