using AAEmu.Game.Bots.Population.Identity;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.UnitTests.Utils.Mocks;
using Newtonsoft.Json;

namespace AAEmu.UnitTests.Game.Core.Managers.BotManagers;

public class BotIdentityFactoryTests
{
    [Test]
    public async Task CompatibilityPatch_UsesNativeInventorySkillsEmptyQuestsAndPersistenceWithoutClientPackets()
    {
        var patch = File.ReadAllText(FindModuleFile(
            "compatibility",
            "aaemu-1.2-r208022-bot-identity-factory.patch"));

        await Assert.That(patch).Contains("character.Inventory = new Inventory(character)");
        await Assert.That(patch).Contains("character.Skills = new CharacterSkills(character)");
        await Assert.That(patch).Contains("skillManager.GetStartAbilitySkills(character.Ability1)");
        await Assert.That(patch).Contains("character.Quests = new CharacterQuests(character)");
        await Assert.That(patch).Contains("ExperienceManager.Instance.GetExpForLevel(request.Level)");
        await Assert.That(patch).Contains("CreateServerOwnedBotModel(template, out var hairItemId)");
        await Assert.That(patch).Contains("lower(CAST(npcOnly AS TEXT)) IN ('0', 'f', 'false')");
        await Assert.That(patch).Contains("new UnitCustomModelParams(UnitCustomModelType.Face)");
        await Assert.That(patch).Contains("ModelId = template.ModelId");
        await Assert.That(patch).Contains("character.SaveDirectlyToDatabase()");
        await Assert.That(patch).DoesNotContain("+            connection.SendPacket");
        await Assert.That(patch).DoesNotContain("+        new GameConnection");
    }

    [Test]
    public async Task ArchetypeCreationPlan_LevelOneIsPlannedAndUnlocksAtConfiguredLevels()
    {
        var manager = new BotArchetypeManager();
        manager.LoadDefinitions(JsonConvert.SerializeObject(BotArchetypeManager.DefaultDefinitions()));

        var levelOneResolved = manager.TryResolveCreationPlan("abolisher", 1, out var levelOne);
        var levelFiveResolved = manager.TryResolveCreationPlan("Abolisher", 5, out var levelFive);
        var levelTenResolved = manager.TryResolveCreationPlan("Abolisher", 10, out var levelTen);

        await Assert.That(levelOneResolved).IsTrue();
        await Assert.That(levelOne.Name).IsEqualTo("Abolisher");
        await Assert.That(levelOne.Ability1).IsEqualTo(AbilityType.Fight);
        await Assert.That(levelOne.Ability2).IsEqualTo(AbilityType.None);
        await Assert.That(levelOne.Ability3).IsEqualTo(AbilityType.None);
        await Assert.That(levelOne.IsFinal).IsFalse();
        await Assert.That(levelFiveResolved).IsTrue();
        await Assert.That(levelFive.Ability2).IsEqualTo(AbilityType.Adamant);
        await Assert.That(levelFive.IsFinal).IsFalse();
        await Assert.That(levelTenResolved).IsTrue();
        await Assert.That(levelTen.Ability3).IsEqualTo(AbilityType.Will);
        await Assert.That(levelTen.IsFinal).IsTrue();
    }

