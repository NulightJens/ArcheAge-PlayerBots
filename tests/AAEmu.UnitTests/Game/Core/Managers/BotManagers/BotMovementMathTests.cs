using System.Numerics;
using AAEmu.Game.Models.Game.Bots;

namespace AAEmu.UnitTests.Game.Core.Managers.BotManagers;

public class BotMovementMathTests
{
    [Test]
    public async Task ComputeFollowDestination_TargetBeyondDistance_ReturnsPointFollowDistanceShortOfTarget()
    {
        var result = BotMovementMath.ComputeFollowDestination(Vector3.Zero, new Vector3(10, 0, 0), 2f);

        await Assert.That(result.Destination).IsEqualTo(new Vector3(8, 0, 0));
        await Assert.That(result.Run).IsFalse();
    }

    [Test]
    public async Task ComputeFollowDestination_TargetWithinDistancePlusTolerance_ReturnsNull()
    {
        var result = BotMovementMath.ComputeFollowDestination(Vector3.Zero, new Vector3(2.05f, 0, 0), 2f);

        await Assert.That(result.Destination).IsNull();
    }

    [Test]
    public async Task ComputeFollowDestination_TargetAt2_11m_ReturnsDestination()
    {
        var result = BotMovementMath.ComputeFollowDestination(Vector3.Zero, new Vector3(2.11f, 0, 0), 2f);

        await Assert.That(result.Destination).IsNotNull();
    }

    [Test]
    [Arguments(10.5f, true)]
    [Arguments(9f, false)]
    public async Task ComputeFollowDestination_TargetDistance_SelectsRun(float distance, bool expectedRun)
    {
        var result = BotMovementMath.ComputeFollowDestination(Vector3.Zero, new Vector3(distance, 0, 0), 2f);

        await Assert.That(result.Run).IsEqualTo(expectedRun);
    }

    [Test]
    public async Task StepTowards_PartialStep_AdvancesSpeedTimesDt()
    {
        var result = BotMovementMath.StepTowards(Vector3.Zero, new Vector3(10, 0, 0), 5.4f, 0.1f);

        await Assert.That(result.Next.X).IsEqualTo(0.54f).Within(1e-4f);
        await Assert.That(result.Arrived).IsFalse();
    }

    [Test]
    public async Task StepTowards_StepCoversRemaining_SnapsToDestination()
    {
        var destination = new Vector3(0.3f, 0, 0);
        var result = BotMovementMath.StepTowards(Vector3.Zero, destination, 5.4f, 0.1f);

        await Assert.That(result.Next).IsEqualTo(destination);
        await Assert.That(result.Arrived).IsTrue();
    }

    [Test]
    public async Task StepTowards_WithinArrivalRadius_ArrivesWithoutMoving()
    {
        var destination = new Vector3(0.4f, 0, 0);
        var result = BotMovementMath.StepTowards(Vector3.Zero, destination, 5.4f, 0.1f);

        await Assert.That(result.Next).IsEqualTo(destination);
        await Assert.That(result.Arrived).IsTrue();
    }

    [Test]
    public async Task StepTowards_ArrivalRadiusBoundary_0_5_IsNotArrived()
    {
        var result = BotMovementMath.StepTowards(Vector3.Zero, new Vector3(0.5f, 0, 0), 1f, 0.1f);

        await Assert.That(result.Arrived).IsFalse();
    }

    [Test]
    public async Task ComputeFacingDegrees_DueEast_ReturnsMinus90()
    {
        var result = BotMovementMath.ComputeFacingDegrees(Vector3.Zero, Vector3.UnitX);

        await Assert.That(result).IsEqualTo(-90f);
    }

    [Test]
    public async Task ComputeFacingDegrees_DueNorth_ReturnsZero()
    {
        var result = BotMovementMath.ComputeFacingDegrees(Vector3.Zero, Vector3.UnitY);

        await Assert.That(result).IsEqualTo(0f);
    }

    [Test]
    public async Task DirectionalMultiplier_MovingAwayFromTarget_Returns0_65()
    {
        var result = BotMovementMath.DirectionalMultiplier(new Vector3(-1, 0, 0), Vector3.UnitX);

        await Assert.That(result).IsEqualTo(0.65f);
    }

    [Test]
    public async Task DirectionalMultiplier_Perpendicular_Returns1_0()
    {
        var result = BotMovementMath.DirectionalMultiplier(Vector3.UnitY, Vector3.UnitX);

        await Assert.That(result).IsEqualTo(1.0f);
    }

    [Test]
    public async Task DirectionalMultiplier_DotExactlyMinus0_3_ReturnsForward()
    {
        var result = BotMovementMath.DirectionalMultiplier(new Vector3(-0.3f, 0.9539392f, 0), Vector3.UnitX);

        await Assert.That(result).IsEqualTo(1.0f);
    }

