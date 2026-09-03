using System.Globalization;
using AAEmu.Game.Models.Game.Bots;
using Newtonsoft.Json.Linq;
using AAEmu.UnitTests.Utils.Mocks;

namespace AAEmu.UnitTests.Game.Core.Managers.BotManagers;

[NotInParallel]
public class BotConfigTests
{
    [Test]
    public async Task Defaults_MatchDocumentedValues()
    {
        var config = new BotConfig();

        await Assert.That(config.SearchRadius).IsEqualTo(60.0);
        await Assert.That(config.AttackRange).IsEqualTo(1.5);
        await Assert.That(config.BowRange).IsEqualTo(20.0);
        await Assert.That(config.ReengageRange).IsEqualTo(60.0);
        await Assert.That(config.RestThresholdPercent).IsEqualTo(50);
        await Assert.That(config.RestHealInterval).IsEqualTo(1.0);
        await Assert.That(config.RestHealPercentPerTick).IsEqualTo(2);
        await Assert.That(config.RespawnDelaySeconds).IsEqualTo(5);
        await Assert.That(config.IdleStanceDelaySeconds).IsEqualTo(8);
        await Assert.That(config.ReactDelayCombatMs).IsEqualTo(300d);
        await Assert.That(config.ReactDelayMovingMs).IsEqualTo(300d);
        await Assert.That(config.ReactDelayIdleMinMs).IsEqualTo(1000d);
        await Assert.That(config.ReactDelayIdleMaxMs).IsEqualTo(3000d);
        await Assert.That(config.ReactDelayRestingMs).IsEqualTo(2000d);
        await Assert.That(config.PassiveDelayMs).IsEqualTo(10000d);
        await Assert.That(config.GroundCheckIntervalMs).IsEqualTo(1000d);
        await Assert.That(config.JumpEnabled).IsTrue();
        await Assert.That(config.JumpLaunchSpeed).IsEqualTo(4.5d);
        await Assert.That(config.JumpCooldownMs).IsEqualTo(1500d);
        await Assert.That(config.AmbientJumpEnabled).IsTrue();
        await Assert.That(config.AmbientJumpMinIntervalMs).IsEqualTo(45000d);
        await Assert.That(config.AmbientJumpMaxIntervalMs).IsEqualTo(90000d);
        await Assert.That(config.ObstacleJumpEnabled).IsFalse();
        await Assert.That(config.ActivityPercent).IsEqualTo(100d);
        await Assert.That(config.ActivityWindowMs).IsEqualTo(30000d);
        await Assert.That(config.ActivityRealPlayerRadius).IsEqualTo(150d);
        await Assert.That(config.HostTickBudgetMs).IsEqualTo(30d);
        await Assert.That(config.ServerTickBudgetMs).IsEqualTo(0d);
        await Assert.That(config.ScanTtlMs).IsEqualTo(2000d);
        await Assert.That(config.RealPlayerScanTtlMs).IsEqualTo(5000d);
        await Assert.That(config.MetricsLogIntervalMs).IsEqualTo(60000d);
        await Assert.That(config.AutoSpawnCharacterIds).IsEmpty();
        await Assert.That(config.AutoSpawnState).IsEqualTo("grind");
        await Assert.That(config.AutoSpawnDelayMs).IsEqualTo(2000);
        await Assert.That(config.QuestIntakeEnabled).IsFalse();
        await Assert.That(config.QuestIntakeScanRadius).IsEqualTo(60d);
        await Assert.That(config.QuestIntakeInteractionRadius).IsEqualTo(6d);
        await Assert.That(config.QuestIntakeRetryBackoffMs).IsEqualTo(30000);
        await Assert.That(config.QuestCompletionEnabled).IsFalse();
        await Assert.That(config.QuestObjectiveScanRadius).IsEqualTo(60d);
        await Assert.That(config.QuestReportScanRadius).IsEqualTo(60d);
        await Assert.That(config.QuestReportInteractionRadius).IsEqualTo(6d);
        await Assert.That(config.QuestTargetSelectionTimeoutMs).IsEqualTo(30000);
        await Assert.That(config.QuestProgressObservationMs).IsEqualTo(3000);
        await Assert.That(config.QuestCompletionObservationMs).IsEqualTo(5000);
        await Assert.That(config.QuestCompletionRetryBackoffMs).IsEqualTo(30000);
        await Assert.That(config.ActivityDirectorEnabled).IsFalse();
        await Assert.That(config.ActivityDirectorZoneId).IsEqualTo(0u);
        await Assert.That(config.ActivityDirectorCharacterIds).IsEmpty();
        await Assert.That(config.ActivityDirectorMinimumPopulation).IsEqualTo(0);
        await Assert.That(config.ActivityDirectorTargetPopulation).IsEqualTo(0);
        await Assert.That(config.ActivityDirectorMaximumPopulation).IsEqualTo(0);
        await Assert.That(config.ActivityDirectorInitialDelayMs).IsEqualTo(2000);
        await Assert.That(config.ActivityDirectorReconciliationIntervalMs).IsEqualTo(5000);
        await Assert.That(config.ActivityDirectorRetryBackoffMs).IsEqualTo(30000);
    }

