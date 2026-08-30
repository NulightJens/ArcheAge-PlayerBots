using System.Numerics;
using AAEmu.Game.Bots.Body;
using AAEmu.Game.Bots.Body.Positioning;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Bots.Social;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.DoodadObj;
using AAEmu.Game.Models.Game.Team;
using AAEmu.Game.Models.Game.Units.Movements;
using AAEmu.Game.Models.Game.World;
using AAEmu.UnitTests.Utils.Mocks;
using Microsoft.Extensions.Time.Testing;

namespace AAEmu.UnitTests.Bots.Body.Positioning;

public class PositioningActionTests
{
    private static readonly DateTime Now = new(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task ReachMelee_StopsAtAttackRange()
    {
        var mover = new RecordingMover();
        var (context, target) = CreateContext(new Vector3(10, 0, 0));
        var action = new ReachMeleeAction(mover);

        var result = action.Execute(context, default);

        await Assert.That(result).IsEqualTo(BotActionResult.Success);
        await Assert.That(mover.Destinations.Single().Position).IsEqualTo(new Vector3(8.5f, 0, 0));
        await Assert.That(mover.Destinations.Single().Run).IsTrue();
        await Assert.That(target.Transform.World.Position).IsEqualTo(new Vector3(10, 0, 0));
    }

    [Test]
    public async Task ReachMelee_UsesThreeDimensionalDirectionForSlopedTarget()
    {
        var mover = new RecordingMover();
        var context = CreateContext(new Vector3(10, 0, 10)).Context;
        var action = new ReachMeleeAction(mover);
        var direction = Vector3.Normalize(new Vector3(10, 0, 10));

        var result = action.Execute(context, default);

        await Assert.That(result).IsEqualTo(BotActionResult.Success);
        await Assert.That(mover.Destinations.Single().Position).IsEqualTo(new Vector3(10, 0, 10) - direction * 1.5f);
    }

    [Test]
    public async Task ReachMelee_StopsWhenAlreadyInRange()
    {
        var mover = new RecordingMover();
        var context = CreateContext(new Vector3(1, 0, 0)).Context;
        var action = new ReachMeleeAction(mover);

        var result = action.Execute(context, default);

        await Assert.That(result).IsEqualTo(BotActionResult.Success);
        await Assert.That(mover.Stops).IsEqualTo(1);
        await Assert.That(mover.Destinations).IsEmpty();
    }

    [Test]
    public async Task ReachSpellRange_StopsAtMaxRangeMinusOne()
    {
        var mover = new RecordingMover();
        var context = CreateContext(new Vector3(30, 0, 0)).Context;
        var action = new ReachSpellRangeAction(20f, mover);

        var result = action.Execute(context, default);

        await Assert.That(result).IsEqualTo(BotActionResult.Success);
        await Assert.That(mover.Destinations.Single().Position).IsEqualTo(new Vector3(11, 0, 0));
    }

    [Test]
    public async Task MaintainSpellRange_ApproachesToMaxRangeMinusOne()
    {
        var mover = new RecordingMover();
        var context = CreateContext(new Vector3(30, 0, 0)).Context;
        var action = new MaintainSpellRangeAction(20f, mover);

        var result = action.Execute(context, default);

        await Assert.That(result).IsEqualTo(BotActionResult.Success);
        await Assert.That(mover.Destinations.Single().Position).IsEqualTo(new Vector3(11, 0, 0));
    }

    [Test]
    public async Task MaintainSpellRange_RetreatsToMaxRangeMinusOne()
    {
        var mover = new RecordingMover();
        var context = CreateContext(new Vector3(2, 0, 0)).Context;
        var action = new MaintainSpellRangeAction(20f, mover);

        var result = action.Execute(context, default);

        await Assert.That(result).IsEqualTo(BotActionResult.Success);
        await Assert.That(mover.Destinations.Single().Position).IsEqualTo(new Vector3(-17, 0, 0));
    }

    [Test]
    public async Task MaintainSpellRange_TangentCloseEscape_IsStableOutwardAndAvoidsBackwardPenalty()
    {
        var mover = new RecordingMover();
        var (context, target) = CreateContext(new Vector3(15, 0, 0));
        var action = new MaintainSpellRangeAction(20f, mover, tangentCloseEscape: true);

        await Assert.That(action.Execute(context, default)).IsEqualTo(BotActionResult.Success);
        await Assert.That(action.Execute(context, default)).IsEqualTo(BotActionResult.Success);

        var first = mover.Destinations[0].Position;
        var second = mover.Destinations[1].Position;
        var moveDirection = Vector3.Normalize(first - context.Bot.Transform.World.Position);
        var toTarget = Vector3.Normalize(target.Transform.World.Position - context.Bot.Transform.World.Position);
        var outward = -toTarget;
        var outwardComponent = Vector3.Dot(moveDirection, outward);
        var tangentComponent = MathF.Sqrt(MathF.Max(0f, 1f - outwardComponent * outwardComponent));

        await Assert.That(first).IsEqualTo(second);
        await Assert.That(Vector3.Distance(first, target.Transform.World.Position)).IsEqualTo(19f).Within(0.001f);
        await Assert.That(outwardComponent).IsEqualTo(0.25f).Within(0.001f);
        await Assert.That(tangentComponent).IsGreaterThan(outwardComponent);
        await Assert.That(BotMovementMath.DirectionalMultiplier(moveDirection, toTarget)).IsEqualTo(1f);
    }

    [Test]
    public async Task MaintainSpellRange_IsNotUsefulInsideTheTwoMeterBand()
    {
        var mover = new RecordingMover();
        var context = CreateContext(new Vector3(19, 0, 0)).Context;
        var action = new MaintainSpellRangeAction(20f, mover);

        await Assert.That(action.MinimumRange).IsEqualTo(18f);
        await Assert.That(action.PreferredRange).IsEqualTo(19f);
        await Assert.That(action.MaximumRange).IsEqualTo(20f);
        await Assert.That(action.IsUseful(context)).IsFalse();
        await Assert.That(action.IsPossible(context)).IsFalse();
    }

    [Test]
    public async Task HealRecipientRange_ApproachesCommittedRecipientToMaximumRangeMinusOne()
    {
        var mover = new RecordingMover();
        var context = CreateHealContext(new Vector3(30, 0, 0), BotMovementOrder.Follow);
        var action = new HealRecipientRangeAction(25f, mover);

        var result = action.Execute(context, default);

        await Assert.That(result).IsEqualTo(BotActionResult.Success);
        await Assert.That(action.MaximumRange).IsEqualTo(25f);
        await Assert.That(action.PreferredRange).IsEqualTo(24f);
        await Assert.That(mover.Destinations.Single().Position).IsEqualTo(new Vector3(6, 0, 0));
        await Assert.That(mover.Destinations.Single().Tolerance).IsEqualTo(0.5f);
    }

    [Test]
    public async Task HealRecipientRange_HoldsInRangeWithoutRetreating()
    {
        var mover = new RecordingMover();
        var context = CreateHealContext(new Vector3(2, 0, 0), BotMovementOrder.Follow);
        var action = new HealRecipientRangeAction(25f, mover);

        var result = action.Execute(context, default);

        await Assert.That(result).IsEqualTo(BotActionResult.Success);
        await Assert.That(mover.Stops).IsEqualTo(1);
        await Assert.That(mover.Destinations).IsEmpty();
    }

    [Test]
    public async Task HealRecipientRange_StayStopsWithoutLeakingACombatDestination()
    {
        var mover = new RecordingMover();
        var context = CreateHealContext(new Vector3(30, 0, 0), BotMovementOrder.Stay);
        var action = new HealRecipientRangeAction(25f, mover);

        var result = action.Execute(context, default);

        await Assert.That(result).IsEqualTo(BotActionResult.Success);
        await Assert.That(mover.Stops).IsEqualTo(1);
        await Assert.That(mover.Destinations).IsEmpty();
    }

    [Test]
    public async Task HealRecipientRange_StayAllowsRequiredMovementDuringCombat()
    {
        var mover = new RecordingMover();
        var context = CreateHealContext(new Vector3(30, 0, 0), BotMovementOrder.Stay);
        context.Runtime.CombatState.TransitionTo(BotCombatStateType.Combat);
        var action = new HealRecipientRangeAction(25f, mover);

        var result = action.Execute(context, default);

        await Assert.That(result).IsEqualTo(BotActionResult.Success);
        await Assert.That(mover.Destinations.Single().Position).IsEqualTo(new Vector3(6, 0, 0));
        await Assert.That(mover.Stops).IsEqualTo(0);
    }

    [Test]
    public async Task SetFacing_SendsAngleAndDeduplicatesForFiftyMilliseconds()
    {
        var mover = new RecordingMover();
        var (context, _) = CreateContext(new Vector3(0, 10, 0));
        var action = new SetFacingAction(mover);

        await Assert.That(action.Execute(context, default)).IsEqualTo(BotActionResult.Success);
        await Assert.That(action.Execute(context, default)).IsEqualTo(BotActionResult.Success);
        await Assert.That(mover.Faces).Count().IsEqualTo(1);
        await Assert.That(mover.Faces[0].Angle).IsEqualTo(90f);
    }

    [Test]
    public async Task BotManagerMover_FaceConvertsWorldHeadingToWireHeading()
    {
        BotTestFixture.RegisterTaskManager();
        try
        {
            var manager = new AAEmu.Game.Core.Managers.Bots.BotManager();
            var bot = BotTestFixture.MakeBot(9, Vector3.Zero);
            var sent = new List<UnitMoveType>();
            var broadcaster = new BotMovementBroadcaster(bot, new FakeTimeProvider(
                new DateTimeOffset(Now)));
            broadcaster.MoveTypeSink = sent.Add;
            BotTestFixture.GetDictionary<BotMovementBroadcaster>(manager, "_broadcasters")[bot.Id] = broadcaster;
            BotTestFixture.RegisterSingletons(manager);

            var time = BotTestFixture.GetPrivateField<FakeTimeProvider>(broadcaster, "_time");
            time.Advance(TimeSpan.FromMilliseconds(51));
            bot.IsInBattle = true;
            BotManagerMover.Instance.Face(bot, 90f);

            await Assert.That(sent).Count().IsEqualTo(1);
            await Assert.That(sent[0].RotationZ).IsEqualTo((sbyte)0);
        }
        finally
        {
            BotTestFixture.ResetTaskManager();
        }
    }

    [Test]
    public async Task RearFlank_UsesNinetyToOneTwentyDegreeOffsetForNormalizedRolls()
    {
        foreach (var (roll, expectedAngle) in new[] { (0f, 90f), (0.5f, 105f), (1f, 120f) })
        {
            var mover = new RecordingMover();
            var (context, target) = CreateContext(Vector3.Zero);
            target.Transform.Local.SetRotation(0, 0, 0);
            var action = new RearFlankAction(mover, () => roll);

            var result = action.Execute(context, default);
            var destination = mover.Destinations.Single().Position;
            var radial = destination - target.Transform.World.Position;
            var angle = MathF.Atan2(radial.Y, radial.X) * 180f / MathF.PI;
            if (angle < 0)
                angle += 360;

            await Assert.That(result).IsEqualTo(BotActionResult.Success);
            await Assert.That(MathF.Abs(radial.Length() - (float)BotConfig.Instance.AttackRange * 0.5f)).IsLessThan(0.0001f);
            await Assert.That(angle).IsEqualTo(expectedAngle);
        }
    }

    [Test]
    public async Task Flee_MovesAwayAlongBotToTargetAxis()
    {
        var mover = new RecordingMover();
        var context = CreateContext(new Vector3(10, 0, 0)).Context;
        var action = new FleeAction(mover, new BotConfig { FleeDistance = 15 });

        var result = action.Execute(context, default);

        await Assert.That(result).IsEqualTo(BotActionResult.Success);
        await Assert.That(mover.Destinations.Single().Position).IsEqualTo(new Vector3(-15, 0, 0));
    }

    [Test]
    public async Task AvoidHazard_ExitsCirclePerpendicularToCaster_NotRadiallyOutward()
    {
        var mover = new RecordingMover();
        var context = CreateContext(new Vector3(1, 1, 0)).Context;
        var caster = BotTestFixture.MakeBot(3, new Vector3(0, -10, 0));
        var owner = new Doodad();
        owner.Transform.Local.SetPosition(Vector3.Zero);
        var trigger = new AreaTrigger
        {
            Owner = owner,
            Caster = caster,
            Shape = new AreaShape { Type = AreaShapeType.Sphere, Value1 = 5 },
            TargetRelation = AAEmu.Game.Models.Game.Skills.SkillTargetRelation.Hostile
        };
        var action = new AvoidHazardAction(_ => [trigger], mover);

        var result = action.Execute(context, default);
        var destination = mover.Destinations.Single().Position;

        await Assert.That(result).IsEqualTo(BotActionResult.Success);
        await Assert.That(destination).IsEqualTo(new Vector3(6, 0, 0));
        await Assert.That(destination).IsNotEqualTo(new Vector3(6 / MathF.Sqrt(2), 6 / MathF.Sqrt(2), 0));
    }

    [Test]
    public async Task AvoidHazard_ExitsNearestFaceOfHostileCuboid()
    {
        var mover = new RecordingMover();
        var context = CreateContext(new Vector3(1, 0, 0)).Context;
        var owner = new Doodad();
        owner.Transform.Local.SetPosition(Vector3.Zero);
        var trigger = new AreaTrigger
        {
            Owner = owner,
            Shape = new AreaShape
            {
                Type = AreaShapeType.Cuboid,
                Value1 = 4,
                Value2 = 8,
                Value3 = 2
            },
            TargetRelation = AAEmu.Game.Models.Game.Skills.SkillTargetRelation.Hostile
        };
        var action = new AvoidHazardAction(_ => [trigger], mover);

        var result = action.Execute(context, default);

        await Assert.That(result).IsEqualTo(BotActionResult.Success);
        await Assert.That(mover.Destinations.Single().Position).IsEqualTo(new Vector3(3, 0, 0));
    }

    private static (BotContext Context, CharacterMock Target) CreateContext(Vector3 targetPosition)
    {
        var bot = BotTestFixture.MakeBot(1, Vector3.Zero);
        bot.IsBot = true;
        bot.Hp = 100;
        bot.MaxHp = 100;
        var target = BotTestFixture.MakeBot(2, targetPosition);
        target.Hp = 100;
        target.MaxHp = 100;
        var runtime = new BotRuntime(
            bot,
            new BotMovementState(),
            new BotCombatState { Target = target },
            config: new BotConfig { UseEngine = false });
        return (new BotContext(bot, runtime, runtime.Blackboard, Now, new BotConfig(), BotEngineKind.Combat), target);
    }

    private static BotContext CreateHealContext(Vector3 recipientPosition, BotMovementOrder movementOrder)
    {
        var leader = FixedCharacter(10, Vector3.Zero, false, 100);
        var healer = FixedCharacter(11, Vector3.Zero, true, 100);
        var recipient = FixedCharacter(12, recipientPosition, true, 50);
        var team = new Team { Id = 77, OwnerId = leader.Id, IsParty = true };
        team.AddMember(leader);
        team.AddMember(healer);
        team.AddMember(recipient);
        var runtime = new BotRuntime(
            healer,
            new BotMovementState(),
            new BotCombatState(),
            config: new BotConfig { UseEngine = false });
        runtime.TeamHooks.Refresh(team);
        if (movementOrder == BotMovementOrder.Follow)
            runtime.Social.ApplyFollow();
        else
            runtime.Social.ApplyStay();
        runtime.Social.CommitLowestHealthMember(45f, 0f, 85f);
        return new BotContext(healer, runtime, runtime.Blackboard, Now, new BotConfig(), BotEngineKind.Combat);
    }

    private static FixedHealthCharacterMock FixedCharacter(uint id, Vector3 position, bool isBot, int health)
    {
        var character = new FixedHealthCharacterMock
        {
            Id = id,
            ObjId = id + 1000,
            Name = $"character{id}",
            IsBot = isBot,
            Hp = health,
            FixedMaxHp = 100
        };
        character.Transform.Local.SetPosition(position);
        return character;
    }
}

internal sealed class RecordingMover : IBotMover
{
    public List<(Vector3 Position, bool Run, float Tolerance)> Destinations { get; } = [];
    public List<(CharacterMock Bot, float Angle)> Faces { get; } = [];
    public List<Vector3> Teleports { get; } = [];
    public int Stops { get; private set; }

    public void SetDestination(AAEmu.Game.Models.Game.Char.Character bot, Vector3 position, bool run, float tolerance)
    {
        Destinations.Add((position, run, tolerance));
    }

    public void StopIfMoving(AAEmu.Game.Models.Game.Char.Character bot)
    {
        Stops++;
    }

    public void StopImmediately(AAEmu.Game.Models.Game.Char.Character bot)
    {
        Stops++;
    }

    public void Face(AAEmu.Game.Models.Game.Char.Character bot, float angle)
    {
        Faces.Add(((CharacterMock)bot, angle));
    }

    public void Teleport(AAEmu.Game.Models.Game.Char.Character bot, Vector3 position)
    {
        Teleports.Add(position);
    }

    public void Follow(AAEmu.Game.Models.Game.Char.Character bot, AAEmu.Game.Models.Game.Char.Character target, float distance)
    {
    }

    public void StopFollow(AAEmu.Game.Models.Game.Char.Character bot)
    {
    }

    public void SendRelaxedStance(AAEmu.Game.Models.Game.Char.Character bot)
    {
    }
}
