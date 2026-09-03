using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using AAEmu.Commons.IO;
using AAEmu.Commons.Utils;
using AAEmu.Commons.Utils.DB;
using AAEmu.Game.Bots.Population.Identity;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.Items.Actions;
using AAEmu.Game.Models.Game.Items.Containers;
using AAEmu.Game.Models.Game.Items.Templates;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.Units;
using Newtonsoft.Json;
using NLog;

namespace AAEmu.Game.Core.Managers.Bots
{
    public class BotArchetypeManager : Singleton<BotArchetypeManager>, IBotArchetypeManager,
        IBotArchetypeCreationPlanStore, ILoadable
    {
        private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();

        private readonly ConcurrentDictionary<uint, BotArchetypeState> _archetypeStates = new();
        private Dictionary<string, BotArchetypeDefinition> _archetypeDefinitions = new();
        private readonly IBotArchetypeStore _store;
        private readonly ISkillManager _skillManager;
        private readonly IExperienceManager _experienceManager;

        private static string ConfigPath => Path.Combine(FileManager.AppPath, "Data", "BotArchetypes.json");
        private const string TableName = "bot_archetype_plans";

        internal BotArchetypeManager() : this(new MySqlBotArchetypeStore(), null, null)
        {
        }

        public BotArchetypeManager(ISkillManager skillManager, IExperienceManager experienceManager)
            : this(new MySqlBotArchetypeStore(), skillManager, experienceManager)
        {
        }

        internal BotArchetypeManager(IBotArchetypeStore store)
            : this(store, null, null)
        {
        }

        private BotArchetypeManager(
            IBotArchetypeStore store,
            ISkillManager skillManager,
            IExperienceManager experienceManager)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _skillManager = skillManager;
            _experienceManager = experienceManager;
        }

        public void Load()
        {
            Load(ConfigPath);
        }

        internal bool Load(string path)
        {
            if (!File.Exists(path))
            {
                Logger.Info($"BotArchetypes.json not found at {path}; using default definitions.");
                return LoadDefinitions(JsonConvert.SerializeObject(DefaultDefinitions()));
            }

            try
            {
                return LoadDefinitions(File.ReadAllText(path));
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Failed to read BotArchetypes.json at {path}.");
                return false;
            }
        }

        public virtual bool Reload()
        {
            return Load(ConfigPath);
        }

        internal bool LoadDefinitions(string json)
        {
            List<BotArchetypeDefinition> definitions;
            try
            {
                definitions = JsonConvert.DeserializeObject<List<BotArchetypeDefinition>>(json);
            }
            catch (Exception e)
            {
                Logger.Warn(e, "Failed to parse BotArchetypes.json; keeping existing definitions.");
                return false;
            }

            if (definitions == null)
            {
                Logger.Warn("BotArchetypes.json did not contain a definition list; keeping existing definitions.");
                return false;
            }

            var next = new Dictionary<string, BotArchetypeDefinition>();
            for (var index = 0; index < definitions.Count; index++)
            {
                var def = definitions[index];
                string reason;
                if (def == null)
                    reason = "definition is null";
                else if (string.IsNullOrWhiteSpace(def.Name))
                    reason = "Name is required";
                else if (def.RequiredAbilities?.Count != 3)
                    reason = "RequiredAbilities must contain exactly 3 entries";
                else if (def.RequiredAbilities.Distinct().Count() != 3)
                    reason = "RequiredAbilities must contain 3 distinct entries";
                else if (def.SkillLearnOrder == null)
                    reason = "SkillLearnOrder is required";
                else if (def.SkillLearnOrder.Any(id => id == 0))
                    reason = "SkillLearnOrder cannot contain zero";
                else if (def.WeaponPriority == null)
                    reason = "WeaponPriority is required";
                else
                    reason = null;

                if (reason != null)
                {
                    Logger.Warn($"Skipping invalid bot archetype definition name={def?.Name ?? "<null>"} index={index} reason={reason}.");
                    continue;
                }

                next[def.Name] = def;
            }

            Interlocked.Exchange(ref _archetypeDefinitions, next);
            Logger.Trace($"BOT ev=archetype_definitions_loaded count={next.Count}");
            return true;
        }

        internal Dictionary<string, BotArchetypeDefinition> GetDefinitionsSnapshot()
        {
            return Volatile.Read(ref _archetypeDefinitions);
        }

