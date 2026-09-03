using System.Numerics;
using AAEmu.Game.Bots.Blackboard;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Tasks.Bots;
using AAEmu.UnitTests.Utils.Mocks;
using Microsoft.Extensions.Time.Testing;

namespace AAEmu.UnitTests.Game.Core.Managers.BotManagers;

[NotInParallel]
public class BotCombatTaskTests
{
    [Before(Test)]
    public void Setup()
    {
        BotTestFixture.RegisterTaskManager();
    }

    [After(Test)]
    public void Teardown()
    {
        BotTestFixture.ResetTaskManager();
    }

    [Test]
    public async Task Execute_BotIsDead_SchedulesRespawnOnce_NotCancelled()
    {
        var bot = BotTestFixture.MakeBot(2, Vector3.Zero);
        BotTestFixture.SetPrivateField(bot, "_parentWorld", BotTestFixture.MakeWorld());
        bot.Hp = 0;
        bot.MaxHp = 100;
        var state = new BotCombatState();
        var broadcaster = new BotMovementBroadcaster(bot);
        var task = new BotCombatTask(bot, state, broadcaster);
        var taskManager = BotTestFixture.RegisterTaskManager();
        taskManager.Schedule(task, TimeSpan.FromMinutes(1));
        var initialQueueCount = taskManager.GetQueueCount();

        task.Execute();

        await Assert.That(task.Cancelled).IsFalse();
        await Assert.That(state.RespawnScheduled).IsTrue();
        await Assert.That(state.ShouldRespawn).IsFalse();
        await Assert.That(taskManager.GetQueueCount()).IsEqualTo(initialQueueCount + 1);
    }

    [Test]
    public async Task Execute_DeadTwoTicks_DoesNotRespawnBeforeTask()
    {
        var bot = BotTestFixture.MakeBot(3, Vector3.Zero);
        BotTestFixture.SetPrivateField(bot, "_parentWorld", BotTestFixture.MakeWorld());
        bot.Hp = 0;
        bot.MaxHp = 100;
        var state = new BotCombatState();
        var broadcaster = new BotMovementBroadcaster(bot);
        var task = new BotCombatTask(bot, state, broadcaster);
        var taskManager = BotTestFixture.RegisterTaskManager();
        taskManager.Schedule(task, TimeSpan.FromMinutes(1));

        task.Execute();
        task.Execute();

        await Assert.That(bot.Hp).IsEqualTo(0);
        await Assert.That(state.RespawnScheduled).IsTrue();
        await Assert.That(state.ShouldRespawn).IsFalse();
    }

    [Test]
    public async Task Execute_HandlerThrows_TaskStillScheduled()
    {
        var bot = BotTestFixture.MakeBot(8, Vector3.Zero);
        var target = BotTestFixture.MakeBot(9, Vector3.One);
        BotTestFixture.SetPrivateField(bot, "_parentWorld", BotTestFixture.MakeWorld());
        bot.Hp = 100;
        bot.MaxHp = 100;
        target.Hp = 100;
        target.MaxHp = 100;
        var state = new BotCombatState
        {
            IsActive = true,
            CurrentState = BotCombatStateType.Combat,
            Target = target
        };
        var task = new BotCombatTask(
            bot,
            state,
            new BotMovementBroadcaster(bot),
            onCancel: null,
            handler: _ => throw new InvalidOperationException("combat handler failed"));
        var taskManager = BotTestFixture.RegisterTaskManager();
        taskManager.Schedule(task, TimeSpan.FromMinutes(1));
        var initialQueueCount = taskManager.GetQueueCount();

        task.Execute();

        await Assert.That(task.Cancelled).IsFalse();
        await Assert.That(taskManager.GetQueueCount()).IsEqualTo(initialQueueCount);
        await Assert.That(state.Diagnostics.LastError).IsTypeOf<InvalidOperationException>();
        await Assert.That(state.Diagnostics.ErrorCount).IsEqualTo(1);
    }

