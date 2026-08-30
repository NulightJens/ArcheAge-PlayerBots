namespace AAEmu.Game.Models.Game.Char;

public partial class Character
{
    public bool IsBot { get; internal set; }

#if PLAYERBOTS_AAEMU_3_0
    public ConcurrentDictionary<uint, AAEmu.Game.Models.Game.NPChar.Aggro> AggroTable { get; } = new();
    public bool DiedInPvp { get; set; }
    public bool DiedInPvpWarZone { get; set; }

    public void ClearAllAggro()
    {
        foreach (var aggro in AggroTable.Values.ToArray())
        {
            if (aggro.Owner is AAEmu.Game.Models.Game.NPChar.Npc npc)
                npc.ClearAggroOfUnit(this);
        }

        AggroTable.Clear();
        IsInAggroListOf.Clear();
        IsInBattle = false;
    }

    public void CheckWantedThreshold()
    {
        // The 3.0 host has no wanted-threshold login hook.
    }

    public void RestoreSavedHpMp()
    {
        Hp = Math.Clamp(Hp, 0, MaxHp);
        Mp = Math.Clamp(Mp, 0, MaxMp);
    }
#endif
}
