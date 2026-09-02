using AAEmu.Game.Bots.Questing;

namespace AAEmu.UnitTests.Game.Scripts.Commands;

public sealed class BotQuestAutonomyTests
{
    [Test]
    public async Task DeterministicRewardSelectionUsesOnlyTheLowestOfferedIndex()
    {
        await Assert.That(BotQuestAuthority.SelectRewardIndex([7, 3, 5])).IsEqualTo(3);
        await Assert.That(BotQuestAuthority.SelectRewardIndex([])).IsEqualTo(0);
        await Assert.That(BotQuestAuthority.SelectRewardIndex([-1, -2])).IsEqualTo(-1);
    }

    [Test]
    public async Task CompatibilityAdapterCarriesAllFailClosedAuthorityGuards()
    {
        var moduleRoot = FindModuleRoot();
        var patch = File.ReadAllText(Path.Combine(
            moduleRoot,
            "compatibility",
            "aaemu-1.2-r208022-doodad-quest-adapter.patch"));

        await Assert.That(patch).Contains("GetPlayerBotDoodadsNear");
        await Assert.That(patch).Contains("TryGetPlayerBotCurrentQuest");
        await Assert.That(patch).Contains("TryGetPlayerBotObjectiveCount");
        await Assert.That(patch).Contains("TryReportPlayerBotQuest");
        await Assert.That(patch).Contains("quest.Status != QuestStatus.Ready");
        await Assert.That(patch).Contains("npcObjId > 0 && doodadObjId > 0");
        await Assert.That(patch).Contains("selectiveIndices.Contains(selected)");
        await Assert.That(patch).Contains("currentQuestId == questContextId");
        await Assert.That(patch).Contains("Take(maximumResults)");
        await Assert.That(patch).Contains("IsPlayerBotReportObjectInRange");
        await Assert.That(patch).Contains("maximumInteractionRange = 10f");
    }

    [Test]
    public async Task AutonomousSourceContainsNoDesireenFixtureObjectOrTemplateLiteral()
    {
        var moduleRoot = FindModuleRoot();
        var relevantFiles = Directory.GetFiles(
                Path.Combine(moduleRoot, "src", "AAEmu.Game", "Bots", "Questing"),
                "*.cs",
                SearchOption.AllDirectories)
            .Append(Path.Combine(
                moduleRoot,
                "compatibility",
                "aaemu-1.2-r208022-doodad-quest-adapter.patch"));
        var source = string.Join('\n', relevantFiles.Select(File.ReadAllText));

        await Assert.That(source).DoesNotContain("20451");
        await Assert.That(source).DoesNotContain("1744");
    }

    [Test]
    public async Task DebugCommandPublishesStableIntakeAndLifecycleState()
    {
        var moduleRoot = FindModuleRoot();
        var source = File.ReadAllText(Path.Combine(
            moduleRoot,
            "src",
            "AAEmu.Game",
            "Scripts",
            "Commands",
            "BotDebugCommand.cs"));

        await Assert.That(source).Contains("Quest intake: state=");
        await Assert.That(source).Contains("giver=");
        await Assert.That(source).Contains("Quest lifecycle: state=");
        await Assert.That(source).Contains("progress=");
        await Assert.That(source).Contains("report_attempts=");
    }

    private static string FindModuleRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "playerbots.module.json")))
                return directory.FullName;

            var nested = Path.Combine(directory.FullName, "modules", "archeage-playerbots");
            if (File.Exists(Path.Combine(nested, "playerbots.module.json")))
                return nested;

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate the archeage-playerbots module root.");
    }
}
