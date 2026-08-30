using System.Collections.Generic;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Models.Game.Bots
{
    /// <summary>
    /// Defines a bot archetype (role) – gear priorities, skill trees, and learning order.
    /// </summary>
    public class BotArchetypeDefinition
    {
        /// <summary>Unique name of the archetype (e.g., "Abolisher").</summary>
        public string Name { get; set; }

        /// <summary>Data-driven combat rotation id.</summary>
        public string RotationId { get; set; }

        public AbilityType StartingAbility { get; set; }   // The first ability at spawn

        /// <summary>The three skill trees required for this archetype.</summary>
        public List<AbilityType> RequiredAbilities { get; set; } = new();

        /// <summary>The primary attribute to prioritize (e.g., Stamina).</summary>
        public UnitAttribute PrimaryStat { get; set; }

        /// <summary>Armor type ID: 1=Cloth, 2=Leather, 3=Plate.</summary>
        public int ArmorType { get; set; }

        /// <summary>List of weapon category names in priority order (e.g., "Sword", "Axe", "Mace").</summary>
        public List<string> WeaponPriority { get; set; } = new();

        /// <summary>Whether the archetype uses a shield in the offhand.</summary>
        public bool UsesShield { get; set; }

        public int Weight { get; set; } = 1;               // Higher = more likely when multiple match

        public int LevelToUnlockSecond { get; set; } = 5;

        public int LevelToUnlockThird { get; set; } = 10;

        /// <summary>Skill IDs to learn in order (by level).</summary>
        public List<uint> SkillLearnOrder { get; set; } = new();

        /// <summary>List of passive buff IDs to learn (usually tied to skill trees).</summary>
        public List<uint> PassiveBuffIds { get; set; } = new();
    }
}
