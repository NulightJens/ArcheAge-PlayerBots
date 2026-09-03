using System.Drawing;
using AAEmu.Game.Bots.Population.Identity;
using AAEmu.Game.Models.Game.Chat;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Scripts.Commands;
using AAEmu.Game.Utils.Scripts;
using AAEmu.UnitTests.Utils.Mocks;

namespace AAEmu.UnitTests.Game.Scripts.Commands;

public class CreateBotCommandTests
{
    [Test]
    public async Task Execute_RaceSpawn_ParsesNamedArgumentsAndEmitsStructuredSuccess()
    {
        BotIdentityCreationRequest captured = null;
        var command = new BotCreateCommand(request =>
        {
            captured = request;
            var bot = new CharacterMock
            {
                Id = 12001,
                Name = request.Name,
                Level = (byte)request.Level,
                Race = request.Race,
                Gender = request.Gender
            };
            BotTestFixture.SetPrivateField(bot.Transform, "_zoneId", 10u);
            return new BotIdentityCreationResult(
                BotIdentityCreationStatus.CreatedAndAdmitted,
                "created_and_admitted",
                bot.Id,
                bot);
        });
        var messages = CaptureMessages(out var output);

        command.Execute(new CharacterMock(), ["NuianBot", "nuian", "female", "Abolisher", "55"], output);

        await Assert.That(captured).IsNotNull();
        await Assert.That(captured.Race).IsEqualTo(Race.Nuian);
        await Assert.That(captured.Gender).IsEqualTo(Gender.Female);
        await Assert.That(captured.Level).IsEqualTo(55);
        await Assert.That(captured.Placement.Mode).IsEqualTo(BotIdentityPlacementMode.RaceSpawn);
        await Assert.That(messages.Any(message => message.Contains(
            "BOT_IDENTITY status=success code=created_and_admitted id=12001",
            StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task Execute_Here_CapturesCallerWorldInstanceZoneAndFiniteTransform()
    {
        BotIdentityCreationRequest captured = null;
        var command = new BotCreateCommand(request =>
        {
            captured = request;
            return new BotIdentityCreationResult(BotIdentityCreationStatus.AdmissionFailed, "test_stop");
        });
        var caller = BotTestFixture.MakeBot(42, new System.Numerics.Vector3(101.5f, 202.5f, 303.5f));
        var world = BotTestFixture.MakeWorld(43);
        BotTestFixture.SetPrivateField(caller, "_parentWorld", world);
        BotTestFixture.SetPrivateField(caller.Transform, "_instanceId", world.Id);
        BotTestFixture.SetPrivateField(caller.Transform, "_zoneId", 601u);
        caller.Transform.Local.SetRotation(0.1f, 0.2f, 0.3f);

        command.Execute(caller, ["HereBot", "Nuian", "Male", "Abolisher", "12", "here"],
            CaptureOutput());

        await Assert.That(captured).IsNotNull();
        await Assert.That(captured.Placement.Mode).IsEqualTo(BotIdentityPlacementMode.Here);
        await Assert.That(captured.Placement.WorldId).IsEqualTo(world.Template.Id);
        await Assert.That(captured.Placement.InstanceId).IsEqualTo(world.Id);
        await Assert.That(captured.Placement.ZoneId).IsEqualTo(601u);
        await Assert.That(captured.Placement.X).IsEqualTo(101.5f);
        await Assert.That(captured.Placement.Y).IsEqualTo(202.5f);
        await Assert.That(captured.Placement.Z).IsEqualTo(303.5f);
        await Assert.That(captured.Placement.Roll).IsEqualTo(0.1f);
        await Assert.That(captured.Placement.Pitch).IsEqualTo(0.2f);
        await Assert.That(captured.Placement.Yaw).IsEqualTo(0.3f);
    }

    [Test]
    public async Task Execute_HereWithStaleTransformInstance_UsesAuthoritativeParentWorld()
    {
        BotIdentityCreationRequest captured = null;
        var command = new BotCreateCommand(request =>
        {
            captured = request;
            return new BotIdentityCreationResult(BotIdentityCreationStatus.AdmissionFailed, "test_stop");
        });
        var caller = BotTestFixture.MakeBot(42, new System.Numerics.Vector3(101.5f, 202.5f, 303.5f));
        var world = BotTestFixture.MakeWorld(43);
        BotTestFixture.SetPrivateField(caller, "_parentWorld", world);
        BotTestFixture.SetPrivateField(caller.Transform, "_instanceId", world.Id + 1);
        BotTestFixture.SetPrivateField(caller.Transform, "_zoneId", 601u);

        command.Execute(caller, ["HereBot", "Nuian", "Male", "Abolisher", "1", "here"],
            CaptureOutput());

        await Assert.That(captured).IsNotNull();
        await Assert.That(captured.Placement.InstanceId).IsEqualTo(world.Id);
        await Assert.That(captured.Placement.WorldId).IsEqualTo(world.Template.Id);
        await Assert.That(captured.Placement.ZoneId).IsEqualTo(601u);
    }

    [Test]
    public async Task Execute_HereWithoutWorld_FailsBeforeFactory()
    {
        var createCalls = 0;
        var command = new BotCreateCommand(_ =>
        {
            createCalls++;
            return null;
        });
        var messages = CaptureMessages(out var output);

        command.Execute(new CharacterMock(), ["NoWorld", "Nuian", "Male", "Abolisher", "1", "here"], output);

        await Assert.That(createCalls).IsEqualTo(0);
        await Assert.That(messages.Any(message => message.Contains(
            "BOT_IDENTITY status=failure code=invalid_placement reason=use_here_or_race-spawn",
            StringComparison.Ordinal))).IsTrue();
    }

    [Test]
    public async Task Execute_NumericRaceOrMalformedLevel_ShowsHelpWithoutFactoryCall()
    {
        var createCalls = 0;
        var command = new BotCreateCommand(_ =>
        {
            createCalls++;
            return null;
        });

        command.Execute(new CharacterMock(), ["Bot", "1", "Male", "Abolisher", "1"], CaptureOutput());
        command.Execute(new CharacterMock(), ["Bot", "Nuian", "Male", "Abolisher", "+1"], CaptureOutput());

        await Assert.That(createCalls).IsEqualTo(0);
    }

    [Test]
    public async Task Execute_DuplicateName_EmitsStructuredFailure()
    {
        var command = new BotCreateCommand(_ => new BotIdentityCreationResult(
            BotIdentityCreationStatus.DuplicateName,
            "name_unavailable"));
        var messages = CaptureMessages(out var output);

        command.Execute(new CharacterMock(), ["TakenName", "Nuian", "Female", "Abolisher", "1"], output);

        await Assert.That(messages.Any(message => message.Contains(
            "BOT_IDENTITY status=failure code=duplicate_name reason=name_unavailable",
            StringComparison.Ordinal))).IsTrue();
    }

    private static List<string> CaptureMessages(out IMessageOutput output)
    {
        var messages = new List<string>();
        var mock = Mock.Of<IMessageOutput>();
        mock.SendMessage(Any<ChatType>(), Any<string>(), Any<Color?>())
            .Callback((ChatType _, string message, Color? _) => messages.Add(message));
        output = mock.Object;
        return messages;
    }

    private static IMessageOutput CaptureOutput() => Mock.Of<IMessageOutput>().Object;
}
