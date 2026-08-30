using System.Collections.Concurrent;
using System.Globalization;
using System.Numerics;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Expeditions;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Scripts.Commands;
using AAEmu.Game.Utils.Scripts;
using AAEmu.UnitTests.Utils.Mocks;

namespace AAEmu.UnitTests.Game.Scripts.Commands;

[NotInParallel]
public class BotCommandsTests
{
    private BotManager _previousBotManager;
    private BotCombatManager _previousCombatManager;
    private BotArchetypeManager _previousArchetypeManager;
    private Func<IDuelManager> _previousDuelManagerResolver;
    private Func<uint, BuffTemplate> _previousBuffTemplateResolver;
    private BotManager _botManager;
    private FakeBotCombatManager _combatManager;
    private FakeBotArchetypeManager _archetypeManager;

    [Before(Test)]
    public void Setup()
    {
        BotTestFixture.RegisterTaskManager();
        _previousBotManager = BotManager.Instance;
        _previousCombatManager = BotCombatManager.Instance;
        _previousArchetypeManager = BotArchetypeManager.Instance;
        _previousDuelManagerResolver = BotDuelCommand.DuelManagerResolver;
        _previousBuffTemplateResolver = BotBuffCommand.BuffTemplateResolver;

        _botManager = new BotManager(_ => null, onlineLookup: _ => null);
        _combatManager = new FakeBotCombatManager();
        _archetypeManager = new FakeBotArchetypeManager();

        BotTestFixture.RegisterSingletons(_botManager, _combatManager, _archetypeManager);
    }

    [After(Test)]
    public void Teardown()
    {
        BotTestFixture.RegisterSingletons(_previousBotManager, _previousCombatManager, _previousArchetypeManager);
        BotDuelCommand.DuelManagerResolver = _previousDuelManagerResolver;
        BotBuffCommand.BuffTemplateResolver = _previousBuffTemplateResolver;
    }

    [Test]
    public async Task SetBotClass_ExposesRecoveredHumanFacingAliases()
    {
        var command = new SetBotClass();

        await Assert.That(command.CommandNames).Contains("setclass");
        await Assert.That(command.CommandNames).Contains("botsetclass");
        await Assert.That(command.CommandNames).Contains("setarchetype");
        await Assert.That(SetBotClass.TreeName(AbilityType.Fight)).IsEqualTo("Battlerage");
        await Assert.That(SetBotClass.TreeName(AbilityType.Will)).IsEqualTo("Auramancy");
        await Assert.That(SetBotClass.TreeName(AbilityType.Vocation)).IsEqualTo("Shadowplay");
    }

    [Test]
    public async Task BotGear_ExposesShowEquipAndInspectWorkflow()
    {
        var command = new BotGearCommand();

        await Assert.That(command.CommandNames).Contains("botgear");
        await Assert.That(command.CommandNames).Contains("botequip");
        await Assert.That(command.GetCommandLineHelp()).Contains("show|equip|inspect");
    }

    [Test]
    public async Task BotState_NonNumeric_SendsHelp()
    {
        var output = Execute(new BotStateCommand(), "x");

        await Assert.That(output.Messages).HasSingleItem();
        await Assert.That(output.Messages.Single()).Contains("Help for |cFFFFFFFF/botstate|r");
    }

    [Test]
    public async Task MoveBot_UnknownFifthArg_DefaultsToRun_Characterization()
    {
        var bot = AddBot(2);

        var output = Execute(new MoveBot(), "2", "1", "2", "3", "crawl");

        var state = _botManager.GetBotState(bot.Id);
        await Assert.That(state.Destination).IsEqualTo(new Vector3(1, 2, 3));
        await Assert.That(state.IsRunning).IsTrue();
        await Assert.That(output.Messages.Single()).Contains("running");
    }

    [Test]
    public async Task MoveBot_DecimalCoordinatesUnderGermanCulture_ParseInvariantly()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            var bot = AddBot(2);

            Execute(new MoveBot(), "2", "12.5", "2", "3");

