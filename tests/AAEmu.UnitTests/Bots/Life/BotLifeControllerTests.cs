using System.Numerics;
using AAEmu.Game.Bots.Blackboard;
using AAEmu.Game.Bots.Life;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Tasks.Bots;
using AAEmu.UnitTests.Utils.Mocks;
using Microsoft.Extensions.Time.Testing;

namespace AAEmu.UnitTests.Bots.Life;

public sealed class BotLifeControllerTests
{
    [Test]
    public async Task SoleFreeIdleBot_WithOpportunity_ActivatesOneKillGrindingWithoutSelectingWork()
    {
        var fixture = MakeFixture();
        var before = fixture.Time.GetUtcNow();

        var logoutRequested = fixture.Runtime.LifeController.Step(fixture.Runtime, true, before);
        fixture.Runtime.LifeController.Step(fixture.Runtime, true, before.AddMilliseconds(1));

        var life = fixture.Runtime.LifeController.Inspect();
        await Assert.That(logoutRequested).IsFalse();
        await Assert.That(life.Life.State).IsEqualTo(BotLifeState.Active);
        await Assert.That(life.Activity).IsEqualTo("grind");
        await Assert.That(life.DecisionReason).IsEqualTo("nearby_mortal");
        await Assert.That(life.DecisionAt).IsEqualTo(before);
        await Assert.That(life.LastTransition?.Event.Kind).IsEqualTo(BotLifeEventKind.ActivityRequested);
        await Assert.That(life.LastTransition?.Outcome).IsEqualTo(BotLifeTransitionOutcome.Accepted);
        await Assert.That(fixture.Runtime.CombatState.CurrentState).IsEqualTo(BotCombatStateType.Grinding);
        await Assert.That(fixture.Runtime.CombatState.IsActive).IsTrue();
        await Assert.That(fixture.Runtime.CombatState.KillGoal).IsEqualTo(1);
        await Assert.That(fixture.Runtime.CombatState.KillCount).IsEqualTo(0);
        await Assert.That(fixture.Runtime.CombatState.ForcedState).IsNull();
        await Assert.That(fixture.Runtime.CombatState.Target).IsNull();
        await Assert.That(fixture.Runtime.Bot.CurrentTarget).IsNull();
        await Assert.That(fixture.Runtime.MovementState.Destination).IsNull();
    }

    [Test]
    public async Task EligibilityGuards_FailClosedWithoutMutatingCombatOrLifeState()
    {
        var multiple = MakeFixture();
        multiple.Runtime.LifeController.Step(multiple.Runtime, false, multiple.Time.GetUtcNow());

        var forced = MakeFixture();
        forced.Runtime.CombatState.ForcedState = BotCombatStateType.Grinding;
        forced.Runtime.LifeController.Step(forced.Runtime, true, forced.Time.GetUtcNow());

        var dead = MakeFixture();
        dead.Runtime.Bot.Hp = 0;
        dead.Runtime.LifeController.Step(dead.Runtime, true, dead.Time.GetUtcNow());

        var missingWorld = MakeFixture();
        BotTestFixture.SetPrivateField<AAEmu.Game.Models.Game.World.WorldInstance>(
            missingWorld.Runtime.Bot,
            "_parentWorld",
            null);
        missingWorld.Runtime.LifeController.Step(missingWorld.Runtime, true, missingWorld.Time.GetUtcNow());

        var noOpportunity = MakeFixture(hasOpportunity: false);
        noOpportunity.Runtime.LifeController.Step(noOpportunity.Runtime, true, noOpportunity.Time.GetUtcNow());

        var moving = MakeFixture();
        moving.Runtime.MovementState.Destination = Vector3.One;
        moving.Runtime.LifeController.Step(moving.Runtime, true, moving.Time.GetUtcNow());

        foreach (var fixture in new[] { multiple, forced, dead, missingWorld, noOpportunity, moving })
        {
            var life = fixture.Runtime.LifeController.Inspect();
            await Assert.That(life.Life.State).IsEqualTo(BotLifeState.Idle);
            await Assert.That(life.Activity).IsNull();
            await Assert.That(life.LastTransition).IsNull();
            await Assert.That(fixture.Runtime.CombatState.CurrentState).IsEqualTo(BotCombatStateType.Idle);
            await Assert.That(fixture.Runtime.CombatState.IsActive).IsFalse();
            await Assert.That(fixture.Runtime.CombatState.KillGoal).IsNull();
            await Assert.That(fixture.Runtime.CombatState.Target).IsNull();
        }
        await Assert.That(moving.Runtime.MovementState.Destination).IsEqualTo(Vector3.One);
    }

