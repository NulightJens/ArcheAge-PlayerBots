namespace AAEmu.Game.Models.Game.Char;

public partial class Character
{
    private int? _savedHpForBotLoad;
    private int? _savedMpForBotLoad;

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

    public void CaptureSavedHpMpForBotLoad()
    {
        _savedHpForBotLoad = Hp;
        _savedMpForBotLoad = Mp;
    }

    public void RestoreSavedHpMp()
    {
        Hp = Math.Clamp(_savedHpForBotLoad ?? Hp, 0, MaxHp);
        Mp = Math.Clamp(_savedMpForBotLoad ?? Mp, 0, MaxMp);
        _savedHpForBotLoad = null;
        _savedMpForBotLoad = null;
    }
#endif
}
