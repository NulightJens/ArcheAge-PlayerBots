using System.Numerics;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Bots.Social;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Team;
using AAEmu.Game.Scripts.Commands;
using AAEmu.UnitTests.Utils.Mocks;

namespace AAEmu.UnitTests.Bots.Social;

[NotInParallel]
public class BotControlTests
{
    [Before(Test)]
    public void Setup()
    {
        BotTestFixture.ResetBotContentRegistry();
    }

    [After(Test)]
    public void Teardown()
    {
        BotTestFixture.ResetBotContentRegistry();
    }

    [Test]
    public async Task Dispatch_NonLeader_FailsClosedWithoutQueuingAction()
    {
        var setup = CreateParty();
        var outsider = MakeCharacter(9, "outsider", Vector3.Zero);

        var result = setup.Dispatcher.Dispatch(outsider, setup.Bot.Id, BotControlVerb.Follow);

        await Assert.That(result.Status).IsEqualTo(BotControlStatus.Unauthorized);
        await Assert.That(setup.Runtime.Engines[(int)BotEngineKind.NonCombat].Queue.Count).IsEqualTo(0);
        await Assert.That(setup.Runtime.Social.MovementOrder).IsEqualTo(BotMovementOrder.Stay);
    }

    [Test]
    public async Task CommandGrammar_OnlyAcceptsTypedVerbAndRoleWithoutRequesterIdentity()
    {
        var parsed = BotControlCommand.TryParse(
            ["2", "role", "tank"],
            out var botId,
            out var verb,
            out var role);
        var spoofFieldAccepted = BotControlCommand.TryParse(
            ["2", "follow", "pretend-owner"],
            out _,
            out _,
            out _);

        await Assert.That(parsed).IsTrue();
        await Assert.That(botId).IsEqualTo(2u);
        await Assert.That(verb).IsEqualTo(BotControlVerb.Role);
        await Assert.That(role).IsEqualTo(MemberRole.Tank);
        await Assert.That(spoofFieldAccepted).IsFalse();
    }

    [Test]
    public async Task FollowThenStay_AreExclusiveAndLatestCommandWins()
    {
        var setup = CreateParty();

        await Assert.That(setup.Dispatcher.Dispatch(setup.Leader, setup.Bot.Id, BotControlVerb.Follow).Accepted).IsTrue();
        Tick(setup.Runtime);
        await Assert.That(setup.Runtime.Social.MovementOrder).IsEqualTo(BotMovementOrder.Follow);
        await Assert.That(setup.Runtime.MovementState.FollowTarget).IsSameReferenceAs(setup.Leader);

        await Assert.That(setup.Dispatcher.Dispatch(setup.Leader, setup.Bot.Id, BotControlVerb.Stay).Accepted).IsTrue();
        Tick(setup.Runtime);

        await Assert.That(setup.Runtime.Social.MovementOrder).IsEqualTo(BotMovementOrder.Stay);
        await Assert.That(setup.Runtime.MovementState.FollowTarget).IsNull();
        await Assert.That(setup.Runtime.CombatState.ForcedState).IsEqualTo(BotCombatStateType.Idle);
    }

    [Test]
    public async Task PartyJoin_DefaultsToAssistSoLeaderCombatCanInterruptFollow()
    {
        var setup = CreateParty();

        await Assert.That(setup.Runtime.Social.CombatOrder).IsEqualTo(BotCombatOrder.Assist);

        setup.Runtime.Social.ApplyPassive();
        await Assert.That(setup.Runtime.Social.CombatOrder).IsEqualTo(BotCombatOrder.Passive);
    }

