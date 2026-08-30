using System.Collections.Generic;
using System.IO;
using System.Linq;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models.Game.Units;
using AAEmu.UnitTests.Utils.Mocks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AAEmu.UnitTests.Game.Core.Managers.BotManagers;

[NotInParallel]
public class BotArchetypeManagerTests
{
    [Test]
    public async Task MatchArchetype_TwoDefinitionsShareAbilities_ExactMatchOnly()
    {
        var abilities = new HashSet<AbilityType> { (AbilityType)1, (AbilityType)3, (AbilityType)4 };
        var definitions = new[]
        {
            new BotArchetypeDefinition
            {
                Name = "B",
                RequiredAbilities = new List<AbilityType> { (AbilityType)1, (AbilityType)3 }
            },
            new BotArchetypeDefinition { Name = "A", RequiredAbilities = abilities.ToList() }
        };

        var name = BotArchetypeManager.MatchArchetype(definitions, abilities);

        await Assert.That(name).IsEqualTo("A");
    }

    [Test]
    public async Task MatchArchetype_TwoIdenticalSets_ReturnsOrdinalFirst()
    {
        var abilities = new HashSet<AbilityType> { (AbilityType)1, (AbilityType)3, (AbilityType)4 };
        var definitions = new[]
        {
            new BotArchetypeDefinition { Name = "Zeta", RequiredAbilities = abilities.ToList() },
            new BotArchetypeDefinition { Name = "Alpha", RequiredAbilities = abilities.ToList() }
        };

        var name = BotArchetypeManager.MatchArchetype(definitions, abilities);

        await Assert.That(name).IsEqualTo("Alpha");
    }

    [Test]
    public async Task WeaponCategoryName_UnknownId_ReturnsNull()
    {
        var category = BotArchetypeManager.WeaponCategoryName(999);

        await Assert.That(category).IsNull();
    }

    [Test]
    public async Task Golden_ArchetypeDefaults_Snapshot()
    {
        var actual = BotArchetypeManager.DefaultDefinitions().OrderBy(def => def.Name).ToList();
        var snapshotPath = BotTestFixture.FindRepoFile(Path.Combine("AAEmu.UnitTests", "TestData", "BotArchetypes.default.json"));
        var expected = JToken.Parse(File.ReadAllText(snapshotPath));
        var serialized = JToken.Parse(JsonConvert.SerializeObject(actual, Formatting.Indented));

        await Assert.That(JToken.DeepEquals(serialized, expected)).IsTrue();
    }

    [Test]
    public async Task Golden_ShippedArchetypeFile_MatchesDefaults()
    {
        var shippedPath = BotTestFixture.FindRepoFile(Path.Combine("AAEmu.Game", "Data", "BotArchetypes.json"));
        var shipped = JArray.Parse(File.ReadAllText(shippedPath));
        var defaults = JArray.FromObject(BotArchetypeManager.DefaultDefinitions());
        var orderedShipped = new JArray(shipped.OrderBy(token => (string)token["Name"]));
        var orderedDefaults = new JArray(defaults.OrderBy(token => (string)token["Name"]));

        await Assert.That(JToken.DeepEquals(orderedShipped, orderedDefaults)).IsTrue();
    }

    [Test]
    public async Task LoadDefinitions_ReplacesDictionaryInstance()
    {
        var manager = new BotArchetypeManager();
        manager.LoadDefinitions(JsonConvert.SerializeObject(BotArchetypeManager.DefaultDefinitions()));
        var before = manager.GetDefinitionsSnapshot();

        manager.LoadDefinitions(JsonConvert.SerializeObject(BotArchetypeManager.DefaultDefinitions().Take(1)));
        var after = manager.GetDefinitionsSnapshot();

        await Assert.That(ReferenceEquals(before, after)).IsFalse();
        await Assert.That(before.Keys).Count().IsEqualTo(7);
        await Assert.That(after.Keys).Count().IsEqualTo(1);
    }