    [Test]
    public async Task Step_GrindingReadsHostileIdsFromBlackboardAndResolvesNpcFromWorld()
    {
        var bot = BotTestFixture.MakeBot(14, Vector3.Zero);
        var world = BotTestFixture.MakeWorld();
        BotTestFixture.SetPrivateField(bot, "_parentWorld", world);
        bot.Hp = 100;
        bot.MaxHp = 100;
        var npc = new AAEmu.Game.Models.Game.NPChar.Npc { ObjId = 1401, Hp = 100, MaxHp = 100 };
        npc.Transform.Local.SetPosition(Vector3.Zero);
        world.SetNpc(npc.ObjId, npc);
        var scans = 0;
        var blackboard = WorldValues.Create(
            bot,
            (_, _) =>
            {
                scans++;
                return [npc];
            },
            (_, _) => [],
            config: new BotConfig());
        var state = new BotCombatState
        {
            BotId = bot.Id,
            IsActive = true,
            CurrentState = BotCombatStateType.Grinding,
            InDuel = true
        };
        var task = new BotCombatTask(
            bot,
            state,
            new BotMovementBroadcaster(bot),
            onCancel: null,
            blackboard: blackboard);

        task.Step();

        await Assert.That(state.Target).IsSameReferenceAs(npc);
        await Assert.That(scans).IsEqualTo(1);
    }

    [Test]
    public async Task Step_CombatWithoutTarget_ClearsRelaxedFlagOnTemporaryStateExit()
    {
        var bot = BotTestFixture.MakeBot(15, Vector3.Zero);
        var world = BotTestFixture.MakeWorld();
        BotTestFixture.SetPrivateField(bot, "_parentWorld", world);
        bot.Hp = 100;
        bot.MaxHp = 100;
        var state = new BotCombatState
        {
            CurrentState = BotCombatStateType.Combat,
            SentRelaxedAfterCombat = true
        };
        var task = new BotCombatTask(bot, state, new BotMovementBroadcaster(bot));

        task.Step();

        await Assert.That(state.SentRelaxedAfterCombat).IsFalse();
    }

    [Test]
    public async Task Step_DeadCombatTargetDoesNotCreateCreditAndReturnsToGrinding()
    {
        var bot = BotTestFixture.MakeBot(16, Vector3.Zero);
        BotTestFixture.SetPrivateField(bot, "_parentWorld", BotTestFixture.MakeWorld());
        bot.Hp = 100;
        bot.MaxHp = 100;
        var deadTarget = BotTestFixture.MakeBot(17, Vector3.One);
        deadTarget.Hp = 0;
        deadTarget.MaxHp = 100;
        var state = new BotCombatState { BotId = bot.Id, IsActive = true };
        state.TransitionTo(BotCombatStateType.Grinding);
        state.TransitionTo(BotCombatStateType.Combat);
        state.Target = deadTarget;
        var blackboard = WorldValues.Create(bot, (_, _) => [], (_, _) => [], config: new BotConfig());
        var task = new BotCombatTask(
            bot,
            state,
            new BotMovementBroadcaster(bot),
            onCancel: null,
            blackboard: blackboard);

        task.Step();

        // Old contract: corpse observation incremented this to 1. New contract: only Unit.Events.OnKill may credit.
        await Assert.That(state.KillCount).IsEqualTo(0);
        await Assert.That(state.CurrentState).IsEqualTo(BotCombatStateType.Grinding);
    }

