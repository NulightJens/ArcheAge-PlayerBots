using System.Numerics;
using AAEmu.Game.Bots.Blackboard;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Tasks.Bots;
using AAEmu.UnitTests.Utils.Mocks;

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
    public async Task Step_ContainedCombatAtHealthFloorDisengagesBeforeAnotherAttack()
    {
        var bot = BotTestFixture.MakeBot(151, Vector3.Zero);
        BotTestFixture.SetPrivateField(bot, "_parentWorld", BotTestFixture.MakeWorld());
        bot.Hp = 100;
        bot.MaxHp = 100;
        var target = new FixedHealthCharacterMock
        {
            ObjId = 152,
            Hp = 50,
            FixedMaxHp = 100
        };
        target.Transform.Local.SetPosition(Vector3.One);
        var handlerCalls = 0;
        var floorCallbackCalls = 0;
        var state = new BotCombatState
        {
            BotId = bot.Id,
            IsActive = true,
            CurrentState = BotCombatStateType.Combat,
            PreviousState = BotCombatStateType.Idle,
            ForcedState = BotCombatStateType.Idle,
            Target = target,
            StopAtTargetHpPercent = 50,
            NonlethalFloorReached = () => floorCallbackCalls++
        };
        bot.CurrentTarget = target;
        var task = new BotCombatTask(
            bot,
            state,
            new BotMovementBroadcaster(bot),
            onCancel: null,
            handler: _ =>
            {
                handlerCalls++;
                return true;
            });

        task.Step();

        await Assert.That(handlerCalls).IsEqualTo(0);
        await Assert.That(floorCallbackCalls).IsEqualTo(1);
        await Assert.That(state.CurrentState).IsEqualTo(BotCombatStateType.Idle);
        await Assert.That(state.Target).IsNull();
        await Assert.That(bot.CurrentTarget).IsNull();
        await Assert.That(state.StopAtTargetHpPercent).IsNull();
        await Assert.That(state.NonlethalFloorReached == null).IsTrue();
    }

    [Test]
    public async Task HasReachedHpFloorUsesExactIntegerBoundary()
    {
        var target = new FixedHealthCharacterMock
        {
            ObjId = 153,
            FixedMaxHp = 18849,
            Hp = 9425
        };

        await Assert.That(BotCombatTask.HasReachedHpFloor(target, 50)).IsFalse();
        target.Hp = 9424;
        await Assert.That(BotCombatTask.HasReachedHpFloor(target, 50)).IsTrue();
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
}
