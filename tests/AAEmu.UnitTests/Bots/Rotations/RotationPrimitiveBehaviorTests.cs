using System.Numerics;
using AAEmu.Game.Bots.Body;
using AAEmu.Game.Bots.Body.Positioning;
using AAEmu.Game.Bots.Content.Rotations;
using AAEmu.Game.Bots.Content.Rotations.Triggers;
using AAEmu.Game.Bots.Content.Rotations.Values;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Static;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.Team;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Tasks.Skills;
using AAEmu.UnitTests.Utils.Mocks;

namespace AAEmu.UnitTests.Bots.Rotations;

[NotInParallel]
public sealed class RotationPrimitiveBehaviorTests
{
    private static readonly DateTime Now = new(2026, 8, 27, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task RotationIdle_RangedBotHoldsTheConfiguredBandInsteadOfFallingThroughToLegacyChase()
    {
        var bot = FixedCharacter(900, Vector3.Zero, true);
        var enemy = FixedCharacter(901, new Vector3(19, 0, 0), false);
        var runtime = new BotRuntime(bot, new BotMovementState(), new BotCombatState { Target = enemy },
            config: new BotConfig { UseEngine = false });
        var context = new BotContext(bot, runtime, runtime.Blackboard, Now,
            new BotConfig { UseEngine = false }, BotEngineKind.Combat);
        var mover = new RecordingMover();
        var action = new RotationIdleAction(20f, mover);

        await Assert.That(action.Name).IsEqualTo("rotation:hold-range");
        await Assert.That(action.IsUseful(context)).IsTrue();
        await Assert.That(action.IsPossible(context)).IsTrue();
        await Assert.That(action.Execute(context, default)).IsEqualTo(BotActionResult.Success);
        await Assert.That(mover.StopIfMovingCalled).IsTrue();

        enemy.Transform.Local.SetPosition(new Vector3(10, 0, 0));
        await Assert.That(action.IsUseful(context)).IsTrue();
        await Assert.That(action.Execute(context, default)).IsEqualTo(BotActionResult.Success);

        var genericIdle = new RotationIdleAction();
        await Assert.That(genericIdle.IsUseful(context)).IsFalse();
    }

    [Test]
    public async Task PartyLowestPrimitive_CommitsBeyondCastRangeWithinExplicitSearchLeash()
    {
        var leader = FixedCharacter(1, Vector3.Zero, false);
        var healer = FixedCharacter(2, Vector3.Zero, true);
        var ally = FixedCharacter(3, new Vector3(30, 0, 0), true);
        ally.Hp = 40;
        var enemy = FixedCharacter(4, new Vector3(10, 0, 0), false);
        var team = new Team { Id = 77, OwnerId = leader.Id, IsParty = true };
        team.AddMember(leader);
        team.AddMember(healer);
        team.AddMember(ally);
        var runtime = new BotRuntime(healer, new BotMovementState(), new BotCombatState { Target = enemy },
            config: new BotConfig { UseEngine = false });
        runtime.TeamHooks.Refresh(team);
        var context = new BotContext(healer, runtime, runtime.Blackboard, Now,
            new BotConfig { UseEngine = false }, BotEngineKind.Combat);
        var when = new BotRotationWhen { Kind = "partyLowest" };
        when.Arguments["max"] = Newtonsoft.Json.Linq.JToken.FromObject(50f);
        when.Arguments["radius"] = Newtonsoft.Json.Linq.JToken.FromObject(45f);
        var trigger = Factory(() => 0).Create(when);

        await Assert.That(trigger.IsActive(context)).IsTrue();
        await Assert.That(runtime.Social.ResolveCommittedHealRecipient()).IsSameReferenceAs(ally);
        await Assert.That(runtime.Social.HealRecipientSelectionScans).IsEqualTo(1);
        ally.Hp = 90;
        await Assert.That(trigger.IsActive(context)).IsFalse();
    }

    [Test]
    public async Task CompiledCastHeal_MovesThenCastsTheCommittedRecipientAndResumesHomeAnchor()
    {
        var leader = FixedCharacter(10, Vector3.Zero, false);
        var healer = FixedCharacter(11, Vector3.Zero, true);
        var ally = FixedCharacter(12, new Vector3(30, 0, 0), true);
        ally.Hp = 30;
        var otherAlly = FixedCharacter(14, new Vector3(20, 0, 0), true);
        otherAlly.Hp = 50;
        var enemy = FixedCharacter(13, new Vector3(10, 0, 0), false);
        var team = new Team { Id = 88, OwnerId = leader.Id, IsParty = true };
        team.AddMember(leader);
        team.AddMember(healer);
        team.AddMember(ally);
        team.AddMember(otherAlly);
        var runtime = new BotRuntime(healer, new BotMovementState(), new BotCombatState { Target = enemy },
            config: new BotConfig { UseEngine = false });
        runtime.TeamHooks.Refresh(team);
        runtime.Social.ApplyFollow();
        var context = new BotContext(healer, runtime, runtime.Blackboard, Now,
            new BotConfig { UseEngine = false }, BotEngineKind.Combat);
        healer.Skills = new CharacterSkills(healer);
        healer.Skills.Skills[44] = new Skill(new SkillTemplate { Id = 44 });
        var definition = new BotRotationDefinition
        {
            Id = "party.heal",
            Archetype = "Cleric",
            Meta = new BotRotationMeta { HomeAnchorSkill = "antithesis" },
            Skills = new Dictionary<string, uint> { ["antithesis"] = 44 },
            Rules =
            [
                new BotRotationRule
                {
                    When = new BotRotationWhen
                    {
                        Kind = "partyLowest",
                        Arguments = new Dictionary<string, Newtonsoft.Json.Linq.JToken>
                        {
                            ["max"] = Newtonsoft.Json.Linq.JToken.FromObject(85f),
                            ["radius"] = Newtonsoft.Json.Linq.JToken.FromObject(45f)
                        }
                    },
                    Then =
                    [
                        new BotRotationRow
                        {
                            Action = "castHeal",
                            Skill = "antithesis",
                            Relevance = 35,
                            As = "heal:antithesis"
                        }
                    ]
                }
            ]
        };
        BotCastRequest request = null;
        var castResult = SkillResult.Failure;
        var mover = new RecordingMover();
        var strategy = new BotRotationCompiler(
            templateResolver: _ => new SkillTemplate
            {
                Id = 44,
                TargetType = SkillTargetType.Party,
                TargetRelation = SkillTargetRelation.Party,
                MaxRange = 25
            },
            mover: mover,
            cast: captured =>
            {
                request = captured;
                return castResult;
            }).Compile(definition);
        var castAction = strategy.Actions.Single(candidate => candidate.Name == "heal:antithesis");
        var moveAction = strategy.Actions.OfType<HealRecipientRangeAction>().Single();
        var homeAction = strategy.Actions.OfType<MaintainSpellRangeAction>().Single();
        var castNode = strategy.TriggerNodes.Single(node =>
            node.Actions.Any(next => next.Name == castAction.Name));
        var moveNode = strategy.TriggerNodes.Single(node =>
            node.Actions.Any(next => next.Name == moveAction.Name));
        var homeNode = strategy.TriggerNodes.Single(node =>
            node.Actions.Any(next => next.Name == homeAction.Name));

        await Assert.That(castNode.Actions.Single().Relevance).IsEqualTo(35f);
        await Assert.That(moveNode.Actions.Single().Relevance).IsEqualTo(34f);
        await Assert.That(homeNode.Actions.Single().Relevance).IsEqualTo(30f);
        await Assert.That(moveNode.Trigger).IsSameReferenceAs(castNode.Trigger);
        var engine = new BotEngine(BotEngineKind.Combat, new BotConfig { UseEngine = false }, [strategy],
            strategy.Actions.Append(strategy.Filler));
        await Assert.That(engine.DoNextAction(context, minimal: false)).IsTrue();
        await Assert.That(engine.SnapshotLog().Any(entry =>
            entry.Action == castAction.Name && entry.Result == BotActionResult.Impossible)).IsTrue();
        await Assert.That(engine.SnapshotLog().Any(entry =>
            entry.Action == moveAction.Name && entry.Result == BotActionResult.Success)).IsTrue();
        await Assert.That(mover.Destination).IsEqualTo(new Vector3(6, 0, 0));
        await Assert.That(runtime.Social.HealRecipientSelectionScans).IsEqualTo(1);

        healer.Transform.Local.SetPosition(new Vector3(6, 0, 0));
        otherAlly.Hp = 5;
        var castContext = new BotContext(healer, runtime, runtime.Blackboard, Now.AddMilliseconds(100),
            new BotConfig { UseEngine = false }, BotEngineKind.Combat, mover: mover);
        await Assert.That(moveNode.Trigger.IsActive(castContext)).IsTrue();
        await Assert.That(runtime.Social.ResolveCommittedHealRecipient()).IsSameReferenceAs(ally);
        await Assert.That(runtime.Social.HealRecipientSelectionScans).IsEqualTo(1);
        await Assert.That(castAction.IsUseful(castContext)).IsTrue();
        await Assert.That(castAction.Execute(castContext, default)).IsEqualTo(BotActionResult.Failure);
        await Assert.That(runtime.Social.ResolveCommittedHealRecipient()).IsSameReferenceAs(ally);

        castResult = SkillResult.Success;
        var successContext = new BotContext(healer, runtime, runtime.Blackboard, Now.AddMilliseconds(200),
            new BotConfig { UseEngine = false }, BotEngineKind.Combat, mover: mover);
        await Assert.That(engine.DoNextAction(successContext, minimal: false)).IsTrue();
        await Assert.That(engine.SnapshotLog().Any(entry =>
            entry.Action == castAction.Name && entry.Result == BotActionResult.Success)).IsTrue();
        await Assert.That(request.Target.ObjId).IsEqualTo(ally.ObjId);
        await Assert.That(runtime.CombatState.Target).IsSameReferenceAs(enemy);
        await Assert.That(runtime.Social.ResolveCommittedHealRecipient()).IsNull();
        await Assert.That(runtime.Social.HealRecipientSelectionScans).IsEqualTo(1);

        await Assert.That(homeAction.IsPossible(successContext)).IsTrue();
        await Assert.That(homeAction.Execute(successContext, default)).IsEqualTo(BotActionResult.Impossible);
        await Assert.That(mover.Destination).IsEqualTo(new Vector3(-14, 0, 0));
    }

    [Test]
    public async Task AllPrimitive_RequiresEveryChild()
    {
        var factory = Factory(() => 0);
        var when = new BotRotationWhen
        {
            Kind = "all",
            Children = [When("pvp", false)]
        };
        var context = Context();

        await Assert.That(factory.Create(when).IsActive(context)).IsFalse();
        context.Runtime.CombatState.Target = BotTestFixture.MakeBot(2, new Vector3(1, 0, 0));
        when.Children[0] = When("pvp", true);
        await Assert.That(factory.Create(when).IsActive(context)).IsTrue();
    }

    [Test]
    public async Task AnyPrimitive_RequiresOneChild()
    {
        var factory = Factory(() => 0);
        var when = new BotRotationWhen
        {
            Kind = "any",
            Children = [When("pvp", false), When("targetCasting", true)]
        };
        var context = Context();

        await Assert.That(factory.Create(when).IsActive(context)).IsFalse();
        context.Runtime.CombatState.Target = BotTestFixture.MakeBot(2, new Vector3(1, 0, 0));
        context.Runtime.CombatState.Target.SkillTask = new TestSkillTask();
        await Assert.That(factory.Create(when).IsActive(context)).IsTrue();
    }

    [Test]
    public async Task TimerPrimitive_UsesEveryAndProbabilityWithContextTime()
    {
        var factory = Factory(() => 0);
        var when = When("timer", true);
        when.Arguments["every"] = Newtonsoft.Json.Linq.JToken.FromObject(500);
        when.Arguments["probability"] = Newtonsoft.Json.Linq.JToken.FromObject(1.0);
        var trigger = factory.Create(when);

        await Assert.That(trigger.IsActive(Context(now: Now))).IsTrue();
        await Assert.That(trigger.IsActive(Context(now: Now.AddMilliseconds(100)))).IsFalse();
        await Assert.That(trigger.IsActive(Context(now: Now.AddMilliseconds(500)))).IsTrue();
    }

    [Test]
    public async Task ControlledPrimitive_TracksStunAndClearState()
    {
        var bot = BotTestFixture.MakeBot(1, Vector3.Zero);
        var buffs = Mock.Of<IBuffs>();
        buffs.HasEffectsMatchingCondition(Any<Func<Buff, bool>>())
            .Returns((Func<Buff, bool> predicate) => predicate(new Buff(bot, bot,
                new SkillCasterUnit(bot.ObjId), new BuffTemplate { Stun = true }, null, Now)));
        bot.Buffs = buffs.Object;
        var trigger = Factory(() => 0).Create(When("controlled", true));

        await Assert.That(trigger.IsActive(Context(bot))).IsTrue();
        bot.Buffs = null;
        await Assert.That(trigger.IsActive(Context(bot))).IsFalse();
    }

    [Test]
    public async Task DebuffMissingPrimitive_UsesTargetAndRemainingLifetime()
    {
        var bot = BotTestFixture.MakeBot(1, Vector3.Zero);
        var target = BotTestFixture.MakeBot(2, new Vector3(1, 0, 0));
        target.Buffs = BuffsWith(target, new BuffTemplate { Id = 42, Kind = BuffKind.Bad }, Now.AddSeconds(10));
        var when = When("debuffMissing", true);
        when.Arguments["spell"] = Newtonsoft.Json.Linq.JToken.FromObject("mark");
        when.Arguments["on"] = Newtonsoft.Json.Linq.JToken.FromObject("target");
        when.Arguments["minLifetime"] = Newtonsoft.Json.Linq.JToken.FromObject(5000);
        var trigger = Factory(() => 0, _ => 42).Create(when);

        await Assert.That(trigger.IsActive(Context(bot, target))).IsFalse();
        target.Buffs = BuffsWith(target, new BuffTemplate { Id = 42, Kind = BuffKind.Bad }, Now.AddSeconds(2));
        await Assert.That(trigger.IsActive(Context(bot, target))).IsTrue();
    }

    [Test]
    public async Task BuffMissingPrimitive_HonorsRefreshBefore()
    {
        var bot = BotTestFixture.MakeBot(1, Vector3.Zero);
        var when = When("buffMissing", true);
        when.Arguments["spell"] = Newtonsoft.Json.Linq.JToken.FromObject("focus");
        when.Arguments["refreshBefore"] = Newtonsoft.Json.Linq.JToken.FromObject(5000);
        var trigger = Factory(() => 0, _ => 42).Create(when);

        bot.Buffs = BuffsWith(bot, new BuffTemplate { Id = 42, Kind = BuffKind.Good }, Now.AddSeconds(3));
        await Assert.That(trigger.IsActive(Context(bot))).IsTrue();
        bot.Buffs = BuffsWith(bot, new BuffTemplate { Id = 42, Kind = BuffKind.Good }, Now.AddSeconds(30));
        await Assert.That(trigger.IsActive(Context(bot))).IsFalse();
    }

    [Test]
    public async Task HasCleansableDebuffPrimitive_RecognizesRootAndStun()
    {
        var bot = BotTestFixture.MakeBot(1, Vector3.Zero);
        var trigger = Factory(() => 0).Create(When("hasCleansableDebuff", true));

        bot.Buffs = BuffsWith(bot, new BuffTemplate { Kind = BuffKind.Bad, Root = true }, Now.AddSeconds(10));
        await Assert.That(trigger.IsActive(Context(bot))).IsTrue();
        bot.Buffs = BuffsWith(bot, new BuffTemplate { Kind = BuffKind.Good }, Now.AddSeconds(10));
        await Assert.That(trigger.IsActive(Context(bot))).IsFalse();
    }

    [Test]
    public async Task StunnedPrimitive_RecognizesStunAndKnockdownOnly()
    {
        var bot = BotTestFixture.MakeBot(2, Vector3.Zero);
        var trigger = Factory(() => 0).Create(When("stunned", true));

        bot.Buffs = BuffsWith(bot, new BuffTemplate { Stun = true }, Now.AddSeconds(10));
        await Assert.That(trigger.IsActive(Context(bot))).IsTrue();
        bot.Buffs = BuffsWith(bot, new BuffTemplate { Root = true }, Now.AddSeconds(10));
        await Assert.That(trigger.IsActive(Context(bot))).IsFalse();
    }

    [Test]
    public async Task RotationAutoAttackIdleWindow_UsesConfiguredReengageRangeAndGlobalDelay()
    {
        var bot = BotTestFixture.MakeBot(3, Vector3.Zero);
        bot.IsAutoAttack = true;
        var target = BotTestFixture.MakeBot(4, new Vector3(50, 0, 0));
        var state = new BotCombatState { Target = target, LastSkillTime = Now.AddMilliseconds(-450) };
        var runtime = new BotRuntime(bot, new BotMovementState(), state,
            config: new BotConfig { UseEngine = false });
        var action = new RotationAutoAttackAction();

        var shortLeash = new BotConfig { UseEngine = false, ReengageRange = 40, GlobalSkillDelayMs = 400 };
        var shortLeashContext = new BotContext(bot, runtime, runtime.Blackboard, Now, shortLeash,
            BotEngineKind.Combat);
        await Assert.That(action.IsUseful(shortLeashContext)).IsTrue();
        await Assert.That(action.IsPossible(shortLeashContext)).IsTrue();

        var longLeash = new BotConfig { UseEngine = false, ReengageRange = 80, GlobalSkillDelayMs = 400 };
        var longLeashContext = new BotContext(bot, runtime, runtime.Blackboard, Now, longLeash,
            BotEngineKind.Combat);
        await Assert.That(action.IsUseful(longLeashContext)).IsFalse();
        await Assert.That(action.IsPossible(longLeashContext)).IsFalse();
    }

    [Test]
    public async Task RotationAutoAttackLeash_ClearsTargetAndStopsImmediately()
    {
        var bot = BotTestFixture.MakeBot(5, Vector3.Zero);
        bot.IsAutoAttack = true;
        var target = BotTestFixture.MakeBot(6, new Vector3(50, 0, 0));
        var state = new BotCombatState
        {
            Target = target,
            IsStalking = true,
            TripleSlashStage = 2,
            IsComboLocked = true,
            PendingComboFollowUp = 42
        };
        var runtime = new BotRuntime(bot, new BotMovementState(), state,
            config: new BotConfig { UseEngine = false });
        var mover = new RecordingMover();
        var context = new BotContext(bot, runtime, runtime.Blackboard, Now,
            new BotConfig { UseEngine = false, ReengageRange = 40 }, BotEngineKind.Combat, mover: mover);
        var action = new RotationAutoAttackAction();

        await Assert.That(action.IsPossible(context)).IsTrue();
        await Assert.That(action.Execute(context, default)).IsEqualTo(BotActionResult.Success);
        await Assert.That(state.Target).IsNull();
        await Assert.That(state.IsStalking).IsFalse();
        await Assert.That(state.TripleSlashStage).IsEqualTo(0);
        await Assert.That(state.IsComboLocked).IsFalse();
        await Assert.That(mover.StopImmediatelyCalled).IsTrue();

        await Assert.That(action.IsUseful(context)).IsTrue();
        await Assert.That(action.IsPossible(context)).IsTrue();

        state.Target = BotTestFixture.MakeBot(7, new Vector3(20, 0, 0));
        state.LastSkillTime = Now.AddMilliseconds(-100);
        await Assert.That(action.IsPossible(context)).IsTrue();
    }

    [Test]
    public async Task NotPrimitive_InvertsItsSingleChild()
    {
        var factory = Factory(() => 0);
        var when = new BotRotationWhen
        {
            Kind = "not",
            Children = [When("pvp", false)]
        };
        var context = Context(target: new Npc { Id = 3, ObjId = 1003, Template = new NpcTemplate { Scale = 1f } });

        await Assert.That(factory.Create(when).IsActive(context)).IsTrue();
        context.Runtime.CombatState.Target = BotTestFixture.MakeBot(4, new Vector3(1, 0, 0));
        await Assert.That(factory.Create(when).IsActive(context)).IsFalse();
    }

    [Test]
    public async Task GroupCooldownPrimitive_SharesItsWindowByGroup()
    {
        var factory = Factory(() => 0);
        var when = When("groupCooldown", true);
        when.Arguments["group"] = Newtonsoft.Json.Linq.JToken.FromObject("gapCloser");
        when.Arguments["ms"] = Newtonsoft.Json.Linq.JToken.FromObject(1000);
        var trigger = factory.Create(when);
        var context = Context(now: Now);

        await Assert.That(trigger.IsActive(context)).IsTrue();
        factory.ClaimGroupCooldown(when, Now);
        await Assert.That(trigger.IsActive(Context(now: Now.AddMilliseconds(999)))).IsFalse();
        await Assert.That(trigger.IsActive(Context(now: Now.AddMilliseconds(1000)))).IsTrue();
    }

    [Test]
    public async Task GroupCooldownPrimitive_DoesNotConsumeWindowWhenFirstActionFails()
    {
        var factory = Factory(() => 0);
        var when = When("groupCooldown", true);
        when.Arguments["group"] = Newtonsoft.Json.Linq.JToken.FromObject("gapCloser");
        when.Arguments["ms"] = Newtonsoft.Json.Linq.JToken.FromObject(1000);
        var firstTrigger = factory.Create(when);
        var secondTrigger = factory.Create(when);
        var context = Context(now: Now);

        await Assert.That(firstTrigger.IsActive(context)).IsTrue();
        await Assert.That(secondTrigger.IsActive(context)).IsTrue();
    }

    [Test]
    public async Task GroupCooldownSuccessHandler_ClaimsWindowAfterSuccessfulCast()
    {
        var factory = Factory(() => 0);
        var when = new BotRotationWhen
        {
            Kind = "all",
            Children =
            [
                When("pvp", true),
                new BotRotationWhen
                {
                    Kind = "groupCooldown",
                    Arguments = new Dictionary<string, Newtonsoft.Json.Linq.JToken>
                    {
                        ["group"] = Newtonsoft.Json.Linq.JToken.FromObject("gapCloser"),
                        ["ms"] = Newtonsoft.Json.Linq.JToken.FromObject(1000)
                    }
                }
            ]
        };
        var trigger = factory.Create(when);
        var context = Context(target: BotTestFixture.MakeBot(9, new Vector3(1, 0, 0)), now: Now);
        var onSuccess = factory.CreateGroupCooldownSuccessHandler(when);

        await Assert.That(trigger.IsActive(context)).IsTrue();
        onSuccess!(context);
        await Assert.That(trigger.IsActive(Context(target: context.Runtime.CombatState.Target,
            now: Now.AddMilliseconds(999)))).IsFalse();
    }

    [Test]
    public async Task CompiledGapCloserSuccess_ClosesAlternateGapCloserGroup()
    {
        var path = BotTestFixture.FindRepoFile("AAEmu.Game/Data/BotRotations/darkrunner.melee.json");
        var templateResolver = new Func<uint, SkillTemplate>(id => new SkillTemplate
        {
            Id = id,
            MaxRange = 100,
            TargetType = SkillTargetType.Hostile,
            TargetRelation = SkillTargetRelation.Hostile
        });
        var manager = new BotRotationManager(_ => true, _ => BotSkillIds.Darkrunner.SkillLearnOrder,
            templateResolver);
        await Assert.That(manager.LoadRotations(File.ReadAllText(path), "darkrunner.melee")).IsTrue();

        var strategy = manager.Compile("darkrunner.melee", templateResolver: templateResolver,
            cast: _ => SkillResult.Success);
        var bot = BotTestFixture.MakeBot(10, Vector3.Zero);
        bot.Hp = bot.MaxHp = 100;
        var target = BotTestFixture.MakeBot(11, new Vector3(2, 0, 0));
        target.Hp = target.MaxHp = 100;
        var runtime = new BotRuntime(bot, new BotMovementState(),
            new BotCombatState { Target = target }, config: new BotConfig { UseEngine = false });
        var context = new BotContext(bot, runtime, runtime.Blackboard, Now,
            new BotConfig { UseEngine = false }, BotEngineKind.Combat);
        bot.Skills = new CharacterSkills(bot);
        foreach (var skillId in BotSkillIds.Darkrunner.SkillLearnOrder)
            bot.Skills.Skills[skillId] = new Skill(templateResolver(skillId));
        var gapCloser = strategy.Actions.Single(action => action.Name == "gap:tigerStrike");
        var alternateGapCloser = strategy.TriggerNodes.Single(node => node.Actions.Any(action => action.Name == "gap:overwhelm"));

        await Assert.That(gapCloser.Execute(context, default)).IsEqualTo(BotActionResult.Success);
        await Assert.That(alternateGapCloser.Trigger.IsActive(new BotContext(bot, runtime, runtime.Blackboard,
            Now.AddMilliseconds(100), new BotConfig { UseEngine = false }, BotEngineKind.Combat))).IsFalse();
    }

    [Test]
    public async Task CompiledCast_OutOfRangeInsideGlobalDelay_IsNotPossible()
    {
        var definition = new BotRotationDefinition
        {
            Id = "delay.rotation",
            Archetype = "Test",
            Skills = new Dictionary<string, uint> { ["strike"] = 42 },
            Default = [new BotRotationRow { Action = "cast", Skill = "strike", Relevance = 11 }]
        };
        var bot = BotTestFixture.MakeBot(12, Vector3.Zero);
        bot.Hp = bot.MaxHp = 100;
        var target = BotTestFixture.MakeBot(13, new Vector3(10, 0, 0));
        target.Hp = target.MaxHp = 100;
        var runtime = new BotRuntime(bot, new BotMovementState(), new BotCombatState
        {
            Target = target,
            LastSkillTime = Now.AddMilliseconds(-100)
        }, config: new BotConfig { UseEngine = false, GlobalSkillDelayMs = 1000 });
        var context = new BotContext(bot, runtime, runtime.Blackboard, Now,
            new BotConfig { UseEngine = false, GlobalSkillDelayMs = 1000 }, BotEngineKind.Combat);
        var strategy = new BotRotationCompiler(
            templateResolver: _ => new SkillTemplate
            {
                Id = 42,
                MaxRange = 5,
                TargetType = SkillTargetType.Hostile,
                TargetRelation = SkillTargetRelation.Hostile
            },
            cast: _ => SkillResult.Success).Compile(definition);
        var action = strategy.Actions.Single();

        await Assert.That(action.IsPossible(context)).IsFalse();
        await Assert.That(action.Execute(context, default)).IsEqualTo(BotActionResult.Impossible);
    }

    [Test]
    public async Task RotationRowComboSideEffect_BeginsComboAfterSuccessfulCast()
    {
        var definition = new BotRotationDefinition
        {
            Id = "combo.rotation",
            Archetype = "Test",
            Skills = new Dictionary<string, uint> { ["charge"] = 42, ["followUp"] = 43 },
            Default =
            [
                new BotRotationRow
                {
                    Action = "cast",
                    Skill = "charge",
                    Relevance = 11,
                    Combo = new BotRotationCombo { Opener = "charge", FollowUp = "followUp" }
                }
            ]
        };
        var bot = BotTestFixture.MakeBot(5, Vector3.Zero);
        var target = BotTestFixture.MakeBot(6, new Vector3(1, 0, 0));
        bot.Hp = bot.MaxHp = 100;
        target.Hp = target.MaxHp = 100;
        var runtime = new BotRuntime(bot, new BotMovementState(), new BotCombatState { Target = target },
            config: new BotConfig { UseEngine = false });
        var context = new BotContext(bot, runtime, runtime.Blackboard, Now,
            new BotConfig { UseEngine = false }, BotEngineKind.Combat);
        bot.Skills = new CharacterSkills(bot);
        bot.Skills.Skills[42] = new Skill(new SkillTemplate { Id = 42 });
        var strategy = new BotRotationCompiler(
            templateResolver: id => new SkillTemplate
            {
                Id = id,
                MaxRange = 20,
                TargetType = SkillTargetType.Hostile,
                TargetRelation = SkillTargetRelation.Hostile
            },
            cast: _ => SkillResult.Success).Compile(definition);

        await Assert.That(strategy.Actions.Single().Execute(context, default)).IsEqualTo(BotActionResult.Success);
        await Assert.That(runtime.CombatState.LastComboSkill).IsEqualTo(42u);
        await Assert.That(runtime.CombatState.PendingComboFollowUp).IsEqualTo(43u);
        await Assert.That(runtime.CombatState.IsComboLocked).IsTrue();
    }

    [Test]
    public async Task RotationMoveAway_MovesOppositeTheTargetAndRejectsMissingTarget()
    {
        var bot = BotTestFixture.MakeBot(7, Vector3.Zero);
        var target = BotTestFixture.MakeBot(8, new Vector3(10, 0, 0));
        bot.Hp = bot.MaxHp = 100;
        target.Hp = target.MaxHp = 100;
        var runtime = new BotRuntime(bot, new BotMovementState(), new BotCombatState { Target = target },
            config: new BotConfig { UseEngine = false });
        var mover = new RecordingMover();
        var context = new BotContext(bot, runtime, runtime.Blackboard, Now,
            new BotConfig { UseEngine = false }, BotEngineKind.Combat, mover: mover);
        var action = new RotationMoveAction("move:away", "away", mover);

        await Assert.That(action.IsPossible(context)).IsTrue();
        await Assert.That(action.Execute(context, default)).IsEqualTo(BotActionResult.Impossible);
        await Assert.That(mover.Destination.X).IsLessThan(0f);
        var firstDestination = mover.Destination;
        var laterContext = new BotContext(bot, runtime, runtime.Blackboard, Now.AddSeconds(1),
            new BotConfig { UseEngine = false }, BotEngineKind.Combat, mover: mover);
        action.Execute(laterContext, default);
        await Assert.That(mover.Destination).IsNotEqualTo(firstDestination);
        runtime.CombatState.Target = null;
        await Assert.That(action.IsPossible(context)).IsFalse();
    }

    [Test]
    public async Task WiredRotationValues_ReadRuntimeStateThroughCompiledPrimitives()
    {
        var bot = BotTestFixture.MakeBot(1, Vector3.Zero);
        bot.Hp = 40;
        bot.MaxHp = 100;
        var target = BotTestFixture.MakeBot(2, new Vector3(5, 0, 0));
        var runtime = new BotRuntime(bot, new BotMovementState(), new BotCombatState
        {
            Target = target,
            IsComboLocked = true,
            IsStalking = true
        }, config: new BotConfig { UseEngine = false });
        var context = new BotContext(bot, runtime, runtime.Blackboard, Now,
            new BotConfig { UseEngine = false }, BotEngineKind.Combat);
        var values = new RotationValueResolver();

        var range = new BotRotationWhen { Kind = "range" };
        range.Arguments["min"] = Newtonsoft.Json.Linq.JToken.FromObject(4f);
        range.Arguments["max"] = Newtonsoft.Json.Linq.JToken.FromObject(6f);
        var factory = new RotationTriggerFactory(_ => 42, _ => new SkillTemplate { Id = 42, MaxRange = 20 }, values: values);

        await Assert.That(values.Distance(context)).IsEqualTo(5f);
        await Assert.That(factory.Create(range, 42).IsActive(context)).IsTrue();
        await Assert.That(factory.Create(new BotRotationWhen { Kind = "comboActive" }).IsActive(context)).IsTrue();
        await Assert.That(factory.Create(new BotRotationWhen { Kind = "resource" }).IsActive(context)).IsTrue();
        await Assert.That(values.AoePosition(context)).IsEqualTo(target.Transform.World.Position);
        await Assert.That(values.StalkerActive(context)).IsTrue();
        await Assert.That(values.Stat(context, "health", true)).IsEqualTo(40);
    }

    [Test]
    public async Task CompiledTriggerSweepAndFillerDraw_AreAllocationFreeAfterWarmup()
    {
        var bot = BotTestFixture.MakeBot(3, Vector3.Zero);
        bot.IsAutoAttack = false;
        var target = BotTestFixture.MakeBot(4, new Vector3(2, 0, 0));
        var runtime = new BotRuntime(bot, new BotMovementState(), new BotCombatState { Target = target },
            config: new BotConfig { UseEngine = false });
        var definition = new BotRotationDefinition
        {
            Id = "allocation.rotation",
            Archetype = "Test",
            Skills = new Dictionary<string, uint> { ["strike"] = 42 },
            Default = [new BotRotationRow { Action = "autoAttack", Relevance = 11, Weight = 1 }],
            Rules =
            [
                new BotRotationRule
                {
                    When = new BotRotationWhen
                    {
                        Kind = "range",
                        Arguments = new Dictionary<string, Newtonsoft.Json.Linq.JToken>
                        {
                            ["min"] = Newtonsoft.Json.Linq.JToken.FromObject(0f),
                            ["max"] = Newtonsoft.Json.Linq.JToken.FromObject(20f)
                        }
                    },
                    Then = [new BotRotationRow { Action = "move", Skill = "melee", Relevance = 31 }]
                },
                new BotRotationRule
                {
                    When = new BotRotationWhen { Kind = "comboActive" },
                    Then = [new BotRotationRow { Action = "move", Skill = "melee", Relevance = 32, As = "combo:melee" }]
                }
            ]
        };
        var strategy = new BotRotationCompiler(roll: () => 0).Compile(definition);
        var contexts = Enumerable.Range(0, 100)
            .Select(index => new BotContext(bot, runtime, runtime.Blackboard,
                DateTime.UtcNow.AddMilliseconds(index), new BotConfig { UseEngine = false }, BotEngineKind.Combat))
            .ToArray();
        for (var warmup = 0; warmup < 10; warmup++)
        {
            foreach (var context in contexts)
            {
                for (var index = 0; index < strategy.TriggerNodes.Count; index++)
                    strategy.TriggerNodes[index].Trigger.IsActive(context);
                strategy.Filler.SelectAction(context);
            }
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var before = GC.GetAllocatedBytesForCurrentThread();
        foreach (var context in contexts)
        {
            for (var index = 0; index < strategy.TriggerNodes.Count; index++)
                strategy.TriggerNodes[index].Trigger.IsActive(context);
        }
        var triggerAllocated = GC.GetAllocatedBytesForCurrentThread() - before;
        before = GC.GetAllocatedBytesForCurrentThread();
        foreach (var context in contexts)
            strategy.Filler.SelectAction(context);
        var fillerAllocated = GC.GetAllocatedBytesForCurrentThread() - before;

        await Assert.That(triggerAllocated).IsEqualTo(0);
        await Assert.That(fillerAllocated).IsEqualTo(0);
    }

    private static RotationTriggerFactory Factory(Func<int> roll, Func<string, uint?> resolver = null) =>
        new(resolver ?? (_ => null), _ => new SkillTemplate { MaxRange = 20 }, roll);

    private static BotRotationWhen When(string kind, bool value) => new() { Kind = kind };

    private static BotContext Context(Character bot = null, Unit target = null, DateTime? now = null)
    {
        bot ??= BotTestFixture.MakeBot(1, Vector3.Zero);
        var runtime = new BotRuntime(bot, new BotMovementState(), new BotCombatState { Target = target },
            config: new BotConfig { UseEngine = false });
        return new BotContext(bot, runtime, runtime.Blackboard, now ?? Now,
            new BotConfig { UseEngine = false }, BotEngineKind.Combat);
    }

    private static IBuffs BuffsWith(Character owner, BuffTemplate template, DateTime endTime)
    {
        var buffs = Mock.Of<IBuffs>();
        buffs.HasEffectsMatchingCondition(Any<Func<Buff, bool>>())
            .Returns((Func<Buff, bool> predicate) => predicate(new Buff(owner, owner,
                new SkillCasterUnit(owner.ObjId), template, null, Now)
            {
                Duration = Math.Max(1, (int)(endTime - Now).TotalMilliseconds),
                EndTime = endTime
            }));
        return buffs.Object;
    }

    private static FixedHealthCharacterMock FixedCharacter(uint id, Vector3 position, bool isBot)
    {
        var character = new FixedHealthCharacterMock
        {
            Id = id,
            ObjId = id + 1000,
            Name = $"character{id}",
            IsBot = isBot,
            Hp = 100,
            Mp = 100
        };
        character.Transform.Local.SetPosition(position);
        return character;
    }

    private sealed class TestSkillTask : SkillTask
    {
        public TestSkillTask() : base(new Skill())
        {
        }

        public override void Execute()
        {
        }
    }

    private sealed class RecordingMover : IBotMover
    {
        public Vector3 Destination { get; private set; }
        public bool StopIfMovingCalled { get; private set; }
        public bool StopImmediatelyCalled { get; private set; }
        public void SetDestination(Character bot, Vector3 position, bool run, float tolerance) => Destination = position;
        public void StopIfMoving(Character bot) => StopIfMovingCalled = true;
        public void StopImmediately(Character bot) => StopImmediatelyCalled = true;
        public void Face(Character bot, float angle) { }
        public void Teleport(Character bot, Vector3 position) { }
        public void Follow(Character bot, Character target, float distance) { }
        public void StopFollow(Character bot) { }
        public void SendRelaxedStance(Character bot) { }
    }
}
