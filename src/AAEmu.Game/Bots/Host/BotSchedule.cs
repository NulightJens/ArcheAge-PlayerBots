namespace AAEmu.Game.Bots.Host;

public sealed class BotSchedule
{
    public DateTime Now { get; set; }
    public DateTime NextBrainAt { get; set; }
    public DateTime LastGroundCheckAt { get; set; } = DateTime.MinValue;
    public bool LastActive { get; set; }
}