    [Test]
    public async Task DirectionalMultiplier_DegenerateDirections_ReturnsForward()
    {
        var result = BotMovementMath.DirectionalMultiplier(Vector3.Zero, Vector3.Zero);

        await Assert.That(result).IsEqualTo(1.0f);
    }

    [Test]
    public async Task ComputeVelocity_CombatMovingAwayFromFacing_ReturnsNegativeForwardTimesSpeed()
    {
        var result = BotMovementMath.ComputeVelocity(new Vector3(0, -1, 0), 0f, 5f, true);

        await Assert.That(result).IsEqualTo(new Vector3(0, -5, 0));
    }

    [Test]
    public async Task ComputeVelocity_CombatDotAboveMinus0_3_ReturnsMoveDirTimesSpeed()
    {
        var result = BotMovementMath.ComputeVelocity(Vector3.UnitX, 0f, 5f, true);

        await Assert.That(result).IsEqualTo(new Vector3(5, 0, 0));
    }

    [Test]
    public async Task ComputeVelocity_NotInCombat_ReturnsMoveDirTimesSpeed()
    {
        var result = BotMovementMath.ComputeVelocity(Vector3.UnitX, 1.2f, 5f, false);

        await Assert.That(result).IsEqualTo(new Vector3(5, 0, 0));
    }

    [Test]
    public async Task ApplyGravity_AboveGround_AcceleratesBy0_981PerTick()
    {
        var result = BotMovementMath.ApplyGravity(10f, 0f, 0f, 0.1f);

        await Assert.That(result.NewFallVelocity).IsEqualTo(0.981f).Within(1e-4f);
        await Assert.That(result.NewZ).IsEqualTo(9.9019f).Within(1e-4f);
        await Assert.That(result.Falling).IsTrue();
    }

    [Test]
    public async Task ApplyGravity_WouldPassGround_LandsAndResetsVelocity()
    {
        var result = BotMovementMath.ApplyGravity(0.05f, 0f, 1f, 0.1f);

        await Assert.That(result.NewZ).IsEqualTo(0f);
        await Assert.That(result.NewFallVelocity).IsEqualTo(0f);
        await Assert.That(result.Landed).IsTrue();
    }

    [Test]
    public async Task ApplyGravity_WithinGroundTolerance_NotFalling()
    {
        var result = BotMovementMath.ApplyGravity(0.1f, 0f, 0f, 0.1f);

        await Assert.That(result.NewZ).IsEqualTo(0.1f);
        await Assert.That(result.Falling).IsFalse();
    }

    [Test]
    public async Task ApplyJump_InitialTick_RisesAndGravityReducesVelocity()
    {
        var result = BotMovementMath.ApplyJump(0f, 0f, 4.5f, 0.1f);

        await Assert.That(result.NewVerticalVelocity).IsEqualTo(3.519f).Within(1e-4f);
        await Assert.That(result.NewZ).IsEqualTo(0.3519f).Within(1e-4f);
        await Assert.That(result.Landed).IsFalse();
    }

    [Test]
    public async Task ApplyJump_DescendingThroughGround_LandsAndResetsVelocity()
    {
        var result = BotMovementMath.ApplyJump(0.05f, 0f, -1f, 0.1f);

        await Assert.That(result.NewZ).IsEqualTo(0f);
        await Assert.That(result.NewVerticalVelocity).IsEqualTo(0f);
        await Assert.That(result.Landed).IsTrue();
    }

    [Test]
    public async Task ApplyJump_AscendingAcrossRisingTerrain_NeverPublishesBelowGround()
    {
        var result = BotMovementMath.ApplyJump(0f, 0.5f, 4.5f, 0.1f);

        await Assert.That(result.NewZ).IsEqualTo(0.5f);
        await Assert.That(result.NewVerticalVelocity).IsGreaterThan(0f);
        await Assert.That(result.Landed).IsFalse();
    }

    [Test]
    public async Task ResolveSpeed_StanceSpeedsCappedByMax()
    {
        var result = BotMovementMath.ResolveSpeed(9f, 3f, 6f);

        await Assert.That(result.Run).IsEqualTo(6f);
        await Assert.That(result.Walk).IsEqualTo(3f);
    }

    [Test]
    public async Task ResolveSpeed_BelowPoint1_UsesFallback()
    {
        var result = BotMovementMath.ResolveSpeed(0.05f, 0f, 6f);

        await Assert.That(result.Run).IsEqualTo(5.4f);
        await Assert.That(result.Walk).IsEqualTo(1.8f);
    }
}
