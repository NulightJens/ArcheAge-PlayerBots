using System.Numerics;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Tasks.Bots;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Tasks.Skills;
using AAEmu.Game.Utils;
using AAEmu.UnitTests.Utils.Mocks;
using Microsoft.Extensions.Time.Testing;

namespace AAEmu.UnitTests.Game.Core.Managers.BotManagers;

[NotInParallel]
public class BotMovementTaskTests
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
    public async Task Execute_BotIsDead_DoesNotCancel_SendsStopOnce()
    {
        var bot = BotTestFixture.MakeBot(2, Vector3.Zero);
        BotTestFixture.SetPrivateField(bot, "_parentWorld", BotTestFixture.MakeWorld());
        bot.Hp = 0;
        bot.MaxHp = 100;
        var state = new BotMovementState
        {
            IsMoving = true,
            Destination = new Vector3(10, 0, 0)
        };
        var broadcaster = new BotMovementBroadcaster(bot);
        var sent = new List<AAEmu.Game.Models.Game.Units.Movements.UnitMoveType>();
        broadcaster.MoveTypeSink = sent.Add;
        var task = new BotMovementTask(bot, state, broadcaster);
        BotTestFixture.RegisterTaskManager().Schedule(task, TimeSpan.FromMinutes(1));

        task.Execute();
        task.Execute();

        await Assert.That(task.Cancelled).IsFalse();
        await Assert.That(sent.Count).IsEqualTo(1);
        await Assert.That(state.IsMoving).IsFalse();
    }

    [Test]
    public async Task IsMovementTaskRunning_AfterSelfCancel_ReportsFalse()
    {
        var manager = new BotManager();
        var bot = BotTestFixture.MakeBot(2, Vector3.Zero);
        var state = new BotMovementState();
        var task = new BotMovementTask(bot, state, new BotMovementBroadcaster(bot))
        {
            Cancelled = true
        };
        BotTestFixture.GetDictionary<AAEmu.Game.Models.Tasks.Task>(manager, "_movementTasks").TryAdd(bot.Id, task);

        await Assert.That(manager.IsMovementTaskRunning(bot.Id)).IsFalse();
    }

    [Test]
    public async Task Execute_GroundHeightThrows_DoesNotPropagateAndRecordsError()
    {
        var bot = BotTestFixture.MakeBot(6, Vector3.Zero);
        BotTestFixture.SetPrivateField(bot, "_parentWorld", BotTestFixture.MakeWorld());
        bot.Hp = 100;
        bot.MaxHp = 100;
        var state = new BotMovementState { Destination = new Vector3(0.1f, 0, 0) };
        var broadcaster = new BotMovementBroadcaster(bot);
        var task = new BotMovementTask(
            bot,
            state,
            broadcaster,
            onCancel: null,
            groundHeight: (_, _) => throw new InvalidOperationException("ground height failed"));

        task.Execute();

        await Assert.That(task.Cancelled).IsFalse();
        await Assert.That(state.Diagnostics.LastError).IsTypeOf<InvalidOperationException>();
        await Assert.That(state.Diagnostics.ErrorCount).IsEqualTo(1);
    }

    [Test]
    public async Task Execute_ReentrantCall_SecondTickSkipped()
    {
        var bot = BotTestFixture.MakeBot(7, Vector3.Zero);
        var state = new BotMovementState { Running = 1 };
        var task = new BotMovementTask(
            bot,
            state,
            new BotMovementBroadcaster(bot),
            onCancel: null,
            groundHeight: (_, _) => throw new InvalidOperationException("should not run"));

        task.Execute();

        await Assert.That(state.Diagnostics.ErrorCount).IsEqualTo(0);
        await Assert.That(task.Cancelled).IsFalse();
    }

    [Test]
    public async Task Execute_MovementImpaired_WhileMoving_SendsStopAndClearsDestination()
    {
        var setup = CreateTask(new Vector3(0, 0, 0));
        var buffs = Mock.Of<IBuffs>();
        setup.Bot.Buffs = buffs.Object;
        buffs.HasEffectsMatchingCondition(Any<Func<Buff, bool>>()).Returns(true);
        setup.State.Destination = new Vector3(10, 0, 0);
        setup.State.IsMoving = true;

        setup.Task.Execute();

        await Assert.That(setup.Broadcaster.Calls.Single().Kind).IsEqualTo("Stop");
        await Assert.That(setup.State.Destination).IsNull();
        await Assert.That(setup.State.IsMoving).IsFalse();
    }

    [Test]
    public async Task Execute_MovementImpaired_WhileIdle_SendsNothing()
    {
        var setup = CreateTask(Vector3.Zero);
        var buffs = Mock.Of<IBuffs>();
        setup.Bot.Buffs = buffs.Object;
        buffs.HasEffectsMatchingCondition(Any<Func<Buff, bool>>()).Returns(true);

        setup.Task.Execute();

        await Assert.That(setup.Broadcaster.Calls).IsEmpty();
    }

    [Test]
    public async Task Execute_SkillTaskActive_WhileMoving_SendsStopOnce()
    {
        var setup = CreateTask(Vector3.Zero);
        setup.Bot.SkillTask = new TestSkillTask();
        setup.State.Destination = new Vector3(10, 0, 0);
        setup.State.IsMoving = true;

        setup.Task.Execute();

        await Assert.That(setup.Broadcaster.Calls.Single().Kind).IsEqualTo("Stop");
        await Assert.That(setup.State.Destination).IsNull();
    }

    [Test]
    public async Task Execute_DestinationSet_AdvancesPositionBySpeedTimesTick()
    {
        var setup = CreateTask(Vector3.Zero);
        setup.State.Destination = new Vector3(10, 0, 0);

        setup.Task.Execute();

        await Assert.That(setup.Bot.Transform.World.Position.X).IsEqualTo(0.54f).Within(1e-4f);
        await Assert.That(setup.Broadcaster.Calls.Single().Kind).IsEqualTo("Move");
    }

    [Test]
    public async Task Execute_DestinationSet_Walking_Advances0_18()
    {
        var setup = CreateTask(Vector3.Zero, running: false);
        setup.State.Destination = new Vector3(10, 0, 0);

        setup.Task.Execute();

        await Assert.That(setup.Bot.Transform.World.Position.X).IsEqualTo(0.18f).Within(1e-4f);
    }

    [Test]
    public async Task Execute_DestinationWithin0_5m_SnapsSendsStopAndClears()
    {
        var setup = CreateTask(Vector3.Zero);
        setup.State.Destination = new Vector3(0.4f, 0, 0);

        setup.Task.Execute();

        await Assert.That(setup.Bot.Transform.World.Position).IsEqualTo(new Vector3(0.4f, 0, 0));
        await Assert.That(setup.Broadcaster.Calls.Single().Kind).IsEqualTo("Stop");
        await Assert.That(setup.State.Destination).IsNull();
    }

    [Test]
    public async Task Execute_ArrivesThisTick_SendsMoveThenStop()
    {
        var setup = CreateTask(Vector3.Zero);
        setup.State.Destination = new Vector3(0.53f, 0, 0);

        setup.Task.Execute();

        await Assert.That(setup.Broadcaster.Calls.Select(call => call.Kind)).IsEquivalentTo(["Move", "Stop"]);
    }

    [Test]
    public async Task Execute_ArrivalTick_VelocityIsZeroNotNaN()
    {
        var setup = CreateTask(Vector3.Zero);
        setup.State.Destination = new Vector3(0.53f, 0, 0);

        setup.Task.Execute();

        await Assert.That(setup.Broadcaster.Calls[0].Velocity).IsEqualTo(Vector3.Zero);
    }

    [Test]
    public async Task Execute_MovementSetsYawTowardDestination()
    {
        var setup = CreateTask(Vector3.Zero);
        setup.State.Destination = new Vector3(10, 0, 0);

        setup.Task.Execute();

        await Assert.That(setup.Bot.Transform.Local.Rotation.Z).IsEqualTo((-90f).DegToRad()).Within(1e-5f);
    }

    [Test]
    public async Task Execute_NoDestination_AboveGround_SendsFallWithVelocity0_981()
    {
        var setup = CreateTask(new Vector3(0, 0, 10));

        setup.Task.Execute();

        await Assert.That(setup.Broadcaster.Calls.Single().Kind).IsEqualTo("Fall");
        await Assert.That(setup.Broadcaster.Calls[0].Velocity.Z).IsEqualTo(0.981f).Within(1e-4f);
        await Assert.That(setup.State.IsFalling).IsTrue();
        await Assert.That(setup.State.FallVelocity).IsEqualTo(0.981f).Within(1e-4f);
    }

    [Test]
    public async Task Execute_NoDestination_LandsThisTick_SendsStopAndClearsFall()
    {
        var setup = CreateTask(new Vector3(0, 0, 0.05f));
        setup.State.FallVelocity = 1f;
        setup.State.IsFalling = true;

        setup.Task.Execute();

        await Assert.That(setup.Broadcaster.Calls.Single().Kind).IsEqualTo("Stop");
        await Assert.That(setup.State.FallVelocity).IsEqualTo(0f);
        await Assert.That(setup.State.IsFalling).IsFalse();
        await Assert.That(setup.Bot.Transform.World.Position.Z).IsEqualTo(0f);
    }

    [Test]
    public async Task Execute_NoDestination_OnGroundAfterMoving_SendsStopOnceThenNothing()
    {
        var setup = CreateTask(Vector3.Zero);
        setup.State.IsMoving = true;

        setup.Task.Execute();
        setup.Task.Execute();

        await Assert.That(setup.Broadcaster.Calls.Count).IsEqualTo(1);
    }

    [Test]
    public async Task RequestJump_IdleBot_UsesAirborneArcAndJumpBroadcast()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero));
        var setup = CreateTask(Vector3.Zero, config: new BotConfig(), time: time);

        var queued = setup.Task.RequestJump();
        setup.Task.Execute();

        await Assert.That(queued).IsTrue();
        await Assert.That(setup.State.IsJumping).IsTrue();
        await Assert.That(setup.Bot.Transform.World.Position.Z).IsEqualTo(0.3519f).Within(1e-4f);
        await Assert.That(setup.Broadcaster.Calls.Single().Kind).IsEqualTo("Jump");
        await Assert.That(setup.Broadcaster.Calls[0].Velocity.Z).IsEqualTo(3.519f).Within(1e-4f);
    }

    [Test]
    public async Task RequestJump_WhileMoving_PreservesHorizontalProgress()
    {
        var setup = CreateTask(Vector3.Zero);
        setup.State.Destination = new Vector3(10, 0, 0);

        setup.Task.RequestJump();
        setup.Task.Execute();

        await Assert.That(setup.Bot.Transform.World.Position.X).IsEqualTo(0.54f).Within(1e-4f);
        await Assert.That(setup.Bot.Transform.World.Position.Z).IsGreaterThan(0f);
        await Assert.That(setup.Broadcaster.Calls.Single().Kind).IsEqualTo("Jump");
    }

    [Test]
    public async Task AmbientFollowJump_IsStaggeredAndDoesNotFireImmediately()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 8, 28, 12, 0, 0, TimeSpan.Zero));
        var config = new BotConfig
        {
            AmbientJumpEnabled = true,
            AmbientJumpMinIntervalMs = 1000,
            AmbientJumpMaxIntervalMs = 1000
        };
        var setup = CreateTask(Vector3.Zero, config: config, time: time);
        setup.State.FollowTarget = BotTestFixture.MakeBot(3, new Vector3(20, 0, 0));

        setup.Task.Execute();
        time.Advance(TimeSpan.FromSeconds(1));
        setup.Task.Execute();

        await Assert.That(setup.Broadcaster.Calls[0].Kind).IsEqualTo("Move");
        await Assert.That(setup.Broadcaster.Calls[1].Kind).IsEqualTo("Jump");
    }

    [Test]
    public async Task Execute_FollowTargetBeyondDistance_SetsDestinationAndRuns()
    {
        var setup = CreateTask(Vector3.Zero);
        setup.State.FollowTarget = BotTestFixture.MakeBot(3, new Vector3(20, 0, 0));

        setup.Task.Execute();

        await Assert.That(setup.State.Destination).IsEqualTo(new Vector3(18, 0, 0));
        await Assert.That(setup.State.IsRunning).IsTrue();
        await Assert.That(setup.Broadcaster.Calls[0].Kind).IsEqualTo("Move");
    }

    [Test]
    public async Task Execute_FollowTargetWithinDistance_ClearsDestination()
    {
        var setup = CreateTask(Vector3.Zero);
        setup.State.Destination = new Vector3(10, 0, 0);
        setup.State.FollowTarget = BotTestFixture.MakeBot(3, new Vector3(1.5f, 0, 0));

        setup.Task.Execute();

        await Assert.That(setup.State.Destination).IsNull();
        await Assert.That(setup.Broadcaster.Calls).IsEmpty();
    }

    [Test]
    public async Task Execute_CombatDestinationOutranksStoredFollowTarget()
    {
        var setup = CreateTask(Vector3.Zero);
        setup.State.FollowTarget = BotTestFixture.MakeBot(3, new Vector3(20, 0, 0));
        setup.State.Destination = new Vector3(0, 20, 0);
        var combat = new BotCombatState { CurrentState = BotCombatStateType.Combat };
        var runtime = new BotRuntime(
            setup.Bot,
            setup.State,
            combat,
            mover: setup.Task,
            config: new BotConfig { UseEngine = false });

        setup.Task.Execute();

        await Assert.That(runtime.MovementState.FollowTarget).IsNotNull();
        await Assert.That(setup.State.Destination).IsEqualTo(new Vector3(0, 20, 0));
        await Assert.That(setup.Bot.Transform.World.Position.X).IsEqualTo(0f).Within(1e-4f);
        await Assert.That(setup.Bot.Transform.World.Position.Y).IsEqualTo(0.54f).Within(1e-4f);
    }

    [Test]
    public async Task Execute_AfterCombatReturnsToFollowing_ResumesStoredFollowTarget()
    {
        var setup = CreateTask(Vector3.Zero);
        setup.State.FollowTarget = BotTestFixture.MakeBot(3, new Vector3(20, 0, 0));
        setup.State.Destination = new Vector3(0, 20, 0);
        var combat = new BotCombatState { CurrentState = BotCombatStateType.Combat };
        _ = new BotRuntime(
            setup.Bot,
            setup.State,
            combat,
            mover: setup.Task,
            config: new BotConfig { UseEngine = false });

        setup.Task.Execute();
        combat.TransitionTo(BotCombatStateType.Following);
        setup.Task.Execute();

        await Assert.That(setup.State.Destination.Value.X).IsGreaterThan(17f);
        await Assert.That(MathF.Abs(setup.State.Destination.Value.Y)).IsLessThan(0.1f);
        await Assert.That(setup.Bot.Transform.World.Position.X).IsGreaterThan(0f);
    }

    [Test]
    public async Task Golden_MovementTask_TenTicksToward10m()
    {
        var setup = CreateTask(Vector3.Zero);
        setup.State.Destination = new Vector3(10, 0, 0);

        for (var tick = 0; tick < 10; tick++)
            setup.Task.Execute();

        await Assert.That(setup.Bot.Transform.World.Position.X).IsEqualTo(5.4f).Within(1e-4f);
        await Assert.That(setup.Broadcaster.Calls.Count).IsEqualTo(10);
    }

    [Test]
    public async Task Golden_MovementTask_ArrivalTick()
    {
        var setup = CreateTask(Vector3.Zero);
        setup.State.Destination = new Vector3(0.54f, 0, 0);

        setup.Task.Execute();

        await Assert.That(setup.Bot.Transform.World.Position).IsEqualTo(new Vector3(0.54f, 0, 0));
        await Assert.That(setup.State.Destination).IsNull();
        await Assert.That(setup.Broadcaster.Calls.Select(call => call.Kind)).IsEquivalentTo(["Move", "Stop"]);
    }

    [Test]
    public async Task Execute_ParentWorldNull_CancelsTask()
    {
        var setup = CreateTask(Vector3.Zero);
        BotTestFixture.SetPrivateField<AAEmu.Game.Models.Game.World.WorldInstance>(setup.Bot, "_parentWorld", null);

        setup.Task.Execute();

        await Assert.That(setup.Task.Cancelled).IsTrue();
    }

    private static (MovementCharacterMock Bot, BotMovementState State, RecordingBroadcaster Broadcaster, BotMovementTask Task) CreateTask(
        Vector3 position,
        bool running = true,
        BotConfig config = null,
        TimeProvider time = null)
    {
        var bot = new MovementCharacterMock { Id = 20, ObjId = 1020, Name = "bot20" };
        bot.Transform.Local.SetPosition(position);
        bot.Hp = 100;
        bot.MaxHp = 100;
        bot.DisabledSetPosition = true;
        BotTestFixture.SetPrivateField(bot, "_parentWorld", BotTestFixture.MakeWorld());
        var state = new BotMovementState { IsRunning = running };
        var broadcaster = new RecordingBroadcaster();
        var task = new BotMovementTask(
            bot,
            state,
            broadcaster,
            baseSpeed: isRunning => isRunning ? 5.4f : 1.8f,
            groundHeight: (_, _) => 0f,
            config: config,
            time: time);
        return (bot, state, broadcaster, task);
    }

    private sealed class TestSkillTask() : SkillTask(null)
    {
        public override void Execute()
        {
        }
    }

    private sealed class MovementCharacterMock : CharacterMock
    {
        public override void SetPosition(float x, float y, float z, float rotationX, float rotationY, float rotationZ)
        {
        }
    }
}