    [Test]
    public async Task Defaults_IncludeR3BodyValues()
    {
        var config = new BotConfig();

        await Assert.That(config.GlobalSkillDelayMs).IsEqualTo(600);
        await Assert.That(config.FleeDistance).IsEqualTo(15d);
        await Assert.That(config.HazardScanTtlMs).IsEqualTo(500d);
        await Assert.That(config.StuckMinMeters).IsEqualTo(0.3d);
        await Assert.That(config.StuckSeconds).IsEqualTo(3d);
        await Assert.That(config.StuckNudgeMeters).IsEqualTo(2d);
        await Assert.That(config.StuckTeleportAttempts).IsEqualTo(5);
        await Assert.That(config.StuckTeleportSeconds).IsEqualTo(90d);
        await Assert.That(config.FollowStopBand).IsEqualTo(0.6d);
    }

    [Test]
    public async Task Validate_NegativeR3BodyValues_ClampsToSafeDefaults()
    {
        var config = new BotConfig
        {
            GlobalSkillDelayMs = -1,
            FleeDistance = -1,
            HazardScanTtlMs = -1,
            StuckMinMeters = -1,
            StuckSeconds = -1,
            StuckNudgeMeters = -1,
            StuckTeleportAttempts = 0,
            StuckTeleportSeconds = -1,
            FollowStopBand = -1
        };

        config.Validate();

        await Assert.That(config.GlobalSkillDelayMs).IsEqualTo(0);
        await Assert.That(config.FleeDistance).IsEqualTo(0d);
        await Assert.That(config.HazardScanTtlMs).IsEqualTo(0d);
        await Assert.That(config.StuckMinMeters).IsEqualTo(0d);
        await Assert.That(config.StuckSeconds).IsEqualTo(0d);
        await Assert.That(config.StuckNudgeMeters).IsEqualTo(0d);
        await Assert.That(config.StuckTeleportAttempts).IsEqualTo(1);
        await Assert.That(config.StuckTeleportSeconds).IsEqualTo(0d);
        await Assert.That(config.FollowStopBand).IsEqualTo(0d);
    }