    [Test]
    public async Task Step_DeadCombatTargetRemainsUncreditedAfterRelease()
    {
        var bot = BotTestFixture.MakeBot(18, Vector3.Zero);
        BotTestFixture.SetPrivateField(bot, "_parentWorld", BotTestFixture.MakeWorld());
        bot.Hp = 100;
        bot.MaxHp = 100;
        var deadTarget = BotTestFixture.MakeBot(19, Vector3.One);
        deadTarget.Hp = 0;
        deadTarget.MaxHp = 100;
        var state = new BotCombatState { BotId = bot.Id, IsActive = true };
        state.TransitionTo(BotCombatStateType.Grinding);
        state.TransitionTo(BotCombatStateType.Combat);
        state.Target = deadTarget;
        var blackboard = WorldValues.Create(bot, (_, _) => [], (_, _) => [], config: new BotConfig());
        var task = new BotCombatTask(
            bot,
            state,
            new BotMovementBroadcaster(bot),
            onCancel: null,
            blackboard: blackboard);

        task.Step();
        state.IsActive = false;
        task.Step();

        await Assert.That(state.KillCount).IsEqualTo(0);
    }

    [Test]
    public async Task Step_CompletedKillGoalIsConsumedAndRemainsFreeIdle()
    {
        var bot = BotTestFixture.MakeBot(20, Vector3.Zero);
        BotTestFixture.SetPrivateField(bot, "_parentWorld", BotTestFixture.MakeWorld());
        bot.Hp = 100;
        bot.MaxHp = 100;
        var state = new BotCombatState
        {
            BotId = bot.Id,
            IsActive = true,
            CurrentState = BotCombatStateType.Grinding,
            ForcedState = BotCombatStateType.Grinding,
            InDuel = true,
            KillCount = 1,
            KillGoal = 1
        };
        var blackboard = WorldValues.Create(bot, (_, _) => [], (_, _) => [], config: new BotConfig());
        var task = new BotCombatTask(
            bot,
            state,
            new BotMovementBroadcaster(bot),
            onCancel: null,
            blackboard: blackboard);

        task.Step();
        task.Step();

        await Assert.That(state.CurrentState).IsEqualTo(BotCombatStateType.Idle);
        await Assert.That(state.IsActive).IsFalse();
        await Assert.That(state.ForcedState).IsNull();
        await Assert.That(state.KillGoal).IsNull();
        await Assert.That(state.KillCount).IsEqualTo(1);
    }

    [Test]
    public async Task Step_QuestingSelectsExactTargetsAndRepeatsUntilKillGoal()
    {
        var bot = BotTestFixture.MakeBot(21, Vector3.Zero);
        var world = BotTestFixture.MakeWorld();
        BotTestFixture.SetPrivateField(bot, "_parentWorld", world);
        bot.Hp = 100;
        bot.MaxHp = 100;

        var wrongTemplate = new AAEmu.Game.Models.Game.NPChar.Npc
        {
            ObjId = 2101,
            TemplateId = 99,
            Template = new AAEmu.Game.Models.Game.NPChar.NpcTemplate { Scale = 1f },
            Hp = 100,
            MaxHp = 100
        };
        wrongTemplate.Transform.Local.SetPosition(new Vector3(1, 0, 0));
        var first = new AAEmu.Game.Models.Game.NPChar.Npc
        {
            ObjId = 2102,
            TemplateId = 42,
            Template = new AAEmu.Game.Models.Game.NPChar.NpcTemplate { Scale = 1f },
            Hp = 100,
            MaxHp = 100
        };
        first.Transform.Local.SetPosition(new Vector3(2, 0, 0));
        var second = new AAEmu.Game.Models.Game.NPChar.Npc
        {
            ObjId = 2103,
            TemplateId = 42,
            Template = new AAEmu.Game.Models.Game.NPChar.NpcTemplate { Scale = 1f },
            Hp = 100,
            MaxHp = 100
        };
        second.Transform.Local.SetPosition(new Vector3(4, 0, 0));
        foreach (var npc in new[] { wrongTemplate, first, second })
            world.SetNpc(npc.ObjId, npc);

        var blackboard = WorldValues.Create(
            bot,
            (_, _) => [wrongTemplate, second, first],
            (_, _) => [],
            config: new BotConfig());
        var state = new BotCombatState
        {
            BotId = bot.Id,
            IsActive = true,
            TargetTypeFilter = 42,
            KillGoal = 2,
            InDuel = true
        };
        state.TransitionTo(BotCombatStateType.Questing);
        state.SetForcedState(BotCombatStateType.Questing);
        var task = new BotCombatTask(
            bot,
            state,
            new BotMovementBroadcaster(bot),
            onCancel: null,
            blackboard: blackboard,
            heightProvider: (_, _) => 0f);

        task.Step();
        await Assert.That(state.Target).IsSameReferenceAs(first);

        state.KillCount = 1;
        first.Hp = 0;
        task.Step();
        task.Step();
        await Assert.That(state.Target).IsSameReferenceAs(second);

        state.KillCount = 2;
        second.Hp = 0;
        task.Step();
        task.Step();

        await Assert.That(state.CurrentState).IsEqualTo(BotCombatStateType.Idle);
        await Assert.That(state.IsActive).IsFalse();
        await Assert.That(state.KillGoal).IsNull();
        await Assert.That(state.ForcedState).IsNull();
    }