    [Test]
    public async Task LoadDefinitions_NullName_SkippedWithWarning()
    {
        var manager = new BotArchetypeManager();
        var definitions = new[]
        {
            new BotArchetypeDefinition { Name = null, RequiredAbilities = [AbilityType.Fight, AbilityType.Adamant, AbilityType.Will] },
            new BotArchetypeDefinition { Name = "Valid", RequiredAbilities = [AbilityType.Fight, AbilityType.Adamant, AbilityType.Will] }
        };

        var loaded = manager.LoadDefinitions(JsonConvert.SerializeObject(definitions));

        await Assert.That(loaded).IsTrue();
        await Assert.That(manager.GetDefinitionsSnapshot().Keys).Contains("Valid");
        await Assert.That(manager.GetDefinitionsSnapshot().Count).IsEqualTo(1);
    }

    [Test]
    public async Task LoadDefinitions_TwoAbilities_Skipped()
    {
        var manager = new BotArchetypeManager();
        var definitions = new[]
        {
            new BotArchetypeDefinition { Name = "Invalid", RequiredAbilities = [AbilityType.Fight, AbilityType.Adamant] },
            new BotArchetypeDefinition { Name = "Valid", RequiredAbilities = [AbilityType.Fight, AbilityType.Adamant, AbilityType.Will] }
        };

        manager.LoadDefinitions(JsonConvert.SerializeObject(definitions));

        await Assert.That(manager.GetDefinitionsSnapshot().Keys).DoesNotContain("Invalid");
        await Assert.That(manager.GetDefinitionsSnapshot().Keys).Contains("Valid");
    }

    [Test]
    public async Task LoadDefinitions_DuplicateAbilities_Skipped()
    {
        var manager = new BotArchetypeManager();
        var definitions = new[]
        {
            new BotArchetypeDefinition
            {
                Name = "Duplicate",
                RequiredAbilities = [AbilityType.Fight, AbilityType.Fight, AbilityType.Will],
                SkillLearnOrder = []
            }
        };

        var loaded = manager.LoadDefinitions(JsonConvert.SerializeObject(definitions));

        await Assert.That(loaded).IsTrue();
        await Assert.That(manager.GetDefinitionsSnapshot().Keys).DoesNotContain("Duplicate");
    }

    [Test]
    public async Task LoadDefinitions_NullWeaponPriority_Skipped()
    {
        var manager = new BotArchetypeManager();
        var definitions = new[]
        {
            new BotArchetypeDefinition
            {
                Name = "NullPriority",
                RequiredAbilities = [AbilityType.Fight, AbilityType.Adamant, AbilityType.Will],
                WeaponPriority = null,
                SkillLearnOrder = []
            }
        };

        var loaded = manager.LoadDefinitions(JsonConvert.SerializeObject(definitions));

        await Assert.That(loaded).IsTrue();
        await Assert.That(manager.GetDefinitionsSnapshot().Keys).DoesNotContain("NullPriority");
    }

    [Test]
    public async Task LoadDefinitions_InvalidJson_ReturnsFalseAndKeepsExisting()
    {
        var manager = new BotArchetypeManager();
        manager.LoadDefinitions(JsonConvert.SerializeObject(BotArchetypeManager.DefaultDefinitions()));
        var before = manager.GetDefinitionsSnapshot();

        var loaded = manager.LoadDefinitions("{bad json");

        await Assert.That(loaded).IsFalse();
        await Assert.That(ReferenceEquals(before, manager.GetDefinitionsSnapshot())).IsTrue();
    }

    [Test]
    public async Task LoadDefinitions_DuplicateNames_LastWins()
    {
        var manager = new BotArchetypeManager();
        var definitions = new[]
        {
            new BotArchetypeDefinition { Name = "X", Weight = 1, RequiredAbilities = [AbilityType.Fight, AbilityType.Adamant, AbilityType.Will] },
            new BotArchetypeDefinition { Name = "X", Weight = 7, RequiredAbilities = [AbilityType.Fight, AbilityType.Adamant, AbilityType.Will] }
        };

        manager.LoadDefinitions(JsonConvert.SerializeObject(definitions));

        await Assert.That(manager.GetDefinitionsSnapshot()["X"].Weight).IsEqualTo(7);
    }