    [Test]
    public async Task PartyLowest_SelectsMostInjuredLivingMemberWithinRange()
    {
        var leader = MakeFixedHealthCharacter(1, "leader", Vector3.Zero, false);
        var healer = MakeFixedHealthCharacter(2, "healer", Vector3.Zero, true);
        var wounded = MakeFixedHealthCharacter(3, "wounded", new Vector3(10, 0, 0), true);
        wounded.Hp = 35;
        var criticalButFar = MakeFixedHealthCharacter(4, "far", new Vector3(40, 0, 0), true);
        criticalButFar.Hp = 10;
        var team = new Team { Id = 77, OwnerId = leader.Id, IsParty = true };
        team.AddMember(leader);
        team.AddMember(healer);
        team.AddMember(wounded);
        team.AddMember(criticalButFar);
        var runtime = CreateRuntime(healer);
        runtime.TeamHooks.Refresh(team);

        var selected = runtime.Social.ResolveLowestHealthMember(25f);

        await Assert.That(selected).IsSameReferenceAs(wounded);
    }

    [Test]
    public async Task HealRecipientSelection_RejectsWrongWorldAndWrongInstance()
    {
        var world = BotTestFixture.MakeWorld(1);
        var otherWorld = BotTestFixture.MakeWorld(2);
        var leader = MakeFixedHealthCharacter(1, "leader", Vector3.Zero, false);
        var healer = MakeFixedHealthCharacter(2, "healer", Vector3.Zero, true);
        var eligible = MakeFixedHealthCharacter(3, "eligible", new Vector3(10, 0, 0), true);
        eligible.Hp = 50;
        var wrongWorld = MakeFixedHealthCharacter(4, "wrong-world", new Vector3(5, 0, 0), true);
        wrongWorld.Hp = 10;
        var wrongInstance = MakeFixedHealthCharacter(5, "wrong-instance", new Vector3(5, 0, 0), true);
        wrongInstance.Hp = 5;
        foreach (var character in new Character[] { leader, healer, eligible, wrongInstance })
            BotTestFixture.SetPrivateField(character, "_parentWorld", world);
        BotTestFixture.SetPrivateField(wrongWorld, "_parentWorld", otherWorld);
        BotTestFixture.SetPrivateField(wrongInstance.Transform, "_instanceId", healer.Transform.InstanceId + 1);
        var team = new Team { Id = 77, OwnerId = leader.Id, IsParty = true };
        team.AddMember(leader);
        team.AddMember(healer);
        team.AddMember(eligible);
        team.AddMember(wrongWorld);
        team.AddMember(wrongInstance);
        var runtime = CreateRuntime(healer);
        runtime.TeamHooks.Refresh(team);

        await Assert.That(runtime.Social.ResolveLowestHealthMember(25f)).IsSameReferenceAs(eligible);
        await Assert.That(runtime.Social.CommitLowestHealthMember(25f, 0f, 85f)).IsSameReferenceAs(eligible);

        eligible.Hp = eligible.MaxHp;
        await Assert.That(runtime.Social.ResolveCommittedHealRecipient()).IsNull();
        await Assert.That(runtime.Social.CommitLowestHealthMember(25f, 0f, 85f)).IsNull();
    }

    [Test]
    public async Task HealRecipientCommit_UsesExplicitLeashAndRemainsStableUntilCleared()
    {
        var leader = MakeFixedHealthCharacter(1, "leader", Vector3.Zero, false);
        var healer = MakeFixedHealthCharacter(2, "healer", Vector3.Zero, true);
        var first = MakeFixedHealthCharacter(3, "first", new Vector3(10, 0, 0), true);
        first.Hp = 50;
        var second = MakeFixedHealthCharacter(4, "second", new Vector3(15, 0, 0), true);
        second.Hp = 60;
        var criticalButOutsideLeash = MakeFixedHealthCharacter(5, "outside", new Vector3(50, 0, 0), true);
        criticalButOutsideLeash.Hp = 5;
        var team = new Team { Id = 77, OwnerId = leader.Id, IsParty = true };
        team.AddMember(leader);
        team.AddMember(healer);
        team.AddMember(first);
        team.AddMember(second);
        team.AddMember(criticalButOutsideLeash);
        var runtime = CreateRuntime(healer);
        runtime.TeamHooks.Refresh(team);

        var committed = runtime.Social.CommitLowestHealthMember(45f, 0f, 85f);
        second.Hp = 10;
        var stable = runtime.Social.CommitLowestHealthMember(45f, 0f, 85f);

        await Assert.That(committed).IsSameReferenceAs(first);
        await Assert.That(stable).IsSameReferenceAs(first);
        await Assert.That(runtime.Social.ResolveCommittedHealRecipient()).IsSameReferenceAs(first);
        await Assert.That(runtime.Social.HealRecipientSelectionScans).IsEqualTo(1);

        runtime.Social.ClearCommittedHealRecipient();
        var reselection = runtime.Social.CommitLowestHealthMember(45f, 0f, 85f);
        await Assert.That(reselection).IsSameReferenceAs(second);
        await Assert.That(runtime.Social.HealRecipientSelectionScans).IsEqualTo(2);
    }