    [Test]
    public async Task CreateAndAdmit_LevelOneNuianAtRaceSpawn_UsesServerAccountAndStartingAbilityOnly()
    {
        var fixture = new FactoryFixture();
        fixture.Archetypes.Plan = new BotArchetypeCreationPlan(
            "Abolisher", AbilityType.Fight, AbilityType.None, AbilityType.None, false);

        var result = fixture.Factory.CreateAndAdmit(Request(level: 1));

        await Assert.That(result.Status).IsEqualTo(BotIdentityCreationStatus.CreatedAndAdmitted);
        await Assert.That(result.CharacterId).IsEqualTo(11001u);
        await Assert.That(fixture.Authority.LastRequest.ServerOwnedAccountId).IsEqualTo(700u);
        await Assert.That(fixture.Authority.LastRequest.Race).IsEqualTo(Race.Nuian);
        await Assert.That(fixture.Authority.LastRequest.Level).IsEqualTo((byte)1);
        await Assert.That(fixture.Authority.LastRequest.Ability1).IsEqualTo(AbilityType.Fight);
        await Assert.That(fixture.Authority.LastRequest.Ability2).IsEqualTo(AbilityType.None);
        await Assert.That(fixture.Authority.LastRequest.Ability3).IsEqualTo(AbilityType.None);
        await Assert.That(fixture.Authority.LastRequest.Placement.Mode)
            .IsEqualTo(BotIdentityPlacementMode.RaceSpawn);
        await Assert.That(fixture.Roster.Entries.Single().Identity.CharacterId).IsEqualTo(11001u);
        await Assert.That(fixture.Archetypes.Registered.Single().Plan.IsFinal).IsFalse();
        await Assert.That(fixture.AdmittedIds).IsEquivalentTo([11001u]);
        await Assert.That(fixture.Authority.CompletedIds).IsEquivalentTo([11001u]);
    }

    [Test]
    public async Task CreateAndAdmit_ServerCapAtHere_ForwardsFinitePlacementAndFinalArchetype()
    {
        var fixture = new FactoryFixture();
        fixture.Archetypes.Plan = new BotArchetypeCreationPlan(
            "Abolisher", AbilityType.Fight, AbilityType.Adamant, AbilityType.Will, true);
        var placement = new BotIdentityPlacement(
            BotIdentityPlacementMode.Here, 9, 43, 601, 1.5f, 2.5f, 3.5f, 0.1f, 0.2f, 0.3f);

        var result = fixture.Factory.CreateAndAdmit(Request(fixture.Authority.MaxPlayerLevel, placement));

        await Assert.That(result.Success).IsTrue();
        await Assert.That(fixture.Authority.LastRequest.Level).IsEqualTo(fixture.Authority.MaxPlayerLevel);
        await Assert.That(fixture.Authority.LastRequest.Placement).IsEqualTo(placement);
        await Assert.That(fixture.Authority.LastRequest.Ability2).IsEqualTo(AbilityType.Adamant);
        await Assert.That(fixture.Authority.LastRequest.Ability3).IsEqualTo(AbilityType.Will);
        await Assert.That(fixture.Archetypes.Registered.Single().Plan.IsFinal).IsTrue();
    }

    [Test]
    public async Task CreateAndAdmit_InvalidInputsFailBeforeHostAllocation()
    {
        var fixture = new FactoryFixture();
        var invalidHere = new BotIdentityPlacement(
            BotIdentityPlacementMode.Here, 9, 43, 601, float.NaN, 2, 3, 0, 0, 0);

        var results = new[]
        {
            fixture.Factory.CreateAndAdmit(Request(level: 0)),
            fixture.Factory.CreateAndAdmit(Request(level: fixture.Authority.MaxPlayerLevel + 1)),
            fixture.Factory.CreateAndAdmit(Request(level: 1) with { Name = " " }),
            fixture.Factory.CreateAndAdmit(Request(level: 1) with { Race = Race.None }),
            fixture.Factory.CreateAndAdmit(Request(level: 1) with { Gender = (Gender)0 }),
            fixture.Factory.CreateAndAdmit(Request(level: 1, placement: invalidHere))
        };

        await Assert.That(results.Select(result => result.Status)).IsEquivalentTo([
            BotIdentityCreationStatus.InvalidLevel,
            BotIdentityCreationStatus.InvalidLevel,
            BotIdentityCreationStatus.InvalidName,
            BotIdentityCreationStatus.InvalidRace,
            BotIdentityCreationStatus.InvalidGender,
            BotIdentityCreationStatus.InvalidPlacement
        ]);
        await Assert.That(fixture.Authority.CreateCalls).IsEqualTo(0);
    }