    [Test]
    public async Task LoadFromJson_FullDocument_OverridesEveryField()
    {
        var config = new BotConfig();

        var loaded = config.LoadFromJson("""
            {
              "SearchRadius": 11.1,
              "AttackRange": 2.2,
              "BowRange": 33.3,
              "ReengageRange": 44.4,
              "RestThresholdPercent": 55,
              "RestHealInterval": 6.6,
              "RestHealPercentPerTick": 7,
              "RespawnDelaySeconds": 8,
              "IdleStanceDelaySeconds": 9,
              "ReactDelayCombatMs": 301.1,
              "ReactDelayMovingMs": 302.2,
              "ReactDelayIdleMinMs": 1001.1,
              "ReactDelayIdleMaxMs": 3002.2,
              "ReactDelayRestingMs": 2003.3,
              "PassiveDelayMs": 10004.4,
              "GroundCheckIntervalMs": 1005.5,
              "ActivityPercent": 66.6,
              "ActivityWindowMs": 30006.6,
              "ActivityRealPlayerRadius": 156.6,
              "HostTickBudgetMs": 36.6,
              "ServerTickBudgetMs": 37.7,
              "ScanTtlMs": 2006.6,
              "RealPlayerScanTtlMs": 5006.6,
              "MetricsLogIntervalMs": 60006.6,
              "AutoSpawnCharacterIds": [2, 3, 4],
              "AutoSpawnState": "questing",
              "AutoSpawnDelayMs": 1234,
              "QuestIntakeEnabled": true,
              "QuestIntakeScanRadius": 56.6,
              "QuestIntakeInteractionRadius": 5.5,
              "QuestIntakeRetryBackoffMs": 4567,
              "QuestCompletionEnabled": true,
              "QuestObjectiveScanRadius": 55.5,
              "QuestReportScanRadius": 54.4,
              "QuestReportInteractionRadius": 4.4,
              "QuestTargetSelectionTimeoutMs": 6543,
              "QuestProgressObservationMs": 7654,
              "QuestCompletionObservationMs": 8765,
              "QuestCompletionRetryBackoffMs": 9876,
              "ActivityDirectorEnabled": true,
              "ActivityDirectorZoneId": 137,
              "ActivityDirectorCharacterIds": [2, 3, 4],
              "ActivityDirectorMinimumPopulation": 1,
              "ActivityDirectorTargetPopulation": 2,
              "ActivityDirectorMaximumPopulation": 3,
              "ActivityDirectorInitialDelayMs": 1234,
              "ActivityDirectorReconciliationIntervalMs": 2345,
              "ActivityDirectorRetryBackoffMs": 3456
            }
            """);

        await Assert.That(loaded).IsTrue();
        await Assert.That(config.SearchRadius).IsEqualTo(11.1);
        await Assert.That(config.AttackRange).IsEqualTo(2.2);
        await Assert.That(config.BowRange).IsEqualTo(33.3);
        await Assert.That(config.ReengageRange).IsEqualTo(44.4);
        await Assert.That(config.RestThresholdPercent).IsEqualTo(55);
        await Assert.That(config.RestHealInterval).IsEqualTo(6.6);
        await Assert.That(config.RestHealPercentPerTick).IsEqualTo(7);
        await Assert.That(config.RespawnDelaySeconds).IsEqualTo(8);
        await Assert.That(config.IdleStanceDelaySeconds).IsEqualTo(9);
        await Assert.That(config.ReactDelayCombatMs).IsEqualTo(301.1);
        await Assert.That(config.ReactDelayMovingMs).IsEqualTo(302.2);
        await Assert.That(config.ReactDelayIdleMinMs).IsEqualTo(1001.1);
        await Assert.That(config.ReactDelayIdleMaxMs).IsEqualTo(3002.2);
        await Assert.That(config.ReactDelayRestingMs).IsEqualTo(2003.3);
        await Assert.That(config.PassiveDelayMs).IsEqualTo(10004.4);
        await Assert.That(config.GroundCheckIntervalMs).IsEqualTo(1005.5);
        await Assert.That(config.ActivityPercent).IsEqualTo(66.6);
        await Assert.That(config.ActivityWindowMs).IsEqualTo(30006.6);
        await Assert.That(config.ActivityRealPlayerRadius).IsEqualTo(156.6);
        await Assert.That(config.HostTickBudgetMs).IsEqualTo(36.6);
        await Assert.That(config.ServerTickBudgetMs).IsEqualTo(37.7);
        await Assert.That(config.ScanTtlMs).IsEqualTo(2006.6);
        await Assert.That(config.RealPlayerScanTtlMs).IsEqualTo(5006.6);
        await Assert.That(config.MetricsLogIntervalMs).IsEqualTo(60006.6);
        await Assert.That(config.AutoSpawnCharacterIds).IsEquivalentTo([2u, 3u, 4u]);
        await Assert.That(config.AutoSpawnState).IsEqualTo("questing");
        await Assert.That(config.AutoSpawnDelayMs).IsEqualTo(1234);
        await Assert.That(config.QuestIntakeEnabled).IsTrue();
        await Assert.That(config.QuestIntakeScanRadius).IsEqualTo(56.6);
        await Assert.That(config.QuestIntakeInteractionRadius).IsEqualTo(5.5);
        await Assert.That(config.QuestIntakeRetryBackoffMs).IsEqualTo(4567);
        await Assert.That(config.QuestCompletionEnabled).IsTrue();
        await Assert.That(config.QuestObjectiveScanRadius).IsEqualTo(55.5);
        await Assert.That(config.QuestReportScanRadius).IsEqualTo(54.4);
        await Assert.That(config.QuestReportInteractionRadius).IsEqualTo(4.4);
        await Assert.That(config.QuestTargetSelectionTimeoutMs).IsEqualTo(6543);
        await Assert.That(config.QuestProgressObservationMs).IsEqualTo(7654);
        await Assert.That(config.QuestCompletionObservationMs).IsEqualTo(8765);
        await Assert.That(config.QuestCompletionRetryBackoffMs).IsEqualTo(9876);
        await Assert.That(config.ActivityDirectorEnabled).IsTrue();
        await Assert.That(config.ActivityDirectorZoneId).IsEqualTo(137u);
        await Assert.That(config.ActivityDirectorCharacterIds).IsEquivalentTo([2u, 3u, 4u]);
        await Assert.That(config.ActivityDirectorMinimumPopulation).IsEqualTo(1);
        await Assert.That(config.ActivityDirectorTargetPopulation).IsEqualTo(2);
        await Assert.That(config.ActivityDirectorMaximumPopulation).IsEqualTo(3);
        await Assert.That(config.ActivityDirectorInitialDelayMs).IsEqualTo(1234);
        await Assert.That(config.ActivityDirectorReconciliationIntervalMs).IsEqualTo(2345);
        await Assert.That(config.ActivityDirectorRetryBackoffMs).IsEqualTo(3456);
    }