    [Test]
    public async Task Load_InvalidExistingFile_KeepsFileAndReturnsFalse()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "BotArchetypes.json");
        Directory.CreateDirectory(directory);
        var original = "{bad json";
        File.WriteAllText(path, original);

        try
        {
            var manager = new BotArchetypeManager();
            var loaded = manager.Load(path);

            await Assert.That(loaded).IsFalse();
            await Assert.That(File.ReadAllText(path)).IsEqualTo(original);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Test]
    public async Task DefaultDefinitions_ContainsSevenArchetypesWithUniqueNames()
    {
        var definitions = BotArchetypeManager.DefaultDefinitions();

        await Assert.That(definitions).Count().IsEqualTo(7);
        await Assert.That(definitions.Select(def => def.Name).Distinct()).Count().IsEqualTo(7);
        await Assert.That(definitions.Select(def => def.Name)).Contains("Abolisher");
        await Assert.That(definitions.Select(def => def.Name)).Contains("Darkrunner");
        await Assert.That(definitions.Select(def => def.Name)).Contains("Reaper");
        await Assert.That(definitions.Select(def => def.Name)).Contains("Templar");
        await Assert.That(definitions.Select(def => def.Name)).Contains("Primeval");
        await Assert.That(definitions.Select(def => def.Name)).Contains("Daggerspell");
        await Assert.That(definitions.Select(def => def.Name)).Contains("Cleric");
    }

    [Test]
    public async Task DefaultDefinitions_RequiredAbilitiesStartWithStartingAbility()
    {
        var definitions = BotArchetypeManager.DefaultDefinitions();

        await Assert.That(definitions.All(def => def.RequiredAbilities.Count == 3 &&
            def.RequiredAbilities[0] == def.StartingAbility)).IsTrue();
    }

    [Test]
    public async Task DefaultDefinitions_SkillLearnOrderHasNoDuplicates()
    {
        var definitions = BotArchetypeManager.DefaultDefinitions();

        await Assert.That(definitions.All(def => def.SkillLearnOrder.Distinct().Count() == def.SkillLearnOrder.Count)).IsTrue();
    }

    [Test]
    public async Task DefaultDefinitions_SerializeThenLoadDefinitions_RoundTrips()
    {
        var manager = new BotArchetypeManager();
        var definitions = BotArchetypeManager.DefaultDefinitions();

        var loaded = manager.LoadDefinitions(JsonConvert.SerializeObject(definitions));

        await Assert.That(loaded).IsTrue();
        await Assert.That(manager.GetDefinitionsSnapshot()["Darkrunner"].SkillLearnOrder)
            .IsEquivalentTo(definitions.Single(def => def.Name == "Darkrunner").SkillLearnOrder);
    }

    [Test]
    public async Task PickWeighted_SingleMatch_ReturnsIt()
    {
        var definition = new BotArchetypeDefinition { Name = "Only", Weight = 1 };

        await Assert.That(BotArchetypeManager.PickWeighted([definition], 0)).IsSameReferenceAs(definition);
    }

    [Test]
    public async Task PickWeighted_Weights1And3_Roll0_ReturnsFirst()
    {
        var definitions = new List<BotArchetypeDefinition>
        {
            new() { Name = "First", Weight = 1 },
            new() { Name = "Second", Weight = 3 }
        };

        await Assert.That(BotArchetypeManager.PickWeighted(definitions, 0).Name).IsEqualTo("First");
    }

    [Test]
    public async Task PickWeighted_Weights1And3_Roll1_ReturnsSecond()
    {
        var definitions = new List<BotArchetypeDefinition>
        {
            new() { Name = "First", Weight = 1 },
            new() { Name = "Second", Weight = 3 }
        };

        await Assert.That(BotArchetypeManager.PickWeighted(definitions, 1).Name).IsEqualTo("Second");
    }

    [Test]
    public async Task PickWeighted_Weights1And3_Roll3_ReturnsSecond()
    {
        var definitions = new List<BotArchetypeDefinition>
        {
            new() { Name = "First", Weight = 1 },
            new() { Name = "Second", Weight = 3 }
        };

        await Assert.That(BotArchetypeManager.PickWeighted(definitions, 3).Name).IsEqualTo("Second");
    }

    [Test]
    public async Task PickWeighted_RollTotalMinusOne_ReturnsLast()
    {
        var definitions = new List<BotArchetypeDefinition>
        {
            new() { Name = "First", Weight = 2 },
            new() { Name = "Second", Weight = 2 }
        };

        await Assert.That(BotArchetypeManager.PickWeighted(definitions, 3).Name).IsEqualTo("Second");
    }

    [Test]
    public async Task MatchArchetype_ExactDarkrunnerSetAnyOrder_ReturnsDarkrunner()
    {
        var name = BotArchetypeManager.MatchArchetype(
            BotArchetypeManager.DefaultDefinitions(),
            new HashSet<AbilityType> { AbilityType.Vocation, AbilityType.Will, AbilityType.Fight });

        await Assert.That(name).IsEqualTo("Darkrunner");
    }

    [Test]
    public async Task MatchArchetype_NoDefinitionMatches_ReturnsNull()
    {
        var name = BotArchetypeManager.MatchArchetype(
            BotArchetypeManager.DefaultDefinitions(),
            new HashSet<AbilityType> { AbilityType.Illusion, AbilityType.Adamant, AbilityType.Will });

        await Assert.That(name).IsNull();
    }

    [Test]
    public async Task WeaponCategoryName_KnownIds_MapAsDocumented()
    {
        await Assert.That(BotArchetypeManager.WeaponCategoryName(70)).IsEqualTo("Sword");
        await Assert.That(BotArchetypeManager.WeaponCategoryName(77)).IsEqualTo("Bow");
        await Assert.That(BotArchetypeManager.WeaponCategoryName(79)).IsEqualTo("Shield");
        await Assert.That(BotArchetypeManager.WeaponCategoryName(128)).IsEqualTo("Nodachi");
    }

    [Test]
    public async Task ScoreWeapon_PriorityIndexZeroOfFour_AddsTwenty()
    {
        await Assert.That(BotArchetypeManager.ScoreWeapon(10, 0, 0, 4)).IsEqualTo(120);
        await Assert.That(BotArchetypeManager.ScoreWeapon(10, 0, 3, 4)).IsEqualTo(105);
        await Assert.That(BotArchetypeManager.ScoreWeapon(10, 0, -1, 4)).IsEqualTo(100);
    }

    [Test]
    public async Task ScoreWeapon_PrimaryStatAddsTwicePerPoint()
    {
        await Assert.That(BotArchetypeManager.ScoreWeapon(1, 7, -1, 0)).IsEqualTo(24);
    }

    [Test]
    public async Task MergeGearCandidates_EquippedItemsComeFirstAndAreNotDuplicated()
    {
        var equipped = new Item { Id = 101, SlotType = SlotType.Equipment, Slot = 0 };
        var bag = new Item { Id = 202, SlotType = SlotType.Inventory, Slot = 1 };

        var candidates = BotArchetypeManager.MergeGearCandidates([equipped], [bag, equipped, null]);

        await Assert.That(candidates).IsEquivalentTo([equipped, bag]);
        await Assert.That(candidates[0]).IsSameReferenceAs(equipped);
        await Assert.That(candidates).Count().IsEqualTo(2);
    }

    [Test]
    public async Task NeedsEquipmentMove_ItemAlreadyInRequestedSlot_IsFalse()
    {
        var equipped = new Item { SlotType = SlotType.Equipment, Slot = 7 };
        var bag = new Item { SlotType = SlotType.Inventory, Slot = 7 };
        var otherEquipmentSlot = new Item { SlotType = SlotType.Equipment, Slot = 8 };

        await Assert.That(BotArchetypeManager.NeedsEquipmentMove(equipped, 7)).IsFalse();
        await Assert.That(BotArchetypeManager.NeedsEquipmentMove(bag, 7)).IsTrue();
        await Assert.That(BotArchetypeManager.NeedsEquipmentMove(otherEquipmentSlot, 7)).IsTrue();
        await Assert.That(BotArchetypeManager.NeedsEquipmentMove(null, 7)).IsFalse();
    }

    [Test]
    [Arguments(1, 0, -1, 10)]
    [Arguments(1, 0, 0, 30)]
    [Arguments(1, 0, 3, 15)]
    [Arguments(1, 5, -1, 20)]
    [Arguments(1, 5, 0, 40)]
    [Arguments(1, 5, 3, 25)]
    [Arguments(10, 0, -1, 100)]
    [Arguments(10, 0, 0, 120)]
    [Arguments(10, 0, 3, 105)]
    [Arguments(10, 5, -1, 110)]
    [Arguments(10, 5, 0, 130)]
    [Arguments(10, 5, 3, 115)]
    [Arguments(30, 0, -1, 300)]
    [Arguments(30, 0, 0, 320)]
    [Arguments(30, 0, 3, 305)]
    [Arguments(30, 5, -1, 310)]
    [Arguments(30, 5, 0, 330)]
    [Arguments(30, 5, 3, 315)]
    public async Task Golden_ScoreWeapon_Table(int level, int primaryStat, int priorityIndex, int expected)
    {
        await Assert.That(BotArchetypeManager.ScoreWeapon(level, primaryStat, priorityIndex, 4)).IsEqualTo(expected);
    }

    [Test]
    public async Task GetEffectiveDefinition_FinalPreferredOverPlanned()
    {
        var manager = new BotArchetypeManager();
        manager.LoadDefinitions(JsonConvert.SerializeObject(BotArchetypeManager.DefaultDefinitions()));

        var definition = manager.GetEffectiveDefinition(new BotArchetypeState
        {
            ArchetypeName = "Reaper",
            PlannedArchetype = "Templar"
        });

        await Assert.That(definition.Name).IsEqualTo("Reaper");
    }

    [Test]
    public async Task GetEffectiveDefinition_UnknownNames_ReturnsNull()
    {
        var manager = new BotArchetypeManager();

        await Assert.That(manager.GetEffectiveDefinition(new BotArchetypeState { ArchetypeName = "Nope" })).IsNull();
    }

    [Test]
    public async Task ClearPassives_RemovesByTemplateBuffId()
    {
        var manager = new BotArchetypeManager();
        var buffs = Mock.Of<IBuffs>();
        var bot = BotTestFixture.MakeBot(2, default);
        bot.Buffs = buffs.Object;
        bot.Skills = new CharacterSkills(bot);
        bot.Skills.PassiveBuffs[30] = new PassiveBuff
        {
            Id = 30,
            Template = new PassiveBuffTemplate { Id = 30, BuffId = 999 }
        };

        manager.ClearArchetypeSkills(bot);

        buffs.RemoveBuff(999).WasCalled(Times.Once);
        buffs.RemoveBuff(30).WasCalled(Times.Never);
        await Assert.That(bot.Skills.PassiveBuffs).IsEmpty();
    }

    [Test]
    public async Task DeleteArchetype_UsesInMemoryStoreWithoutDatabaseConnection()
    {
        var store = new InMemoryBotArchetypeStore();
        store.Save(2, "Darkrunner", true);
        var manager = new BotArchetypeManager(store);

        manager.DeleteArchetype(2);

        var plan = store.Get(2);
        await Assert.That(plan.archetypeName).IsNull();
        await Assert.That(plan.isFinal).IsFalse();
    }
}