    [Test]
    public async Task HealRecipientCommit_InvalidatesAndClearsOnSocialLifecycle()
    {
        var leader = MakeFixedHealthCharacter(1, "leader", Vector3.Zero, false);
        var healer = MakeFixedHealthCharacter(2, "healer", Vector3.Zero, true);
        var wounded = MakeFixedHealthCharacter(3, "wounded", new Vector3(10, 0, 0), true);
        wounded.Hp = 30;
        var team = new Team { Id = 77, OwnerId = leader.Id, IsParty = true };
        team.AddMember(leader);
        team.AddMember(healer);
        team.AddMember(wounded);
        var runtime = CreateRuntime(healer);
        runtime.TeamHooks.Refresh(team);

        await Assert.That(runtime.Social.CommitLowestHealthMember(45f, 0f, 85f)).IsSameReferenceAs(wounded);
        wounded.Hp = wounded.MaxHp;
        await Assert.That(runtime.Social.ResolveCommittedHealRecipient()).IsNull();

        wounded.Hp = 30;
        runtime.Social.CommitLowestHealthMember(45f, 0f, 85f);
        wounded.Hp = 0;
        await Assert.That(runtime.Social.ResolveCommittedHealRecipient()).IsNull();

        wounded.Hp = 30;
        runtime.Social.CommitLowestHealthMember(45f, 0f, 85f);
        team.RemoveMember(wounded.Id);
        runtime.TeamHooks.Refresh(team);
        await Assert.That(runtime.Social.ResolveCommittedHealRecipient()).IsNull();

        team.AddMember(wounded);
        runtime.TeamHooks.Refresh(team);
        runtime.Social.CommitLowestHealthMember(45f, 0f, 85f);
        runtime.Social.SafeHold();
        await Assert.That(runtime.Social.ResolveCommittedHealRecipient()).IsNull();

        runtime.Social.CommitLowestHealthMember(45f, 0f, 85f);
        runtime.Social.ClearTeam();
        await Assert.That(runtime.Social.ResolveCommittedHealRecipient()).IsNull();
    }

    [Test]
    public async Task AttackThenPassiveBeforeTick_DeterministicallyKeepsPassive()
    {
        var setup = CreateParty();
        var target = MakeCharacter(20, "target", new Vector3(10, 0, 0));
        setup.Leader.CurrentTarget = target;

        await Assert.That(setup.Dispatcher.Dispatch(setup.Leader, setup.Bot.Id, BotControlVerb.Attack).Accepted).IsTrue();
        await Assert.That(setup.Dispatcher.Dispatch(setup.Leader, setup.Bot.Id, BotControlVerb.Passive).Accepted).IsTrue();
        Tick(setup.Runtime);

        await Assert.That(setup.Runtime.Social.CombatOrder).IsEqualTo(BotCombatOrder.Passive);
        await Assert.That(setup.Runtime.Social.AssistTargetObjId).IsEqualTo(0u);
        await Assert.That(setup.Runtime.CombatState.Target).IsNull();
        await Assert.That(setup.Bot.CurrentTarget).IsNull();
    }