    [Test]
    public async Task CreateAndAdmit_UnknownArchetypeFailsBeforeHostAllocation()
    {
        var fixture = new FactoryFixture();
        fixture.Archetypes.CanResolve = false;

        var result = fixture.Factory.CreateAndAdmit(Request(level: 1));

        await Assert.That(result.Status).IsEqualTo(BotIdentityCreationStatus.InvalidArchetype);
        await Assert.That(fixture.Authority.CreateCalls).IsEqualTo(0);
    }

    [Test]
    public async Task CreateAndAdmit_UnconfiguredAccountFailsClosed()
    {
        var fixture = new FactoryFixture(options: new BotIdentityFactoryOptions(0, "roster.json"));

        var result = fixture.Factory.CreateAndAdmit(Request(level: 1));

        await Assert.That(result.Status).IsEqualTo(BotIdentityCreationStatus.ConfigurationUnavailable);
        await Assert.That(fixture.Authority.CreateCalls).IsEqualTo(0);
    }

    [Test]
    public async Task CreateAndAdmit_HostFailuresRemainStructuredAndDoNotRegisterPolicy()
    {
        var mappings = new[]
        {
            (BotIdentityAuthorityStatus.AccountUnavailable, BotIdentityCreationStatus.AccountUnavailable),
            (BotIdentityAuthorityStatus.AccountOnline, BotIdentityCreationStatus.AccountOnline),
            (BotIdentityAuthorityStatus.InvalidName, BotIdentityCreationStatus.InvalidName),
            (BotIdentityAuthorityStatus.NameUnavailable, BotIdentityCreationStatus.DuplicateName),
            (BotIdentityAuthorityStatus.TemplateUnavailable, BotIdentityCreationStatus.TemplateUnavailable),
            (BotIdentityAuthorityStatus.IdUnavailable, BotIdentityCreationStatus.IdUnavailable),
            (BotIdentityAuthorityStatus.PlacementUnavailable, BotIdentityCreationStatus.InvalidPlacement),
            (BotIdentityAuthorityStatus.PersistenceFailed, BotIdentityCreationStatus.PersistenceFailed)
        };

        foreach (var (authorityStatus, expectedStatus) in mappings)
        {
            var fixture = new FactoryFixture();
            fixture.Authority.Result = BotIdentityAuthorityResult.Failure(authorityStatus, "host_reason");

            var result = fixture.Factory.CreateAndAdmit(Request(level: 1));

            await Assert.That(result.Status).IsEqualTo(expectedStatus);
            await Assert.That(result.Reason).IsEqualTo("host_reason");
            await Assert.That(fixture.Archetypes.Registered).IsEmpty();
            await Assert.That(fixture.Roster.Entries).IsEmpty();
            await Assert.That(fixture.AdmittedIds).IsEmpty();
        }
    }

    [Test]
    public async Task CreateAndAdmit_ArchetypeRegistrationFailure_RollsBackHostIdentity()
    {
        var fixture = new FactoryFixture();
        fixture.Archetypes.ThrowOnRegister = true;

        var result = fixture.Factory.CreateAndAdmit(Request(level: 1));

        await Assert.That(result.Status).IsEqualTo(BotIdentityCreationStatus.ArchetypeRegistrationFailed);
        await Assert.That(fixture.Archetypes.RolledBack).IsEquivalentTo([11001u]);
        await Assert.That(fixture.Authority.RollbackIds).IsEquivalentTo([11001u]);
        await Assert.That(fixture.Roster.Entries).IsEmpty();
    }

    [Test]
    public async Task CreateAndAdmit_RosterRegistrationFailure_RollsBackAllEarlierStages()
    {
        var fixture = new FactoryFixture();
        fixture.Roster.ThrowOnCreate = true;

        var result = fixture.Factory.CreateAndAdmit(Request(level: 1));

        await Assert.That(result.Status).IsEqualTo(BotIdentityCreationStatus.RosterRegistrationFailed);
        await Assert.That(fixture.Roster.RollbackIds).IsEquivalentTo([11001u]);
        await Assert.That(fixture.Archetypes.RolledBack).IsEquivalentTo([11001u]);
        await Assert.That(fixture.Authority.RollbackIds).IsEquivalentTo([11001u]);
    }

