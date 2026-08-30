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
              "AutoSpawnDelayMs": 1234
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
        await Assert.That((double)json["ActivityPercent"]).IsEqualTo(100d);
        await Assert.That((double)json["ServerTickBudgetMs"]).IsEqualTo(0d);
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
    public async Task Validate_AutoSpawnFields_ClampsDelayAndInvalidStateToIdle()
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
    [Arguments(-1)]
    [Arguments(60001)]
    public async Task Validate_AutoSpawnDelay_ClampsToSupportedRange(int delay)
    {
        var config = new BotConfig { AutoSpawnDelayMs = delay };

        config.Validate();

        await Assert.That(config.AutoSpawnDelayMs).IsEqualTo(delay < 0 ? 0 : 60000);
    }
}