    [Test]
    public async Task LoadFromJson_PartialDocument_KeepsDefaultsForMissingKeys()
    {
        var config = new BotConfig();

        config.LoadFromJson("{\"SearchRadius\":10}");

        await Assert.That(config.SearchRadius).IsEqualTo(10);
        await Assert.That(config.AttackRange).IsEqualTo(1.5);
    }

    [Test]
    public async Task LoadFromJson_FailsMidDocument_KeepsPreviousValues()
    {
        var config = new BotConfig();

        await Assert.That(config.LoadFromJson("{\"SearchRadius\":42,\"AttackRange\":3}"))
            .IsTrue();

        var loaded = config.LoadFromJson("{\"SearchRadius\":-5,\"AttackRange\":\"abc\"}");

        await Assert.That(loaded).IsFalse();
        await Assert.That(config.SearchRadius).IsEqualTo(42.0);
        await Assert.That(config.AttackRange).IsEqualTo(3.0);
    }

    [Test]
    public async Task LoadFromJson_InvalidJson_ReturnsFalseAndKeepsValues()
    {
        var config = new BotConfig();

        var loaded = config.LoadFromJson("{not json");

        await Assert.That(loaded).IsFalse();
        await Assert.That(config.SearchRadius).IsEqualTo(60.0);
        await Assert.That(config.AttackRange).IsEqualTo(1.5);
    }

    [Test]
    public async Task LoadFromJson_Whitespace_ReturnsFalse()
    {
        var config = new BotConfig();

        var loaded = config.LoadFromJson("   ");

        await Assert.That(loaded).IsFalse();
    }

    [Test]
    public async Task LoadFromJson_IntegerForDoubleField_Parses()
    {
        var config = new BotConfig();

        config.LoadFromJson("{\"AttackRange\":2}");

        await Assert.That(config.AttackRange).IsEqualTo(2.0);
    }

    [Test]
    public async Task LoadFromJson_UnknownKeys_Ignored()
    {
        var config = new BotConfig();

        var loaded = config.LoadFromJson("{\"Foo\":1}");

        await Assert.That(loaded).IsTrue();
        await Assert.That(config.SearchRadius).IsEqualTo(60.0);
        await Assert.That(config.AttackRange).IsEqualTo(1.5);
    }