    [Test]
    public async Task LeaderDisconnect_HoldsPositionAndDropsCombatImmediately()
    {
        var setup = CreateParty();
        var target = MakeCharacter(20, "target", new Vector3(10, 0, 0));
        setup.Runtime.Social.ApplyFollow();
        setup.Runtime.Social.ApplyAttack(target);

        setup.Leader.Events.OnDisconnect(setup.Leader, new OnDisconnectArgs { Player = setup.Leader });

        await Assert.That(setup.Runtime.Social.MovementOrder).IsEqualTo(BotMovementOrder.Stay);
        await Assert.That(setup.Runtime.Social.CombatOrder).IsEqualTo(BotCombatOrder.Passive);
        await Assert.That(setup.Runtime.MovementState.FollowTarget).IsNull();
        await Assert.That(setup.Runtime.CombatState.Target).IsNull();
    }

    [Test]
    public async Task OfflineLeader_IsRejectedByCachedGuardWithoutDisconnectEvent()
    {
        var setup = CreateParty();
        var target = MakeCharacter(20, "target", new Vector3(10, 0, 0));
        setup.Runtime.Social.ApplyFollow();
        setup.Runtime.Social.ApplyAttack(target);

        var available = setup.Runtime.Social.GuardLeader();

        await Assert.That(available).IsFalse();
        await Assert.That(setup.Runtime.Social.MovementOrder).IsEqualTo(BotMovementOrder.Stay);
        await Assert.That(setup.Runtime.Social.CombatOrder).IsEqualTo(BotCombatOrder.Passive);
        await Assert.That(setup.Runtime.MovementState.FollowTarget).IsNull();
        await Assert.That(setup.Runtime.CombatState.Target).IsNull();
    }

    [Test]
    public async Task LeaderDeath_HoldsPositionAndDropsCombatImmediately()
    {
        var setup = CreateParty();
        var target = MakeCharacter(20, "target", new Vector3(10, 0, 0));
        setup.Runtime.Social.ApplyFollow();
        setup.Runtime.Social.ApplyAttack(target);

        setup.Leader.Events.OnDeath(setup.Leader, new AAEmu.Game.Models.Game.Units.OnDeathArgs
        {
            Victim = setup.Leader
        });

        await Assert.That(setup.Runtime.Social.MovementOrder).IsEqualTo(BotMovementOrder.Stay);
        await Assert.That(setup.Runtime.Social.CombatOrder).IsEqualTo(BotCombatOrder.Passive);
        await Assert.That(setup.Runtime.CombatState.Target).IsNull();
    }

    [Test]
    public async Task LeaderInstanceChange_IsCaughtByCachedGuardWithoutTeamScan()
    {
        var setup = CreateParty();
        var target = MakeCharacter(20, "target", new Vector3(10, 0, 0));
        setup.Runtime.Social.ApplyFollow();
        setup.Runtime.Social.ApplyAttack(target);
        BotTestFixture.SetPrivateField(
            setup.Leader.Transform,
            "_instanceId",
            setup.Bot.Transform.InstanceId + 1);

        var available = setup.Runtime.Social.GuardLeader();

        await Assert.That(available).IsFalse();
        await Assert.That(setup.Runtime.Social.MovementOrder).IsEqualTo(BotMovementOrder.Stay);
        await Assert.That(setup.Runtime.Social.CombatOrder).IsEqualTo(BotCombatOrder.Passive);
        await Assert.That(setup.Runtime.CombatState.Target).IsNull();
    }

    [Test]
    public async Task TeamOwnerTransition_RebindsAuthorityAndDetachesOldLeader()
    {
        var setup = CreateParty();
        var nextLeader = MakeCharacter(3, "next", Vector3.One);
        var team = new Team { Id = 77, OwnerId = setup.Leader.Id, IsParty = true };
        team.AddMember(setup.Leader);
        team.AddMember(setup.Bot);
        team.AddMember(nextLeader);
        setup.Runtime.TeamHooks.Refresh(team);
        team.OwnerId = nextLeader.Id;

        setup.Bot.Events.OnTeamChanged(team, new OnTeamChangedArgs { Team = team, Player = setup.Bot });
        setup.Leader.Events.OnDisconnect(setup.Leader, new OnDisconnectArgs { Player = setup.Leader });

        await Assert.That(setup.Runtime.Social.MasterId).IsEqualTo(nextLeader.Id);
        setup.Runtime.Social.ApplyFollow();
        await Assert.That(setup.Runtime.MovementState.FollowTarget).IsSameReferenceAs(nextLeader);
    }

