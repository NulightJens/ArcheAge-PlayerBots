using System.Numerics;
using System.Runtime.CompilerServices;
using AAEmu.Game.Bots.Blackboard;
using AAEmu.Game.Bots.Life;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.Items.Containers;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Tasks.Bots;
using AAEmu.UnitTests.Utils.Mocks;
using Microsoft.Extensions.Time.Testing;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace AAEmu.UnitTests.Bots.Life;

[NotInParallel]
public sealed class BotLifeControllerTests
{
    [Test]
    public async Task ProgressionObservation_CapturesExactDeltasAndStableInventoryFingerprint()
    {
        var fixture = MakeFixture();
        var bot = fixture.Runtime.Bot;
        bot.Level = 10;
        SetExperience(bot, 1200);
        bot.Hp = 80;
        bot.Mp = 35;
        SetMaxResources(bot, maxHp: 100, maxMp: 80);
        var bag = SetBag(
            bot,
            MakeItem(slot: 4, templateId: 200, count: 3),
            MakeItem(slot: 1, templateId: 100, count: 2));
        var activatedAt = fixture.Time.GetUtcNow();

        fixture.Runtime.LifeController.Step(fixture.Runtime, true, activatedAt);

        var active = fixture.Runtime.LifeController.Inspect();
        var baseline = active.ProgressionBaseline;
        await Assert.That(baseline.HasValue).IsTrue();
        await Assert.That(baseline.Value.CapturedAt).IsEqualTo(activatedAt);
        await Assert.That(baseline.Value.Level).IsEqualTo(10L);
        await Assert.That(baseline.Value.Experience).IsEqualTo(1200L);
        await Assert.That(baseline.Value.Hp).IsEqualTo(80L);
        await Assert.That(baseline.Value.MaxHp).IsEqualTo(100L);
        await Assert.That(baseline.Value.Mp).IsEqualTo(35L);
        await Assert.That(baseline.Value.MaxMp).IsEqualTo(80L);
        await Assert.That(baseline.Value.OccupiedBagSlots).IsEqualTo(2L);
        await Assert.That(baseline.Value.BagItemUnits).IsEqualTo(5L);
        await Assert.That(baseline.Value.InventoryAvailable).IsTrue();
        await Assert.That(baseline.Value.InventorySummary).IsEqualTo("1:100:2,4:200:3");
        await Assert.That(baseline.Value.InventoryFingerprint)
            .IsEqualTo("1ef68acdcaef081b6245bffa499708a8b613c2e22a281e627307c7556553f493");
        await Assert.That(active.ProgressionCompletion).IsNull();
        await Assert.That(active.ProgressionDelta).IsNull();

        bot.Level = 11;
        SetExperience(bot, 1325);
        bot.Hp = 95;
        bot.Mp = 60;
        SetMaxResources(bot, maxHp: 110, maxMp: 90);
        bag.Items =
        [
            MakeItem(slot: 2, templateId: 300, count: 1),
            MakeItem(slot: 4, templateId: 200, count: 3),
            MakeItem(slot: 1, templateId: 100, count: 5)
        ];
        fixture.Runtime.CombatState.KillCount = 1;
        fixture.Runtime.CombatState.TransitionTo(BotCombatStateType.Idle);
        var completedAt = activatedAt.AddSeconds(3);

        var requested = fixture.Runtime.LifeController.Step(fixture.Runtime, true, completedAt);

        var completed = fixture.Runtime.LifeController.Inspect();
        var completion = completed.ProgressionCompletion;
        var delta = completed.ProgressionDelta;
        await Assert.That(requested).IsTrue();
        await Assert.That(completion.HasValue).IsTrue();
        await Assert.That(completion.Value.CapturedAt).IsEqualTo(completedAt);
        await Assert.That(completion.Value.Level).IsEqualTo(11L);
        await Assert.That(completion.Value.Experience).IsEqualTo(1325L);
        await Assert.That(completion.Value.Hp).IsEqualTo(95L);
        await Assert.That(completion.Value.MaxHp).IsEqualTo(110L);
        await Assert.That(completion.Value.Mp).IsEqualTo(60L);
        await Assert.That(completion.Value.MaxMp).IsEqualTo(90L);
        await Assert.That(completion.Value.OccupiedBagSlots).IsEqualTo(3L);
        await Assert.That(completion.Value.BagItemUnits).IsEqualTo(9L);
        await Assert.That(completion.Value.InventorySummary).IsEqualTo("1:100:5,2:300:1,4:200:3");
        await Assert.That(completion.Value.InventoryFingerprint)
            .IsEqualTo("ca30b0aaa1de6bad22e19cf00d8b77d611942ef69c049d669eb64f77a07dc84f");
        await Assert.That(delta.HasValue).IsTrue();
        await Assert.That(delta.Value.Level).IsEqualTo(1L);
        await Assert.That(delta.Value.Experience).IsEqualTo(125L);
        await Assert.That(delta.Value.Hp).IsEqualTo(15L);
        await Assert.That(delta.Value.MaxHp).IsEqualTo(10L);
        await Assert.That(delta.Value.Mp).IsEqualTo(25L);
        await Assert.That(delta.Value.MaxMp).IsEqualTo(10L);
        await Assert.That(delta.Value.OccupiedBagSlots).IsEqualTo(1L);
        await Assert.That(delta.Value.BagItemUnits).IsEqualTo(4L);
        await Assert.That(delta.Value.InventoryChanged).IsTrue();
    }

