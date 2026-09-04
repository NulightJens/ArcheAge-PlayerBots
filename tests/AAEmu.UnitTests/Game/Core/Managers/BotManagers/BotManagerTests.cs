using System.Numerics;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Core.Managers.Id;
using AAEmu.Game.Core.Managers.UnitManagers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.AI.Bots;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Models;
using AAEmu.Game.Scripts.Commands;
using AAEmu.Game.Utils.Scripts;
using AAEmu.Game.Models.Tasks.Bots;
using AAEmu.UnitTests.Utils.Mocks;

namespace AAEmu.UnitTests.Game.Core.Managers.BotManagers;

[NotInParallel]
public class BotManagerTests
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
    public async Task Constructor_DoesNotCallDeps()
    {
        var mockWorld = Mock.Of<IWorldManager>();
        var mockCharacters = Mock.Of<ICharacterManager>();
        var mockSkills = Mock.Of<ISkillManager>();
        var mockObjectIds = Mock.Of<IObjectIdManager>();
        var mockTasks = Mock.Of<ITaskManager>();
        var mockEnterWorld = Mock.Of<IEnterWorldManager>();
        var mockArchetypes = Mock.Of<IBotArchetypeManager>();
        var mockCombat = Mock.Of<IBotCombatManager>();

        var manager = new BotManager(
            mockWorld.Object,
            mockCharacters.Object,
            mockSkills.Object,
            mockObjectIds.Object,
            mockTasks.Object,
            mockEnterWorld.Object,
            mockArchetypes.Object,
            mockCombat.Object);

        await Assert.That(manager).IsNotNull();
        Mock.VerifyNoOtherCalls(mockWorld);
        Mock.VerifyNoOtherCalls(mockCharacters);
        Mock.VerifyNoOtherCalls(mockSkills);
        Mock.VerifyNoOtherCalls(mockObjectIds);
        Mock.VerifyNoOtherCalls(mockTasks);
        Mock.VerifyNoOtherCalls(mockEnterWorld);
        Mock.VerifyNoOtherCalls(mockArchetypes);
        Mock.VerifyNoOtherCalls(mockCombat);
    }
    [Test]
    public async Task StopImmediately_KeepsFollowTarget()
    {
        var manager = new BotManager();
        var bot = BotTestFixture.MakeBot(2, Vector3.Zero);
        var target = BotTestFixture.MakeBot(3, Vector3.One);
        var state = new BotMovementState
        {
            Destination = new Vector3(4, 5, 6),
            FollowTarget = target,
            IsMoving = true,
            IsFalling = true,
            FallVelocity = 2
        };
        var broadcaster = new BotMovementBroadcaster(bot);
        BotTestFixture.GetDictionary<BotMovementState>(manager, "_botStates").TryAdd(bot.Id, state);
        BotTestFixture.GetDictionary<BotMovementBroadcaster>(manager, "_broadcasters").TryAdd(bot.Id, broadcaster);

        manager.StopImmediately(bot);

        await Assert.That(state.Destination).IsNull();
        await Assert.That(state.FollowTarget).IsSameReferenceAs(target);
        await Assert.That(state.IsMoving).IsFalse();
        await Assert.That(state.IsFalling).IsFalse();
        await Assert.That(state.FallVelocity).IsEqualTo(0f);
    }

    [Test]
    public async Task StopIfMoving_NoDestination_DoesNotStop()
    {
        var manager = new BotManager();
        var bot = BotTestFixture.MakeBot(4, Vector3.Zero);
        var state = new BotMovementState { IsFalling = true, FallVelocity = 3f };
        var broadcaster = new BotMovementBroadcaster(bot);
        var stopPackets = 0;
        broadcaster.MoveTypeSink = _ => stopPackets++;
        BotTestFixture.GetDictionary<BotMovementState>(manager, "_botStates")[bot.Id] = state;
        BotTestFixture.GetDictionary<BotMovementBroadcaster>(manager, "_broadcasters")[bot.Id] = broadcaster;

        var stopped = manager.StopIfMoving(bot);

        await Assert.That(stopped).IsFalse();
        await Assert.That(state.IsFalling).IsTrue();
        await Assert.That(state.FallVelocity).IsEqualTo(3f);
        await Assert.That(stopPackets).IsEqualTo(0);
    }

    [Test]
    public async Task StopIfMoving_WithDestination_Stops()
    {
        var manager = new BotManager();
        var bot = BotTestFixture.MakeBot(5, Vector3.Zero);
        var state = new BotMovementState
        {
            Destination = new Vector3(2, 0, 0),
            IsMoving = true
        };
        var broadcaster = new BotMovementBroadcaster(bot);
        var stopPackets = 0;
        broadcaster.MoveTypeSink = _ => stopPackets++;
        BotTestFixture.GetDictionary<BotMovementState>(manager, "_botStates")[bot.Id] = state;
        BotTestFixture.GetDictionary<BotMovementBroadcaster>(manager, "_broadcasters")[bot.Id] = broadcaster;

        var stopped = manager.StopIfMoving(bot);

        await Assert.That(stopped).IsTrue();
        await Assert.That(state.Destination).IsNull();
        await Assert.That(state.IsMoving).IsFalse();
        await Assert.That(stopPackets).IsEqualTo(1);
    }

    [Test]
    public async Task SetBotDestinationIfChanged_WithinTolerance_ReturnsFalse()
    {
        var manager = new BotManager();
        var bot = BotTestFixture.MakeBot(6, Vector3.Zero);
        var state = new BotMovementState
        {
            Destination = Vector3.Zero,
            IsRunning = false
        };
        BotTestFixture.GetDictionary<BotMovementState>(manager, "_botStates")[bot.Id] = state;

        var changed = manager.SetBotDestinationIfChanged(
            bot,
            new Vector3(0.25f, 0, 0),
            run: true,
            tolerance: 0.5f);

        await Assert.That(changed).IsFalse();
        await Assert.That(state.Destination).IsEqualTo(Vector3.Zero);
        await Assert.That(state.IsRunning).IsFalse();
    }

    [Test]
    public async Task SetBotDestinationIfChanged_MaxTolerance_OnlySetsWhenNoDestination()
    {
        var manager = new BotManager();
        var bot = BotTestFixture.MakeBot(7, Vector3.Zero);
        var state = new BotMovementState { Destination = Vector3.One };
        BotTestFixture.GetDictionary<BotMovementState>(manager, "_botStates")[bot.Id] = state;

        var existingChanged = manager.SetBotDestinationIfChanged(
            bot,
            new Vector3(100, 100, 100),
            run: false,
            tolerance: float.MaxValue);
        state.Destination = null;
        var missingChanged = manager.SetBotDestinationIfChanged(
            bot,
            new Vector3(3, 4, 5),
            run: true,
            tolerance: float.MaxValue);

        await Assert.That(existingChanged).IsFalse();
        await Assert.That(missingChanged).IsTrue();
        await Assert.That(state.Destination).IsEqualTo(new Vector3(3, 4, 5));
    }

    [Test]
    public async Task BasicCombat_Chase_ExistingDestination_NotRetargeted()
    {
        var previousBotManager = BotManager.Instance;
        var manager = new BotManager();
        var bot = BotTestFixture.MakeBot(8, Vector3.Zero);
        var target = BotTestFixture.MakeBot(9, new Vector3(10, 0, 0));
        var movementState = new BotMovementState
        {
            Destination = new Vector3(2, 0, 0),
            IsRunning = false
        };
        BotTestFixture.GetDictionary<BotMovementState>(manager, "_botStates")[bot.Id] = movementState;
        var modelManager = ModelManager.Instance;
        var previousModelTypes = BotTestFixture.GetPrivateField<Dictionary<uint, ModelType>>(modelManager, "_modelTypes");
        BotTestFixture.SetPrivateField(modelManager, "_modelTypes", new Dictionary<uint, ModelType>());

        try
        {
            BotTestFixture.RegisterSingletons(manager);

            var handled = BasicCombat.Execute(bot, new BotCombatState(), target);

            await Assert.That(handled).IsTrue();
            await Assert.That(movementState.Destination).IsEqualTo(new Vector3(2, 0, 0));
            await Assert.That(movementState.IsRunning).IsFalse();
        }
        finally
        {
            BotTestFixture.RegisterSingletons(previousBotManager);
            BotTestFixture.SetPrivateField(modelManager, "_modelTypes", previousModelTypes);
        }
    }

    [Test]
    public async Task BasicCombat_ChaseDestination_UsesHorizontalRangeAndNavigationSurface()
    {
        var destination = BasicCombat.ComputeChaseDestination(
            new Vector3(0f, 0f, 10f),
            new Vector3(10f, 0f, 20f),
            meleeRange: 1.5f,
            groundHeight: (_, _) => 12.25f);

        await Assert.That(destination).IsEqualTo(new Vector3(8.5f, 0f, 12.25f));
    }

    [Test]
    public async Task BasicCombat_ChaseDestination_UsesBotHeightWhenSurfaceIsUnavailable()
    {
        var destination = BasicCombat.ComputeChaseDestination(
            new Vector3(0f, 0f, 10f),
            new Vector3(10f, 0f, 20f),
            meleeRange: 1.5f,
            groundHeight: (_, _) => float.NaN);

        await Assert.That(destination).IsEqualTo(new Vector3(8.5f, 0f, 10f));
    }

    [Test]
    public async Task DespawnBot_Active_CancelsBothTasksAndRemovesAllState()
    {
        var previousCombatManager = BotCombatManager.Instance;
        var previousArchetypeManager = BotArchetypeManager.Instance;
        var fakeCombatManager = new FakeBotCombatManager();
        var fakeArchetypeManager = new FakeBotArchetypeManager();
        BotTestFixture.RegisterSingletons(fakeCombatManager, fakeArchetypeManager);

        try
        {
            var bot = BotTestFixture.MakeBot(2, Vector3.Zero);
            var state = new BotMovementState();
            var broadcaster = new BotMovementBroadcaster(bot);
            var movementTask = new BotMovementTask(bot, state, broadcaster);
            var taskManager = BotTestFixture.RegisterTaskManager();
            taskManager.Schedule(movementTask, TimeSpan.FromMinutes(1));

            var manager = new BotManager(Character.Load, leaveWorld: _ => { });
            BotTestFixture.GetDictionary<Character>(manager, "ActiveBots").TryAdd(bot.Id, bot);
            BotTestFixture.GetDictionary<BotMovementState>(manager, "_botStates").TryAdd(bot.Id, state);
            BotTestFixture.GetDictionary<BotMovementBroadcaster>(manager, "_broadcasters").TryAdd(bot.Id, broadcaster);
            BotTestFixture.GetDictionary<AAEmu.Game.Models.Tasks.Task>(manager, "_movementTasks").TryAdd(bot.Id, movementTask);
            fakeCombatManager.States[bot.Id] = new BotCombatState();
            fakeArchetypeManager.States[bot.Id] = new BotArchetypeState();

            var result = manager.DespawnBot(bot.Id);

            await Assert.That(result).IsTrue();
            await Assert.That(movementTask.Cancelled).IsTrue();
            await Assert.That(fakeCombatManager.StopListeningCalls).Contains(bot.Id);
            await Assert.That(fakeCombatManager.GetState(bot)).IsNull();
            await Assert.That(fakeArchetypeManager.RemoveStateCalls).Contains(bot.Id);
            await Assert.That(fakeArchetypeManager.GetState(bot)).IsNull();
            await Assert.That(manager.GetBotState(bot.Id)).IsNull();
            await Assert.That(manager.GetBroadcaster(bot.Id)).IsNull();
            await Assert.That(bot.DisabledSetPosition).IsTrue();
        }
        finally
        {
            BotTestFixture.RegisterSingletons(previousCombatManager, previousArchetypeManager);
        }
    }

    [Test]
    public async Task IsMovementTaskRunning_AfterUnscheduledTaskSelfCancels_ReportsFalse()
    {
        var manager = new BotManager();
        var bot = BotTestFixture.MakeBot(12, Vector3.Zero);
        var state = new BotMovementState();
        var task = new BotMovementTask(bot, state, new BotMovementBroadcaster(bot));
        BotTestFixture.SetPrivateField<AAEmu.Game.Models.Game.World.WorldInstance>(bot, "_parentWorld", null);

        task.Execute();
        BotTestFixture.GetDictionary<AAEmu.Game.Models.Tasks.Task>(manager, "_movementTasks")[bot.Id] = task;

        await Assert.That(task.Cancelled).IsTrue();
        await Assert.That(manager.IsMovementTaskRunning(bot.Id)).IsFalse();
    }

    [Test]
    public async Task SpawnBot_AfterDespawnSameId_GetsFreshCombatTask()
    {
        var previousBotManager = BotManager.Instance;
        var previousCombatManager = BotCombatManager.Instance;

        try
        {
            var taskManager = BotTestFixture.RegisterTaskManager();
            var manager = new BotManager(Character.Load, leaveWorld: _ => { });
            var combatManager = new BotCombatManager();
            BotTestFixture.RegisterSingletons(manager, combatManager);

            var firstBot = BotTestFixture.MakeBot(2, Vector3.Zero);
            var firstState = new BotMovementState();
            var firstBroadcaster = new BotMovementBroadcaster(firstBot);
            var firstMovementTask = new BotMovementTask(firstBot, firstState, firstBroadcaster);
            taskManager.Schedule(firstMovementTask, TimeSpan.FromMinutes(1));
            BotTestFixture.GetDictionary<Character>(manager, "ActiveBots").TryAdd(firstBot.Id, firstBot);
            BotTestFixture.GetDictionary<BotMovementState>(manager, "_botStates").TryAdd(firstBot.Id, firstState);
            BotTestFixture.GetDictionary<BotMovementBroadcaster>(manager, "_broadcasters").TryAdd(firstBot.Id, firstBroadcaster);
            BotTestFixture.GetDictionary<AAEmu.Game.Models.Tasks.Task>(manager, "_movementTasks").TryAdd(firstBot.Id, firstMovementTask);
            combatManager.StartListening(firstBot);

            await Assert.That(combatManager.IsTaskRunning(firstBot.Id)).IsTrue();
            await Assert.That(manager.DespawnBot(firstBot.Id)).IsTrue();
            await Assert.That(combatManager.IsTaskRunning(firstBot.Id)).IsFalse();

            var secondBot = BotTestFixture.MakeBot(2, Vector3.One);
            var secondState = new BotMovementState();
            var secondBroadcaster = new BotMovementBroadcaster(secondBot);
            BotTestFixture.GetDictionary<Character>(manager, "ActiveBots").TryAdd(secondBot.Id, secondBot);
            BotTestFixture.GetDictionary<BotMovementState>(manager, "_botStates").TryAdd(secondBot.Id, secondState);
            BotTestFixture.GetDictionary<BotMovementBroadcaster>(manager, "_broadcasters").TryAdd(secondBot.Id, secondBroadcaster);

            combatManager.StartListening(secondBot);

            await Assert.That(combatManager.IsTaskRunning(secondBot.Id)).IsTrue();
        }
        finally
        {
            BotTestFixture.RegisterSingletons(previousBotManager, previousCombatManager);
        }
    }

    [Test]
    public async Task SpawnBot_ArchetypeThrows_RollsBackAndRethrows()
    {
        var bot = BotTestFixture.MakeBot(7, Vector3.Zero);
        bot.MaxHp = 100;
        bot.Hp = 100;
        var previousCombatManager = BotCombatManager.Instance;
        var previousArchetypeManager = BotArchetypeManager.Instance;
        var fakeCombatManager = new FakeBotCombatManager();
        var fakeArchetypeManager = new FakeBotArchetypeManager();
        var rolledBack = new List<Character>();
        var manager = new BotManager(
            _ => bot,
            onlineLookup: _ => null,
            fullLoader: _ => { },
            onBotSpawn: _ => throw new InvalidOperationException("archetype setup failed"),
            saveAndRemove: rolledBack.Add,
            leaveWorld: _ => { },
            setWorld: character => BotTestFixture.SetPrivateField(character, "_parentWorld", BotTestFixture.MakeWorld()),
            prepareCharacter: _ => false,
            spawn: _ => { });

        try
        {
            Character.UsedCharacterObjIds[bot.Id] = bot.ObjId;
            BotTestFixture.RegisterSingletons(fakeCombatManager, fakeArchetypeManager);

            Assert.Throws<InvalidOperationException>(() => manager.SpawnBot(bot.Id));

            await Assert.That(manager.GetAllBots()).IsEmpty();
            await Assert.That(rolledBack).Contains(bot);
            await Assert.That(fakeCombatManager.StopListeningCalls).Contains(bot.Id);
            await Assert.That(fakeArchetypeManager.RemoveStateCalls).Contains(bot.Id);
        }
        finally
        {
            Character.UsedCharacterObjIds.Remove(bot.Id);
            BotTestFixture.RegisterSingletons(previousCombatManager, previousArchetypeManager);
        }
    }

    [Test]
    public async Task SpawnBot_InitializesArchetypeSilentlyBeforeWorldSpawn()
    {
        var previousBotManager = BotManager.Instance;
        var previousCombatManager = BotCombatManager.Instance;
        var fakeCombatManager = new FakeBotCombatManager();
        var bot = BotTestFixture.MakeBot(1008, Vector3.Zero);
        bot.MaxHp = 100;
        bot.Hp = 100;
        var sequence = new List<string>();
        var suppressedDuringArchetype = false;
        var suppressionClearedBeforeSpawn = false;
        Character.UsedCharacterObjIds.Remove(bot.Id);

        var manager = new BotManager(
            _ => bot,
            onlineLookup: _ => null,
            fullLoader: _ => { },
            onBotSpawn: character =>
            {
                sequence.Add("archetype");
                suppressedDuringArchetype = character.SuppressBroadcastPackets;
            },
            saveAndRemove: _ => { },
            leaveWorld: _ => { },
            setWorld: character => BotTestFixture.SetPrivateField(character, "_parentWorld", BotTestFixture.MakeWorld()),
            prepareCharacter: _ => false,
            spawn: character =>
            {
                sequence.Add("spawn");
                suppressionClearedBeforeSpawn = !character.SuppressBroadcastPackets;
            });

        try
        {
            Character.UsedCharacterObjIds[bot.Id] = bot.ObjId;
            BotTestFixture.RegisterSingletons(manager, fakeCombatManager);

            var result = manager.SpawnBot(bot.Id, out var spawnedBot);

            await Assert.That(result).IsEqualTo(SpawnResult.Ok);
            await Assert.That(spawnedBot).IsSameReferenceAs(bot);
            await Assert.That(sequence).IsEquivalentTo(["archetype", "spawn"]);
            await Assert.That(suppressedDuringArchetype).IsTrue();
            await Assert.That(suppressionClearedBeforeSpawn).IsTrue();
            await Assert.That(bot.SuppressBroadcastPackets).IsFalse();
        }
        finally
        {
            manager.DespawnBot(bot.Id);
            Character.UsedCharacterObjIds.Remove(bot.Id);
            BotTestFixture.RegisterSingletons(previousBotManager, previousCombatManager);
        }
    }

    [Test]
    public async Task SpawnBot_PublishesEquipmentVisibilityAfterWorldSpawn()
    {
        var previousBotManager = BotManager.Instance;
        var previousCombatManager = BotCombatManager.Instance;
        var fakeCombatManager = new FakeBotCombatManager();
        var bot = BotTestFixture.MakeBot(1010, Vector3.Zero);
        bot.MaxHp = 100;
        bot.Hp = 100;
        var sequence = new List<string>();
        Character published = null;
        Character.UsedCharacterObjIds.Remove(bot.Id);

        var manager = new BotManager(
            _ => bot,
            onlineLookup: _ => null,
            fullLoader: _ => { },
            onBotSpawn: _ => sequence.Add("archetype"),
            saveAndRemove: _ => { },
            leaveWorld: _ => { },
            setWorld: character => BotTestFixture.SetPrivateField(character, "_parentWorld", BotTestFixture.MakeWorld()),
            prepareCharacter: _ => false,
            spawn: _ => sequence.Add("spawn"),
            publishEquipmentVisibility: character =>
            {
                sequence.Add("equipment-public");
                published = character;
            });

        try
        {
            Character.UsedCharacterObjIds[bot.Id] = bot.ObjId;
            BotTestFixture.RegisterSingletons(manager, fakeCombatManager);

            var result = manager.SpawnBot(bot.Id, out var spawnedBot);

            await Assert.That(result).IsEqualTo(SpawnResult.Ok);
            await Assert.That(spawnedBot).IsSameReferenceAs(bot);
            await Assert.That(published).IsSameReferenceAs(bot);
            await Assert.That(sequence).IsEquivalentTo(["archetype", "spawn", "equipment-public"]);
        }
        finally
        {
            manager.DespawnBot(bot.Id);
            Character.UsedCharacterObjIds.Remove(bot.Id);
            BotTestFixture.RegisterSingletons(previousBotManager, previousCombatManager);
        }
    }

    [Test]
    public async Task SpawnBot_Success_RebindsTeamOnceAfterRuntimeAndActiveRegistration()
    {
        var previousBotManager = BotManager.Instance;
        var previousCombatManager = BotCombatManager.Instance;
        var fakeCombatManager = new FakeBotCombatManager();
        var bot = BotTestFixture.MakeBot(1009, Vector3.Zero);
        bot.MaxHp = 100;
        bot.Hp = 100;
        var sequence = new List<string>();
        var rebindCalls = 0;
        var runtimeVisibleDuringRebind = false;
        var activeBotVisibleDuringRebind = false;
        BotManager manager = null;
        Character.UsedCharacterObjIds.Remove(bot.Id);

        manager = new BotManager(
            _ => bot,
            onlineLookup: _ => null,
            fullLoader: _ => { },
            onBotSpawn: _ => sequence.Add("archetype"),
            saveAndRemove: _ => { },
            leaveWorld: _ => { },
            setWorld: character => BotTestFixture.SetPrivateField(character, "_parentWorld", BotTestFixture.MakeWorld()),
            prepareCharacter: _ => false,
            spawn: _ => sequence.Add("spawn"),
            teamLoginRebind: character =>
            {
                sequence.Add("team-rebind");
                rebindCalls++;
                runtimeVisibleDuringRebind = BotHost.Instance.GetRuntime(character.Id) != null;
                activeBotVisibleDuringRebind = ReferenceEquals(manager.GetBot(character.Id), character);
            });

        try
        {
            Character.UsedCharacterObjIds[bot.Id] = bot.ObjId;
            BotTestFixture.RegisterSingletons(manager, fakeCombatManager);

            var result = manager.SpawnBot(bot.Id, out var spawnedBot);

            await Assert.That(result).IsEqualTo(SpawnResult.Ok);
            await Assert.That(spawnedBot).IsSameReferenceAs(bot);
            await Assert.That(sequence).Count().IsEqualTo(3);
            await Assert.That(sequence[0]).IsEqualTo("archetype");
            await Assert.That(sequence[1]).IsEqualTo("spawn");
            await Assert.That(sequence[2]).IsEqualTo("team-rebind");
            await Assert.That(rebindCalls).IsEqualTo(1);
            await Assert.That(runtimeVisibleDuringRebind).IsTrue();
            await Assert.That(activeBotVisibleDuringRebind).IsTrue();

            await Assert.That(manager.DespawnBot(bot.Id)).IsTrue();
            await Assert.That(rebindCalls).IsEqualTo(1);
        }
        finally
        {
            BotHost.Instance.Unregister(bot.Id);
            Character.UsedCharacterObjIds.Remove(bot.Id);
            BotTestFixture.RegisterSingletons(previousBotManager, previousCombatManager);
        }
    }

    [Test]
    public async Task SpawnBot_TeamRebindThrows_RollsBackWithoutRetry()
    {
        var previousBotManager = BotManager.Instance;
        var previousCombatManager = BotCombatManager.Instance;
        var previousArchetypeManager = BotArchetypeManager.Instance;
        var fakeCombatManager = new FakeBotCombatManager();
        var fakeArchetypeManager = new FakeBotArchetypeManager();
        var bot = BotTestFixture.MakeBot(1010, Vector3.Zero);
        bot.MaxHp = 100;
        bot.Hp = 100;
        var rebindCalls = 0;
        var rolledBack = new List<Character>();
        Character.UsedCharacterObjIds.Remove(bot.Id);

        var manager = new BotManager(
            _ => bot,
            onlineLookup: _ => null,
            fullLoader: _ => { },
            onBotSpawn: fakeArchetypeManager.OnBotSpawn,
            saveAndRemove: rolledBack.Add,
            leaveWorld: _ => { },
            setWorld: character => BotTestFixture.SetPrivateField(character, "_parentWorld", BotTestFixture.MakeWorld()),
            prepareCharacter: _ => false,
            spawn: _ => { },
            teamLoginRebind: _ =>
            {
                rebindCalls++;
                throw new InvalidOperationException("team rebind failed");
            });

        try
        {
            Character.UsedCharacterObjIds[bot.Id] = bot.ObjId;
            BotTestFixture.RegisterSingletons(manager, fakeCombatManager, fakeArchetypeManager);

            Assert.Throws<InvalidOperationException>(() => manager.SpawnBot(bot.Id));

            await Assert.That(rebindCalls).IsEqualTo(1);
            await Assert.That(manager.GetAllBots()).IsEmpty();
            await Assert.That(BotHost.Instance.GetRuntime(bot.Id)).IsNull();
            await Assert.That(rolledBack).Contains(bot);
            await Assert.That(fakeCombatManager.StopListeningCalls).Contains(bot.Id);
            await Assert.That(fakeArchetypeManager.RemoveStateCalls).Contains(bot.Id);
        }
        finally
        {
            Character.UsedCharacterObjIds.Remove(bot.Id);
            BotTestFixture.RegisterSingletons(previousBotManager, previousCombatManager, previousArchetypeManager);
        }
    }

    [Test]
    public async Task SpawnBot_ConcurrentSameId_SecondRollsBack()
    {
        var previousBotManager = BotManager.Instance;
        var previousCombatManager = BotCombatManager.Instance;
        var previousArchetypeManager = BotArchetypeManager.Instance;
        var fakeCombatManager = new FakeBotCombatManager();
        var fakeArchetypeManager = new FakeBotArchetypeManager();
        var taskManager = BotTestFixture.RegisterTaskManager();
        var activeBot = BotTestFixture.MakeBot(1007, Vector3.Zero);
        var loadedBot = BotTestFixture.MakeBot(1007, Vector3.One);
        var rolledBack = new List<Character>();
        var teamRebindCalls = 0;
        BotManager manager = null;
        manager = new BotManager(
            _ =>
            {
                BotTestFixture.GetDictionary<Character>(manager, "ActiveBots").TryAdd(activeBot.Id, activeBot);
                return loadedBot;
            },
            onlineLookup: _ => null,
            fullLoader: _ => { },
            onBotSpawn: fakeArchetypeManager.OnBotSpawn,
            saveAndRemove: rolledBack.Add,
            leaveWorld: _ => { },
            setWorld: character => BotTestFixture.SetPrivateField(character, "_parentWorld", BotTestFixture.MakeWorld()),
            prepareCharacter: _ => false,
            spawn: _ => { },
            teamLoginRebind: _ => teamRebindCalls++);

        try
        {
            Character.UsedCharacterObjIds[loadedBot.Id] = loadedBot.ObjId;
            BotTestFixture.RegisterSingletons(manager, fakeCombatManager, fakeArchetypeManager);

            var result = manager.SpawnBot(loadedBot.Id, out var bot);

            await Assert.That(result).IsEqualTo(SpawnResult.AlreadyActive);
            await Assert.That(bot).IsNull();
            await Assert.That(manager.GetBot(loadedBot.Id)).IsSameReferenceAs(activeBot);
            await Assert.That(manager.GetBotState(loadedBot.Id)).IsNull();
            await Assert.That(manager.GetBroadcaster(loadedBot.Id)).IsNull();
            await Assert.That(taskManager.GetQueueCount()).IsEqualTo(0);
            await Assert.That(rolledBack).Contains(loadedBot);
            await Assert.That(teamRebindCalls).IsEqualTo(0);
            await Assert.That(fakeCombatManager.StopListeningCalls).Contains(loadedBot.Id);
            await Assert.That(fakeArchetypeManager.RemoveStateCalls).Contains(loadedBot.Id);
        }
        finally
        {
            BotTestFixture.GetDictionary<Character>(manager, "ActiveBots").TryRemove(activeBot.Id, out _);
            Character.UsedCharacterObjIds.Remove(loadedBot.Id);
            BotTestFixture.RegisterSingletons(previousBotManager, previousCombatManager, previousArchetypeManager);
        }
    }

    [Test]
    public async Task SpawnBot_AlreadyActive_ReturnsAlreadyActiveWithoutSecondLoad()
    {
        var previousBotManager = BotManager.Instance;
        var previousCombatManager = BotCombatManager.Instance;
        var fakeCombatManager = new FakeBotCombatManager();
        var bot = BotTestFixture.MakeBot(1002, Vector3.Zero);
        var loadCalls = 0;
        Character.UsedCharacterObjIds.Remove(bot.Id);
        var manager = new BotManager(
            _ =>
            {
                loadCalls++;
                return bot;
            },
            onlineLookup: _ => null,
            fullLoader: _ => { },
            onBotSpawn: _ => { },
            leaveWorld: _ => { },
            setWorld: character => BotTestFixture.SetPrivateField(character, "_parentWorld", BotTestFixture.MakeWorld()),
            prepareCharacter: _ => false,
            spawn: _ => { });

        try
        {
            Character.UsedCharacterObjIds[bot.Id] = bot.ObjId;
            BotTestFixture.RegisterSingletons(manager, fakeCombatManager);

            var firstResult = manager.SpawnBot(bot.Id, out var firstBot);
            var secondResult = manager.SpawnBot(bot.Id, out var secondBot);

            await Assert.That(firstResult).IsEqualTo(SpawnResult.Ok);
            await Assert.That(firstBot).IsSameReferenceAs(bot);
            await Assert.That(secondResult).IsEqualTo(SpawnResult.AlreadyActive);
            await Assert.That(secondBot).IsNull();
            await Assert.That(loadCalls).IsEqualTo(1);
        }
        finally
        {
            manager.DespawnBot(bot.Id);
            Character.UsedCharacterObjIds.Remove(bot.Id);
            BotTestFixture.RegisterSingletons(previousBotManager, previousCombatManager);
        }
    }

    [Test]
    public async Task SpawnBot_CharacterOnline_ReturnsOnline()
    {
        var onlineBot = BotTestFixture.MakeBot(1003, Vector3.Zero);
        var loadCalls = 0;
        var manager = new BotManager(
            _ =>
            {
                loadCalls++;
                return onlineBot;
            },
            onlineLookup: _ => onlineBot);

        var result = manager.SpawnBot(onlineBot.Id, out var bot);

        await Assert.That(result).IsEqualTo(SpawnResult.Online);
        await Assert.That(bot).IsNull();
        await Assert.That(loadCalls).IsEqualTo(0);
    }

    [Test]
    public async Task AddBot_AlreadyActive_SendsAlreadySpawnedError()
    {
        var previousBotManager = BotManager.Instance;
        var manager = new BotManager(_ => throw new InvalidOperationException("loader should not run"), onlineLookup: _ => null);
        var bot = BotTestFixture.MakeBot(1004, Vector3.Zero);
        BotTestFixture.GetDictionary<Character>(manager, "ActiveBots").TryAdd(bot.Id, bot);

        try
        {
            BotTestFixture.RegisterSingletons(manager);
            var output = new CharacterMessageOutput(Mock.Of<ICharacter>().Object);

            new AddBot().Execute(null, [bot.Id.ToString()], output);

            await Assert.That(output.Messages.Single()).Contains("already spawned");
        }
        finally
        {
            BotTestFixture.GetDictionary<Character>(manager, "ActiveBots").TryRemove(bot.Id, out _);
            BotTestFixture.RegisterSingletons(previousBotManager);
        }
    }

    [Test]
    public async Task BotReset_Active_CallsResetBotOnce()
    {
        var previousBotManager = BotManager.Instance;
        var previousCombatManager = BotCombatManager.Instance;
        var manager = new BotManager(_ => throw new InvalidOperationException("loader should not run"), onlineLookup: _ => null);
        var combatManager = new FakeBotCombatManager();
        var bot = BotTestFixture.MakeBot(1005, Vector3.Zero);
        BotTestFixture.GetDictionary<Character>(manager, "ActiveBots").TryAdd(bot.Id, bot);

        try
        {
            BotTestFixture.RegisterSingletons(manager, combatManager);
            var output = new CharacterMessageOutput(Mock.Of<ICharacter>().Object);

            new BotResetCommand().Execute(null, [bot.Id.ToString()], output);

            await Assert.That(combatManager.ResetBotCalls).Contains(bot.Id);
            await Assert.That(combatManager.ResetBotCalls.Count).IsEqualTo(1);
        }
        finally
        {
            BotTestFixture.GetDictionary<Character>(manager, "ActiveBots").TryRemove(bot.Id, out _);
            BotTestFixture.RegisterSingletons(previousBotManager, previousCombatManager);
        }
    }

    [Test]
    public async Task RemoveBot_InDuel_RefusedWithMessage()
    {
        var previousBotManager = BotManager.Instance;
        var manager = new BotManager(_ => throw new InvalidOperationException("loader should not run"), onlineLookup: _ => null);
        var bot = BotTestFixture.MakeBot(1006, Vector3.Zero);
        bot.IsInDuel = true;
        BotTestFixture.GetDictionary<Character>(manager, "ActiveBots").TryAdd(bot.Id, bot);

        try
        {
            BotTestFixture.RegisterSingletons(manager);
            var output = new CharacterMessageOutput(Mock.Of<ICharacter>().Object);

            new RemoveBot().Execute(null, [bot.Id.ToString()], output);

            await Assert.That(output.Messages.Single()).Contains("end the duel first");
            await Assert.That(manager.GetBot(bot.Id)).IsSameReferenceAs(bot);
        }
        finally
        {
            BotTestFixture.GetDictionary<Character>(manager, "ActiveBots").TryRemove(bot.Id, out _);
            BotTestFixture.RegisterSingletons(previousBotManager);
        }
    }
}