        public void EnsureSchema()
        {
            using var connection = MySQL.CreateConnection();
            using var command = connection.CreateCommand();

            command.CommandText = $"SHOW TABLES LIKE '{TableName}'";
            var tableExists = command.ExecuteScalar() != null;

            if (!tableExists)
            {
                command.CommandText = $@"
                    CREATE TABLE `{TableName}` (
                        `character_id` INT UNSIGNED NOT NULL PRIMARY KEY,
                        `archetype_name` VARCHAR(64) NOT NULL,
                        `is_final` BOOLEAN NOT NULL DEFAULT FALSE,
                        `created_at` DATETIME DEFAULT CURRENT_TIMESTAMP,
                        `updated_at` DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
                    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
                command.ExecuteNonQuery();
                Logger.Trace($"BOT ev=archetype_schema_created table={TableName}");
            }
            else
            {
                command.CommandText = $"SHOW COLUMNS FROM `{TableName}` LIKE 'archetype_name'";
                var colExists = command.ExecuteScalar() != null;

                if (!colExists)
                {
                    command.CommandText = $@"
                        ALTER TABLE `{TableName}`
                        CHANGE COLUMN `planned_archetype` `archetype_name` VARCHAR(64) NOT NULL,
                        ADD COLUMN `is_final` BOOLEAN NOT NULL DEFAULT FALSE,
                        ADD COLUMN `updated_at` DATETIME DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP;";
                    command.ExecuteNonQuery();
                    Logger.Trace($"BOT ev=archetype_schema_migrated table={TableName}");
                }
            }
        }

        // ---- Weapon Category Mapping ----
        private static string GetWeaponCategory(ItemTemplate template)
        {
            if (template is WeaponTemplate weapon)
                return WeaponCategoryName(weapon.CategoryId);
            return null;
        }

        internal static string WeaponCategoryName(int categoryId)
        {
            return categoryId switch
            {
                70 => "Sword",
                69 => "Dagger",
                73 => "Axe",
                77 => "Bow",
                131 => "Staff",
                128 => "Nodachi",
                127 => "Greatsword",
                129 => "Greataxe",
                130 => "Greatclub",
                132 => "Longspear",
                74 => "Club",
                75 => "Scepter",
                72 => "Katana",
                79 => "Shield",
                80 => "Lute",
                81 => "Flute",
                71 => "Shortspear",
                _ => null
            };
        }

        internal static List<BotArchetypeDefinition> DefaultDefinitions()
        {
            var definitions = new List<BotArchetypeDefinition>
            {
                // Abolisher: Battlerage + Defense + Auramancy (1 + 3 + 4)
                new()
                {
                    Name = "Abolisher",
                    RotationId = "abolisher.tank",
                    StartingAbility = (AbilityType)1,
                    RequiredAbilities = new List<AbilityType> { (AbilityType)1, (AbilityType)3, (AbilityType)4 },
                    PrimaryStat = UnitAttribute.Str,
                    ArmorType = 3,
                    WeaponPriority = new List<string> { "Sword" },
                    UsesShield = true,
                    Weight = 1,
                    LevelToUnlockSecond = 5,
                    LevelToUnlockThird = 10,
                    SkillLearnOrder = BotSkillIds.Abolisher.SkillLearnOrder.ToList(),
                    PassiveBuffIds = new List<uint> { 30, 11, 13, 90, 249, 31, 250, 97 }
                },

                // Darkrunner: Battlerage + Auramancy + Shadowplay (1 + 4 + 8)
                new()
                {
                    Name = "Darkrunner",
                    RotationId = "darkrunner.melee",
                    StartingAbility = (AbilityType)1,
                    RequiredAbilities = new List<AbilityType> { (AbilityType)1, (AbilityType)4, (AbilityType)8 },
                    PrimaryStat = UnitAttribute.Str,
                    ArmorType = 2,
                    WeaponPriority = new List<string> { "Nodachi", "Greatsword", "Sword", "Axe", "Katana", "Greataxe" },
                    UsesShield = false,
                    Weight = 1,
                    LevelToUnlockSecond = 5,
                    LevelToUnlockThird = 10,
                    SkillLearnOrder = BotSkillIds.Darkrunner.SkillLearnOrder.ToList(),
                    PassiveBuffIds = new List<uint> { 3, 20, 13, 29, 9, 6, 90 }
                },

                // Reaper: Sorcery + Occultism + Shadowplay (7 + 5 + 8)
                new()
                {
                    Name = "Reaper",
                    RotationId = "reaper.caster",
                    StartingAbility = (AbilityType)7,
                    RequiredAbilities = new List<AbilityType> { (AbilityType)7, (AbilityType)5, (AbilityType)8 },
                    PrimaryStat = UnitAttribute.Int,
                    ArmorType = 1,
                    WeaponPriority = new List<string> { "Staff" },
                    UsesShield = false,
                    Weight = 1,
                    LevelToUnlockSecond = 5,
                    LevelToUnlockThird = 10,
                    SkillLearnOrder = BotSkillIds.Reaper.SkillLearnOrder.ToList(),
                    PassiveBuffIds = new List<uint> { 15, 38, 17, 39, 258, 14, 37, 99, 9 }
                },

                // Daggerspell: Sorcery + Shadowplay + Witchcraft (7 + 2 + 8)
                new()
                {
                    Name = "Daggerspell",
                    RotationId = "daggerspell.caster",
                    StartingAbility = (AbilityType)7,
                    RequiredAbilities = new List<AbilityType> { (AbilityType)7, (AbilityType)2, (AbilityType)8 },
                    PrimaryStat = UnitAttribute.Int,
                    ArmorType = 1,
                    WeaponPriority = new List<string> { "Staff" },
                    UsesShield = false,
                    Weight = 1,
                    LevelToUnlockSecond = 5,
                    LevelToUnlockThird = 10,
                    SkillLearnOrder = BotSkillIds.Daggerspell.SkillLearnOrder.ToList(),
                    PassiveBuffIds = new List<uint> { 4, 28, 38, 41, 247 }
                },

                // Templar: Vitalism + Defense + Auramancy (10 + 3 + 4)
                new()
                {
                    Name = "Templar",
                    RotationId = "templar.support",
                    StartingAbility = (AbilityType)10,
                    RequiredAbilities = new List<AbilityType> { (AbilityType)10, (AbilityType)3, (AbilityType)4 },
                    PrimaryStat = UnitAttribute.Spi,
                    ArmorType = 1,
                    WeaponPriority = new List<string> { "Club" },
                    UsesShield = true,
                    Weight = 1,
                    LevelToUnlockSecond = 5,
                    LevelToUnlockThird = 10,
                    SkillLearnOrder = BotSkillIds.Templar.SkillLearnOrder.ToList(),
                    PassiveBuffIds = new List<uint> { 22, 30, 19, 24, 11, 13, 263, 109, 249, 101 }
                },

                // Cleric: Vitalism + Auramancy + Songcraft (10 + 4 + 9)
                new()
                {
                    Name = "Cleric",
                    RotationId = "cleric.support",
                    StartingAbility = (AbilityType)10,
                    RequiredAbilities = new List<AbilityType> { (AbilityType)10, (AbilityType)4, (AbilityType)9 },
                    PrimaryStat = UnitAttribute.Spi,
                    ArmorType = 1,
                    WeaponPriority = new List<string> { "Club" },
                    UsesShield = true,
                    Weight = 1,
                    LevelToUnlockSecond = 5,
                    LevelToUnlockThird = 10,
                    SkillLearnOrder = BotSkillIds.Cleric.SkillLearnOrder.ToList(),
                    PassiveBuffIds = new List<uint>()
                },

                // Primeval: Archery + Auramancy + Shadowplay (6 + 4 + 8)
                new()
                {
                    Name = "Primeval",
                    RotationId = "primeval.archer",
                    StartingAbility = (AbilityType)6,
                    RequiredAbilities = new List<AbilityType> { (AbilityType)6, (AbilityType)4, (AbilityType)8 },
                    PrimaryStat = UnitAttribute.Dex,
                    ArmorType = 2,
                    WeaponPriority = new List<string> { "Bow", "Dagger", "Sword", "Shortspear" },
                    UsesShield = false,
                    Weight = 1,
                    LevelToUnlockSecond = 5,
                    LevelToUnlockThird = 10,
					SkillLearnOrder = BotSkillIds.Primeval.SkillLearnOrder.ToList(),
					PassiveBuffIds = new List<uint> { 7, 255, 20, 35, 1, 34, 2, 256 }
                }
            };

            return definitions;
        }

        /// <summary>
        /// Deterministically assigns all three ability trees for an archetype, persists the final plan,
        /// synchronizes the requested level, then rebuilds the bot's learned skills and equipment.
        /// </summary>
        public bool SetArchetype(Character bot, string archetypeName, byte targetLevel, out string resolvedName)
        {
            resolvedName = null;
            if (bot == null || string.IsNullOrWhiteSpace(archetypeName))
                return false;

            var definition = GetDefinitionsSnapshot().Values.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, archetypeName, StringComparison.OrdinalIgnoreCase));
            if (definition?.RequiredAbilities == null || definition.RequiredAbilities.Count != 3)
                return false;