    [Test]
    public async Task QuestTargetVolumeRequiresThreeDimensionalRangeAndHeightmapAgreement()
    {
        var botPosition = new Vector3(10, 20, 100);

        await Assert.That(BotCombatTask.IsWithinNavigableQuestTargetVolume(
            botPosition,
            new Vector3(13, 24, 100),
            navigationSurfaceZ: 100,
            searchRadius: 5)).IsTrue();
        await Assert.That(BotCombatTask.IsWithinNavigableQuestTargetVolume(
            botPosition,
            new Vector3(10, 20, 161),
            navigationSurfaceZ: 161,
            searchRadius: 60)).IsFalse();
        await Assert.That(BotCombatTask.IsWithinNavigableQuestTargetVolume(
            botPosition,
            new Vector3(13, 24, 100),
            navigationSurfaceZ: 130,
            searchRadius: 60)).IsFalse();
        await Assert.That(BotCombatTask.IsWithinNavigableQuestTargetVolume(
            botPosition,
            new Vector3(121, 20, 100),
            navigationSurfaceZ: 100,
            searchRadius: BotCombatTask.MaximumQuestTargetSearchRadius)).IsFalse();
    }

    [Test]
    public async Task Step_QuestingRejectsExactTargetOutsideThreeDimensionalSearchRadius()
    {
        var bot = BotTestFixture.MakeBot(22, new Vector3(10, 20, 100));
        var world = BotTestFixture.MakeWorld();
        BotTestFixture.SetPrivateField(bot, "_parentWorld", world);
        bot.Hp = 100;
        bot.MaxHp = 100;
        var verticallyRemote = new AAEmu.Game.Models.Game.NPChar.Npc
        {
            ObjId = 2201,
            TemplateId = 42,
            Template = new AAEmu.Game.Models.Game.NPChar.NpcTemplate { Scale = 1f },
            Hp = 100,
            MaxHp = 100
        };
        verticallyRemote.Transform.Local.SetPosition(new Vector3(10, 20, 161));
        world.SetNpc(verticallyRemote.ObjId, verticallyRemote);
        var blackboard = WorldValues.Create(
            bot,
            (_, _) => [verticallyRemote],
            (_, _) => [],
            config: new BotConfig { SearchRadius = 60 });
        var state = new BotCombatState
        {
            BotId = bot.Id,
            IsActive = true,
            TargetTypeFilter = 42,
            KillGoal = 1,
            InDuel = true
        };
        state.TransitionTo(BotCombatStateType.Questing);
        state.SetForcedState(BotCombatStateType.Questing);
        var task = new BotCombatTask(
            bot,
            state,
            new BotMovementBroadcaster(bot),
            onCancel: null,
            blackboard: blackboard,
            heightProvider: (_, _) => 0f);

        task.Step();

        await Assert.That(state.Target).IsNull();
        await Assert.That(state.CurrentState).IsEqualTo(BotCombatStateType.Questing);
        await Assert.That(state.KillCount).IsEqualTo(0);
    }

