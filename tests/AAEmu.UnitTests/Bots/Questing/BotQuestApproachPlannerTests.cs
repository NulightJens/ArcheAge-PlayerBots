using System.Numerics;
using AAEmu.Game.Bots.Questing;

namespace AAEmu.UnitTests.Bots.Questing;

public sealed class BotQuestApproachPlannerTests
{
    [Test]
    public async Task SurfaceAlignedObjectKeepsItsExactDestination()
    {
        var target = new Vector3(10, 0, 100);

        var result = BotQuestApproachPlanner.ForWorldObject(
            Vector3.Zero,
            target,
            6,
            (_, _) => 100);

        await Assert.That(result).IsEqualTo(target);
    }

    [Test]
    public async Task OffSurfaceObjectUsesGroundedStandOffWithinInteractionRadius()
    {
        var bot = new Vector3(20, 0, 100);
        var target = new Vector3(10, 0, 104);

        var result = BotQuestApproachPlanner.ForWorldObject(
            bot,
            target,
            6,
            (_, _) => 100);

        await Assert.That(result).IsNotEqualTo(target);
        await Assert.That(result.Z).IsEqualTo(100);
        await Assert.That(Vector3.Distance(result, target)).IsLessThanOrEqualTo(5.5f);
        await Assert.That(result.X).IsGreaterThan(target.X);
    }
}