            await Assert.That(_botManager.GetBotState(bot.Id).Destination).IsEqualTo(new Vector3(12.5f, 2, 3));
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Test]
    public async Task MoveBot_NaNCoordinate_Rejected()
    {
        AddBot(2);

        var output = Execute(new MoveBot(), "2", "NaN", "2", "3");

        await Assert.That(output.Messages.Single()).Contains("Help for |cFFFFFFFF/movebot|r");
        await Assert.That(_botManager.GetBotState(2).Destination).IsNull();
    }

    [Test]
    public async Task BotArchetype_UnknownKeyword_Rejected()
    {
        var bot = AddBot(2);
        _archetypeManager.States[bot.Id] = new BotArchetypeState
        {
            IsInitialized = true,
            ArchetypeName = "Darkrunner"
        };

        var output = Execute(new BotArchetypeCommand(), "2", "rerol");

        await Assert.That(output.Messages.Single()).Contains("Unknown archetype action");
    }

    [Test]
    public async Task BotState_Free_MessageReportsActualCurrentState()
    {
        var bot = AddBot(2);
        var state = new BotCombatState
        {
            CurrentState = BotCombatStateType.Grinding,
            ForcedState = BotCombatStateType.Grinding,
            IsActive = true
        };
        _combatManager.States[bot.Id] = state;

        var output = Execute(new BotStateCommand(), "2", "free");

        await Assert.That(state.IsForced).IsFalse();
        await Assert.That(_combatManager.StartListeningCalls).Contains(bot.Id);
        await Assert.That(output.Messages.Single()).Contains("current state: Grinding");
        await Assert.That(output.Messages.Single()).DoesNotContain("returned to idle");
    }

    [Test]
    public async Task BotState_Free_IdleActive_TransitionsToGrinding()
    {
        var bot = AddBot(2);
        var state = new BotCombatState
        {
            CurrentState = BotCombatStateType.Idle,
            IsActive = true,
            ForcedState = BotCombatStateType.Grinding
        };
        _combatManager.States[bot.Id] = state;

        var output = Execute(new BotStateCommand(), "2", "free");

        await Assert.That(state.CurrentState).IsEqualTo(BotCombatStateType.Grinding);
        await Assert.That(output.Messages.Single()).Contains("current state: Grinding");
    }

    [Test]
    public async Task BotState_GrindingKillGoalArmsOneKill()
    {
        var bot = AddBot(2);
        var state = new BotCombatState
        {
            CurrentState = BotCombatStateType.Idle,
            KillCount = 7,
            KillGoal = 9
        };
        _combatManager.States[bot.Id] = state;

        var output = Execute(new BotStateCommand(), "2", "grind", "1");

        await Assert.That(state.KillGoal).IsEqualTo(1);
        await Assert.That(state.KillCount).IsEqualTo(0);
        await Assert.That(state.IsActive).IsTrue();
        await Assert.That(state.ForcedState).IsEqualTo(BotCombatStateType.Grinding);
        await Assert.That(_combatManager.StartListeningCalls).Contains(bot.Id);
        await Assert.That(output.Messages.Single()).Contains("kill goal 1");
    }

    [Test]
    public async Task BotState_Following_ReattachesListenerEvenWhenInactive()
    {
        var bot = AddBot(2);
        var state = new BotCombatState
        {
            CurrentState = BotCombatStateType.Idle,
            IsActive = false
        };
        _combatManager.States[bot.Id] = state;

        Execute(new BotStateCommand(), "2", "following");

        await Assert.That(state.CurrentState).IsEqualTo(BotCombatStateType.Following);
        await Assert.That(_combatManager.StartListeningCalls).Contains(bot.Id);
    }

    [Test]
    public async Task BotState_GrindingRejectsNonPositiveKillGoal()
    {
        var bot = AddBot(2);
        var state = new BotCombatState
        {
            CurrentState = BotCombatStateType.Idle,
            KillCount = 7,
            KillGoal = 9
        };
        _combatManager.States[bot.Id] = state;

        var output = Execute(new BotStateCommand(), "2", "grind", "0");

        await Assert.That(state.KillGoal).IsEqualTo(9);
        await Assert.That(state.KillCount).IsEqualTo(7);
        await Assert.That(state.CurrentState).IsEqualTo(BotCombatStateType.Idle);
        await Assert.That(output.Messages.Single()).Contains("positive kill goal");
    }

    [Test]
    public async Task BotState_IdleWhileInCombat_DisengagesImmediately()
    {
        var bot = AddBot(2);
        var target = AddBot(3);
        var state = new BotCombatState
        {
            CurrentState = BotCombatStateType.Combat,
            ForcedState = BotCombatStateType.Grinding,
            IsActive = true,
            Target = target
        };
        bot.CurrentTarget = target;
        _combatManager.States[bot.Id] = state;
        _botManager.SetFollowTarget(bot, target);
        _botManager.SetBotDestination(bot, 10, 20, 30);

        var output = Execute(new BotStateCommand(), "2", "idle");

        var movement = _botManager.GetBotState(bot.Id);
        await Assert.That(state.CurrentState).IsEqualTo(BotCombatStateType.Idle);
        await Assert.That(state.ForcedState).IsEqualTo(BotCombatStateType.Idle);
        await Assert.That(state.IsActive).IsFalse();
        await Assert.That(state.Target).IsNull();
        await Assert.That(bot.CurrentTarget).IsNull();
        await Assert.That(movement.FollowTarget).IsNull();
        await Assert.That(movement.Destination).IsNull();
        await Assert.That(output.Messages.Single()).Contains("forced into Idle state");
    }

    [Test]
    public async Task BotDebug_SearchingState_ExposesLossSearchAndDuelDiagnostics()
    {
        var bot = AddDebugBot(2);
        var opponent = AddBot(3);
        bot.CurrentTarget = opponent;
        var state = new BotCombatState
        {
            CurrentState = BotCombatStateType.Searching,
            PreviousState = BotCombatStateType.Dueling,
            ForcedState = BotCombatStateType.Grinding,
            IsActive = true,
            IsSearching = true,
            SearchStartTime = DateTime.UtcNow.AddSeconds(-5),
            SearchRadius = 4.5f,
            SearchAngle = 1.25f,
            LastKnownTargetPosition = new Vector3(10, 20, 30),
            InDuel = true,
            DuelOpponent = opponent
        };
        _combatManager.States[bot.Id] = state;

        var output = Execute(new BotDebugCommand(), "2");

        await Assert.That(output.Messages).Contains(message =>
            message.Contains("State: Searching, Previous: Dueling, Forced: Grinding"));
        await Assert.That(output.Messages).Contains("[botdebug] Stealthed: False");
        await Assert.That(output.Messages).Contains(message =>
            message.Contains("Target: null, CurrentTarget: bot3"));
        await Assert.That(output.Messages).Contains(message =>
            message.Contains("Duel: active=True, opponent=bot3"));
        await Assert.That(output.Messages).Contains(message =>
            message.Contains("Search: active=True") &&
            message.Contains("radius=4.50") &&
            message.Contains("angle=1.25") &&
            message.Contains("last_known=<10, 20, 30>"));
    }

    [Test]
    public async Task BotBuff_AppliesAndRemovesStealthWithoutASelectedTarget()
    {
        var bot = AddBot(2);
        var active = false;
        Buff appliedBuff = null;
        var buffs = Mock.Of<IBuffs>();
        buffs.CheckBuff(599).Returns(_ => active);
        buffs.AddBuff(Any<Buff>(), Any<uint>(), Any<int>())
            .Callback((Buff buff, uint index, int forcedDuration) =>
            {
                appliedBuff = buff;
                active = true;
            });
        buffs.RemoveBuff(599).Callback(_ => active = false);
        bot.Buffs = buffs.Object;
        BotBuffCommand.BuffTemplateResolver = id => id == 599
            ? new BuffTemplate { Id = 599, Duration = 45000, Stealth = true }
            : null;

        var applied = Execute(new BotBuffCommand(), "2", "599", "1");
        var wasApplied = bot.Buffs.CheckBuff(599);
        var removed = Execute(new BotBuffCommand(), "2", "-599");

        await Assert.That(wasApplied).IsTrue();
        await Assert.That(bot.Buffs.CheckBuff(599)).IsFalse();
        await Assert.That(appliedBuff).IsNotNull();
        await Assert.That(appliedBuff.Template.Stealth).IsTrue();
        await Assert.That(appliedBuff.Owner).IsEqualTo(bot);
        await Assert.That(appliedBuff.Caster).IsEqualTo(bot);
        await Assert.That(applied.Messages.Single()).Contains("stealth=True");
        await Assert.That(removed.Messages.Single()).Contains("Removed buff 599");
    }

    [Test]
    public async Task BotBuff_UnknownBuff_ReportsErrorWithoutMutation()
    {
        var bot = AddBot(2);
        BotBuffCommand.BuffTemplateResolver = _ => null;

        var output = Execute(new BotBuffCommand(), "2", "999999");

        await Assert.That(bot.Buffs.CheckBuff(999999)).IsFalse();
        await Assert.That(output.Messages.Single()).Contains("Unknown buff id 999999");
    }

    [Test]
    public async Task BotDuel_DeadBot_Refused()
    {
        var bot1 = AddBot(2);
        var bot2 = AddBot(3);
        bot1.Hp = 0;

        var output = Execute(new BotDuelCommand(), "2", "3");

        await Assert.That(output.Messages.Single()).Contains("dead bot");
    }

    [Test]
    public async Task BotDuel_DifferentInstances_Refused()
    {
        var bot1 = AddBot(2);
        var bot2 = AddBot(3);
        BotTestFixture.SetPrivateField(bot2.Transform, "_instanceId", bot1.Transform.InstanceId + 1);

        var output = Execute(new BotDuelCommand(), "2", "3");

        await Assert.That(output.Messages.Single()).Contains("same instance");
    }

    [Test]
    public async Task BotDuel_CharacterAlreadyInDuel_Refused()
    {
        var bot1 = AddBot(2);
        var bot2 = AddBot(3);
        bot2.IsInDuel = true;

        var output = Execute(new BotDuelCommand(), "2", "3");

        await Assert.That(output.Messages.Single()).Contains("out of a duel");
    }

    [Test]
    public async Task BotDuel_BothFree_UsesResolver()
    {
        var bot1 = AddBot(2);
        var bot2 = AddBot(3);
        var requested = false;
        var duelManager = Mock.Of<IDuelManager>();
        duelManager.DuelRequest(Any<Character>(), Any<uint>())
            .Callback((Character challenger, uint challengedId) => requested = challenger == bot1 && challengedId == bot2.Id);
        BotDuelCommand.DuelManagerResolver = () => duelManager.Object;

        var output = Execute(new BotDuelCommand(), "2", "3");

        await Assert.That(requested).IsTrue();
        await Assert.That(output.Messages.Single()).Contains("challenged 'bot3'");
    }

    [Test]
    public async Task BotDuel_BotInExpedition_Refused()
    {
        var bot1 = AddBot(2);
        var bot2 = AddBot(3);
        bot2.Expedition = new Expedition();
        var requested = false;
        var duelManager = Mock.Of<IDuelManager>();
        duelManager.DuelRequest(Any<Character>(), Any<uint>())
            .Callback((Character challenger, uint challengedId) => requested = challenger == bot1 && challengedId == bot2.Id);
        BotDuelCommand.DuelManagerResolver = () => duelManager.Object;

        var output = Execute(new BotDuelCommand(), "2", "3");

        await Assert.That(requested).IsFalse();
        await Assert.That(output.Messages.Single()).Contains("expedition");
    }

    [Test]
    public async Task ReloadBotArchetype_ParseFailure_ReportsError()
    {
        _archetypeManager.ReloadResult = false;

        var output = Execute(new BotArchetypeReloadCommand());

        await Assert.That(_archetypeManager.ReloadCalls).IsEqualTo(1);
        await Assert.That(output.Messages.Single()).Contains("reload failed");
    }

    private CharacterMock AddBot(uint id)
    {
        var bot = BotTestFixture.MakeBot(id, Vector3.Zero);
        bot.MaxHp = 100;
        bot.Hp = 100;
        BotTestFixture.GetPrivateField<ConcurrentDictionary<uint, Character>>(_botManager, "ActiveBots")[id] = bot;
        BotTestFixture.GetPrivateField<ConcurrentDictionary<uint, BotMovementState>>(_botManager, "_botStates")[id] = new BotMovementState();
        return bot;
    }

    private DebugCharacterMock AddDebugBot(uint id)
    {
        var bot = new DebugCharacterMock { Id = id, ObjId = 1000 + id, Name = $"bot{id}", Hp = 100, Mp = 100 };
        bot.Transform.Local.SetPosition(Vector3.Zero);
        BotTestFixture.GetPrivateField<ConcurrentDictionary<uint, Character>>(_botManager, "ActiveBots")[id] = bot;
        BotTestFixture.GetPrivateField<ConcurrentDictionary<uint, BotMovementState>>(_botManager, "_botStates")[id] = new BotMovementState();
        return bot;
    }

    private sealed class DebugCharacterMock : CharacterMock
    {
        public override int MaxHp => 100;
        public override int MaxMp => 100;
    }

    private static CharacterMessageOutput Execute(ICommand command, params string[] args)
    {
        var output = new CharacterMessageOutput(new CharacterMock());
        command.Execute(new CharacterMock(), args, output);
        return output;
    }
}