    [Test]
    public async Task Step_StealthedNpc_ReacquiresTheExactLostObjectWhenVisibleInRadius()
    {
        var bot = BotTestFixture.MakeBot(30, Vector3.Zero);
        var world = BotTestFixture.MakeWorld();
        BotTestFixture.SetPrivateField(bot, "_parentWorld", world);
        bot.Hp = 100;
        bot.MaxHp = 100;
        var target = new Npc
        {
            ObjId = 9301,
            TemplateId = 7301,
            Template = new NpcTemplate { Scale = 1f },
            Hp = 100,
            MaxHp = 100
        };
        target.Transform.Local.SetPosition(new Vector3(5, 0, 0));
        world.SetNpc(target.ObjId, target);
        var stealthed = true;
        var buffs = Mock.Of<IBuffs>();
        buffs.HasEffectsMatchingCondition(Any<Func<Buff, bool>>()).Returns(_ => stealthed);
        target.Buffs = buffs.Object;
        var state = new BotCombatState { BotId = bot.Id, IsActive = true, Target = target };
        state.SetForcedState(BotCombatStateType.Idle);
        state.TransitionTo(BotCombatStateType.Combat);
        var task = new BotCombatTask(bot, state, new BotMovementBroadcaster(bot), onCancel: null,
            handler: _ => true);

        task.Step();
        await Assert.That(state.CurrentState).IsEqualTo(BotCombatStateType.Searching);
        await Assert.That(state.LostTarget).IsSameReferenceAs(target);
        await Assert.That(state.Target).IsNull();

        stealthed = false;
        task.Step();

        await Assert.That(state.CurrentState).IsEqualTo(BotCombatStateType.Combat);
        await Assert.That(state.Target).IsSameReferenceAs(target);
        await Assert.That(state.LostTarget).IsNull();
        await Assert.That(state.IsSearching).IsFalse();
    }

    [Test]
    public async Task Step_SearchTimeout_ReleasesExactLostNpcToIdleWithoutSleeping()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 8, 31, 18, 0, 0, TimeSpan.Zero));
        var bot = BotTestFixture.MakeBot(31, Vector3.Zero);
        var world = BotTestFixture.MakeWorld();
        BotTestFixture.SetPrivateField(bot, "_parentWorld", world);
        bot.Hp = 100;
        bot.MaxHp = 100;
        var target = new Npc
        {
            ObjId = 9302,
            TemplateId = 7302,
            Template = new NpcTemplate { Scale = 1f },
            Hp = 100,
            MaxHp = 100
        };
        target.Transform.Local.SetPosition(new Vector3(5, 0, 0));
        var buffs = Mock.Of<IBuffs>();
        buffs.HasEffectsMatchingCondition(Any<Func<Buff, bool>>()).Returns(true);
        target.Buffs = buffs.Object;
        var state = new BotCombatState
        {
            BotId = bot.Id,
            IsActive = true,
            LostTarget = target,
            LastKnownTargetPosition = target.Transform.World.Position,
            SearchStartTime = time.GetUtcNow().UtcDateTime,
            IsSearching = true
        };
        state.SetForcedState(BotCombatStateType.Idle);
        state.TransitionTo(BotCombatStateType.Combat);
        state.TransitionTo(BotCombatStateType.Searching);
        var task = new BotCombatTask(bot, state, new BotMovementBroadcaster(bot), onCancel: null,
            timeProvider: time);

        time.Advance(TimeSpan.FromSeconds(51));
        task.Step();

        await Assert.That(state.CurrentState).IsEqualTo(BotCombatStateType.Idle);
        await Assert.That(state.Target).IsNull();
        await Assert.That(state.LostTarget).IsNull();
        await Assert.That(state.LastKnownTargetPosition).IsNull();
        await Assert.That(state.IsSearching).IsFalse();
        await Assert.That(state.SearchRadius).IsEqualTo(0f);
    }
}