    [Test]
    public async Task CreateAndAdmit_AdmissionFailure_RemovesRosterAndRollsBackIdentity()
    {
        var fixture = new FactoryFixture { AdmissionResult = SpawnResult.LoadFailed };

        var result = fixture.Factory.CreateAndAdmit(Request(level: 1));

        await Assert.That(result.Status).IsEqualTo(BotIdentityCreationStatus.AdmissionFailed);
        await Assert.That(fixture.Roster.Entries).IsEmpty();
        await Assert.That(fixture.Archetypes.RolledBack).IsEquivalentTo([11001u]);
        await Assert.That(fixture.Authority.RollbackIds).IsEquivalentTo([11001u]);
    }

    [Test]
    public async Task CreateAndAdmit_RollbackFailureOverridesOriginalFailure()
    {
        var fixture = new FactoryFixture { AdmissionResult = SpawnResult.LoadFailed };
        fixture.Authority.RollbackResult = false;

        var result = fixture.Factory.CreateAndAdmit(Request(level: 1));

        await Assert.That(result.Status).IsEqualTo(BotIdentityCreationStatus.RollbackFailed);
        await Assert.That(result.CharacterId).IsEqualTo(11001u);
        await Assert.That(result.Reason).Contains("rollback_failed");
    }

    [Test]
    public async Task BotManager_CreateBotUsesInjectedFactoryWithoutInvokingPersistedCharacterLoader()
    {
        var identityFactory = new RecordingIdentityFactory();
        var loaderCalls = 0;
        var manager = new BotManager(
            _ =>
            {
                loaderCalls++;
                return null;
            },
            botIdentityFactory: identityFactory);

        var result = manager.CreateBot(Request(level: 1));

        await Assert.That(result.Status).IsEqualTo(BotIdentityCreationStatus.CreatedAndAdmitted);
        await Assert.That(identityFactory.Requests).Count().IsEqualTo(1);
        await Assert.That(loaderCalls).IsEqualTo(0);
    }

    private static BotIdentityCreationRequest Request(
        int level,
        BotIdentityPlacement? placement = null)
    {
        return new BotIdentityCreationRequest(
            "NuianBot",
            Race.Nuian,
            Gender.Female,
            "Abolisher",
            level,
            placement ?? BotIdentityPlacement.RaceSpawn);
    }

    private static string FindModuleFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var direct = Path.Combine(new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(direct))
                return direct;

