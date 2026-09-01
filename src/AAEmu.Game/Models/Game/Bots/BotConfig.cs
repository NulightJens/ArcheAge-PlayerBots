using System;
using System.IO;
using AAEmu.Commons;
using AAEmu.Commons.IO;
using AAEmu.Commons.Utils;
using AAEmu.Game.Core.Managers;
using Newtonsoft.Json;
using NLog;

namespace AAEmu.Game.Models.Game.Bots
{
    public readonly record struct BotActivityDirectorConfiguration(
        bool Enabled,
        bool Valid,
        string Reason,
        uint ZoneId,
        uint[] CharacterIds,
        int MinimumPopulation,
        int TargetPopulation,
        int MaximumPopulation,
        TimeSpan InitialDelay,
        TimeSpan ReconciliationInterval,
        TimeSpan RetryBackoff);

    public class BotConfig : Singleton<BotConfig>, ILoadable
    {
        private const int ActivityDirectorInitialDelayMaximumMs = 300000;
        private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();
        /// <summary>Fallback hazard radius in metres when an area-trigger row has no positive radius; 40 m covers the legacy hazard envelope.</summary>
        public const float DefaultHazardRadius = 40f;

        // All numeric values are stored as double (to avoid JSON parse issues)
        public double SearchRadius { get; set; } = 60.0;
        public double AttackRange { get; set; } = 1.5;
        public double BowRange { get; set; } = 20.0;
        public double ReengageRange { get; set; } = 60.0;
        public double FleeDistance { get; set; } = 15.0;
        public double StuckMinMeters { get; set; } = 0.3;
        public double StuckSeconds { get; set; } = 3.0;
        public double StuckNudgeMeters { get; set; } = 2.0;
        public int StuckTeleportAttempts { get; set; } = 5;
        public double StuckTeleportSeconds { get; set; } = 90.0;
        public double FollowStopBand { get; set; } = 0.6;
        public int RestThresholdPercent { get; set; } = 50;
        public double RestHealInterval { get; set; } = 1.0;
        public int RestHealPercentPerTick { get; set; } = 2;
        public int RespawnDelaySeconds { get; set; } = 5;
        public int IdleStanceDelaySeconds { get; set; } = 8;
        public double ReactDelayCombatMs { get; set; } = 300;
        public double ReactDelayMovingMs { get; set; } = 300;
        public double ReactDelayIdleMinMs { get; set; } = 1000;
        public double ReactDelayIdleMaxMs { get; set; } = 3000;
        public double ReactDelayRestingMs { get; set; } = 2000;
        public double PassiveDelayMs { get; set; } = 10000;
        public double GroundCheckIntervalMs { get; set; } = 1000;
        public bool JumpEnabled { get; set; } = true;
        public double JumpLaunchSpeed { get; set; } = 4.5;
        public double JumpCooldownMs { get; set; } = 1500;
        /// <summary>Rare cosmetic jumps while a non-combat bot is actively following.</summary>
        public bool AmbientJumpEnabled { get; set; } = true;
        public double AmbientJumpMinIntervalMs { get; set; } = 45000;
        public double AmbientJumpMaxIntervalMs { get; set; } = 90000;
        /// <summary>
        /// Opt-in terrain-step look-ahead. It does not detect doodad or physics collision.
        /// </summary>
        public bool ObstacleJumpEnabled { get; set; }
        public double ObstacleJumpProbeIntervalMs { get; set; } = 500;
        public double ObstacleJumpProbeDistance { get; set; } = 0.9;
        public double ObstacleJumpMinRise { get; set; } = 0.25;
        public double ObstacleJumpMaxRise { get; set; } = 0.85;
        /// <summary>Requested share of independent bots that receive full brain steps. Omitted configuration means 100%.</summary>
        public double ActivityPercent { get; set; } = 100;
        public double ActivityWindowMs { get; set; } = 30000;
        public double ActivityRealPlayerRadius { get; set; } = 150;
        public double HostTickBudgetMs { get; set; } = 30;
        /// <summary>Whole-server pressure budget. Zero explicitly disables this governor input until a measured budget is configured.</summary>
        public double ServerTickBudgetMs { get; set; } = 0;
        public double ScanTtlMs { get; set; } = 2000;
        public double HazardScanTtlMs { get; set; } = 500;
        public double RealPlayerScanTtlMs { get; set; } = 5000;
        public double MetricsLogIntervalMs { get; set; } = 60000;
        public bool UseEngine { get; set; } = true;
        public int IterationsPerTick { get; set; } = 10;
        public int ExpireActionTimeMs { get; set; } = 5000;
        public int GlobalSkillDelayMs { get; set; } = 600;
        public bool ActivityDirectorEnabled { get; set; }
        public uint ActivityDirectorZoneId { get; set; }
        public List<uint> ActivityDirectorCharacterIds { get; set; } = [];
        public int ActivityDirectorMinimumPopulation { get; set; }
        public int ActivityDirectorTargetPopulation { get; set; }
        public int ActivityDirectorMaximumPopulation { get; set; }
        public int ActivityDirectorInitialDelayMs { get; set; } = 2000;
        public int ActivityDirectorReconciliationIntervalMs { get; set; } = 5000;
        public int ActivityDirectorRetryBackoffMs { get; set; } = 30000;

