using System.Numerics;
using AAEmu.Game.Bots.Social;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Scripts.Commands;
using AAEmu.UnitTests.Utils.Mocks;

namespace AAEmu.UnitTests.Bots.Social;

public class BotFormationTests
{
    [Test]
    public async Task PositionFor_PartyFollow_UsesStableLooseGroupInsteadOfMirroredRows()
    {
        var leader = MakeLeader(new Vector3(10f, 20f, 5f));
        var firstSample = Enumerable.Range(0, 4)
            .Select(slot => BotFormation.PositionFor(leader, slot, 2f))
            .ToArray();
        var secondSample = Enumerable.Range(0, 4)
            .Select(slot => BotFormation.PositionFor(leader, slot, 2f))
            .ToArray();

        await Assert.That(secondSample).IsEquivalentTo(firstSample);
        await Assert.That(firstSample.Distinct().Count()).IsEqualTo(4);
        await Assert.That(firstSample.All(position => position.X < leader.Transform.World.Position.X)).IsTrue();
        await Assert.That(MathF.Abs(firstSample[0].X - firstSample[1].X)).IsGreaterThan(0.05f);
        await Assert.That(MathF.Abs(MathF.Abs(firstSample[0].Y - 20f) - MathF.Abs(firstSample[1].Y - 20f)))
            .IsGreaterThan(0.05f);
        await Assert.That(MathF.Abs(firstSample[2].X - firstSample[3].X)).IsGreaterThan(0.05f);
    }

    [Test]
    public async Task SpreadPositionFor_OneHundredBots_ProducesCompactDistinctGrid()
    {
        var leader = MakeLeader(Vector3.Zero);
        const int memberCount = 100;
        const int columns = 10;
        var positions = Enumerable.Range(0, memberCount)
            .Select(slot => BotFormation.SpreadPositionFor(leader, slot, memberCount, 3f, columns, 2.5f))
            .ToArray();

        await Assert.That(positions.Distinct().Count()).IsEqualTo(memberCount);
        await Assert.That(positions.Min(position => position.X)).IsEqualTo(-25.5f);
        await Assert.That(positions.Max(position => position.X)).IsEqualTo(-3f);
        await Assert.That(positions.Min(position => position.Y)).IsEqualTo(-11.25f);
        await Assert.That(positions.Max(position => position.Y)).IsEqualTo(11.25f);
    }

    [Test]
    public async Task SpreadPositionFor_PartialLastRow_CentersItsMembers()
    {
        var leader = MakeLeader(Vector3.Zero);

        var first = BotFormation.SpreadPositionFor(leader, 10, 12, 3f, 10, 2.5f);
        var second = BotFormation.SpreadPositionFor(leader, 11, 12, 3f, 10, 2.5f);

        await Assert.That(first).IsEqualTo(new Vector3(-5.5f, -1.25f, 0f));
        await Assert.That(second).IsEqualTo(new Vector3(-5.5f, 1.25f, 0f));
    }

    [Test]
    public async Task PositionFor_SpreadState_RotatesGridWithLeader()
    {
        var leader = MakeLeader(new Vector3(10f, 20f, 5f));
        leader.Transform.Local.SetRotation(0f, 0f, 90f);
        var movement = new BotMovementState
        {
            FormationSlot = 0,
            FormationColumns = 1,
            FormationMemberCount = 1,
            FormationSpacing = 2.5f,
            FollowDistance = 3f
        };

        var position = BotFormation.PositionFor(leader, movement);

        await Assert.That(MathF.Abs(position.X - 10f)).IsLessThan(0.001f);
        await Assert.That(MathF.Abs(position.Y - 17f)).IsLessThan(0.001f);
        await Assert.That(position.Z).IsEqualTo(5f);
    }

    [Test]
    [Arguments(0, 0)]
    [Arguments(0, 1)]
    [Arguments(0, 100)]
    [Arguments(7, 5)]
    [Arguments(50, 10)]
    public async Task ResolveColumns_UsesCompactAutomaticSquareOrClampsRequestedWidth(
        int requestedColumns,
        int botCount)
    {
        var columns = BotFollowCommand.ResolveColumns(requestedColumns, botCount);
        var expected = botCount switch
        {
            0 => 0,
            1 => 1,
            100 when requestedColumns == 0 => 10,
            5 => 5,
            _ => 10
        };

        await Assert.That(columns).IsEqualTo(expected);
    }

    private static CharacterMock MakeLeader(Vector3 position)
    {
        var leader = BotTestFixture.MakeBot(1, position);
        leader.Name = "leader";
        return leader;
    }
}