    [Test]
    public async Task BuildDefaultJson_RoundTripsThroughLoadFromJson()
    {
        var config = new BotConfig();
        var json = config.BuildDefaultJson();

        var loaded = config.LoadFromJson(json);

        await Assert.That(loaded).IsTrue();
        await Assert.That(config.SearchRadius).IsEqualTo(60.0);
        await Assert.That(config.AttackRange).IsEqualTo(1.5);
        await Assert.That(config.BowRange).IsEqualTo(20.0);
        await Assert.That(config.ReengageRange).IsEqualTo(60.0);
        await Assert.That(config.RestThresholdPercent).IsEqualTo(50);
        await Assert.That(config.RestHealInterval).IsEqualTo(1.0);
        await Assert.That(config.RestHealPercentPerTick).IsEqualTo(2);
        await Assert.That(config.RespawnDelaySeconds).IsEqualTo(5);
        await Assert.That(config.IdleStanceDelaySeconds).IsEqualTo(8);
        await Assert.That(config.ReactDelayCombatMs).IsEqualTo(300d);
        await Assert.That(config.ReactDelayMovingMs).IsEqualTo(300d);
        await Assert.That(config.ReactDelayIdleMinMs).IsEqualTo(1000d);
        await Assert.That(config.ReactDelayIdleMaxMs).IsEqualTo(3000d);
        await Assert.That(config.ReactDelayRestingMs).IsEqualTo(2000d);
        await Assert.That(config.PassiveDelayMs).IsEqualTo(10000d);
        await Assert.That(config.GroundCheckIntervalMs).IsEqualTo(1000d);
        await Assert.That(config.ActivityPercent).IsEqualTo(100d);
        await Assert.That(config.ActivityWindowMs).IsEqualTo(30000d);
        await Assert.That(config.ActivityRealPlayerRadius).IsEqualTo(150d);
        await Assert.That(config.HostTickBudgetMs).IsEqualTo(30d);
        await Assert.That(config.ServerTickBudgetMs).IsEqualTo(0d);
        await Assert.That(config.ScanTtlMs).IsEqualTo(2000d);
        await Assert.That(config.RealPlayerScanTtlMs).IsEqualTo(5000d);
        await Assert.That(config.MetricsLogIntervalMs).IsEqualTo(60000d);
        await Assert.That(config.AutoSpawnCharacterIds).IsEmpty();
        await Assert.That(config.AutoSpawnState).IsEqualTo("grind");
        await Assert.That(config.AutoSpawnDelayMs).IsEqualTo(2000);
        await Assert.That(config.QuestIntakeEnabled).IsFalse();
        await Assert.That(config.QuestIntakeScanRadius).IsEqualTo(60d);
        await Assert.That(config.QuestIntakeInteractionRadius).IsEqualTo(6d);
        await Assert.That(config.QuestIntakeRetryBackoffMs).IsEqualTo(30000);
        await Assert.That(config.QuestCompletionEnabled).IsFalse();
        await Assert.That(config.QuestObjectiveScanRadius).IsEqualTo(60d);
        await Assert.That(config.QuestReportScanRadius).IsEqualTo(60d);
        await Assert.That(config.QuestReportInteractionRadius).IsEqualTo(6d);
        await Assert.That(config.QuestTargetSelectionTimeoutMs).IsEqualTo(30000);
        await Assert.That(config.QuestProgressObservationMs).IsEqualTo(3000);
        await Assert.That(config.QuestCompletionObservationMs).IsEqualTo(5000);
        await Assert.That(config.QuestCompletionRetryBackoffMs).IsEqualTo(30000);
        await Assert.That(config.ActivityDirectorEnabled).IsFalse();
        await Assert.That(config.ActivityDirectorZoneId).IsEqualTo(0u);
        await Assert.That(config.ActivityDirectorCharacterIds).IsEmpty();
        await Assert.That(config.ActivityDirectorMinimumPopulation).IsEqualTo(0);
        await Assert.That(config.ActivityDirectorTargetPopulation).IsEqualTo(0);
        await Assert.That(config.ActivityDirectorMaximumPopulation).IsEqualTo(0);
        await Assert.That(config.ActivityDirectorInitialDelayMs).IsEqualTo(2000);
        await Assert.That(config.ActivityDirectorReconciliationIntervalMs).IsEqualTo(5000);
        await Assert.That(config.ActivityDirectorRetryBackoffMs).IsEqualTo(30000);
    }

    [Test]
    [NotInParallel]
    public async Task BuildDefaultJson_UnderGermanCulture_UsesDecimalPoint()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var config = new BotConfig();

            var json = config.BuildDefaultJson();

