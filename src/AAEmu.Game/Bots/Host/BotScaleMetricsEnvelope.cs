using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game.Bots;

namespace AAEmu.Game.Bots.Host;

/// <summary>
/// Versioned operator snapshot consumed by the PlayerBots scale harness. This is live-server
/// evidence; the harness adds its external CPU, database, build and scenario samples.
/// </summary>
public sealed record BotScaleMetricsEnvelope(
    string SchemaVersion,
    string Provenance,
    DateTime CapturedAtUtc,
    int RuntimeCount,
    BotScaleConfigSnapshot Config,
    ServerTickMetricsSnapshot Server,
    BotHostMetricsSnapshot Bots)
{
    public const string CurrentSchemaVersion = "t021.scale-metrics.v1";

    public static BotScaleMetricsEnvelope Capture(
        BotHostMetrics botMetrics,
        ServerTickMetrics serverMetrics,
        BotConfig config,
        int runtimeCount)
    {
        return new BotScaleMetricsEnvelope(
            CurrentSchemaVersion,
            "live-server",
            DateTime.UtcNow,
            runtimeCount,
            new BotScaleConfigSnapshot(
                config.ActivityPercent,
                config.ActivityWindowMs,
                config.ActivityRealPlayerRadius,
                config.HostTickBudgetMs,
                config.ServerTickBudgetMs,
                config.ScanTtlMs,
                config.HazardScanTtlMs,
                config.RealPlayerScanTtlMs),
            serverMetrics.Snapshot(),
            botMetrics.Snapshot());
    }
}

public sealed record BotScaleConfigSnapshot(
    double ActivityPercent,
    double ActivityWindowMs,
    double ActivityRealPlayerRadius,
    double HostTickBudgetMs,
    double ServerTickBudgetMs,
    double ScanTtlMs,
    double HazardScanTtlMs,
    double RealPlayerScanTtlMs);
