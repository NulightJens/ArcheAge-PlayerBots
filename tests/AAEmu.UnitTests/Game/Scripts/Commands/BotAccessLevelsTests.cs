using AAEmu.Game.Models.Game;
using AAEmu.Game.Scripts.Commands;
using AAEmu.UnitTests.Utils.Mocks;
using Newtonsoft.Json.Linq;

namespace AAEmu.UnitTests.Game.Scripts.Commands;

public class BotAccessLevelsTests
{
    [Test]
    public async Task AllBotCommands_PrimaryNamesAreUnique()
    {
        var commands = GetBotCommands();
        var names = commands.Select(command => command.CommandNames[0]).ToList();

        await Assert.That(names.Count).IsEqualTo(names.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Test]
    public async Task AccessLevels_ContainsPrimaryNameOfEveryBotCommand()
    {
        var accessLevels = LoadAccessLevels();
        var entries = accessLevels.Properties()
            .ToDictionary(property => property.Name, property => property.Value, StringComparer.OrdinalIgnoreCase);

        foreach (var command in GetBotCommands())
        {
            if (!entries.ContainsKey(command.CommandNames[0]))
                throw new InvalidOperationException($"Missing AccessLevels entry for '{command.CommandNames[0]}'; keys={string.Join(',', entries.Keys)}.");
        }
    }

    [Test]
    public async Task AccessLevels_HasNoEntryWithoutAnImplementation()
    {
        var commandNames = GetBotCommands()
            .SelectMany(command => command.CommandNames)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var property in LoadAccessLevels().Properties().Where(property =>
                     property.Name.Contains("bot", StringComparison.OrdinalIgnoreCase) ||
                     property.Name is "rbc" or "reloadarchetype"))
        {
            await Assert.That(commandNames.Contains(property.Name)).IsTrue();
        }
    }

    private static IReadOnlyList<ICommand> GetBotCommands()
    {
        return typeof(AddBot).Assembly
            .GetTypes()
            .Where(type => typeof(ICommand).IsAssignableFrom(type) && !type.IsAbstract)
            .Where(type => type.Name.StartsWith("Bot", StringComparison.Ordinal) ||
                           type.Name is nameof(AddBot) or nameof(RemoveBot) or nameof(MoveBot) or nameof(ExportWorldCommand))
            .Select(type => (ICommand)Activator.CreateInstance(type)!)
            .ToList();
    }

    private static JObject LoadAccessLevels()
    {
        var path = BotTestFixture.FindRepoFile(Path.Combine("AAEmu.Game", "Configurations", "AccessLevels.json"));
        return (JObject)JObject.Parse(File.ReadAllText(path))["AccessLevel"]!;
    }
}