            await Assert.That(json).Contains("\"AttackRange\": 1.5");
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Test]
    public async Task Load_MissingFile_WritesDefaultFileAndKeepsDefaults()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "BotConfig.json");
        try
        {
            var config = new BotConfig();

            config.Load(path);

            await Assert.That(File.Exists(path)).IsTrue();
            await Assert.That(config.SearchRadius).IsEqualTo(60.0);
            await Assert.That(config.AttackRange).IsEqualTo(1.5);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Test]
    public async Task Golden_GeneratedDefaultBotConfig_ContainsHeadlessBootstrapDefaults()
    {
        // The server writes Configurations/BotConfig.json itself from BuildDefaultJson() when it is missing
        // (BotConfig.Load -> SaveDefault); nothing ships in the repo, so that generator is the golden source.
        var json = JObject.Parse(new BotConfig().BuildDefaultJson());

        await Assert.That((JArray)json["AutoSpawnCharacterIds"]).IsEmpty();
        await Assert.That((string)json["AutoSpawnState"]).IsEqualTo("grind");
        await Assert.That((int)json["AutoSpawnDelayMs"]).IsEqualTo(2000);
        await Assert.That((bool)json["ActivityDirectorEnabled"]).IsFalse();
        await Assert.That((uint)json["ActivityDirectorZoneId"]).IsEqualTo(0u);
        await Assert.That((JArray)json["ActivityDirectorCharacterIds"]).IsEmpty();
        await Assert.That((int)json["ActivityDirectorMinimumPopulation"]).IsEqualTo(0);
        await Assert.That((int)json["ActivityDirectorTargetPopulation"]).IsEqualTo(0);
        await Assert.That((int)json["ActivityDirectorMaximumPopulation"]).IsEqualTo(0);
        await Assert.That((int)json["ActivityDirectorInitialDelayMs"]).IsEqualTo(2000);
        await Assert.That((int)json["ActivityDirectorReconciliationIntervalMs"]).IsEqualTo(5000);
        await Assert.That((int)json["ActivityDirectorRetryBackoffMs"]).IsEqualTo(30000);
        await Assert.That((double)json["ActivityPercent"]).IsEqualTo(100d);
        await Assert.That((double)json["ServerTickBudgetMs"]).IsEqualTo(0d);
        await Assert.That((bool)json["QuestIntakeEnabled"]).IsFalse();
        await Assert.That((double)json["QuestIntakeScanRadius"]).IsEqualTo(60d);
        await Assert.That((double)json["QuestIntakeInteractionRadius"]).IsEqualTo(6d);
        await Assert.That((int)json["QuestIntakeRetryBackoffMs"]).IsEqualTo(30000);
        await Assert.That((bool)json["QuestCompletionEnabled"]).IsFalse();
        await Assert.That((double)json["QuestObjectiveScanRadius"]).IsEqualTo(60d);
        await Assert.That((double)json["QuestReportScanRadius"]).IsEqualTo(60d);
        await Assert.That((double)json["QuestReportInteractionRadius"]).IsEqualTo(6d);
        await Assert.That((int)json["QuestTargetSelectionTimeoutMs"]).IsEqualTo(30000);
        await Assert.That((int)json["QuestProgressObservationMs"]).IsEqualTo(3000);
        await Assert.That((int)json["QuestCompletionObservationMs"]).IsEqualTo(5000);
        await Assert.That((int)json["QuestCompletionRetryBackoffMs"]).IsEqualTo(30000);
    }

    [Test]
    public async Task Validate_QuestIntakeBoundsAndNonfiniteValues_FailClosedToSafeDefaults()
    {
        var config = new BotConfig
        {
            QuestIntakeScanRadius = double.NaN,
            QuestIntakeInteractionRadius = double.PositiveInfinity,
            QuestIntakeRetryBackoffMs = -1
        };

        config.Validate();

        await Assert.That(config.QuestIntakeScanRadius).IsEqualTo(60d);
        await Assert.That(config.QuestIntakeInteractionRadius).IsEqualTo(6d);
        await Assert.That(config.QuestIntakeRetryBackoffMs).IsEqualTo(1000);

        config.QuestIntakeScanRadius = 2;
        config.QuestIntakeInteractionRadius = 9;
        config.QuestIntakeRetryBackoffMs = 700000;
        config.Validate();

        await Assert.That(config.QuestIntakeScanRadius).IsEqualTo(2d);
        await Assert.That(config.QuestIntakeInteractionRadius).IsEqualTo(2d);
        await Assert.That(config.QuestIntakeRetryBackoffMs).IsEqualTo(600000);
    }

    [Test]
    public async Task Validate_QuestCompletionBoundsAndNonfiniteValues_FailClosedToSafeDefaults()
    {
        var config = new BotConfig
        {
            QuestObjectiveScanRadius = double.NaN,
            QuestReportScanRadius = double.PositiveInfinity,
            QuestReportInteractionRadius = double.NaN,
            QuestTargetSelectionTimeoutMs = -1,
            QuestProgressObservationMs = -1,
            QuestCompletionObservationMs = 70000,
            QuestCompletionRetryBackoffMs = 700000
        };

        config.Validate();

        await Assert.That(config.QuestObjectiveScanRadius).IsEqualTo(60d);
        await Assert.That(config.QuestReportScanRadius).IsEqualTo(60d);
        await Assert.That(config.QuestReportInteractionRadius).IsEqualTo(6d);
        await Assert.That(config.QuestTargetSelectionTimeoutMs).IsEqualTo(1000);
        await Assert.That(config.QuestProgressObservationMs).IsEqualTo(100);
        await Assert.That(config.QuestCompletionObservationMs).IsEqualTo(60000);
        await Assert.That(config.QuestCompletionRetryBackoffMs).IsEqualTo(600000);

        config.QuestObjectiveScanRadius = 500;
        config.QuestReportScanRadius = 2;
        config.QuestReportInteractionRadius = 9;
        config.Validate();

        await Assert.That(config.QuestObjectiveScanRadius).IsEqualTo(100d);
        await Assert.That(config.QuestReportScanRadius).IsEqualTo(2d);
        await Assert.That(config.QuestReportInteractionRadius).IsEqualTo(2d);
    }

    [Test]
    public async Task TryParseAutoSpawnState_AcceptsTheBotstateAliases()
    {
        foreach (var alias in new[] { "grind", "grinding", "quest", "questing", "roam", "roaming", "follow", "following", "rest", "resting" })
            await Assert.That(BotConfig.TryParseAutoSpawnState(alias, out _)).IsTrue();
        await Assert.That(BotConfig.TryParseAutoSpawnState("grinding", out var parsed)).IsTrue();
        await Assert.That(parsed == BotCombatStateType.Grinding).IsTrue();
        await Assert.That(BotConfig.TryParseAutoSpawnState("free", out var free)).IsTrue();
        await Assert.That(free == null).IsTrue();
        await Assert.That(BotConfig.TryParseAutoSpawnState("not-a-state", out _)).IsFalse();
    }

    [Test]
    public async Task Validate_NegativeSearchRadius_ClampsToZero()
    {
        var config = new BotConfig { SearchRadius = -5 };

        config.Validate();

        await Assert.That(config.SearchRadius).IsEqualTo(0);
    }

    [Test]
    public async Task Validate_RestHealIntervalZero_ClampsToMinimum()
    {
        var config = new BotConfig { RestHealInterval = 0 };

        config.Validate();

        await Assert.That(config.RestHealInterval).IsEqualTo(0.1);
    }

    [Test]
    public async Task Validate_NegativeRestThreshold_ClampsToZero()
    {
        var config = new BotConfig { RestThresholdPercent = -5 };

        config.Validate();

        await Assert.That(config.RestThresholdPercent).IsEqualTo(0);
    }

    [Test]
    public async Task Validate_RestThresholdAbove100_ClampsTo100()
    {
        var config = new BotConfig { RestThresholdPercent = 125 };

        config.Validate();

        await Assert.That(config.RestThresholdPercent).IsEqualTo(100);
    }

    [Test]
    public async Task Validate_NegativeRespawnDelay_ClampsToZero()
    {
        var config = new BotConfig { RespawnDelaySeconds = -5 };

        config.Validate();

        await Assert.That(config.RespawnDelaySeconds).IsEqualTo(0);
    }

    [Test]
    public async Task Validate_NegativeIdleStanceDelay_ClampsToZero()
    {
        var config = new BotConfig { IdleStanceDelaySeconds = -5 };

        config.Validate();

        await Assert.That(config.IdleStanceDelaySeconds).IsEqualTo(0);
    }

    [Test]
    public async Task Validate_NewTimingAndActivityFields_ClampsToSafeRanges()
    {
        var config = new BotConfig
        {
            ReactDelayCombatMs = -1,
            ReactDelayMovingMs = -1,
            ReactDelayIdleMinMs = 300,
            ReactDelayIdleMaxMs = -1,
            ReactDelayRestingMs = -1,
            PassiveDelayMs = -1,
            GroundCheckIntervalMs = -1,
            ActivityPercent = 125,
            ActivityWindowMs = -1,
            ActivityRealPlayerRadius = -1,
            HostTickBudgetMs = -1,
            ServerTickBudgetMs = -1,
            ScanTtlMs = -1,
            RealPlayerScanTtlMs = -1,
            MetricsLogIntervalMs = -1
        };

        config.Validate();

        await Assert.That(config.ReactDelayCombatMs).IsEqualTo(0d);
        await Assert.That(config.ReactDelayMovingMs).IsEqualTo(0d);
        await Assert.That(config.ReactDelayIdleMinMs).IsEqualTo(300d);
        await Assert.That(config.ReactDelayIdleMaxMs).IsEqualTo(300d);
        await Assert.That(config.ReactDelayRestingMs).IsEqualTo(0d);
        await Assert.That(config.PassiveDelayMs).IsEqualTo(0d);
        await Assert.That(config.GroundCheckIntervalMs).IsEqualTo(0d);
        await Assert.That(config.ActivityPercent).IsEqualTo(100d);
        await Assert.That(config.ActivityWindowMs).IsEqualTo(0d);
        await Assert.That(config.ActivityRealPlayerRadius).IsEqualTo(0d);
        await Assert.That(config.HostTickBudgetMs).IsEqualTo(0d);
        await Assert.That(config.ServerTickBudgetMs).IsEqualTo(0d);
        await Assert.That(config.ScanTtlMs).IsEqualTo(0d);
        await Assert.That(config.RealPlayerScanTtlMs).IsEqualTo(0d);
        await Assert.That(config.MetricsLogIntervalMs).IsEqualTo(0d);
    }

    [Test]
    public async Task Validate_AutoSpawnFields_UsesSafeBounds()
    {
        var config = new BotConfig
        {
            AutoSpawnCharacterIds = null,
            AutoSpawnState = "not-a-state",
            AutoSpawnDelayMs = 70000
        };

        config.Validate();

        await Assert.That(config.AutoSpawnCharacterIds).IsEmpty();
        await Assert.That(config.AutoSpawnState).IsEqualTo("idle");
        await Assert.That(config.AutoSpawnDelayMs).IsEqualTo(60000);
    }

    [Test]
    public async Task Validate_ActivityDirectorTimings_AreBoundedAndNullRosterBecomesEmpty()
    {
        var config = new BotConfig
        {
            ActivityDirectorCharacterIds = null,
            ActivityDirectorInitialDelayMs = 300001,
            ActivityDirectorReconciliationIntervalMs = 0,
            ActivityDirectorRetryBackoffMs = 700000
        };

        config.Validate();

        await Assert.That(config.ActivityDirectorCharacterIds).IsEmpty();
        await Assert.That(config.ActivityDirectorInitialDelayMs).IsEqualTo(300000);
        await Assert.That(config.ActivityDirectorReconciliationIntervalMs).IsEqualTo(100);
        await Assert.That(config.ActivityDirectorRetryBackoffMs).IsEqualTo(600000);
    }

    [Test]
    [Arguments(-1, 0)]
    [Arguments(60000, 60000)]
    [Arguments(180000, 180000)]
    [Arguments(300000, 300000)]
    [Arguments(300001, 300000)]
    public async Task Validate_ActivityDirectorInitialDelay_ClampsToSupportedRange(int delay, int expected)
    {
        var config = new BotConfig { ActivityDirectorInitialDelayMs = delay };

        config.Validate();

        await Assert.That(config.ActivityDirectorInitialDelayMs).IsEqualTo(expected);
    }

    [Test]
    public async Task ActivityDirectorConfiguration_RuntimeConversionCarriesWidenedInitialDelay()
    {
        var config = new BotConfig { ActivityDirectorInitialDelayMs = 180000 };

        var result = config.GetActivityDirectorConfiguration();

        await Assert.That(result.InitialDelay).IsEqualTo(TimeSpan.FromMilliseconds(180000));
    }

    [Test]
    public async Task ActivityDirectorConfiguration_DisabledDefaultsAreValidAndFailClosed()
    {
        var result = new BotConfig().GetActivityDirectorConfiguration();

        await Assert.That(result.Enabled).IsFalse();
        await Assert.That(result.Valid).IsTrue();
        await Assert.That(result.Reason).IsEqualTo("disabled");
        await Assert.That(result.CharacterIds).IsEmpty();
    }

    [Test]
    public async Task ActivityDirectorConfiguration_EveryRequiredInvalidShapeHasStableReason()
    {
        var invalid = new (BotConfig Config, string Reason)[]
        {
            (DirectorConfig(zoneId: 0), "zone_zero"),
            (DirectorConfig(characterIds: []), "identities_empty"),
            (DirectorConfig(characterIds: [7, 0]), "identity_zero"),
            (DirectorConfig(characterIds: [7, 7]), "identity_duplicate"),
            (DirectorConfig(minimum: -1), "population_negative"),
            (DirectorConfig(minimum: 2, target: 1), "population_order_invalid"),
            (DirectorConfig(target: 3, maximum: 2), "population_order_invalid"),
            (DirectorConfig(characterIds: [7, 8], maximum: 3), "maximum_exceeds_identities")
        };

        foreach (var (config, reason) in invalid)
        {
            var result = config.GetActivityDirectorConfiguration();
            await Assert.That(result.Enabled).IsTrue();
            await Assert.That(result.Valid).IsFalse();
            await Assert.That(result.Reason).IsEqualTo(reason);
        }
    }

    [Test]
    public async Task ActivityDirectorConfiguration_ValidRosterPreservesOrderAndBounds()
    {
        var result = DirectorConfig(characterIds: [9, 7, 8], minimum: 1, target: 2, maximum: 3)
            .GetActivityDirectorConfiguration();

        await Assert.That(result.Valid).IsTrue();
        await Assert.That(result.Reason).IsEqualTo("valid");
        await Assert.That(string.Join(",", result.CharacterIds)).IsEqualTo("9,7,8");
        await Assert.That(result.InitialDelay).IsEqualTo(TimeSpan.FromSeconds(2));
        await Assert.That(result.ReconciliationInterval).IsEqualTo(TimeSpan.FromSeconds(5));
        await Assert.That(result.RetryBackoff).IsEqualTo(TimeSpan.FromSeconds(30));
    }

    private static BotConfig DirectorConfig(
        uint zoneId = 137,
        List<uint> characterIds = null,
        int minimum = 1,
        int target = 1,
        int maximum = 2) =>
        new()
        {
            ActivityDirectorEnabled = true,
            ActivityDirectorZoneId = zoneId,
            ActivityDirectorCharacterIds = characterIds ?? [7, 8],
            ActivityDirectorMinimumPopulation = minimum,
            ActivityDirectorTargetPopulation = target,
            ActivityDirectorMaximumPopulation = maximum
        };
}