            resolvedName = definition.Name;
            bot.Ability1 = definition.RequiredAbilities[0];
            bot.Ability2 = definition.RequiredAbilities[1];
            bot.Ability3 = definition.RequiredAbilities[2];

#if PLAYERBOTS_AAEMU_3_0
            var maxLevel = ExperienceManager.MaxPlayerLevel;
#else
            var experienceManager = _experienceManager ?? ExperienceManager.Instance;
            var maxLevel = experienceManager.MaxPlayerLevel;
#endif
            if (targetLevel >= 2 && targetLevel <= maxLevel)
            {
#if PLAYERBOTS_AAEMU_3_0
                var characterExp = ExperienceManager.Instance.GetExpNeededToGivenLevel(bot.Experience, targetLevel);
#else
                var characterExp = experienceManager.GetExpNeededToGivenLevel(bot.Experience, targetLevel);
#endif
                if (characterExp > 0)
                    bot.AddExp(characterExp, false);

                foreach (var ability in ActiveAbilities(bot))
                {
                    if (IsEmptyAbility(ability) || !bot.Abilities.Abilities.TryGetValue(ability, out var abilityData))
                        continue;

#if PLAYERBOTS_AAEMU_3_0
                    var abilityExp = ExperienceManager.Instance.GetExpNeededToGivenLevel(abilityData.Exp, targetLevel);
#else
                    var abilityExp = experienceManager.GetExpNeededToGivenLevel(abilityData.Exp, targetLevel);
#endif
                    if (abilityExp > 0)
                        bot.Abilities.AddExp(ability, abilityExp);
                }
            }

            var state = _archetypeStates.GetOrAdd(bot.Id, _ => new BotArchetypeState());
            state.ArchetypeName = definition.Name;
            state.PlannedArchetype = null;
            state.IsInitialized = true;
            state.LastKnownLevel = bot.Level;
            SaveArchetype(bot.Id, definition.Name, true);