            var module = Path.Combine(
                new[] { directory.FullName, "modules", "archeage-playerbots" }
                    .Concat(segments)
                    .ToArray());
            if (File.Exists(module))
                return module;
            directory = directory.Parent;
        }

        throw new FileNotFoundException(string.Join(Path.DirectorySeparatorChar, segments));
    }

    private sealed class FactoryFixture
    {
        public FactoryFixture(BotIdentityFactoryOptions options = null)
        {
            Factory = new BotIdentityFactory(
                options ?? new BotIdentityFactoryOptions(700, "roster.json"),
                Authority,
                Archetypes,
                Roster,
                Admit);
        }

        public FakeAuthority Authority { get; } = new();
        public FakeArchetypes Archetypes { get; } = new();
        public FakeRoster Roster { get; } = new();
        public List<uint> AdmittedIds { get; } = [];
        public SpawnResult AdmissionResult { get; set; } = SpawnResult.Ok;
        public BotIdentityFactory Factory { get; }

        private SpawnResult Admit(uint characterId, out Character character)
        {
            AdmittedIds.Add(characterId);
            character = AdmissionResult == SpawnResult.Ok ? Authority.CreatedCharacter : null;
            return AdmissionResult;
        }
    }

    private sealed class FakeAuthority : IBotIdentityAuthority
    {
        public Character CreatedCharacter { get; } = new CharacterMock
        {
            Id = 11001,
            AccountId = 700,
            Name = "NuianBot",
            Race = Race.Nuian,
            Gender = Gender.Female,
            Level = 1
        };

        public byte MaxPlayerLevel { get; set; } = 55;
        public int CreateCalls { get; private set; }
        public BotIdentityAuthorityRequest LastRequest { get; private set; }
        public BotIdentityAuthorityResult Result { get; set; }
        public bool RollbackResult { get; set; } = true;
        public List<uint> RollbackIds { get; } = [];
        public List<uint> CompletedIds { get; } = [];

        public bool CharacterExists(uint characterId) => characterId == CreatedCharacter.Id;

        public BotIdentityAuthorityResult CreateServerOwnedBot(BotIdentityAuthorityRequest request)
        {
            CreateCalls++;
            LastRequest = request;
            CreatedCharacter.Level = request.Level;
            CreatedCharacter.Ability1 = request.Ability1;
            CreatedCharacter.Ability2 = request.Ability2;
            CreatedCharacter.Ability3 = request.Ability3;
            return Result ?? BotIdentityAuthorityResult.Created(CreatedCharacter);
        }

        public bool RollbackServerOwnedBot(uint serverOwnedAccountId, uint characterId)
        {
            RollbackIds.Add(characterId);
            return RollbackResult;
        }

        public void CompleteServerOwnedBotCreation(uint serverOwnedAccountId, uint characterId) =>
            CompletedIds.Add(characterId);
    }

    private sealed class FakeArchetypes : IBotArchetypeCreationPlanStore
    {
        public bool CanResolve { get; set; } = true;
        public bool ThrowOnRegister { get; set; }
        public BotArchetypeCreationPlan Plan { get; set; } = new(
            "Abolisher", AbilityType.Fight, AbilityType.None, AbilityType.None, false);
        public List<(uint CharacterId, BotArchetypeCreationPlan Plan)> Registered { get; } = [];
        public List<uint> RolledBack { get; } = [];

        public bool TryResolveCreationPlan(string archetypeName, byte level, out BotArchetypeCreationPlan plan)
        {
            plan = CanResolve ? Plan : null;
            return CanResolve;
        }

        public void RegisterCreationPlan(uint characterId, BotArchetypeCreationPlan plan)
        {
            if (ThrowOnRegister)
                throw new InvalidOperationException("archetype unavailable");
            Registered.Add((characterId, plan));
        }

        public void RollbackCreationPlan(uint characterId) => RolledBack.Add(characterId);
    }

    private sealed class FakeRoster : IBotRosterStore
    {
        public bool ThrowOnCreate { get; set; }
        public List<BotRosterEntry> Entries { get; } = [];
        public List<uint> RollbackIds { get; } = [];

        public BotRosterSnapshot Read() => new(Entries);

        public BotRosterEntry Create(BotRosterEntry entry)
        {
            if (ThrowOnCreate)
                throw new IOException("roster unavailable");
            Entries.Add(entry);
            return entry;
        }

        public BotRosterEntry Update(BotRosterEntry entry) => throw new NotSupportedException();

        public bool RemoveForCreationRollback(BotIdentity identity)
        {
            RollbackIds.Add(identity.CharacterId);
            Entries.RemoveAll(entry => entry.Identity == identity);
            return true;
        }
    }

    private sealed class RecordingIdentityFactory : IBotIdentityFactory
    {
        public List<BotIdentityCreationRequest> Requests { get; } = [];

        public BotIdentityCreationResult CreateAndAdmit(BotIdentityCreationRequest request)
        {
            Requests.Add(request);
            var character = new CharacterMock { Id = 20001, Name = request.Name };
            return new BotIdentityCreationResult(
                BotIdentityCreationStatus.CreatedAndAdmitted,
                "created_and_admitted",
                character.Id,
                character);
        }
    }
}
