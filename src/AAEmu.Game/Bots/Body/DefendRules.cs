using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Bots.Body;

public static class DefendRules
{
    public static bool IsBeingAttackedByPlayer(Character bot, out Unit attacker)
    {
        attacker = null;
        if (bot == null)
            return false;

        foreach (var kvp in bot.AggroTable)
        {
            var unit = kvp.Value.Owner;
            if (unit is Character player && player != bot && !player.IsDead &&
                (bot.IsActivelyHostile(player) || player.IsActivelyHostile(bot)))
            {
                attacker = player;
                return true;
            }
        }

        return false;
    }
}
