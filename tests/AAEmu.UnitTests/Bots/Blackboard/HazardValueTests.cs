using System.Collections.Concurrent;
using System.Numerics;
using AAEmu.Game.Bots.Blackboard;
using AAEmu.Game.Bots.Content.Triggers;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.DoodadObj;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.World;
using AAEmu.UnitTests.Utils.Mocks;

namespace AAEmu.UnitTests.Bots.Blackboard;

public class HazardValueTests
{
    private static readonly DateTime Now = new(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task HostileAreaTriggersNearby_ContainsBotAndIgnoresFriendlyTrigger()
    {
        var manager = new AreaTriggerManager();
        var bot = BotTestFixture.MakeBot(1, new Vector3(1, 0, 0));
        var hostile = MakeTrigger(SkillTargetRelation.Hostile, 0);
        var friendly = MakeTrigger(SkillTargetRelation.Friendly, 0);
        manager.AddAreaTrigger(hostile);
        manager.AddAreaTrigger(friendly);
        manager.Tick(TimeSpan.Zero);
        var blackboard = CreateBlackboard(bot, manager, 500);

        var hazards = blackboard.Get(BotValues.HostileAreaTriggersNearby, Now);

        await Assert.That(hazards).Count().IsEqualTo(1);
        await Assert.That(hazards[0]).IsSameReferenceAs(hostile);
    }

    [Test]
    public async Task HostileAreaTriggersNearby_UsesFiveHundredMillisecondTtl()
    {
        var manager = new AreaTriggerManager();
        var bot = BotTestFixture.MakeBot(1, Vector3.Zero);
        var first = MakeTrigger(SkillTargetRelation.Hostile, 0);
        manager.AddAreaTrigger(first);
        manager.Tick(TimeSpan.Zero);
        var blackboard = CreateBlackboard(bot, manager, 500);

        var initial = blackboard.Get(BotValues.HostileAreaTriggersNearby, Now);
        var second = MakeTrigger(SkillTargetRelation.Hostile, 0);
        manager.AddAreaTrigger(second);
        manager.Tick(TimeSpan.Zero);
        var beforeExpiry = blackboard.Get(BotValues.HostileAreaTriggersNearby, Now.AddMilliseconds(499));
        var afterExpiry = blackboard.Get(BotValues.HostileAreaTriggersNearby, Now.AddMilliseconds(500));

        await Assert.That(initial).Count().IsEqualTo(1);
        await Assert.That(beforeExpiry).Count().IsEqualTo(1);
        await Assert.That(afterExpiry).Count().IsEqualTo(2);
    }

    [Test]
    public async Task InHostileAreaTrigger_IsActiveOnlyForCachedHostileHazards()
    {
        var manager = new AreaTriggerManager();
        var bot = BotTestFixture.MakeBot(1, Vector3.Zero);
        manager.AddAreaTrigger(MakeTrigger(SkillTargetRelation.Friendly, 0));
        manager.Tick(TimeSpan.Zero);
        var runtime = new BotRuntime(
            bot,
            new BotMovementState(),
            new BotCombatState(),
            config: new BotConfig { UseEngine = false });
        var blackboard = CreateBlackboard(bot, manager, 500);
        var context = new BotContext(bot, runtime, blackboard, Now, new BotConfig(), BotEngineKind.Combat);

        var trigger = new InHostileAreaTrigger();

        await Assert.That(trigger.IsActive(context)).IsFalse();
        manager.AddAreaTrigger(MakeTrigger(SkillTargetRelation.Hostile, 0));
        manager.Tick(TimeSpan.Zero);
        blackboard.Invalidate(BotValues.HostileAreaTriggersNearby);
        await Assert.That(trigger.IsActive(context)).IsTrue();
    }

    [Test]
    public async Task Active_ReturnsSnapshotDuringConcurrentTriggerMutation()
    {
        var manager = new AreaTriggerManager();
        manager.AddAreaTrigger(MakeTrigger(SkillTargetRelation.Hostile, 0));
        manager.Tick(TimeSpan.Zero);
        var faults = new ConcurrentQueue<Exception>();
        var start = new Barrier(2);

        var reader = Task.Run(() =>
        {
            try
            {
                start.SignalAndWait();
                for (var i = 0; i < 1000; i++)
                {
                    var snapshot = manager.Active.ToArray();
                    if (snapshot.Any(trigger => trigger == null) || snapshot.Distinct().Count() != snapshot.Length)
                        faults.Enqueue(new InvalidOperationException("Active returned an inconsistent snapshot."));
                }
            }
            catch (Exception exception)
            {
                faults.Enqueue(exception);
            }
        });
        var writer = Task.Run(() =>
        {
            try
            {
                start.SignalAndWait();
                for (var i = 0; i < 1000; i++)
                {
                    var trigger = MakeTrigger(SkillTargetRelation.Hostile, i % 2 == 0 ? 10 : 20);
                    manager.AddAreaTrigger(trigger);
                    manager.Tick(TimeSpan.Zero);
                    manager.RemoveAreaTrigger(trigger);
                    manager.Tick(TimeSpan.Zero);
                }
            }
            catch (Exception exception)
            {
                faults.Enqueue(exception);
            }
        });

        await Task.WhenAll(reader, writer);

        await Assert.That(faults).IsEmpty();
        await Assert.That(manager.Active).Count().IsEqualTo(1);
    }

    [Test]
    public async Task HostileAreaTriggersNearby_CuboidUsesAbsoluteExtentsLikeAvoidance()
    {
        var manager = new AreaTriggerManager();
        var bot = BotTestFixture.MakeBot(1, new Vector3(0.5f, 1f, 1f));
        var owner = new Doodad();
        owner.Transform.Local.SetPosition(Vector3.Zero);
        var trigger = new AreaTrigger
        {
            Owner = owner,
            Shape = new AreaShape
            {
                Type = AreaShapeType.Cuboid,
                Value1 = -4,
                Value2 = -8,
                Value3 = -2
            },
            TargetRelation = SkillTargetRelation.Hostile
        };
        manager.AddAreaTrigger(trigger);
        manager.Tick(TimeSpan.Zero);
        var blackboard = CreateBlackboard(bot, manager, 0);

        var hazards = blackboard.Get(BotValues.HostileAreaTriggersNearby, Now);

        await Assert.That(hazards).Contains(trigger);
    }

    private static BotBlackboard CreateBlackboard(CharacterMock bot, AreaTriggerManager manager, double ttlMs)
    {
        var blackboard = new BotBlackboard();
        blackboard.Register(BotValues.HostileAreaTriggersNearby,
            HazardValues.Create(bot, () => manager.Active, TimeSpan.FromMilliseconds(ttlMs)));
        return blackboard;
    }

    private static AreaTrigger MakeTrigger(SkillTargetRelation relation, float x)
    {
        var caster = BotTestFixture.MakeBot(2, new Vector3(x, 0, 0));
        var owner = new Doodad();
        owner.Transform.Local.SetPosition(new Vector3(x, 0, 0));
        return new AreaTrigger
        {
            Owner = owner,
            Caster = caster,
            Shape = new AreaShape { Type = AreaShapeType.Sphere, Value1 = 5 },
            TargetRelation = relation
        };
    }
}