    [Test]
    public async Task QueuedCommand_OwnerChangeBeforeTick_IsVetoed()
    {
        var setup = CreateParty();
        await Assert.That(setup.Dispatcher.Dispatch(setup.Leader, setup.Bot.Id, BotControlVerb.Follow).Accepted).IsTrue();
        var replacement = MakeCharacter(3, "replacement", Vector3.One);
        var changedTeam = new Team { Id = 77, OwnerId = replacement.Id, IsParty = true };
        changedTeam.AddMember(setup.Leader);
        changedTeam.AddMember(setup.Bot);
        changedTeam.AddMember(replacement);
        setup.Runtime.TeamHooks.Refresh(changedTeam);

        Tick(setup.Runtime);

        await Assert.That(setup.Runtime.Social.MasterId).IsEqualTo(replacement.Id);
        await Assert.That(setup.Runtime.Social.MovementOrder).IsEqualTo(BotMovementOrder.Stay);
    }

    [Test]
    public async Task RoleCommand_UsesTypedRoleAndUpdatesCachedRole()
    {
        var setup = CreateParty();

        var result = setup.Dispatcher.Dispatch(
            setup.Leader,
            setup.Bot.Id,
            BotControlVerb.Role,
            MemberRole.Tank);
        Tick(setup.Runtime);

        await Assert.That(result.Accepted).IsTrue();
        await Assert.That(setup.Runtime.Social.Role).IsEqualTo(MemberRole.Tank);
    }

    [Test]
    public async Task TeamLeave_ClearsCachedOwnershipAndDetachesLeaderSafetyHooks()
    {
        var setup = CreateParty();
        setup.Runtime.Social.ApplyFollow();

        setup.Bot.Events.OnTeamLeave(setup.Bot, new OnTeamLeaveArgs
        {
            Id = 77,
            Player = setup.Bot,
            Team = new Team { Id = 77 }
        });

        await Assert.That(setup.Runtime.Social.TeamId).IsEqualTo(0u);
        await Assert.That(setup.Runtime.Social.MasterId).IsEqualTo(0u);
        await Assert.That(setup.Runtime.Social.MovementOrder).IsEqualTo(BotMovementOrder.Stay);
        await Assert.That(setup.Runtime.MovementState.FollowTarget).IsNull();
    }

    [Test]
    public async Task TwoBots_FollowDistinctSlotsAndAssistOneSelectedTarget()
    {
        var leader = MakeCharacter(1, "leader", Vector3.Zero);
        var botOne = MakeBot(2, new Vector3(-5, 0, 0));
        var botTwo = MakeBot(3, new Vector3(5, 0, 0));
        var target = MakeCharacter(20, "target", new Vector3(10, 0, 0));
        var team = new Team { Id = 77, OwnerId = leader.Id, IsParty = true };
        team.AddMember(leader);
        team.AddMember(botOne);
        team.AddMember(botTwo);
        team.ChangeRole(botOne.Id, MemberRole.Tank);

        var first = CreateRuntime(botOne);
        var second = CreateRuntime(botTwo);
        first.TeamHooks.Refresh(team);
        second.TeamHooks.Refresh(team);
        var dispatcher = CreateDispatcher(team, (botOne.Id, first), (botTwo.Id, second));

        leader.CurrentTarget = target;
        await Assert.That(dispatcher.Dispatch(leader, botOne.Id, BotControlVerb.Follow).Accepted).IsTrue();
        await Assert.That(dispatcher.Dispatch(leader, botTwo.Id, BotControlVerb.Follow).Accepted).IsTrue();
        Tick(first);
        Tick(second);

        var firstSlot = BotFormation.PositionFor(leader, first.Social.FormationSlot, 2f);
        var secondSlot = BotFormation.PositionFor(leader, second.Social.FormationSlot, 2f);
        await Assert.That(first.Social.FormationSlot).IsNotEqualTo(second.Social.FormationSlot);
        await Assert.That(firstSlot).IsNotEqualTo(secondSlot);
        await Assert.That(first.Social.MainTankId).IsEqualTo(botOne.Id);
        await Assert.That(second.Social.MainTankId).IsEqualTo(botOne.Id);

        await Assert.That(dispatcher.Dispatch(leader, botOne.Id, BotControlVerb.Attack).Accepted).IsTrue();
        await Assert.That(dispatcher.Dispatch(leader, botTwo.Id, BotControlVerb.Attack).Accepted).IsTrue();
        Tick(first);
        Tick(second);

        await Assert.That(first.CombatState.Target).IsSameReferenceAs(target);
        await Assert.That(second.CombatState.Target).IsSameReferenceAs(target);
        await Assert.That(first.Social.AssistTargetObjId).IsEqualTo(target.ObjId);
        await Assert.That(second.Social.AssistTargetObjId).IsEqualTo(target.ObjId);

        first.TeamHooks.Dispose();
        second.TeamHooks.Dispose();
    }

