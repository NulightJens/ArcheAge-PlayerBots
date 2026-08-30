using System.Numerics;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.UnitTests.Utils.Mocks;

namespace AAEmu.UnitTests.Bots.Host;

[NotInParallel]
public class BotSchedulerTests
{
    [Test]
    public async Task Classify_UsesCombatStateBeforeMovement()
    {
        var runtime = MakeRuntime(2);
        runtime.CombatState.CurrentState = BotCombatStateType.Combat;
        runtime.MovementState.Destination = Vector3.One;

        await Assert.That(BotScheduler.Classify(runtime)).IsEqualTo(BotCadence.Combat);
    }

    [Test]
    public async Task Classify_MovementAndFollowAreMoving()
    {
        var destinationRuntime = MakeRuntime(3);
        destinationRuntime.MovementState.Destination = Vector3.One;
        var followRuntime = MakeRuntime(4);
        followRuntime.MovementState.FollowTarget = BotTestFixture.MakeBot(5, Vector3.Zero);

        await Assert.That(BotScheduler.Classify(destinationRuntime)).IsEqualTo(BotCadence.Moving);
        await Assert.That(BotScheduler.Classify(followRuntime)).IsEqualTo(BotCadence.Moving);
    }

    [Test]
    public async Task Classify_RestingAndIdleUseTheirBranches()
    {
        var restingRuntime = MakeRuntime(6);
        restingRuntime.CombatState.CurrentState = BotCombatStateType.Resting;
        var idleRuntime = MakeRuntime(7);

        await Assert.That(BotScheduler.Classify(restingRuntime)).IsEqualTo(BotCadence.Resting);
        await Assert.That(BotScheduler.Classify(idleRuntime)).IsEqualTo(BotCadence.Idle);
    }

    [Test]
    public async Task NextDelay_UsesConfiguredCadenceDelays()
    {
        var config = BotConfig.Instance;

        await Assert.That(BotScheduler.NextDelay(BotCadence.Combat, 2, 0)).IsEqualTo(TimeSpan.FromMilliseconds(config.ReactDelayCombatMs));
        await Assert.That(BotScheduler.NextDelay(BotCadence.Moving, 2, 0)).IsEqualTo(TimeSpan.FromMilliseconds(config.ReactDelayMovingMs));
        await Assert.That(BotScheduler.NextDelay(BotCadence.Resting, 2, 0)).IsEqualTo(TimeSpan.FromMilliseconds(config.ReactDelayRestingMs));
        await Assert.That(BotScheduler.NextDelay(BotCadence.Inactive, 2, 0)).IsEqualTo(TimeSpan.FromMilliseconds(config.PassiveDelayMs));
    }

    [Test]
    public async Task NextDelay_IdleIsInclusiveAndDeterministic()
    {
        var config = BotConfig.Instance;
        var min = BotScheduler.NextDelay(BotCadence.Idle, 9, 0);
        var max = BotScheduler.NextDelay(BotCadence.Idle, 9, int.MaxValue);
        var repeat = BotScheduler.NextDelay(BotCadence.Idle, 9, 123456);

        await Assert.That(min).IsGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(config.ReactDelayIdleMinMs));
        await Assert.That(min).IsLessThanOrEqualTo(TimeSpan.FromMilliseconds(config.ReactDelayIdleMaxMs));
        await Assert.That(max).IsGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(config.ReactDelayIdleMinMs));
        await Assert.That(max).IsLessThanOrEqualTo(TimeSpan.FromMilliseconds(config.ReactDelayIdleMaxMs));
        await Assert.That(repeat).IsEqualTo(BotScheduler.NextDelay(BotCadence.Idle, 9, 123456));
    }

    [Test]
    public async Task InitialStagger_IsTenMillisecondsPerFinalBotIdDigit()
    {
        await Assert.That(BotScheduler.InitialStagger(0)).IsEqualTo(TimeSpan.Zero);
        await Assert.That(BotScheduler.InitialStagger(7)).IsEqualTo(TimeSpan.FromMilliseconds(70));
        await Assert.That(BotScheduler.InitialStagger(19)).IsEqualTo(TimeSpan.FromMilliseconds(90));
    }

    private static BotRuntime MakeRuntime(uint id)
    {
        return new BotRuntime(BotTestFixture.MakeBot(id, Vector3.Zero), new BotMovementState(), new BotCombatState());
    }
}