        private static string ConfigPath => Path.Combine(FileManager.AppPath, "Configurations", "BotConfig.json");

        public void Load() => Load(ConfigPath);

        internal void Load(string path)
        {
            if (!File.Exists(path))
            {
                SaveDefault(path);
                return;
            }

            var json = File.ReadAllText(path);
            LoadFromJson(json);
        }

        internal bool LoadFromJson(string json)
        {
            var backup = JsonConvert.SerializeObject(this);
            try
            {
                if (string.IsNullOrWhiteSpace(json))
                    return false;

                JsonConvert.PopulateObject(json, this);
                Validate();
                return true;
            }
            catch (Exception e)
            {
                JsonConvert.PopulateObject(backup, this);
                Logger.Warn(e, "Failed to parse BotConfig.json, using current values.");
                return false;
            }
        }

        public void Reload()
        {
            Load();
            Logger.Info("BotConfig reloaded from file.");
        }

        private void SaveDefault(string path)
        {
            var json = BuildDefaultJson();
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, json);
            Logger.Info($"Created default BotConfig.json at {path}");
        }

        internal string BuildDefaultJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        internal void Validate()
        {
            SearchRadius = Math.Max(0, SearchRadius);
            AttackRange = Math.Max(0, AttackRange);
            BowRange = Math.Max(0, BowRange);
            ReengageRange = Math.Max(0, ReengageRange);
            FleeDistance = Math.Max(0, FleeDistance);
            StuckMinMeters = Math.Max(0, StuckMinMeters);
            StuckSeconds = Math.Max(0, StuckSeconds);
            StuckNudgeMeters = Math.Max(0, StuckNudgeMeters);
            StuckTeleportAttempts = Math.Max(1, StuckTeleportAttempts);
            StuckTeleportSeconds = Math.Max(0, StuckTeleportSeconds);
            FollowStopBand = Math.Max(0, FollowStopBand);
            RestHealInterval = Math.Max(0.1, RestHealInterval);
            RestHealPercentPerTick = Math.Clamp(RestHealPercentPerTick, 0, 100);
            RestThresholdPercent = Math.Clamp(RestThresholdPercent, 0, 100);
            RespawnDelaySeconds = Math.Max(0, RespawnDelaySeconds);
            IdleStanceDelaySeconds = Math.Max(0, IdleStanceDelaySeconds);
            ReactDelayCombatMs = Math.Max(0, ReactDelayCombatMs);
            ReactDelayMovingMs = Math.Max(0, ReactDelayMovingMs);
            ReactDelayIdleMinMs = Math.Max(0, ReactDelayIdleMinMs);
            ReactDelayIdleMaxMs = Math.Max(ReactDelayIdleMinMs, ReactDelayIdleMaxMs);
            ReactDelayRestingMs = Math.Max(0, ReactDelayRestingMs);
            PassiveDelayMs = Math.Max(0, PassiveDelayMs);
            GroundCheckIntervalMs = Math.Max(0, GroundCheckIntervalMs);
            JumpLaunchSpeed = Math.Clamp(JumpLaunchSpeed, 0.1, 12);
            JumpCooldownMs = Math.Max(0, JumpCooldownMs);
            AmbientJumpMinIntervalMs = Math.Max(1000, AmbientJumpMinIntervalMs);
            AmbientJumpMaxIntervalMs = Math.Max(AmbientJumpMinIntervalMs, AmbientJumpMaxIntervalMs);
            ObstacleJumpProbeIntervalMs = Math.Max(100, ObstacleJumpProbeIntervalMs);
            ObstacleJumpProbeDistance = Math.Clamp(ObstacleJumpProbeDistance, 0.1, 5);
            ObstacleJumpMinRise = Math.Max(0.05, ObstacleJumpMinRise);
            ObstacleJumpMaxRise = Math.Max(ObstacleJumpMinRise, ObstacleJumpMaxRise);
            ActivityPercent = Math.Clamp(ActivityPercent, 0, 100);
            ActivityWindowMs = Math.Max(0, ActivityWindowMs);
            ActivityRealPlayerRadius = Math.Max(0, ActivityRealPlayerRadius);
            HostTickBudgetMs = Math.Max(0, HostTickBudgetMs);
            ServerTickBudgetMs = Math.Max(0, ServerTickBudgetMs);
            ScanTtlMs = Math.Max(0, ScanTtlMs);
            HazardScanTtlMs = Math.Max(0, HazardScanTtlMs);
            RealPlayerScanTtlMs = Math.Max(0, RealPlayerScanTtlMs);
            MetricsLogIntervalMs = Math.Max(0, MetricsLogIntervalMs);
            IterationsPerTick = Math.Max(1, IterationsPerTick);
            ExpireActionTimeMs = Math.Max(0, ExpireActionTimeMs);
            GlobalSkillDelayMs = Math.Max(0, GlobalSkillDelayMs);
            ActivityDirectorCharacterIds ??= [];
            ActivityDirectorInitialDelayMs = Math.Clamp(
                ActivityDirectorInitialDelayMs,
                0,
                ActivityDirectorInitialDelayMaximumMs);
            ActivityDirectorReconciliationIntervalMs = Math.Clamp(
                ActivityDirectorReconciliationIntervalMs,
                100,
                60000);
            ActivityDirectorRetryBackoffMs = Math.Clamp(ActivityDirectorRetryBackoffMs, 100, 600000);
        }