    private static PartySetup CreateParty()
    {
        var leader = MakeCharacter(1, "leader", Vector3.Zero);
        var bot = MakeBot(2, new Vector3(5, 0, 0));
        var team = new Team { Id = 77, OwnerId = leader.Id, IsParty = true };
        team.AddMember(leader);
        team.AddMember(bot);
        var runtime = CreateRuntime(bot);
        runtime.TeamHooks.Refresh(team);
        return new PartySetup(leader, bot, runtime, CreateDispatcher(team, (bot.Id, runtime)));
    }

    private static BotRuntime CreateRuntime(Character bot)
    {
        return new BotRuntime(bot, new BotMovementState(), new BotCombatState { BotId = bot.Id });
    }

    private static BotControlDispatcher CreateDispatcher(Team team, params (uint id, BotRuntime runtime)[] runtimes)
    {
        var bots = Mock.Of<IBotManager>();
        var host = Mock.Of<IBotHost>();
        var teams = Mock.Of<ITeamManager>();
        foreach (var (id, runtime) in runtimes)
        {
            bots.GetBot(id).Returns(runtime.Bot);
            host.GetRuntime(id).Returns(runtime);
        }
        teams.GetActiveTeamByUnit(Any<uint>()).Returns(team);
        return new BotControlDispatcher(bots.Object, host.Object, teams.Object, TimeProvider.System);
    }

    private static void Tick(BotRuntime runtime)
    {
        var kind = runtime.CombatState.CurrentState is BotCombatStateType.Combat or BotCombatStateType.Dueling or BotCombatStateType.Searching
            ? BotEngineKind.Combat
            : BotEngineKind.NonCombat;
        var context = new BotContext(
            runtime.Bot,
            runtime,
            runtime.Blackboard,
            DateTime.UtcNow,
            new BotConfig(),
            kind);
        runtime.Engines[(int)kind].DoNextAction(context, minimal: false);
    }

    private static CharacterMock MakeBot(uint id, Vector3 position)
    {
        var bot = MakeCharacter(id, $"bot{id}", position);
        bot.IsBot = true;
        return bot;
    }

    private static CharacterMock MakeCharacter(uint id, string name, Vector3 position)
    {
        var character = BotTestFixture.MakeBot(id, position);
        character.Name = name;
        character.Hp = character.MaxHp = 100;
        return character;
    }

    private static FixedHealthCharacterMock MakeFixedHealthCharacter(uint id, string name, Vector3 position, bool isBot)
    {
        var character = new FixedHealthCharacterMock
        {
            Id = id,
            ObjId = id + 1000,
            Name = name,
            IsBot = isBot,
            Hp = 100
        };
        character.Transform.Local.SetPosition(position);
        return character;
    }

    private sealed record PartySetup(
        Character Leader,
        Character Bot,
        BotRuntime Runtime,
        BotControlDispatcher Dispatcher);
}
