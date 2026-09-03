using System.Collections.Concurrent;
using System;
using AAEmu.Game.Bots.Body;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Bots.Blackboard;
using AAEmu.Game.Bots.Content.Actions;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Tasks.Bots;
using AAEmu.Game.Models.Game.DoodadObj;
using AAEmu.Game.Models.Game.Duels;
using AAEmu.Game.Models.Game.Expeditions;
using AAEmu.UnitTests.Utils.Mocks;

namespace AAEmu.UnitTests.Game.Core.Managers.BotManagers;

[NotInParallel]
public class BotCombatManagerTests
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
    public async Task EndDuel_CalledTwice_SecondIsNoOp()
    {
        var manager = new BotCombatManager();
        var bot = BotTestFixture.MakeBot(2, default);
        var state = new BotCombatState { IsActive = true, WasCombatActive = true, InDuel = true };
        state.TransitionTo(BotCombatStateType.Grinding);
        state.TransitionTo(BotCombatStateType.Dueling);
        BotTestFixture.GetDictionary<BotCombatState>(manager, "_combatStates").TryAdd(bot.Id, state);

        manager.EndDuel(bot);
        var stateAfterFirstCall = state.CurrentState;

        manager.EndDuel(bot);

        await Assert.That(stateAfterFirstCall).IsEqualTo(BotCombatStateType.Grinding);
        await Assert.That(state.CurrentState).IsEqualTo(stateAfterFirstCall);
    }

    [Test]
    public async Task DisableCombat_KeepsStateObject_MarksInactive()
    {
        var manager = new BotCombatManager();
        var bot = BotTestFixture.MakeBot(2, default);
        var state = new BotCombatState
        {
            IsActive = true,
            LostTarget = bot,
            IsSearching = true,
            SearchRadius = 12f,
            SearchAngle = 1f
        };
        BotTestFixture.GetDictionary<BotCombatState>(manager, "_combatStates").TryAdd(bot.Id, state);

        manager.DisableCombat(bot);

        await Assert.That(manager.GetState(bot)).IsSameReferenceAs(state);
        await Assert.That(state.IsActive).IsFalse();
        await Assert.That(state.CurrentState).IsEqualTo(BotCombatStateType.Idle);
        await Assert.That(state.LostTarget).IsNull();
        await Assert.That(state.IsSearching).IsFalse();
        await Assert.That(state.SearchRadius).IsEqualTo(0f);
    }

    [Test]
    public async Task EndDuel_AfterDisableCombat_StaysIdleInactive()
    {
        var manager = new BotCombatManager();
        var bot = BotTestFixture.MakeBot(3, default);
        var opponent = BotTestFixture.MakeBot(4, default);

        BotTestFixture.GetDictionary<BotCombatState>(manager, "_combatStates")[bot.Id] = new BotCombatState
        {
            BotId = bot.Id
        };

        manager.EnableCombat(bot);
        manager.DisableCombat(bot);
        manager.StartDuel(bot, opponent);
        manager.EndDuel(bot);

        var state = manager.GetState(bot);
        await Assert.That(state.CurrentState).IsEqualTo(BotCombatStateType.Idle);
        await Assert.That(state.IsActive).IsFalse();
        await Assert.That(manager.IsTaskRunning(bot.Id)).IsFalse();
    }

    [Test]
    public async Task StartListening_AfterInactiveDuel_ReattachesBrainWithoutReplacingRuntime()
    {
        var manager = new BotCombatManager();
        var bot = BotTestFixture.MakeBot(30, default);
        var opponent = BotTestFixture.MakeBot(31, default);

        manager.EnableCombat(bot);
        var runtime = BotHost.Instance.GetRuntime(bot.Id);
        manager.DisableCombat(bot);
        manager.StartDuel(bot, opponent);
        manager.EndDuel(bot);

        await Assert.That(manager.IsTaskRunning(bot.Id)).IsFalse();

        manager.StartListening(bot);

        await Assert.That(manager.IsTaskRunning(bot.Id)).IsTrue();
        await Assert.That(BotHost.Instance.GetRuntime(bot.Id)).IsSameReferenceAs(runtime);

        manager.StopListening(bot);
    }

    [Test]
    public async Task StartListening_ReplacesBrainBoundToStaleCombatState()
    {
        var manager = new BotCombatManager();
        var bot = BotTestFixture.MakeBot(32, default);

        manager.StartListening(bot);
        var runtime = BotHost.Instance.GetRuntime(bot.Id);
        var originalBrain = (BotCombatTask)runtime.Brain;
        var replacementState = new BotCombatState { BotId = bot.Id };
        BotTestFixture.GetDictionary<BotCombatState>(manager, "_combatStates")[bot.Id] = replacementState;

        manager.StartListening(bot);

        var replacementBrain = (BotCombatTask)runtime.Brain;
        await Assert.That(replacementBrain).IsNotSameReferenceAs(originalBrain);
        await Assert.That(replacementBrain.State).IsSameReferenceAs(replacementState);
        await Assert.That(originalBrain.Cancelled).IsTrue();
        await Assert.That(BotHost.Instance.GetRuntime(bot.Id)).IsSameReferenceAs(runtime);

        manager.StopListening(bot);
    }

    [Test]
    public async Task ResetBot_WasActive_ReEnablesAndKeepsForcedState()
    {
        var previousBotManager = BotManager.Instance;
        var previousArchetypeManager = BotArchetypeManager.Instance;
        var botManager = new BotManager(_ => null, onlineLookup: _ => null);
        var archetypeManager = new FakeBotArchetypeManager();
        var manager = new BotCombatManager();
        var bot = BotTestFixture.MakeBot(4, default);
        var state = new BotCombatState
        {
            IsActive = true,
            CurrentState = BotCombatStateType.Combat,
            ForcedState = BotCombatStateType.Grinding,
            TargetTypeFilter = 7,
            KillGoal = 3,
            Target = BotTestFixture.MakeBot(5, default)
        };
        BotTestFixture.GetDictionary<BotCombatState>(manager, "_combatStates").TryAdd(bot.Id, state);

        try
        {
            BotTestFixture.RegisterSingletons(botManager, archetypeManager);

            manager.ResetBot(bot);

            await Assert.That(manager.GetState(bot)).IsSameReferenceAs(state);
            await Assert.That(state.IsActive).IsTrue();
            await Assert.That(state.CurrentState).IsEqualTo(BotCombatStateType.Grinding);
            await Assert.That(state.ForcedState).IsEqualTo(BotCombatStateType.Grinding);
            await Assert.That(state.TargetTypeFilter).IsEqualTo(7u);
            await Assert.That(state.KillGoal).IsEqualTo(3);
        }
        finally
        {
            BotTestFixture.RegisterSingletons(previousBotManager, previousArchetypeManager);
        }
    }

    [Test]
    public async Task DuelAcceptTask_BotDespawned_DoesNotAccept()
    {
        var previousBotManager = BotManager.Instance;
        var botManager = new BotManager(_ => null, onlineLookup: _ => null);
        var bot = BotTestFixture.MakeBot(6, default);
        var challenger = BotTestFixture.MakeBot(7, default);
        var state = new BotCombatState
        {
            DuelRequestPending = true,
            DuelChallenger = challenger
        };
        var task = new BotCombatManager.DuelAcceptTask(bot, challenger, state);

        try
        {
            BotTestFixture.RegisterSingletons(botManager);

            task.Execute();

            await Assert.That(state.DuelRequestPending).IsTrue();
            await Assert.That(state.DuelChallenger).IsSameReferenceAs(challenger);
        }
        finally
        {
            BotTestFixture.RegisterSingletons(previousBotManager);
        }
    }

    [Test]
    public async Task FollowStrategy_LeaderTargetsDoodad_StaysFollowing()
    {
        var previousBotManager = BotManager.Instance;
        var bot = BotTestFixture.MakeBot(8, default);
        bot.Hp = 100;
        bot.MaxHp = 100;
        var leader = BotTestFixture.MakeBot(9, default);
        leader.IsInBattle = true;
        leader.CurrentTarget = new Doodad();
        var movementState = new BotMovementState { FollowTarget = leader };
        var state = new BotCombatState
        {
            BotId = bot.Id,
            CurrentState = BotCombatStateType.Following
        };

        try
        {
            var runtime = new BotRuntime(bot, movementState, state, config: new BotConfig { UseEngine = false });
            var context = new BotContext(bot, runtime, runtime.Blackboard, DateTime.UtcNow, new BotConfig { UseEngine = false }, BotEngineKind.NonCombat);
            new FollowAction().Execute(context, default);

            await Assert.That(state.CurrentState).IsEqualTo(BotCombatStateType.Following);
            await Assert.That(state.Target).IsNull();
        }
        finally
        {
            BotTestFixture.RegisterSingletons(previousBotManager);
        }
    }

    [Test]
    public async Task OnDuelRequested_BotInExpedition_ClearsPendingDuelForBothIds()
    {
        BotTestFixture.ResetSingleton<DuelManager>();

        try
        {
            var manager = new BotCombatManager();
            var bot = BotTestFixture.MakeBot(10, default);
            bot.Expedition = new Expedition();
            var challenger = BotTestFixture.MakeBot(11, default);
            var state = new BotCombatState { BotId = bot.Id };
            BotTestFixture.GetDictionary<BotCombatState>(manager, "_combatStates")[bot.Id] = state;
            var duelManager = DuelManager.Instance;
            var duel = new Duel(challenger, bot);
            var duels = BotTestFixture.GetDictionary<Duel>(duelManager, "_duels");
            duels[challenger.Id] = duel;
            duels[bot.Id] = duel;
            var taskManager = BotTestFixture.RegisterTaskManager();
            var queueCount = taskManager.GetQueueCount();

            var handled = manager.OnDuelRequested(bot, challenger);

            await Assert.That(handled).IsTrue();
            await Assert.That(state.DuelRequestPending).IsFalse();
            await Assert.That(taskManager.GetQueueCount()).IsEqualTo(queueCount);
            await Assert.That(duelManager.TryGetDuel(challenger.Id, out _)).IsFalse();
            await Assert.That(duelManager.TryGetDuel(bot.Id, out _)).IsFalse();
        }
        finally
        {
            BotTestFixture.ResetTaskManager();
            BotTestFixture.ResetSingleton<DuelManager>();
        }
    }

    [Test]
    public async Task ResetCombat_InvalidatesRuntimeBlackboard()
    {
        var bot = BotTestFixture.MakeBot(20, default);
        var state = new BotCombatState { BotId = bot.Id };
        var board = new BotBlackboard();
        var value = new CalculatedValue<int>(() => 1, TimeSpan.FromMinutes(1));
        var key = new ValueKey<int>("reset");
        board.Register(key, value);
        var runtime = new BotRuntime(bot, new BotMovementState(), state, blackboard: board);
        var host = BotHost.Instance;
        var manager = new BotCombatManager();
        BotTestFixture.GetDictionary<BotCombatState>(manager, "_combatStates")[bot.Id] = state;
        host.Register(runtime);

        try
        {
            _ = board.Get(key, DateTime.UtcNow);
            manager.ResetCombat(bot);

            await Assert.That(value.ComputedAt).IsNull();
        }
        finally
        {
            host.Unregister(bot.Id);
        }
    }

    [Test]
    public async Task EndDuel_ActiveBotInvalidatesRuntimeBlackboard()
    {
        var bot = BotTestFixture.MakeBot(21, default);
        var state = new BotCombatState
        {
            BotId = bot.Id,
            IsActive = true,
            WasCombatActive = true,
            InDuel = true
        };
        state.TransitionTo(BotCombatStateType.Grinding);
        state.TransitionTo(BotCombatStateType.Dueling);
        var board = new BotBlackboard();
        var value = new CalculatedValue<int>(() => 1, TimeSpan.FromMinutes(1));
        var key = new ValueKey<int>("duel_end");
        board.Register(key, value);
        var runtime = new BotRuntime(bot, new BotMovementState(), state, blackboard: board);
        var host = BotHost.Instance;
        var manager = new BotCombatManager();
        BotTestFixture.GetDictionary<BotCombatState>(manager, "_combatStates")[bot.Id] = state;
        host.Register(runtime);

        try
        {
            _ = board.Get(key, DateTime.UtcNow);
            manager.EndDuel(bot);

            await Assert.That(value.ComputedAt).IsNull();
        }
        finally
        {
            host.Unregister(bot.Id);
        }
    }

    [Test]
    public async Task EnableCombat_UsesRuntimeCombatStateInstance()
    {
        var bot = BotTestFixture.MakeBot(22, default);
        var runtimeState = new BotCombatState { BotId = bot.Id };
        var runtime = new BotRuntime(bot, new BotMovementState(), runtimeState);
        var host = BotHost.Instance;
        var manager = new BotCombatManager();
        host.Register(runtime);

        try
        {
            manager.EnableCombat(bot);

            await Assert.That(manager.GetState(bot)).IsSameReferenceAs(runtimeState);
        }
        finally
        {
            manager.StopListening(bot);
        }
    }

    [Test]
    public async Task EnableCombat_EnsureTaskPathCreatesRuntimeScopedMover()
    {
        var botManager = new BotManager(_ => null, onlineLookup: _ => null);
        BotTestFixture.RegisterSingletons(botManager);
        try
        {
            var bot = BotTestFixture.MakeBot(23, default);
            var movementState = new BotMovementState();
            var broadcaster = new BotMovementBroadcaster(bot);
            BotTestFixture.GetDictionary<BotMovementState>(botManager, "_botStates")[bot.Id] = movementState;
            BotTestFixture.GetDictionary<BotMovementBroadcaster>(botManager, "_broadcasters")[bot.Id] = broadcaster;
            var manager = new BotCombatManager();

            manager.EnableCombat(bot);

            await Assert.That(BotHost.Instance.GetRuntime(bot.Id)?.Mover).IsNotNull();
        }
        finally
        {
            BotTestFixture.ResetTaskManager();
        }
    }
}
