using System.Numerics;
using AAEmu.Game.Bots.Body;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Static;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.Team;
using AAEmu.UnitTests.Utils.Mocks;

namespace AAEmu.UnitTests.Bots.Body;

public class BotCastSkillActionTests
{
    private static readonly DateTime Now = new(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task Execute_Success_StampsCombatAndBattleState()
    {
        var (context, target) = CreateContext();
        var metrics = new BotHostMetrics();
        context.Runtime.HostMetrics = metrics;
        BotCastRequest request = null;
        var action = new BotCastSkillAction(
            41,
            TargetSource.CurrentTarget,
            _ => Skill(41),
            cast: captured =>
            {
                request = captured;
                return SkillResult.Success;
            });

        var result = action.Execute(context, default);

        await Assert.That(result).IsEqualTo(BotActionResult.Success);
        await Assert.That(request).IsNotNull();
        await Assert.That(request.Skill.Id).IsEqualTo(41u);
        await Assert.That(request.Caster.ObjId).IsEqualTo(context.Bot.ObjId);
        await Assert.That(request.Target).IsTypeOf<SkillCastUnitTarget>();
        await Assert.That(request.Target.ObjId).IsEqualTo(target.ObjId);
        await Assert.That(context.Runtime.CombatState.LastSkillTime).IsEqualTo(Now);
        await Assert.That(context.Bot.IsInBattle).IsTrue();
        await Assert.That(target.IsInBattle).IsTrue();
        await Assert.That(metrics.Snapshot().CastAttempts).IsEqualTo(1L);
        await Assert.That(metrics.Snapshot().CastSuccesses).IsEqualTo(1L);
    }

    [Test]
    public async Task Execute_GateFailure_ReturnsImpossibleWithReason()
    {
        var (context, _) = CreateContext();
        context.Bot.Cooldowns.Cooldowns[41] = Now.AddSeconds(1);
        var action = new BotCastSkillAction(41, TargetSource.CurrentTarget, _ => Skill(41));

        var result = action.Execute(context, default);

        await Assert.That(result).IsEqualTo(BotActionResult.Impossible);
        await Assert.That(action.LastGate.Reason).IsEqualTo(GateReason.Cooldown);
    }

    [Test]
    public async Task Execute_SkillFailure_ReturnsFailureWithoutCombatStamp()
    {
        var (context, target) = CreateContext();
        var metrics = new BotHostMetrics();
        context.Runtime.HostMetrics = metrics;
        var action = new BotCastSkillAction(
            41,
            TargetSource.CurrentTarget,
            _ => Skill(41),
            cast: _ => SkillResult.Failure);

        var result = action.Execute(context, default);

        await Assert.That(result).IsEqualTo(BotActionResult.Failure);
        await Assert.That(context.Runtime.CombatState.LastSkillTime).IsEqualTo(DateTime.MinValue);
        await Assert.That(context.Bot.IsInBattle).IsFalse();
        await Assert.That(target.IsInBattle).IsFalse();
        await Assert.That(metrics.Snapshot().CastAttempts).IsEqualTo(1L);
        await Assert.That(metrics.Snapshot().CastFailures).IsEqualTo(1L);
    }

    [Test]
    public async Task Execute_PositionSource_BuildsPositionTarget()
    {
        var (context, _) = CreateContext();
        BotCastRequest request = null;
        var action = new BotCastSkillAction(
            42,
            TargetSource.Position,
            _ => Skill(42, SkillTargetType.Pos),
            cast: captured =>
            {
                request = captured;
                return SkillResult.Success;
            });

        var result = action.Execute(context, new BotEvent("ground", new Vector3(4, 5, 6)));

        await Assert.That(result).IsEqualTo(BotActionResult.Success);
        var position = request.Target as SkillCastPositionTarget;
        await Assert.That(position).IsNotNull();
        await Assert.That(position.PosX).IsEqualTo(4f);
        await Assert.That(position.PosY).IsEqualTo(5f);
        await Assert.That(position.PosZ).IsEqualTo(6f);
    }

    [Test]
    [Arguments(13282, 5)]
    [Arguments(10644, 9)]
    public async Task Execute_SelfCenteredHostileAoe_UsesEffectRadiusBoundary(int skillId, int effectRadius)
    {
        var (insideContext, _) = CreateContext(new Vector3(effectRadius, 0, 0));
        var insideCasts = 0;
        var insideAction = new BotCastSkillAction(
            (uint)skillId,
            TargetSource.CurrentTarget,
            _ => SelfCenteredHostileAoe((uint)skillId, effectRadius),
            cast: _ =>
            {
                insideCasts++;
                return SkillResult.Success;
            });

        var insideResult = insideAction.Execute(insideContext, default);

        await Assert.That(insideResult).IsEqualTo(BotActionResult.Success);
        await Assert.That(insideAction.LastGate.Reason).IsEqualTo(GateReason.Ok);
        await Assert.That(insideCasts).IsEqualTo(1);

        var (outsideContext, _) = CreateContext(new Vector3(effectRadius + 0.01f, 0, 0));
        var outsideCasts = 0;
        var outsideAction = new BotCastSkillAction(
            (uint)skillId,
            TargetSource.CurrentTarget,
            _ => SelfCenteredHostileAoe((uint)skillId, effectRadius),
            cast: _ =>
            {
                outsideCasts++;
                return SkillResult.Success;
            });

        var outsideResult = outsideAction.Execute(outsideContext, default);

        await Assert.That(outsideResult).IsEqualTo(BotActionResult.Impossible);
        await Assert.That(outsideAction.LastGate.Reason).IsEqualTo(GateReason.OutOfRange);
        await Assert.That(outsideCasts).IsEqualTo(0);
    }

    [Test]
    public async Task Execute_SelfBuff_IgnoresHostileEffectRangeGate()
    {
        var (context, _) = CreateContext(new Vector3(100, 0, 0));
        var casts = 0;
        var action = new BotCastSkillAction(
            10377,
            TargetSource.CurrentTarget,
            _ => new SkillTemplate
            {
                Id = 10377,
                TargetType = SkillTargetType.Self,
                TargetRelation = SkillTargetRelation.Any,
                TargetAreaRadius = 1
            },
            cast: _ =>
            {
                casts++;
                return SkillResult.Success;
            });

        var result = action.Execute(context, default);

        await Assert.That(result).IsEqualTo(BotActionResult.Success);
        await Assert.That(action.LastGate.Reason).IsEqualTo(GateReason.Ok);
        await Assert.That(casts).IsEqualTo(1);
    }

    [Test]
    public async Task Execute_SelfSourceHostileAoe_UsesCurrentCombatTargetForEffectRange()
    {
        var (context, _) = CreateContext(new Vector3(5.01f, 0, 0));
        var casts = 0;
        var action = new BotCastSkillAction(
            13282,
            TargetSource.Self,
            _ => SelfCenteredHostileAoe(13282, 5),
            cast: _ =>
            {
                casts++;
                return SkillResult.Success;
            });

        var result = action.Execute(context, default);

        await Assert.That(result).IsEqualTo(BotActionResult.Impossible);
        await Assert.That(action.LastGate.Reason).IsEqualTo(GateReason.OutOfRange);
        await Assert.That(casts).IsEqualTo(0);
    }

    [Test]
    public async Task Execute_PositionSourceWithoutPayload_TargetsCurrentHostileAtMaximumRange()
    {
        var (context, target) = CreateContext(new Vector3(25, 0, 0));
        BotCastRequest request = null;
        var action = new BotCastSkillAction(
            13286,
            TargetSource.Position,
            _ => PositionSkill(13286, 10, 25),
            cast: captured =>
            {
                request = captured;
                return SkillResult.Success;
            });

        var result = action.Execute(context, default);

        await Assert.That(result).IsEqualTo(BotActionResult.Success);
        await Assert.That(action.LastGate.Reason).IsEqualTo(GateReason.Ok);
        var position = request.Target as SkillCastPositionTarget;
        await Assert.That(position).IsNotNull();
        await Assert.That(position.PosX).IsEqualTo(target.Transform.World.Position.X);
        await Assert.That(position.PosY).IsEqualTo(target.Transform.World.Position.Y);
        await Assert.That(position.PosZ).IsEqualTo(target.Transform.World.Position.Z);
    }

    [Test]
    [Arguments(9.99f)]
    [Arguments(25.01f)]
    public async Task Execute_PositionSourceWithoutPayload_RejectsPointOutsideTemplateRange(float distance)
    {
        var (context, _) = CreateContext(new Vector3(distance, 0, 0));
        var casts = 0;
        var action = new BotCastSkillAction(
            13286,
            TargetSource.Position,
            _ => PositionSkill(13286, 10, 25),
            cast: _ =>
            {
                casts++;
                return SkillResult.Success;
            });

        var result = action.Execute(context, default);

        await Assert.That(result).IsEqualTo(BotActionResult.Impossible);
        await Assert.That(action.LastGate.Reason).IsEqualTo(GateReason.OutOfRange);
        await Assert.That(casts).IsEqualTo(0);
    }

    [Test]
    public async Task Execute_PartyLowestSource_TargetsTheMostInjuredMemberInsteadOfEnemy()
    {
        var leader = FixedCharacter(10, Vector3.Zero, false);
        var bot = FixedCharacter(1, Vector3.Zero, true);
        var enemy = FixedCharacter(2, new Vector3(10, 0, 0), false);
        var ally = FixedCharacter(11, new Vector3(5, 0, 0), true);
        ally.Hp = 25;
        var runtime = new BotRuntime(
            bot,
            new BotMovementState(),
            new BotCombatState { Target = enemy },
            config: new BotConfig { UseEngine = false });
        var context = new BotContext(bot, runtime, runtime.Blackboard, Now,
            new BotConfig { UseEngine = false }, BotEngineKind.Combat);
        var team = new Team { Id = 77, OwnerId = leader.Id, IsParty = true };
        team.AddMember(leader);
        team.AddMember(bot);
        team.AddMember(ally);
        context.Runtime.TeamHooks.Refresh(team);
        context.Runtime.Social.CommitLowestHealthMember(45f, 0f, 85f);
        BotCastRequest request = null;
        var action = new BotCastSkillAction(
            44,
            TargetSource.PartyLowest,
            _ => new SkillTemplate
            {
                Id = 44,
                TargetType = SkillTargetType.Party,
                TargetRelation = SkillTargetRelation.Party,
                MaxRange = 30
            },
            cast: captured =>
            {
                request = captured;
                return SkillResult.Success;
            });

        var result = action.Execute(context, default);

        await Assert.That(result).IsEqualTo(BotActionResult.Success);
        await Assert.That(request.Target.ObjId).IsEqualTo(ally.ObjId);
        await Assert.That(request.Target.ObjId).IsNotEqualTo(enemy.ObjId);
        await Assert.That(runtime.CombatState.Target).IsSameReferenceAs(enemy);
    }

    [Test]
    public async Task Execute_PartyLowestSource_UsesTheMemberValidatedByTheCachedGate()
    {
        var leader = FixedCharacter(10, Vector3.Zero, false);
        var bot = FixedCharacter(1, Vector3.Zero, true);
        var firstAlly = FixedCharacter(11, new Vector3(5, 0, 0), true);
        var secondAlly = FixedCharacter(12, new Vector3(6, 0, 0), true);
        firstAlly.Hp = 25;
        secondAlly.Hp = 50;
        var runtime = new BotRuntime(
            bot,
            new BotMovementState(),
            new BotCombatState(),
            config: new BotConfig { UseEngine = false });
        var context = new BotContext(bot, runtime, runtime.Blackboard, Now,
            new BotConfig { UseEngine = false }, BotEngineKind.Combat);
        var team = new Team { Id = 77, OwnerId = leader.Id, IsParty = true };
        team.AddMember(leader);
        team.AddMember(bot);
        team.AddMember(firstAlly);
        team.AddMember(secondAlly);
        context.Runtime.TeamHooks.Refresh(team);
        context.Runtime.Social.CommitLowestHealthMember(45f, 0f, 85f);
        BotCastRequest request = null;
        var action = new BotCastSkillAction(
            44,
            TargetSource.PartyLowest,
            _ => new SkillTemplate
            {
                Id = 44,
                TargetType = SkillTargetType.Party,
                TargetRelation = SkillTargetRelation.Party,
                MaxRange = 30
            },
            cast: captured =>
            {
                request = captured;
                return SkillResult.Success;
            });

        await Assert.That(action.IsPossible(context)).IsTrue();
        secondAlly.Hp = 10;

        var result = action.Execute(context, default);

        await Assert.That(result).IsEqualTo(BotActionResult.Success);
        await Assert.That(request.Target.ObjId).IsEqualTo(firstAlly.ObjId);
        await Assert.That(firstAlly.IsInBattle).IsTrue();
        await Assert.That(secondAlly.IsInBattle).IsFalse();
    }

    [Test]
    public async Task IsPossible_UsesGate()
    {
        var (context, _) = CreateContext();
        context.Bot.Cooldowns.Cooldowns[41] = Now.AddSeconds(1);
        var action = new BotCastSkillAction(41, TargetSource.CurrentTarget, _ => Skill(41));

        var possible = action.IsPossible(context);

        await Assert.That(possible).IsFalse();
        await Assert.That(action.LastGate.Reason).IsEqualTo(GateReason.Cooldown);
    }

    [Test]
    public async Task IsPossible_CompiledRotationRequirementRejectsUnlearnedSkill()
    {
        var (context, _) = CreateContext();
        context.Bot.Skills = new CharacterSkills(context.Bot);
        var action = new BotCastSkillAction(
            41,
            TargetSource.CurrentTarget,
            _ => Skill(41, SkillTargetType.Hostile),
            requireKnownSkill: true);

        var possible = action.IsPossible(context);

        await Assert.That(possible).IsFalse();
        await Assert.That(action.LastGate.Reason).IsEqualTo(GateReason.Unlearned);
    }

    [Test]
    public async Task IsPossible_CompiledRotationRequirementAllowsLearnedSkill()
    {
        var (context, _) = CreateContext();
        context.Bot.Skills = new CharacterSkills(context.Bot);
        context.Bot.Skills.Skills[41] = new Skill(Skill(41, SkillTargetType.Hostile));
        var action = new BotCastSkillAction(
            41,
            TargetSource.CurrentTarget,
            _ => Skill(41, SkillTargetType.Hostile),
            requireKnownSkill: true);

        var possible = action.IsPossible(context);

        await Assert.That(possible).IsTrue();
        await Assert.That(action.LastGate.Reason).IsEqualTo(GateReason.Ok);
    }

    [Test]
    public async Task Execute_UsesGateResultCachedByIsPossible()
    {
        var (context, _) = CreateContext();
        var templateCalls = 0;
        var action = new BotCastSkillAction(
            41,
            TargetSource.Self,
            _ =>
            {
                templateCalls++;
                return Skill(41);
            },
            cast: _ => SkillResult.Success);

        await Assert.That(action.IsPossible(context)).IsTrue();
        await Assert.That(action.Execute(context, default)).IsEqualTo(BotActionResult.Success);
        await Assert.That(templateCalls).IsEqualTo(2);
    }

    [Test]
    public async Task IsPossible_RechecksGateOnTheNextTick()
    {
        var (context, _) = CreateContext();
        var templateCalls = 0;
        var action = new BotCastSkillAction(41, TargetSource.Self, _ =>
        {
            templateCalls++;
            return Skill(41);
        });

        await Assert.That(action.IsPossible(context)).IsTrue();
        var nextTick = new BotContext(context.Bot, context.Runtime, context.Blackboard,
            Now.AddMilliseconds(1), context.Config, context.EngineKind);
        await Assert.That(action.IsPossible(nextTick)).IsTrue();
        await Assert.That(templateCalls).IsEqualTo(2);
    }

    private static (BotContext Context, CharacterMock Target) CreateContext(Vector3? targetPosition = null)
    {
        var bot = BotTestFixture.MakeBot(1, Vector3.Zero);
        bot.IsBot = true;
        bot.Hp = 100;
        bot.MaxHp = 100;
        bot.Mp = 100;
        var target = BotTestFixture.MakeBot(2, targetPosition ?? new Vector3(1, 0, 0));
        target.Hp = 100;
        target.MaxHp = 100;
        var runtime = new BotRuntime(
            bot,
            new BotMovementState(),
            new BotCombatState { Target = target },
            config: new BotConfig { UseEngine = false });
        return (new BotContext(bot, runtime, runtime.Blackboard, Now, new BotConfig(), BotEngineKind.Combat), target);
    }

    private static SkillTemplate Skill(uint id, SkillTargetType targetType = SkillTargetType.Self)
    {
        return new SkillTemplate
        {
            Id = id,
            TargetType = targetType,
            TargetRelation = SkillTargetRelation.Any,
            MaxRange = 100
        };
    }

    private static SkillTemplate SelfCenteredHostileAoe(uint id, int effectRadius)
    {
        return new SkillTemplate
        {
            Id = id,
            TargetType = SkillTargetType.Self,
            TargetRelation = SkillTargetRelation.Hostile,
            TargetAreaRadius = effectRadius
        };
    }

    private static SkillTemplate PositionSkill(uint id, int minRange, int maxRange)
    {
        return new SkillTemplate
        {
            Id = id,
            TargetType = SkillTargetType.Pos,
            TargetRelation = SkillTargetRelation.Hostile,
            MinRange = minRange,
            MaxRange = maxRange
        };
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
}