            ReapplyArchetype(bot, state);
            state.LastGearCheck = DateTime.UtcNow;
            bot.SaveDirectlyToDatabase();
            Logger.Info(
                $"BOT id={bot.Id} ev=archetype_set name={definition.Name} abilities={bot.Ability1}/{bot.Ability2}/{bot.Ability3} level={bot.Level}");
            return true;
        }

        public IReadOnlyList<string> GetArchetypeNames()
        {
            return GetDefinitionsSnapshot().Values
                .Select(definition => definition.Name)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public bool TryResolveCreationPlan(string archetypeName, byte level, out BotArchetypeCreationPlan plan)
        {
            plan = null;
            if (string.IsNullOrWhiteSpace(archetypeName) || level == 0)
                return false;

            var definition = GetDefinitionsSnapshot().Values.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, archetypeName, StringComparison.OrdinalIgnoreCase));
            if (definition?.RequiredAbilities == null || definition.RequiredAbilities.Count != 3 ||
                definition.RequiredAbilities.Any(IsEmptyAbility) ||
                definition.RequiredAbilities.Distinct().Count() != 3)
                return false;

            var ability1 = definition.StartingAbility;
            if (IsEmptyAbility(ability1) || !definition.RequiredAbilities.Contains(ability1))
                return false;

            var ability2 = level >= definition.LevelToUnlockSecond
                ? definition.RequiredAbilities[1]
                : AbilityType.None;
            var ability3 = level >= definition.LevelToUnlockThird
                ? definition.RequiredAbilities[2]
                : AbilityType.None;
            plan = new BotArchetypeCreationPlan(
                definition.Name,
                ability1,
                ability2,
                ability3,
                !IsEmptyAbility(ability2) && !IsEmptyAbility(ability3));
            return true;
        }

        public void RegisterCreationPlan(uint characterId, BotArchetypeCreationPlan plan)
        {
            if (characterId == 0)
                throw new ArgumentOutOfRangeException(nameof(characterId));
            ArgumentNullException.ThrowIfNull(plan);

            var definition = GetDefinitionsSnapshot().Values.FirstOrDefault(candidate =>
                string.Equals(candidate.Name, plan.Name, StringComparison.OrdinalIgnoreCase));
            if (definition == null)
                throw new InvalidOperationException($"Unknown bot archetype '{plan.Name}'.");

            var state = _archetypeStates.GetOrAdd(characterId, _ => new BotArchetypeState());
            state.ArchetypeName = plan.IsFinal ? definition.Name : null;
            state.PlannedArchetype = plan.IsFinal ? null : definition.Name;
            state.IsInitialized = true;
            SaveArchetype(characterId, definition.Name, plan.IsFinal);
        }

        public void RollbackCreationPlan(uint characterId)
        {
            if (characterId == 0)
                return;

            DeleteArchetype(characterId);
            RemoveState(characterId);
        }

        // ---- Database helpers ----
        private (string archetypeName, bool isFinal) GetArchetypeFromDb(uint characterId)
        {
            return _store.Get(characterId);
        }

        private void SaveArchetype(uint characterId, string archetypeName, bool isFinal)
        {
            _store.Save(characterId, archetypeName, isFinal);
        }

        public void DeleteArchetype(uint characterId)
        {
            _store.Delete(characterId);
        }

        // ---- Public API ----

        private static bool IsEmptyAbility(AbilityType ability) => ability == AbilityType.General || ability == AbilityType.None;

        private static AbilityType[] ActiveAbilities(Character bot)
        {
            return [bot.Ability1, bot.Ability2, bot.Ability3];
        }

        private static bool TryUnlockAbilities(Character bot, BotArchetypeState state, BotArchetypeDefinition definition)
        {
            if (string.IsNullOrEmpty(state.PlannedArchetype))
                return false;

            var updated = false;
            if (bot.Level >= definition.LevelToUnlockSecond && IsEmptyAbility(bot.Ability2))
            {
                bot.Ability2 = definition.RequiredAbilities[1];
                updated = true;
                Logger.Trace($"BOT id={bot.Id} ev=ability_unlocked slot=2 ability={bot.Ability2} archetype={definition.Name}");
            }
            if (bot.Level >= definition.LevelToUnlockThird && IsEmptyAbility(bot.Ability3))
            {
                bot.Ability3 = definition.RequiredAbilities[2];
                updated = true;
                Logger.Trace($"BOT id={bot.Id} ev=ability_unlocked slot=3 ability={bot.Ability3} archetype={definition.Name}");
            }
            return updated;
        }

        /// <summary>
        /// Sets ability experience for the bot's active abilities to match its current level.
        /// </summary>
        private void SynchronizeAbilityExp(Character bot)
        {
            if (bot == null) return;

            var expForLevel = (_experienceManager ?? ExperienceManager.Instance).GetExpForLevel(bot.Level);
            var activeAbilities = ActiveAbilities(bot);
            bool changed = false;

            foreach (var ability in activeAbilities)
            {
                if (ability == AbilityType.General || ability == AbilityType.None)
                    continue;

                if (bot.Abilities.Abilities.TryGetValue(ability, out var abilityData))
                {
                    if (abilityData.Exp < expForLevel)
                    {
                        abilityData.Exp = (int)expForLevel;
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                using var connection = MySQL.CreateConnection();
                bot.Abilities.Save(connection, null);
                Logger.Debug($"Synchronized ability exp for bot '{bot.Name}' to level {bot.Level} (exp: {expForLevel})");
            }
        }

        private void ReapplyArchetype(Character bot, BotArchetypeState state)
        {
            SynchronizeAbilityExp(bot);
            // Planned low-level classes retain the native starter skills granted
            // at character creation. Rebuilding from an archetype list can replace
            // a visible starter with a hidden/internal chain variant.
            if (!string.IsNullOrEmpty(state.ArchetypeName))
                ClearArchetypeSkills(bot);
            LearnSkills(bot, state);
            EquipBestGear(bot, state);
        }

        public virtual void OnBotSpawn(Character bot)
        {
            if (bot == null) return;

            var state = _archetypeStates.GetOrAdd(bot.Id, _ => new BotArchetypeState());

            // 1. Check for saved archetype in DB
            var (savedName, savedIsFinal) = GetArchetypeFromDb(bot.Id);
            if (!string.IsNullOrEmpty(savedName))
            {
                if (savedIsFinal)
                {
                    state.ArchetypeName = savedName;
                    state.PlannedArchetype = null;
                }
                else
                {
                    state.PlannedArchetype = savedName;
                    state.ArchetypeName = null;
                }
                state.IsInitialized = true;
            }
            else
            {
                // No saved archetype – assign one
                if (!state.IsInitialized)
                {
                    var abilities = new List<AbilityType> { bot.Ability1, bot.Ability2, bot.Ability3 };
                    var nonZeroAbilities = abilities.Where(a => !IsEmptyAbility(a)).ToList();

                    if (nonZeroAbilities.Count == 3)
                    {
                        AssignArchetype(bot, state);
                        if (!string.IsNullOrEmpty(state.ArchetypeName))
                            SaveArchetype(bot.Id, state.ArchetypeName, true);
                    }
                    else if (nonZeroAbilities.Count == 1)
                    {
                        var firstAbility = nonZeroAbilities[0];
                        var matches = Volatile.Read(ref _archetypeDefinitions).Values
                            .Where(def => def.StartingAbility == firstAbility)
                            .ToList();

                        if (matches.Count > 0)
                        {
                            var totalWeight = matches.Sum(m => Math.Max(0, m.Weight));
                            var selected = PickWeighted(matches, Random.Shared.Next(Math.Max(1, totalWeight)));

                            state.PlannedArchetype = selected.Name;
                            SaveArchetype(bot.Id, selected.Name, false);
                            Logger.Trace($"BOT id={bot.Id} ev=archetype_planned name={selected.Name} ability={firstAbility}");
                        }
                        else
                        {
                            Logger.Warn($"Bot '{bot.Name}' has no matching archetype for starting ability {firstAbility}");
                        }
                    }
                    else
                    {
                        Logger.Warn($"Bot '{bot.Name}' has {nonZeroAbilities.Count} abilities at spawn (expected 1 or 3).");
                    }
                    state.IsInitialized = true;
                }
            }

            // Ensure abilities are set according to archetype plan
            var effectiveDef = GetEffectiveDefinition(state);
            if (effectiveDef != null)
            {
                // Set starting ability if needed
                if (bot.Ability1 == AbilityType.General || bot.Ability1 == AbilityType.None)
                    bot.Ability1 = effectiveDef.StartingAbility;

                TryUnlockAbilities(bot, state, effectiveDef);

                TryFinalizeArchetype(bot, state, string.IsNullOrEmpty(savedName));

                ReapplyArchetype(bot, state);
            }

            state.LastKnownLevel = bot.Level;
            state.LastGearCheck = DateTime.UtcNow;
            bot.SaveDirectlyToDatabase();
        }

        public void OnLevelUp(Character bot)
        {
            if (bot == null) return;
            if (!_archetypeStates.TryGetValue(bot.Id, out var state) || !state.IsInitialized)
                return;

            state.LastKnownLevel = bot.Level;

            var def = GetEffectiveDefinition(state);
            if (def == null) return;

            // Unlock abilities if planned archetype
            if (!string.IsNullOrEmpty(state.PlannedArchetype))
            {
                bool updated = TryUnlockAbilities(bot, state, def);

                if (updated)
                {
                    bot.SaveDirectlyToDatabase();
                    TryFinalizeArchetype(bot, state, persist: true);
                }
            }
            else if (string.IsNullOrEmpty(state.ArchetypeName))
            {
                // No plan and no archetype – try to assign if all abilities set
                if (!IsEmptyAbility(bot.Ability1) && !IsEmptyAbility(bot.Ability2) && !IsEmptyAbility(bot.Ability3))
                {
                    TryFinalizeArchetype(bot, state, persist: true);
                }
            }

            ReapplyArchetype(bot, state);
            bot.SaveDirectlyToDatabase();
        }

        public virtual BotArchetypeState GetState(Character bot)
        {
            if (bot == null) return null;
            _archetypeStates.TryGetValue(bot.Id, out var state);
            return state;
        }

        public virtual void RemoveState(uint characterId)
        {
            _archetypeStates.TryRemove(characterId, out _);
        }

        public void CheckForUpdates(Character bot)
        {
            if (bot == null) return;
            if (!_archetypeStates.TryGetValue(bot.Id, out var state) || !state.IsInitialized)
                return;

            if (bot.Level != state.LastKnownLevel)
                OnLevelUp(bot);

            var def = GetEffectiveDefinition(state);
            if (def != null && (DateTime.UtcNow - state.LastGearCheck).TotalSeconds > 30)
            {
                EquipBestGear(bot, state);
                state.LastGearCheck = DateTime.UtcNow;
            }
        }

        public BotArchetypeDefinition GetEffectiveDefinition(BotArchetypeState state)
        {
            var definitions = Volatile.Read(ref _archetypeDefinitions);
            if (!string.IsNullOrEmpty(state.ArchetypeName) && definitions.TryGetValue(state.ArchetypeName, out var def))
                return def;
            if (!string.IsNullOrEmpty(state.PlannedArchetype) && definitions.TryGetValue(state.PlannedArchetype, out var plannedDef))
                return plannedDef;
            return null;
        }

        /// <summary>
        /// Force re-evaluate gear and skills without changing archetype assignment.
        /// Clears all current skills and relearns based on current abilities.
        /// </summary>
        public void ForceReevaluate(Character bot)
        {
            if (bot == null) return;
            var state = _archetypeStates.GetOrAdd(bot.Id, _ => new BotArchetypeState());
            var effectiveDef = GetEffectiveDefinition(state);
            if (effectiveDef != null)
            {
                ReapplyArchetype(bot, state);
            }
            state.LastGearCheck = DateTime.UtcNow;
        }

        /// <summary>
        /// Force a new archetype assignment (random), clear skills, re-learn and re-equip.
        /// </summary>
        public void RerollArchetype(Character bot)
        {
            if (bot == null) return;

            DeleteArchetype(bot.Id);

            var state = _archetypeStates.GetOrAdd(bot.Id, _ => new BotArchetypeState());
            state.ArchetypeName = null;
            state.PlannedArchetype = null;
            state.IsInitialized = false;

            ClearArchetypeSkills(bot);
            // Reset abilities to General (keep only Ability1? Actually we want a fresh start)
            bot.Ability2 = AbilityType.General;
            bot.Ability3 = AbilityType.General;
            bot.SaveDirectlyToDatabase();

            OnBotSpawn(bot);
            BotCombatManager.Instance.ResetCombat(bot);
            Logger.Trace($"BOT id={bot.Id} ev=archetype_rerolled");
        }

        /// <summary>
        /// Completely clear all skills and passives from the bot.
        /// </summary>
        public void ClearArchetypeSkills(Character bot)
        {
            if (bot == null) return;

            // Remove skills from all three ability trees
            var abilities = ActiveAbilities(bot);
            foreach (var ability in abilities)
            {
                if (!IsEmptyAbility(ability))
                    bot.Skills.Reset(ability);
            }

            // Remove passive buffs
            foreach (var pb in bot.Skills.PassiveBuffs.Values.ToList())
            {
                pb.Remove(bot);
                bot.Skills.PassiveBuffs.Remove(pb.Id);
            }

            Logger.Debug($"Cleared all skills and passives for bot '{bot.Name}'");
        }

        internal static BotArchetypeDefinition PickWeighted(IReadOnlyList<BotArchetypeDefinition> matches, int roll)
        {
            return PickWeightedCore(matches, roll);
        }

        private static BotArchetypeDefinition PickWeightedCore(IReadOnlyList<BotArchetypeDefinition> matches, int roll)
        {
            if (matches.Count == 0)
                return null;

            var totalWeight = matches.Sum(match => Math.Max(0, match.Weight));
            if (totalWeight <= 0)
                return matches[0];

            var remaining = Math.Clamp(roll, 0, totalWeight - 1);
            foreach (var match in matches)
            {
                remaining -= Math.Max(0, match.Weight);
                if (remaining < 0)
                    return match;
            }

            return matches[^1];
        }

        internal static string MatchArchetype(IEnumerable<BotArchetypeDefinition> definitions, ISet<AbilityType> abilities)
        {
            var matches = definitions
                .Where(def => def.RequiredAbilities?.Count == 3 && abilities.Count == 3 &&
                              new HashSet<AbilityType>(def.RequiredAbilities).SetEquals(abilities))
                .OrderBy(def => def.Name, StringComparer.Ordinal)
                .ToList();
            if (matches.Count == 0)
                return null;
            if (matches.Count > 1)
                Logger.Warn($"Multiple bot archetypes match abilities: {string.Join(", ", matches.Select(def => def.Name))}; using {matches[0].Name}.");
            return matches[0].Name;
        }

        private bool TryFinalizeArchetype(Character bot, BotArchetypeState state, bool persist)
        {
            if (IsEmptyAbility(bot.Ability1) || IsEmptyAbility(bot.Ability2) || IsEmptyAbility(bot.Ability3))
                return false;

            AssignArchetype(bot, state);
            if (string.IsNullOrEmpty(state.ArchetypeName))
                return false;

            if (persist)
            {
                SaveArchetype(bot.Id, state.ArchetypeName, true);
                state.PlannedArchetype = null;
            }
            return true;
        }

        private void AssignArchetype(Character bot, BotArchetypeState state)
        {
            var abilities = new HashSet<AbilityType> { bot.Ability1, bot.Ability2, bot.Ability3 };
            abilities.RemoveWhere(IsEmptyAbility);
            state.ArchetypeName = MatchArchetype(Volatile.Read(ref _archetypeDefinitions).Values, abilities);
            if (!string.IsNullOrEmpty(state.ArchetypeName))
            {
                Logger.Trace($"BOT id={bot.Id} ev=archetype_finalized name={state.ArchetypeName}");
                return;
            }

            Logger.Warn($"Bot '{bot.Name}' has no matching archetype even with all abilities set (abilities: {string.Join(", ", abilities)})");
        }

        // ---- Skills ----
        public void LearnSkills(Character bot, BotArchetypeState state)
        {
            var def = GetEffectiveDefinition(state);
            if (def == null) return;

            // Active skills
            foreach (uint skillId in def.SkillLearnOrder)
            {
                var skillTemplate = (_skillManager ?? SkillManager.Instance).GetSkillTemplate(skillId);
                if (skillTemplate == null)
                {
                    Logger.Warn($"Skill ID {skillId} does not exist, skipping.");
                    continue;
                }
                if (!IsPlayerLearnableActiveSkill(skillTemplate))
                {
                    Logger.Debug($"Skill ID {skillId} is hidden or not player-learnable, skipping.");
                    continue;
                }

                var abilityType = (AbilityType)skillTemplate.AbilityId;
                if (!IsAbilityActive(bot, abilityType))
                    continue;

                int abilityLevel = bot.GetAbLevel(abilityType);
                if (abilityLevel < skillTemplate.AbilityLevel)
                    continue;

                if (bot.Skills.Skills.ContainsKey(skillId))
                    continue;

                bot.Skills.AddSkill(skillId);
                Logger.Debug($"Bot '{bot.Name}' learned skill {skillId}");
            }

            // Passive buffs
            foreach (uint buffId in def.PassiveBuffIds)
            {
                var buffTemplate = (_skillManager ?? SkillManager.Instance).GetPassiveBuffTemplate(buffId);
                if (buffTemplate == null)
                {
                    Logger.Warn($"Passive buff ID {buffId} does not exist, skipping.");
                    continue;
                }

                var abilityType = (AbilityType)buffTemplate.AbilityId;
                if (!IsAbilityActive(bot, abilityType))
                    continue;

                int abilityLevel = bot.GetAbLevel(abilityType);
                if (abilityLevel < buffTemplate.Level)
                    continue;

                if (bot.Skills.PassiveBuffs.ContainsKey(buffId))
                    continue;

                bot.Skills.AddBuff(buffId);
                Logger.Debug($"Bot '{bot.Name}' learned passive buff {buffId}");
            }
        }

        private bool IsAbilityActive(Character bot, AbilityType ability)
        {
            return ability == bot.Ability1 || ability == bot.Ability2 || ability == bot.Ability3;
        }

        internal static bool IsPlayerLearnableActiveSkill(SkillTemplate template) =>
            template is { Show: true, NeedLearn: true };

        // ---- Gear methods ----
        private class WeaponConfiguration
        {
            public Item Mainhand { get; set; }
            public Item Offhand { get; set; }
            public int Score { get; set; }
        }

        public bool EquipBestGear(Character bot, BotArchetypeState state)
        {
            var def = GetEffectiveDefinition(state);
            if (def == null) return false;

            var inventory = bot.Inventory;
            var equipmentChanged = false;

            // ---- 1. Pick best weapon configuration ----
            var weaponCandidates = GearCandidates(
                inventory,
                EquipmentItemSlot.Mainhand,
                EquipmentItemSlot.Offhand);
            var bestConfig = PickBestWeaponConfiguration(bot, def, weaponCandidates);
            if (bestConfig != null)
            {
                equipmentChanged |= EquipItemToSlot(bot, bestConfig.Mainhand, (byte)EquipmentItemSlot.Mainhand);
                if (bestConfig.Offhand != null)
                    equipmentChanged |= EquipItemToSlot(bot, bestConfig.Offhand, (byte)EquipmentItemSlot.Offhand);
                else
                {
                    // Unequip offhand if no offhand selected
                    var currentOffhand = bot.Inventory.Equipment.GetItemBySlot((int)EquipmentItemSlot.Offhand);
                    if (currentOffhand != null)
                    {
                        var freeBagSlot = inventory.Bag.GetUnusedSlot(-1);
                        if (freeBagSlot >= 0)
                        {
                            equipmentChanged |= bot.Inventory.SplitOrMoveItem(
                                ItemTaskType.Invalid,
                                currentOffhand.Id,
                                SlotType.Equipment,
                                (byte)EquipmentItemSlot.Offhand,
                                0,
                                SlotType.Inventory,
                                (byte)freeBagSlot);
                        }
                    }
                }
                // If UsesShield is false, we never select a shield; if true, we selected one.
                // Ensure no shield is equipped if not using one (already handled by configuration)
            }

            // ---- 2. Ranged weapon ----
            var bestBow = GearCandidates(inventory, EquipmentItemSlot.Ranged)
                .Where(i => i.Template is WeaponTemplate &&
                            EquipmentContainer.GetAllowedGearSlots(i.Template).Contains(EquipmentItemSlot.Ranged) &&
                            GetWeaponCategory(i.Template) == "Bow")
                .OrderByDescending(item => ScoreWeapon(def, item))
                .FirstOrDefault();
            if (bestBow != null)
                equipmentChanged |= EquipItemToSlot(bot, bestBow, (byte)EquipmentItemSlot.Ranged);

            // ---- 3. Musical instrument ----
            var bestInstrument = GearCandidates(inventory, EquipmentItemSlot.Musical)
                .Where(i => i.Template is WeaponTemplate &&
                            EquipmentContainer.GetAllowedGearSlots(i.Template).Contains(EquipmentItemSlot.Musical) &&
                            (GetWeaponCategory(i.Template) == "Lute" || GetWeaponCategory(i.Template) == "Flute"))
                .OrderByDescending(item => ScoreWeapon(def, item))
                .FirstOrDefault();
            if (bestInstrument != null)
                equipmentChanged |= EquipItemToSlot(bot, bestInstrument, (byte)EquipmentItemSlot.Musical);

            // ---- 4. Armor & Accessories ----
            var armorSlots = new[]
            {
                EquipmentItemSlot.Head, EquipmentItemSlot.Chest, EquipmentItemSlot.Legs,
                EquipmentItemSlot.Hands, EquipmentItemSlot.Feet, EquipmentItemSlot.Waist,
                EquipmentItemSlot.Arms, EquipmentItemSlot.Neck, EquipmentItemSlot.Ear1,
                EquipmentItemSlot.Ear2, EquipmentItemSlot.Finger1, EquipmentItemSlot.Finger2,
                EquipmentItemSlot.Undershirt, EquipmentItemSlot.Underpants, EquipmentItemSlot.Back
            };

            foreach (var slot in armorSlots)
            {
                var bestItem = PickBestArmor(def, GearCandidates(inventory, slot), slot);
                if (bestItem != null)
                    equipmentChanged |= EquipItemToSlot(bot, bestItem, (byte)slot);
            }

            // SplitOrMoveItem already emits the slot-level equipment changes. Only send the
            // full visual snapshot when at least one move actually succeeded. Periodic checks
            // with an unchanged loadout must be completely silent to nearby 1.2 clients.
            if (equipmentChanged)
                bot.BroadcastPacket(new SCUnitStatePacket(bot), true);

            return equipmentChanged;
        }

        private static List<Item> GearCandidates(Inventory inventory, params EquipmentItemSlot[] equippedSlots)
        {
            var equippedItems = equippedSlots
                .Select(slot => inventory.Equipment.GetItemBySlot((int)slot));
            return MergeGearCandidates(equippedItems, inventory.Bag.Items);
        }

        internal static List<Item> MergeGearCandidates(IEnumerable<Item> equippedItems, IEnumerable<Item> bagItems)
        {
            // Equipped items come first so LINQ's stable ordering retains the current item
            // when scores tie. Distinct also protects against malformed duplicate references.
            return (equippedItems ?? Enumerable.Empty<Item>())
                .Concat(bagItems ?? Enumerable.Empty<Item>())
                .Where(item => item != null)
                .Distinct()
                .ToList();
        }

        private WeaponConfiguration PickBestWeaponConfiguration(Character bot, BotArchetypeDefinition def, List<Item> bagItems)
        {
            // Separate weapons and shields, but only those whose category is in the priority list
            var oneHandedWeapons = new List<Item>();
            var twoHandedWeapons = new List<Item>();
            var shields = new List<Item>();

            // Build set of allowed categories for quick lookup
            var allowedCategories = new HashSet<string>(def.WeaponPriority, StringComparer.OrdinalIgnoreCase);

            foreach (var item in bagItems)
            {
                if (!(item.Template is WeaponTemplate weapon))
                    continue;

                var category = GetWeaponCategory(item.Template);
                if (category == null || !allowedCategories.Contains(category))
                    continue; // skip weapons not in the priority list

                var slotType = (EquipmentItemSlotType)weapon.HoldableTemplate.SlotTypeId;
                if (slotType == EquipmentItemSlotType.TwoHanded)
                    twoHandedWeapons.Add(item);
                else if (slotType == EquipmentItemSlotType.OneHanded || slotType == EquipmentItemSlotType.Mainhand || slotType == EquipmentItemSlotType.Offhand)
                    oneHandedWeapons.Add(item);
                else if (slotType == EquipmentItemSlotType.Shield)
                    shields.Add(item);
            }

            if (oneHandedWeapons.Count == 0 && twoHandedWeapons.Count == 0)
                return null; // no valid weapons found

            var candidates = new List<WeaponConfiguration>();

            // 1. Two-handed configurations
            foreach (var weapon in twoHandedWeapons)
            {
                var score = ScoreWeapon(def, weapon);
                candidates.Add(new WeaponConfiguration { Mainhand = weapon, Offhand = null, Score = score });
            }

            // 2. One-handed + shield (only if UsesShield and shields available)
            if (def.UsesShield && shields.Count > 0)
            {
                foreach (var weapon in oneHandedWeapons)
                {
                    // Pick the best shield (by level)
                    var bestShield = shields.OrderByDescending(s => s.Template.Level).First();
                    var score = ScoreWeapon(def, weapon) + ScoreWeapon(def, bestShield) + 10; // small bonus for shield
                    candidates.Add(new WeaponConfiguration { Mainhand = weapon, Offhand = bestShield, Score = score });
                }
            }

            // 3. Dual-wield (only if not using shield)
            if (!def.UsesShield && oneHandedWeapons.Count >= 2)
            {
                for (int i = 0; i < oneHandedWeapons.Count; i++)
                {
                    for (int j = i + 1; j < oneHandedWeapons.Count; j++)
                    {
                        var mainhand = oneHandedWeapons[i];
                        var offhand = oneHandedWeapons[j];
                        int scoreMain = ScoreWeapon(def, mainhand);
                        int scoreOff = ScoreWeapon(def, offhand);
                        // Put the better weapon in mainhand
                        if (scoreOff > scoreMain)
                        {
                            var temp = mainhand;
                            mainhand = offhand;
                            offhand = temp;
                            int tempScore = scoreMain;
                            scoreMain = scoreOff;
                            scoreOff = tempScore;
                        }
                        int totalScore = scoreMain + scoreOff + 20; // dual-wield bonus
                        candidates.Add(new WeaponConfiguration { Mainhand = mainhand, Offhand = offhand, Score = totalScore });
                    }
                }
            }

            if (candidates.Count == 0)
                return null;

            // Pick highest scoring configuration
            var best = candidates.OrderByDescending(c => c.Score).First();
            Logger.Debug($"Best weapon config: Mainhand={best.Mainhand?.Template?.Name ?? "null"} Offhand={best.Offhand?.Template?.Name ?? "null"} Score={best.Score}");
            return best;
        }

        private int ScoreWeapon(BotArchetypeDefinition def, Item item)
        {
            if (item == null) return 0;
            var primaryStatValue = 0;

            if (item is EquipItem equip)
                primaryStatValue = GetUnitAttributeValue(equip, def.PrimaryStat);

            // Priority bonus (preferred weapon types get a slight edge)
            var category = GetWeaponCategory(item.Template);
            var priorityIndex = category == null
                ? -1
                : def.WeaponPriority.FindIndex(p => p.Equals(category, StringComparison.OrdinalIgnoreCase));
            return ScoreWeapon(item.Template.Level, primaryStatValue, priorityIndex, def.WeaponPriority.Count);
        }

        internal static int ScoreWeapon(int level, int primaryStat, int priorityIndex, int priorityCount)
        {
            var score = level * 10 + primaryStat * 2;
            if (priorityIndex >= 0)
                score += (priorityCount - priorityIndex) * 5;
            return score;
        }

        private int ScoreArmor(BotArchetypeDefinition def, Item item)
        {
            int score = item.Template.Level * 10;
            if (item is EquipItem equip)
            {
                var primaryStatValue = GetUnitAttributeValue(equip, def.PrimaryStat);
                score += primaryStatValue * 2;
            }
            return score;
        }

        private int GetUnitAttributeValue(EquipItem equip, UnitAttribute attr)
        {
            if (equip == null) return 0;
            return attr switch
            {
                UnitAttribute.Str => equip.Str,
                UnitAttribute.Dex => equip.Dex,
                UnitAttribute.Sta => equip.Sta,
                UnitAttribute.Int => equip.Int,
                UnitAttribute.Spi => equip.Spi,
                _ => 0
            };
        }

        internal static bool NeedsEquipmentMove(Item item, byte slot)
        {
            return item != null && (item.SlotType != SlotType.Equipment || item.Slot != slot);
        }

        private bool EquipItemToSlot(Character bot, Item item, byte slot)
        {
            if (!NeedsEquipmentMove(item, slot))
                return false;

            if (item.SlotType is SlotType.Inventory or SlotType.Equipment)
            {
                var targetItem = bot.Inventory.Equipment.GetItemBySlot(slot);
                var moved = bot.Inventory.SplitOrMoveItem(ItemTaskType.Invalid, item.Id, item.SlotType, (byte)item.Slot,
                    targetItem?.Id ?? 0, SlotType.Equipment, slot);
                if (moved)
                    Logger.Debug($"Bot '{bot.Name}' equipped {item.Template.Name} to slot {slot}.");
                return moved;
            }

            return false;
        }

        // ---- Armor selection helpers (unchanged) ----
        private Item PickBestArmor(BotArchetypeDefinition def, List<Item> bagItems, EquipmentItemSlot slot)
        {
            var candidates = new List<Item>();

            foreach (var item in bagItems)
            {
                var allowedSlots = EquipmentContainer.GetAllowedGearSlots(item.Template);
                if (!allowedSlots.Contains(slot))
                    continue;

                if (slot == EquipmentItemSlot.Neck || slot == EquipmentItemSlot.Ear1 || slot == EquipmentItemSlot.Ear2 ||
                    slot == EquipmentItemSlot.Finger1 || slot == EquipmentItemSlot.Finger2)
                {
                    if (item.Template is AccessoryTemplate)
                        candidates.Add(item);
                }
                else
                {
                    if (item.Template is ArmorTemplate armor && armor.KindTemplate.TypeId == def.ArmorType)
                        candidates.Add(item);
                }
            }

            if (candidates.Count == 0)
                return null;

            return candidates.OrderByDescending(item => ScoreArmor(def, item)).FirstOrDefault();
        }
    }
}
