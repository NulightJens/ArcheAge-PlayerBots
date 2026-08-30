using System.Numerics;
using AAEmu.Game.Bots.Body;
using AAEmu.Game.Bots.Content.Actions;
using AAEmu.Game.Bots.Content.Strategies;
using AAEmu.Game.Bots.Content.Triggers;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Faction;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Units;
using AAEmu.UnitTests.Utils.Mocks;

namespace AAEmu.UnitTests.Bots.Content;

public sealed class R3LowTests
{
    private static readonly DateTime s_now = new(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task BotConfig_ExposesNamedDefaultHazardRadius()
    {
        await Assert.That(BotConfig.DefaultHazardRadius).IsEqualTo(40f);
    }

    [Test]
    public async Task EnemyOutOfSpellRange_DefaultsToConfiguredBowRange()
    {
        var bot = BotTestFixture.MakeBot(1, Vector3.Zero);
        var target = BotTestFixture.MakeBot(2, new Vector3(20, 0, 0));
        var config = new BotConfig { UseEngine = false, BowRange = 20 };
        var runtime = new BotRuntime(bot, new BotMovementState(), new BotCombatState { Target = target },
            config: config);
        var context = new BotContext(bot, runtime, runtime.Blackboard, s_now, config,
            BotEngineKind.Combat);
        var trigger = new EnemyOutOfSpellRangeTrigger();

        await Assert.That(trigger.IsActive(context)).IsFalse();
        var farTarget = BotTestFixture.MakeBot(3, new Vector3(20.1f, 0, 0));
        farTarget.Hp = 100;
        farTarget.MaxHp = 100;
        var farRuntime = new BotRuntime(bot, new BotMovementState(), new BotCombatState { Target = farTarget },
            config: config);
        var farContext = new BotContext(bot, farRuntime, farRuntime.Blackboard, s_now, config,
            BotEngineKind.Combat);
        await Assert.That(trigger.IsActive(farContext)).IsTrue();
    }

    [Test]
    public async Task FollowDistanceTrigger_OnlyReportsTooFarAndLabelsTheSide()
    {
        var bot = BotTestFixture.MakeBot(1, Vector3.Zero);
        var leader = BotTestFixture.MakeBot(2, new Vector3(1, 0, 0));
        var config = new BotConfig { UseEngine = false, FollowStopBand = 0.6 };
        var movement = new BotMovementState { FollowTarget = leader, FollowDistance = 2f };
        var runtime = new BotRuntime(bot, movement, new BotCombatState { CurrentState = BotCombatStateType.Following },
            config: config);
        var context = new BotContext(bot, runtime, runtime.Blackboard, s_now, config,
            BotEngineKind.NonCombat);
        var trigger = new FollowDistanceTrigger();

        await Assert.That(trigger.IsActive(context)).IsFalse();
        leader.Transform.Local.SetPosition(new Vector3(3, 0, 0));
        await Assert.That(trigger.IsActive(context)).IsTrue();
        await Assert.That(trigger.Event.Payload).IsEqualTo("too-far");
    }

    [Test]
    public async Task FollowAction_StopsOnlyWhenCloserThanFollowBand()
    {
        var bot = BotTestFixture.MakeBot(1, Vector3.Zero);
        var leader = BotTestFixture.MakeBot(2, new Vector3(1, 0, 0));
        var config = new BotConfig { UseEngine = false, FollowStopBand = 0.6 };
        var movement = new BotMovementState { FollowTarget = leader, FollowDistance = 2f };
        var runtime = new BotRuntime(bot, movement, new BotCombatState { CurrentState = BotCombatStateType.Following },
            config: config);
        var mover = new FollowMover();
        var context = new BotContext(bot, runtime, runtime.Blackboard, s_now, config,
            BotEngineKind.NonCombat);

        new FollowAction(mover).Execute(context, default);

        await Assert.That(mover.StopFollowCount).IsEqualTo(1);
    }

    [Test]
    public async Task FormationFollow_HoldsAssignedSlotWithoutClearingLeader()
    {
        var leader = BotTestFixture.MakeBot(2, Vector3.Zero);
        var movement = new BotMovementState
        {
            FollowTarget = leader,
            FollowDistance = 3f,
            FormationSlot = 0,
            FormationColumns = 1,
            FormationMemberCount = 1,
            FormationSpacing = 2.5f
        };
        var bot = BotTestFixture.MakeBot(1, new Vector3(-3f, 0f, 0f));
        var config = new BotConfig { UseEngine = false, FollowStopBand = 0.6 };
        var runtime = new BotRuntime(bot, movement,
            new BotCombatState { CurrentState = BotCombatStateType.Following }, config: config);
        var mover = new FollowMover();
        var context = new BotContext(bot, runtime, runtime.Blackboard, s_now, config,
            BotEngineKind.NonCombat, mover: mover);

        new FollowAction(mover).Execute(context, default);

        await Assert.That(mover.StopFollowCount).IsEqualTo(0);
        await Assert.That(mover.StopIfMovingCount).IsEqualTo(1);
        await Assert.That(movement.FollowTarget).IsSameReferenceAs(leader);
    }

    [Test]
    public async Task FollowDistanceTrigger_UsesAssignedFormationSlot()
    {
        var leader = BotTestFixture.MakeBot(2, Vector3.Zero);
        var movement = new BotMovementState
        {
            FollowTarget = leader,
            FollowDistance = 3f,
            FormationSlot = 99,
            FormationColumns = 10,
            FormationMemberCount = 100,
            FormationSpacing = 2.5f
        };
        var slot = AAEmu.Game.Bots.Social.BotFormation.PositionFor(leader, movement);
        var bot = BotTestFixture.MakeBot(1, slot);
        var config = new BotConfig { UseEngine = false, FollowStopBand = 0.6 };
        var runtime = new BotRuntime(bot, movement,
            new BotCombatState { CurrentState = BotCombatStateType.Following }, config: config);
        var context = new BotContext(bot, runtime, runtime.Blackboard, s_now, config,
            BotEngineKind.NonCombat);
        var trigger = new FollowDistanceTrigger();

        await Assert.That(trigger.IsActive(context)).IsFalse();
        leader.Transform.Local.SetPosition(new Vector3(2f, 0f, 0f));
        await Assert.That(trigger.IsActive(context)).IsTrue();
    }

    [Test]
    public async Task TargetInvalidTrigger_NullTargetIsInactive_DeadTargetIsActive()
    {
        var bot = BotTestFixture.MakeBot(1, Vector3.Zero);
        var state = new BotCombatState { CurrentState = BotCombatStateType.Combat };
        var config = new BotConfig { UseEngine = false };
        var runtime = new BotRuntime(bot, new BotMovementState(), state, config: config);
        var metrics = new BotHostMetrics();
        runtime.HostMetrics = metrics;
        var context = new BotContext(bot, runtime, runtime.Blackboard, s_now, config, BotEngineKind.Combat);
        var trigger = new TargetInvalidTrigger();

        await Assert.That(trigger.IsActive(context)).IsFalse();

        var deadTarget = BotTestFixture.MakeBot(2, Vector3.One);
        deadTarget.Hp = 0;
        state.Target = deadTarget;

        await Assert.That(trigger.IsActive(context)).IsTrue();
        await Assert.That(metrics.Snapshot().InvalidTargets).IsEqualTo(1L);
        await Assert.That(metrics.Snapshot().ObservedKills).IsEqualTo(1L);
    }

    [Test]
    public async Task TargetStealthedTrigger_AcceptsDuelOpponentRejectedByNormalAttackability()
    {
        var bot = BotTestFixture.MakeBot(1, Vector3.Zero);
        var target = BotTestFixture.MakeBot(2, new Vector3(20f, 0f, 0f));
        target.Hp = target.MaxHp = 100;
        target.ObjId = bot.ObjId;
        var faction = new SystemFaction();
        bot.Faction = faction;
        target.Faction = faction;
        var buffs = Mock.Of<IBuffs>();
        buffs.HasEffectsMatchingCondition(Any<Func<Buff, bool>>()).Returns(true);
        target.Buffs = buffs.Object;
        var state = new BotCombatState
        {
            CurrentState = BotCombatStateType.Dueling,
            Target = target,
            InDuel = true,
            DuelOpponent = target
        };
        var config = new BotConfig { UseEngine = false };
        var runtime = new BotRuntime(bot, new BotMovementState(), state, config: config);
        var context = new BotContext(bot, runtime, runtime.Blackboard, s_now, config, BotEngineKind.Combat);
        var trigger = new TargetStealthedTrigger();

        await Assert.That(bot.CanAttack(target)).IsFalse();
        await Assert.That(trigger.IsActive(context)).IsTrue();
    }

    [Test]
    public async Task CombatBase_StealthedDuelOpponentPreemptsContinuousRotationFiller()
    {
        var bot = BotTestFixture.MakeBot(1, Vector3.Zero);
        var target = BotTestFixture.MakeBot(2, new Vector3(20f, 0f, 0f));
        target.Hp = target.MaxHp = 100;
        var buffs = Mock.Of<IBuffs>();
        buffs.HasEffectsMatchingCondition(Any<Func<Buff, bool>>()).Returns(true);
        target.Buffs = buffs.Object;
        var state = new BotCombatState
        {
            CurrentState = BotCombatStateType.Dueling,
            Target = target,
            InDuel = true,
            DuelOpponent = target,
            IsActive = true
        };
        var config = new BotConfig { UseEngine = false };
        var runtime = new BotRuntime(bot, new BotMovementState(), state, config: config);
        var mover = new FollowMover();
        var rotation = new ProbeAction("rotation-probe");
        var engine = new BotEngine(
            BotEngineKind.Combat,
            config,
            [new CombatBaseStrategy(), new RotationProbeStrategy()],
            [new BeginSearchAction(mover), rotation]);
        var context = new BotContext(bot, runtime, runtime.Blackboard, s_now, config,
            BotEngineKind.Combat, mover: mover);

        var result = engine.DoNextAction(context, minimal: false);

        await Assert.That(result).IsTrue();
        await Assert.That(engine.LastActionLog[^1].Action).IsEqualTo("begin-search");
        await Assert.That(state.CurrentState).IsEqualTo(BotCombatStateType.Searching);
        await Assert.That(state.IsSearching).IsTrue();
        await Assert.That(state.Target).IsNull();
        await Assert.That(state.LastKnownTargetPosition).IsEqualTo(target.Transform.World.Position);
        await Assert.That(rotation.ExecuteCount).IsEqualTo(0);
    }

    [Test]
    public async Task LivingParentlessTarget_IsInvalidAndCanBeDropped()
    {
        var bot = BotTestFixture.MakeBot(1, Vector3.Zero);
        var target = BotTestFixture.MakeBot(2, Vector3.One);
        target.Hp = target.MaxHp = 100;
        BotTestFixture.SetPrivateField(bot, "_parentWorld", BotTestFixture.MakeWorld());
        var state = new BotCombatState { CurrentState = BotCombatStateType.Combat, Target = target };
        bot.CurrentTarget = target;
        var config = new BotConfig { UseEngine = false };
        var runtime = new BotRuntime(bot, new BotMovementState(), state, config: config);
        var metrics = new BotHostMetrics();
        runtime.HostMetrics = metrics;
        var context = new BotContext(bot, runtime, runtime.Blackboard, s_now, config, BotEngineKind.Combat);

        await Assert.That(new TargetInvalidTrigger().IsActive(context)).IsTrue();
        await Assert.That(new DropTargetAction().IsUseful(context)).IsTrue();
        await Assert.That(new DropTargetAction().Execute(context, default)).IsEqualTo(BotActionResult.Success);
        await Assert.That(state.Target).IsNull();
        await Assert.That(bot.CurrentTarget).IsNull();
        await Assert.That(metrics.Snapshot().InvalidTargets).IsEqualTo(1L);
        await Assert.That(metrics.Snapshot().ObservedKills).IsEqualTo(0L);
    }

    [Test]
    public async Task LivingNonAttackableTarget_IsInvalidAndCanBeDropped()
    {
        var bot = BotTestFixture.MakeBot(1, Vector3.Zero);
        var target = BotTestFixture.MakeBot(2, Vector3.One);
        target.Hp = target.MaxHp = 100;
        target.ObjId = bot.ObjId;
        var faction = new SystemFaction();
        bot.Faction = faction;
        target.Faction = faction;
        var world = BotTestFixture.MakeWorld();
        BotTestFixture.SetPrivateField(bot, "_parentWorld", world);
        BotTestFixture.SetPrivateField(target, "_parentWorld", world);
        var state = new BotCombatState { CurrentState = BotCombatStateType.Combat, Target = target };
        bot.CurrentTarget = target;
        var config = new BotConfig { UseEngine = false };
        var runtime = new BotRuntime(bot, new BotMovementState(), state, config: config);
        var metrics = new BotHostMetrics();
        runtime.HostMetrics = metrics;
        var context = new BotContext(bot, runtime, runtime.Blackboard, s_now, config, BotEngineKind.Combat);

        await Assert.That(bot.CanAttack(target)).IsFalse();
        await Assert.That(new TargetInvalidTrigger().IsActive(context)).IsTrue();
        await Assert.That(new DropTargetAction().IsUseful(context)).IsTrue();
        await Assert.That(new DropTargetAction().Execute(context, default)).IsEqualTo(BotActionResult.Success);
        await Assert.That(state.Target).IsNull();
        await Assert.That(bot.CurrentTarget).IsNull();
        await Assert.That(metrics.Snapshot().InvalidTargets).IsEqualTo(1L);
        await Assert.That(metrics.Snapshot().ObservedKills).IsEqualTo(0L);
    }

    [Test]
    public async Task NotFacingTargetTrigger_AcceptsWireHeadingOffsetAsFacing()
    {
        var bot = BotTestFixture.MakeBot(1, Vector3.Zero);
        var target = BotTestFixture.MakeBot(2, new Vector3(10, 0, 0));
        target.Hp = 100;
        target.MaxHp = 100;
        var state = new BotCombatState { CurrentState = BotCombatStateType.Combat, Target = target };
        var config = new BotConfig { UseEngine = false };
        var runtime = new BotRuntime(bot, new BotMovementState(), state, config: config);
        var context = new BotContext(bot, runtime, runtime.Blackboard, s_now, config, BotEngineKind.Combat);
        var trigger = new NotFacingTargetTrigger();

        bot.Transform.Local.SetRotationDegree(0f, 0f, -90f);
        await Assert.That(trigger.IsActive(context)).IsFalse();

        bot.Transform.Local.SetRotationDegree(0f, 0f, 0f);
        await Assert.That(trigger.IsActive(context)).IsTrue();
    }

    [Test]
    public async Task DropTargetAction_ClearsStateAndCharacterTargetWithoutCreatingKillCredit()
    {
        var bot = BotTestFixture.MakeBot(1, Vector3.Zero);
        var deadTarget = BotTestFixture.MakeBot(2, Vector3.One);
        deadTarget.Hp = 0;
        var state = new BotCombatState
        {
            CurrentState = BotCombatStateType.Combat,
            Target = deadTarget
        };
        bot.CurrentTarget = deadTarget;
        var config = new BotConfig { UseEngine = false };
        var runtime = new BotRuntime(bot, new BotMovementState(), state, config: config);
        var context = new BotContext(bot, runtime, runtime.Blackboard, s_now, config, BotEngineKind.Combat);

        var result = new DropTargetAction().Execute(context, default);

        await Assert.That(result).IsEqualTo(BotActionResult.Success);
        // Old contract: corpse observation incremented this to 1. New contract: only Unit.Events.OnKill may credit.
        await Assert.That(state.KillCount).IsEqualTo(0);
        await Assert.That(state.Target).IsNull();
        await Assert.That(bot.CurrentTarget).IsNull();
    }

    [Test]
    public async Task InvalidTarget_DropAllowsLegacyTickOnNextEngineStep()
    {
        var bot = BotTestFixture.MakeBot(1, Vector3.Zero);
        var deadTarget = BotTestFixture.MakeBot(2, Vector3.One);
        deadTarget.Hp = 0;
        var state = new BotCombatState
        {
            CurrentState = BotCombatStateType.Combat,
            Target = deadTarget
        };
        bot.CurrentTarget = deadTarget;
        var config = new BotConfig { UseEngine = false };
        var runtime = new BotRuntime(bot, new BotMovementState(), state, config: config);
        var legacy = new ProbeAction("legacy tick");
        var engine = new BotEngine(
            BotEngineKind.Combat,
            config,
            [new BodyBaseStrategy(), new LegacyStrategy()],
            [new DropTargetAction(), legacy]);

        engine.DoNextAction(
            new BotContext(bot, runtime, runtime.Blackboard, s_now, config, BotEngineKind.Combat),
            minimal: false);
        engine.DoNextAction(
            new BotContext(bot, runtime, runtime.Blackboard, s_now.AddMilliseconds(101), config, BotEngineKind.Combat),
            minimal: false);

        await Assert.That(engine.LastActionLog[^2].Action).IsEqualTo("drop-target");
        await Assert.That(engine.LastActionLog[^1].Action).IsEqualTo("legacy tick");
        await Assert.That(legacy.ExecuteCount).IsEqualTo(1);
    }

    private sealed class FollowMover : IBotMover
    {
        public int StopFollowCount { get; private set; }
        public int StopIfMovingCount { get; private set; }

        public void SetDestination(Character bot, Vector3 position, bool run, float tolerance) { }
        public void StopIfMoving(Character bot) => StopIfMovingCount++;
        public void StopImmediately(Character bot) { }
        public void Face(Character bot, float angle) { }
        public void Teleport(Character bot, Vector3 position) { }
        public void Follow(Character bot, Character target, float distance) { }
        public void StopFollow(Character bot) => StopFollowCount++;
        public void SendRelaxedStance(Character bot) { }
    }

    private sealed class ProbeAction(string name) : IBotAction
    {
        public string Name { get; } = name;
        public int ExecuteCount { get; private set; }
        public bool IsUseful(BotContext context) => true;
        public bool IsPossible(BotContext context) => true;
        public BotActionResult Execute(BotContext context, BotEvent ev)
        {
            ExecuteCount++;
            return BotActionResult.Success;
        }
    }

    private sealed class RotationProbeStrategy : IBotStrategy
    {
        public string Name => "rotation-probe-strategy";
        public string SiblingGroup => "rotation";
        public IReadOnlyList<BotNextAction> DefaultActions { get; } =
            [new BotNextAction("rotation-probe", 11f)];

        public void InitTriggers(List<BotTriggerNode> triggers) { }
        public void InitMultipliers(List<IBotMultiplier> multipliers) { }
    }
}
