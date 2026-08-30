using AAEmu.Game.Scripts.Commands;

namespace AAEmu.UnitTests.Game.Scripts.Commands;

public class BotCommandArgsTests
{
    [Test]
    public async Task TryBotId_Empty_FalseWithHelpReason()
    {
        var result = BotCommandArgs.TryBotId([], 0, out _, out var error);

        await Assert.That(result).IsFalse();
        await Assert.That(error).IsEqualTo("help");
    }

    [Test]
    public async Task TryBotId_NonNumeric_False()
    {
        await Assert.That(BotCommandArgs.TryBotId(["abc"], 0, out _, out _)).IsFalse();
    }

    [Test]
    public async Task TryBotId_Negative_False()
    {
        await Assert.That(BotCommandArgs.TryBotId(["-1"], 0, out _, out _)).IsFalse();
    }

    [Test]
    public async Task TryBotId_Zero_False()
    {
        await Assert.That(BotCommandArgs.TryBotId(["0"], 0, out _, out _)).IsFalse();
    }

    [Test]
    public async Task TryBotId_Valid_ReturnsUint()
    {
        var result = BotCommandArgs.TryBotId(["42"], 0, out var id, out _);

        await Assert.That(result).IsTrue();
        await Assert.That(id).IsEqualTo(42u);
    }

    [Test]
    public async Task TryCoord_InvariantDecimalPoint_Parses()
    {
        var result = BotCommandArgs.TryCoord("12.5", out var coordinate);

        await Assert.That(result).IsTrue();
        await Assert.That(coordinate).IsEqualTo(12.5f);
    }

    [Test]
    public async Task TryCoord_GermanCommaDecimal_Fails()
    {
        var result = BotCommandArgs.TryCoord("12,5", out _);

        await Assert.That(result).IsFalse();
    }

    [Test]
    public async Task TryCoord_NaN_Fails()
    {
        await Assert.That(BotCommandArgs.TryCoord("NaN", out _)).IsFalse();
    }

    [Test]
    public async Task TryCoord_Infinity_Fails()
    {
        await Assert.That(BotCommandArgs.TryCoord("Infinity", out _)).IsFalse();
    }
}