    [Test]
    public async Task ProgressionObservation_NullAndPartialInventoryRemainExplicitlyUnavailable()
    {
        var fixture = MakeFixture();
        var now = fixture.Time.GetUtcNow();

        fixture.Runtime.LifeController.Step(fixture.Runtime, true, now);

        var baseline = fixture.Runtime.LifeController.Inspect().ProgressionBaseline;
        await Assert.That(baseline.HasValue).IsTrue();
        await Assert.That(baseline.Value.InventoryAvailable).IsFalse();
        await Assert.That(baseline.Value.OccupiedBagSlots).IsNull();
        await Assert.That(baseline.Value.BagItemUnits).IsNull();
        await Assert.That(baseline.Value.InventorySummary).IsEqualTo("unavailable");
        await Assert.That(baseline.Value.InventoryFingerprint).IsEqualTo("unavailable");

        SetBag(fixture.Runtime.Bot, (Item)null);
        fixture.Runtime.CombatState.KillCount = 1;
        fixture.Runtime.CombatState.TransitionTo(BotCombatStateType.Idle);

        var requested = fixture.Runtime.LifeController.Step(fixture.Runtime, true, now.AddSeconds(1));

        var completed = fixture.Runtime.LifeController.Inspect();
        await Assert.That(requested).IsTrue();
        await Assert.That(completed.ProgressionCompletion?.InventoryAvailable).IsFalse();
        await Assert.That(completed.ProgressionCompletion?.InventorySummary).IsEqualTo("unavailable");
        await Assert.That(completed.ProgressionDelta?.OccupiedBagSlots).IsNull();
        await Assert.That(completed.ProgressionDelta?.BagItemUnits).IsNull();
        await Assert.That(completed.ProgressionDelta?.InventoryChanged).IsNull();
    }

