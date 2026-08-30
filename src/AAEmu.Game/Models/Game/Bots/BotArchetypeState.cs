using System;

namespace AAEmu.Game.Models.Game.Bots
{
    public class BotArchetypeState
    {
        public string ArchetypeName { get; set; }
        public string PlannedArchetype { get; set; }   // Name of the archetype we're working towards
        public byte LastKnownLevel { get; set; }
        public DateTime LastGearCheck { get; set; } = DateTime.MinValue;
        public bool IsInitialized { get; set; }
    }
}