    [Test]
    public async Task CreditedKill_WaitsForTargetlessIdleBoundary_AndQueuesLogoutOnlyOnce()
    {
        var fixture = MakeFixture();
        var now = fixture.Time.GetUtcNow();
        fixture.Runtime.LifeController.Step(fixture.Runtime, true, now);
        fixture.Runtime.CombatState.KillCount = 1;

        var retainedTarget = new Npc { ObjId = 9002, Hp = 100, MaxHp = 100 };
        fixture.Runtime.CombatState.Target = retainedTarget;
        fixture.Runtime.Bot.CurrentTarget = retainedTarget;
        fixture.Runtime.CombatState.CurrentState = BotCombatStateType.Combat;
        var whileCombat = fixture.Runtime.LifeController.Step(fixture.Runtime, true, now.AddSeconds(1));
        fixture.Runtime.CombatState.TransitionTo(BotCombatStateType.Idle);
        var whileTargeted = fixture.Runtime.LifeController.Step(fixture.Runtime, true, now.AddSeconds(2));
        retainedTarget.Hp = 0;
        var first = fixture.Runtime.LifeController.Step(fixture.Runtime, true, now.AddSeconds(3));
        var duplicate = fixture.Runtime.LifeController.Step(fixture.Runtime, true, now.AddSeconds(4));

        var pending = fixture.Runtime.LifeController.Inspect();
        await Assert.That(whileCombat).IsFalse();
        await Assert.That(whileTargeted).IsFalse();
        await Assert.That(first).IsTrue();
        await Assert.That(duplicate).IsFalse();
        await Assert.That(fixture.Runtime.CombatState.Target).IsNull();
        await Assert.That(fixture.Runtime.Bot.CurrentTarget).IsNull();
        await Assert.That(pending.Life.State).IsEqualTo(BotLifeState.Despawning);
        await Assert.That(pending.LastTransition?.Event.Kind).IsEqualTo(BotLifeEventKind.LogoutRequested);
        await Assert.That(pending.LogoutRequestedAt).IsEqualTo(now.AddSeconds(3));

        await Assert.That(fixture.Runtime.LifeController.TryBeginLogoutCallback(now.AddSeconds(3))).IsTrue();
        await Assert.That(fixture.Runtime.LifeController.TryBeginLogoutCallback(now.AddSeconds(3))).IsFalse();
        fixture.Runtime.LifeController.RecordLogoutResult(fixture.Runtime.Bot.Id, true, now.AddSeconds(3));

        var completed = fixture.Runtime.LifeController.Inspect();
        await Assert.That(completed.Life.State).IsEqualTo(BotLifeState.Offline);
        await Assert.That(completed.LogoutSucceeded).IsTrue();
        await Assert.That(completed.LastTransition?.Event.Kind).IsEqualTo(BotLifeEventKind.DespawnCompleted);
    }

    private static Fixture MakeFixture(bool hasOpportunity = true, BotLifeController controller = null)
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero));
        var bot = BotTestFixture.MakeBot(63, Vector3.Zero);
        bot.Hp = 100;
        bot.MaxHp = 100;
        var world = BotTestFixture.MakeWorld();
        BotTestFixture.SetPrivateField(bot, "_parentWorld", world);
        var movement = new BotMovementState();
        var combat = new BotCombatState();
        var blackboard = new BotBlackboard();
        blackboard.Register(
            BotValues.NearbyHostileNpcIds,
            new ManualValue<List<uint>>(hasOpportunity ? [9001u] : []));
        if (hasOpportunity)
            world.AddObject(new Npc { ObjId = 9001, Hp = 100, MaxHp = 100 });
        var broadcaster = new BotMovementBroadcaster(bot, time);
        var mover = new BotMovementTask(bot, movement, broadcaster);
        var brain = new BotCombatTask(
            bot,
            combat,
            broadcaster,
            onCancel: null,
            blackboard: blackboard,
            timeProvider: time);
        var runtime = new BotRuntime(
            bot,
            movement,
            combat,
            broadcaster,
            mover,
            brain,
            blackboard,
            new BotConfig { UseEngine = false },
            controller);
        runtime.LifeController.ResetPostSpawn(bot.Id, time.GetUtcNow());
        return new Fixture(time, runtime);
    }

    private sealed record Fixture(FakeTimeProvider Time, BotRuntime Runtime);
}