    [Test]
    public async Task ProgressionObservation_LogsOneStructuredRecordBeforeCallbackAndNeverDuplicates()
    {
        var fixture = MakeFixture();
        var bag = SetBag(fixture.Runtime.Bot, MakeItem(slot: 3, templateId: 400, count: 2));
        using var target = new MemoryTarget { Layout = "${message}" };
        var previousConfiguration = LogManager.Configuration;
        var configuration = new LoggingConfiguration();
        configuration.LoggingRules.Add(new LoggingRule(
            "AAEmu.Game.Bots.Life.BotLifeController",
            LogLevel.Info,
            target));
        LogManager.Configuration = configuration;
        LogManager.ReconfigExistingLoggers();

        try
        {
            var now = fixture.Time.GetUtcNow();
            fixture.Runtime.LifeController.Step(fixture.Runtime, true, now);
            fixture.Runtime.Bot.Hp = 90;
            bag.Items = [MakeItem(slot: 3, templateId: 400, count: 3)];
            fixture.Runtime.CombatState.KillCount = 1;
            fixture.Runtime.CombatState.TransitionTo(BotCombatStateType.Idle);

            var requested = fixture.Runtime.LifeController.Step(fixture.Runtime, true, now.AddSeconds(2));
            var duplicate = fixture.Runtime.LifeController.Step(fixture.Runtime, true, now.AddSeconds(3));
            var callbackStarted = fixture.Runtime.LifeController.TryBeginLogoutCallback(now.AddSeconds(4));
            fixture.Runtime.LifeController.RecordLogoutResult(
                fixture.Runtime.Bot.Id,
                false,
                now.AddSeconds(5));
            fixture.Runtime.LifeController.Step(fixture.Runtime, true, now.AddSeconds(6));

            var records = target.Logs.Where(message => message.Contains("ev=life_progression")).ToArray();
            await Assert.That(requested).IsTrue();
            await Assert.That(duplicate).IsFalse();
            await Assert.That(callbackStarted).IsTrue();
            await Assert.That(records.Length).IsEqualTo(1);
            await Assert.That(records[0]).Contains("id=63 ev=life_progression activity=grind reason=nearby_mortal");
            await Assert.That(records[0]).Contains("baseline_at=2026-09-01T12:00:00.0000000+00:00");
            await Assert.That(records[0]).Contains("completion_at=2026-09-01T12:00:02.0000000+00:00");
            await Assert.That(records[0]).Contains("hp_before=100 hp_after=90 hp_delta=-10");
            await Assert.That(records[0]).Contains("bag_units_before=2 bag_units_after=3 bag_units_delta=+1");
            await Assert.That(records[0]).Contains("inventory_summary_before=3:400:2 inventory_summary_after=3:400:3");
            await Assert.That(records[0]).Contains("inventory_fingerprint_before=");
            await Assert.That(records[0]).Contains("inventory_fingerprint_after=");
        }
        finally
        {
            LogManager.Configuration = previousConfiguration;
            LogManager.ReconfigExistingLoggers();
        }
    }

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
        var firstObservation = fixture.Runtime.LifeController.Inspect();
        fixture.Runtime.Bot.Hp = 77;
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
        await Assert.That(firstObservation.ProgressionCompletion?.Hp).IsEqualTo(100L);
        await Assert.That(pending.ProgressionCompletion).IsEqualTo(firstObservation.ProgressionCompletion);
        await Assert.That(pending.ProgressionDelta).IsEqualTo(firstObservation.ProgressionDelta);

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
        var bot = new ProgressionCharacterMock { Id = 63, ObjId = 1063, Name = "bot63" };
        bot.Transform.Local.SetPosition(Vector3.Zero);
        bot.Hp = 100;
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

    private static void SetExperience(Character bot, int experience) =>
        BotTestFixture.SetPrivateField(bot, "<Experience>k__BackingField", experience);

    private static void SetMaxResources(Character bot, int maxHp, int maxMp)
    {
        var observed = (ProgressionCharacterMock)bot;
        observed.FixedMaxHp = maxHp;
        observed.FixedMaxMp = maxMp;
    }

    private static TestItemContainer SetBag(Character bot, params Item[] items)
    {
        var inventory = (Inventory)RuntimeHelpers.GetUninitializedObject(typeof(Inventory));
        var bag = new TestItemContainer { Items = [.. items] };
        BotTestFixture.SetPrivateField(inventory, "<Bag>k__BackingField", bag);
        bot.Inventory = inventory;
        return bag;
    }

    private static Item MakeItem(int slot, uint templateId, int count)
    {
        var item = (Item)RuntimeHelpers.GetUninitializedObject(typeof(Item));
        item.Slot = slot;
        item.TemplateId = templateId;
        item.Count = count;
        return item;
    }

    private sealed class TestItemContainer : ItemContainer
    {
    }

    private sealed class ProgressionCharacterMock : CharacterMock
    {
        internal int FixedMaxHp { get; set; } = 100;
        internal int FixedMaxMp { get; set; } = 100;

        public override int MaxHp => FixedMaxHp;
        public override int MaxMp => FixedMaxMp;
    }

    private sealed record Fixture(FakeTimeProvider Time, BotRuntime Runtime);
}
