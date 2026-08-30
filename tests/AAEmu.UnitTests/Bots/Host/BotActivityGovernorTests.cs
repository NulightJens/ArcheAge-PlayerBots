using System.Numerics;
using AAEmu.Game.Bots.Blackboard;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.UnitTests.Utils.Mocks;

namespace AAEmu.UnitTests.Bots.Host;

[NotInParallel]
public class BotActivityGovernorTests
{
    [Test]
    public async Task IsAlwaysActive_CombatStatesAreAlwaysActive()
    {
        foreach (var state in new[] { BotCombatStateType.Combat, BotCombatStateType.Dueling, BotCombatStateType.Searching })
        {
            var runtime = MakeRuntime(2);
            runtime.CombatState.CurrentState = state;

            await Assert.That(BotActivityGovernor.IsAlwaysActive(runtime)).IsTrue();
        }
    }

    [Test]
    public async Task IsAlwaysActive_FollowForcedAndNearbyRealPlayerAreAlwaysActive()
    {
        var follow = MakeRuntime(3);
        follow.MovementState.FollowTarget = BotTestFixture.MakeBot(4, Vector3.Zero);
        var forced = MakeRuntime(5);
        forced.CombatState.ForcedState = BotCombatStateType.Grinding;
        var nearby = MakeRuntime(6);
        nearby.Blackboard.Register(BotValues.NearestRealPlayerDistance, new ManualValue<float>(10));

        await Assert.That(BotActivityGovernor.IsAlwaysActive(follow)).IsTrue();
        await Assert.That(BotActivityGovernor.IsAlwaysActive(forced)).IsTrue();
        await Assert.That(BotActivityGovernor.IsAlwaysActive(nearby)).IsTrue();
    }

    [Test]
    public async Task IsInRotation_UsesStableWindowHashAndExpectedDistribution()
    {
        var active = Enumerable.Range(1, 1000).Count(id => BotActivityGovernor.IsInRotation((uint)id, 12, 10));
        var sameWindow = BotActivityGovernor.IsInRotation(42, 12, 10);
        var changedId = Enumerable.Range(1, 1000).First(id =>
            Enumerable.Range(13, 100).Any(window =>
                BotActivityGovernor.IsInRotation((uint)id, 12, 10) !=
                BotActivityGovernor.IsInRotation((uint)id, window, 10)));
        var changedWindow = Enumerable.Range(13, 100).First(window =>
            BotActivityGovernor.IsInRotation((uint)changedId, 12, 10) !=
            BotActivityGovernor.IsInRotation((uint)changedId, window, 10));

        await Assert.That(active).IsBetween(70, 130);
        await Assert.That(sameWindow).IsEqualTo(BotActivityGovernor.IsInRotation(42, 12, 10));
        await Assert.That(BotActivityGovernor.IsInRotation((uint)changedId, changedWindow, 10))
            .IsNotEqualTo(BotActivityGovernor.IsInRotation((uint)changedId, 12, 10));
    }

    [Test]
    public async Task EffectiveActivePercent_ScalesToBudgetAndClamps()
    {
        await Assert.That(BotActivityGovernor.EffectiveActivePercent(80, 15, 30)).IsEqualTo(80);
        await Assert.That(BotActivityGovernor.EffectiveActivePercent(80, 30, 30)).IsEqualTo(80);
        await Assert.That(BotActivityGovernor.EffectiveActivePercent(80, 45, 30)).IsEqualTo(40);
        await Assert.That(BotActivityGovernor.EffectiveActivePercent(80, 60, 30)).IsEqualTo(0);
        await Assert.That(BotActivityGovernor.EffectiveActivePercent(80, 90, 30)).IsEqualTo(0);
    }

    [Test]
    public async Task EffectiveActivePercent_UsesMostRestrictiveHostOrWholeServerPressure()
    {
        await Assert.That(BotActivityGovernor.EffectiveActivePercent(80, 15, 30, 15, 30)).IsEqualTo(80);
        await Assert.That(BotActivityGovernor.EffectiveActivePercent(80, 15, 30, 45, 30)).IsEqualTo(40);
        await Assert.That(BotActivityGovernor.EffectiveActivePercent(80, 45, 30, 15, 30)).IsEqualTo(40);
        await Assert.That(BotActivityGovernor.EffectiveActivePercent(80, 15, 30, 60, 30)).IsEqualTo(0);
        await Assert.That(BotActivityGovernor.EffectiveActivePercent(80, 15, 30, 90, 0)).IsEqualTo(80);
    }

    private static BotRuntime MakeRuntime(uint id)
    {
        return new BotRuntime(BotTestFixture.MakeBot(id, Vector3.Zero), new BotMovementState(), new BotCombatState());
    }
}
