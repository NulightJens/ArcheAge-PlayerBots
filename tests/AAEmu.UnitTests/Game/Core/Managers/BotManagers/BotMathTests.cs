using System.Numerics;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Utils;
using AAEmu.UnitTests.Utils.Mocks;

namespace AAEmu.UnitTests.Game.Core.Managers.BotManagers;

public class BotMathTests
{
    [Test]
    [Arguments(0f)]
    [Arguments(90f)]
    [Arguments(180f)]
    [Arguments(270f)]
    public async Task Forward_MatchesUpstreamAddDistanceToFront(float facingDegrees)
    {
        var bot = BotTestFixture.MakeBot(2, Vector3.Zero);
        bot.Transform.Local.SetRotationDegree(0f, 0f, facingDegrees - 90f);
        var start = bot.Transform.Local.Position;
        bot.Transform.Local.AddDistanceToFront(1f);
        var expected = bot.Transform.Local.Position - start;

        var actual = BotMath.Forward(bot.Transform.World.Rotation.Z);

        await Assert.That(actual.X).IsEqualTo(expected.X).Within(1e-4f);
        await Assert.That(actual.Y).IsEqualTo(expected.Y).Within(1e-4f);
        await Assert.That(actual.Z).IsEqualTo(expected.Z).Within(1e-4f);
    }
}