        public BotActivityDirectorConfiguration GetActivityDirectorConfiguration()
        {
            var characterIds = ActivityDirectorCharacterIds?.ToArray() ?? [];
            var initialDelay = TimeSpan.FromMilliseconds(Math.Clamp(
                ActivityDirectorInitialDelayMs,
                0,
                ActivityDirectorInitialDelayMaximumMs));
            var interval = TimeSpan.FromMilliseconds(Math.Clamp(ActivityDirectorReconciliationIntervalMs, 100, 60000));
            var backoff = TimeSpan.FromMilliseconds(Math.Clamp(ActivityDirectorRetryBackoffMs, 100, 600000));

            if (!ActivityDirectorEnabled)
            {
                return new BotActivityDirectorConfiguration(
                    false,
                    true,
                    "disabled",
                    ActivityDirectorZoneId,
                    characterIds,
                    ActivityDirectorMinimumPopulation,
                    ActivityDirectorTargetPopulation,
                    ActivityDirectorMaximumPopulation,
                    initialDelay,
                    interval,
                    backoff);
            }

            var reason = ValidateActivityDirector(
                ActivityDirectorZoneId,
                characterIds,
                ActivityDirectorMinimumPopulation,
                ActivityDirectorTargetPopulation,
                ActivityDirectorMaximumPopulation);
            return new BotActivityDirectorConfiguration(
                true,
                reason == null,
                reason ?? "valid",
                ActivityDirectorZoneId,
                characterIds,
                ActivityDirectorMinimumPopulation,
                ActivityDirectorTargetPopulation,
                ActivityDirectorMaximumPopulation,
                initialDelay,
                interval,
                backoff);
        }

        private static string ValidateActivityDirector(
            uint zoneId,
            IReadOnlyList<uint> characterIds,
            int minimum,
            int target,
            int maximum)
        {
            if (zoneId == 0)
                return "zone_zero";
            if (characterIds.Count == 0)
                return "identities_empty";

            var unique = new HashSet<uint>();
            for (var index = 0; index < characterIds.Count; index++)
            {
                var characterId = characterIds[index];
                if (characterId == 0)
                    return "identity_zero";
                if (!unique.Add(characterId))
                    return "identity_duplicate";
            }

            if (minimum < 0 || target < 0 || maximum < 0)
                return "population_negative";
            if (minimum > target || target > maximum)
                return "population_order_invalid";
            if (maximum > characterIds.Count)
                return "maximum_exceeds_identities";

            return null;
        }

        internal static bool TryParseAutoSpawnState(string state, out BotCombatStateType? parsedState)
        {
            // Same words (and aliases) as /botstate in Scripts/Commands/BotState.cs, so the config never drifts from the command.
            parsedState = state switch
            {
                "idle" => BotCombatStateType.Idle,
                "grind" or "grinding" => BotCombatStateType.Grinding,
                "quest" or "questing" => BotCombatStateType.Questing,
                "roam" or "roaming" => BotCombatStateType.Roaming,
                "follow" or "following" => BotCombatStateType.Following,
                "rest" or "resting" => BotCombatStateType.Resting,
                "free" => null,
                _ => null
            };

            return state is "idle" or "grind" or "grinding" or "quest" or "questing" or "roam" or "roaming"
                or "follow" or "following" or "rest" or "resting" or "free";
        }
    }
}
