using AAEmu.Game.Scripts.Commands;
using AAEmu.Game.Utils.Scripts;
using AAEmu.UnitTests.Utils.Mocks;

namespace AAEmu.UnitTests.Game.Scripts.Commands;

public class BotHelpCommandTests
{
    [Test]
    public async Task Execute_NoTopic_ShowsHumanQuickStart()
    {
        var output = Execute();
        var messages = output.Messages.ToArray();

        await Assert.That(messages.Length).IsEqualTo(2);
        await Assert.That(messages[0]).Contains("/addbot <characterId>");
        await Assert.That(messages[1]).Contains("/bot party");
    }

    [Test]
    public async Task Execute_PartyTopic_ShowsNativeAndDirectControls()
    {
        var output = Execute("PaRtY");
        var messages = output.Messages.ToArray();

        await Assert.That(messages[0]).Contains("/botcontrol");
        await Assert.That(messages[1]).Contains("/botfollow");
    }

    [Test]
    public async Task Execute_UnknownTopic_ShowsCommandHelp()
    {
        var output = Execute("unknown");

        await Assert.That(output.Messages).HasSingleItem();
        await Assert.That(output.Messages.Single()).Contains("Help for |cFFFFFFFF/bot|r");
    }

    private static CharacterMessageOutput Execute(params string[] args)
    {
        var character = new CharacterMock();
        var output = new CharacterMessageOutput(character);
        var command = new BotHelpCommand();
        command.Execute(character, args, output);
        return output;
    }
}
