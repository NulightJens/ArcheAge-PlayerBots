using AAEmu.Game.Bots.Host;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game.Bots;

namespace AAEmu.UnitTests.Bots.Host;

public class BotScaleMetricsEnvelopeTests
{
    [Test]
    public async Task Capture_LabelsLiveProvenanceAndIncludesBothMetricPlanes()
    {
        var botMetrics = new BotHostMetrics();
        var serverMetrics = new ServerTickMetrics();
        var config = new BotConfig { ActivityPercent = 25, ServerTickBudgetMs = 12.5 };
        botMetrics.RecordHostTick(2, 10, 3, 25, 20);
        serverMetrics.RecordTick(7, 27);

        var snapshot = BotScaleMetricsEnvelope.Capture(botMetrics, serverMetrics, config, 10);

        await Assert.That(snapshot.SchemaVersion).IsEqualTo(BotScaleMetricsEnvelope.CurrentSchemaVersion);
        await Assert.That(snapshot.Provenance).IsEqualTo("live-server");
        await Assert.That(snapshot.RuntimeCount).IsEqualTo(10);
        await Assert.That(snapshot.Config.ActivityPercent).IsEqualTo(25d);
        await Assert.That(snapshot.Config.ServerTickBudgetMs).IsEqualTo(12.5d);
        await Assert.That(snapshot.Server.Work.Count).IsEqualTo(1L);
        await Assert.That(snapshot.Bots.HostTick.Count).IsEqualTo(1L);
    }
}
