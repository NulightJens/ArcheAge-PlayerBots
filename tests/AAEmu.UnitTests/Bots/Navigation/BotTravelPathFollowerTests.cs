using System.Numerics;
using AAEmu.Game.Bots.Navigation;

namespace AAEmu.UnitTests.Bots.Navigation;

public class BotTravelPathFollowerTests
{
    [Test]
    public async Task SelectSteeringTarget_StraightRouteLooksPastImmediateWaypoint()
    {
        var result = BotTravelPathFollower.SelectSteeringTarget(
            Vector3.Zero,
            new Vector3(1f, 0f, 0f),
            new Queue<Vector3>([new Vector3(2f, 0f, 0f), new Vector3(3f, 0f, 0f)]),
            speed: 0f);

        await Assert.That(result).IsEqualTo(new Vector3(1.25f, 0f, 0f));
    }

    [Test]
    public async Task SelectSteeringTarget_HairpinDoesNotCutAcrossCorner()
    {
        var corner = new Vector3(1f, 0f, 0f);

        var result = BotTravelPathFollower.SelectSteeringTarget(
            Vector3.Zero,
            corner,
            new Queue<Vector3>([Vector3.Zero]),
            speed: 5.4f);

        await Assert.That(result).IsEqualTo(corner);
    }

    [Test]
    public async Task TurnTowards_NinetyDegreeTurnIsRateLimited()
    {
        var result = BotTravelPathFollower.TurnTowards(Vector3.UnitX, Vector3.UnitY, MathF.PI / 4f);

        await Assert.That(result.X).IsEqualTo(MathF.Sqrt(0.5f)).Within(1e-5f);
        await Assert.That(result.Y).IsEqualTo(MathF.Sqrt(0.5f)).Within(1e-5f);
    }

    [Test]
    public async Task AdvanceSpeed_FromRestAcceleratesWithoutInstantFullSpeed()
    {
        var result = BotTravelPathFollower.AdvanceSpeed(0f, 5.4f, 10f, 0.1f);

        await Assert.That(result).IsEqualTo(1.2f).Within(1e-5f);
    }

    [Test]
    public async Task AdvanceSpeed_NearDestinationAppliesBoundedBraking()
    {
        var result = BotTravelPathFollower.AdvanceSpeed(5.4f, 5.4f, 0.6f, 0.1f);

        await Assert.That(result).IsEqualTo(3.8f).Within(1e-5f);
    }

    [Test]
    public async Task ShouldAdvance_UsesWiderRadiusOnlyForIntermediateWaypoint()
    {
        var current = new Vector3(0.7f, 0f, 0f);

        await Assert.That(BotTravelPathFollower.ShouldAdvance(current, Vector3.Zero, true)).IsTrue();
        await Assert.That(BotTravelPathFollower.ShouldAdvance(current, Vector3.Zero, false)).IsFalse();
    }

    [Test]
    public async Task MeasureRemaining_SumsRouteSegmentsOnce()
    {
        var result = BotTravelPathFollower.MeasureRemaining(
            Vector3.Zero,
            new Vector3(3f, 0f, 0f),
            new Queue<Vector3>([new Vector3(3f, 4f, 0f), new Vector3(6f, 4f, 0f)]));

        await Assert.That(result).IsEqualTo(10f).Within(1e-5f);
    }
}
