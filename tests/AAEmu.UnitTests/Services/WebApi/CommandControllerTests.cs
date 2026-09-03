using System.Collections.Concurrent;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using AAEmu.Commons.Utils;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Bots.Ops;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Core.Managers.Id;
using AAEmu.Game.Core.Managers.UnitManagers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models;
using AAEmu.Game.Models.Account;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.World;
using AAEmu.Game.Models.Game.World.Transform;
using AAEmu.Game.Models.Game.World.Zones;
using AAEmu.Game.Services.WebApi.Controllers;
using AAEmu.Game.Utils.Scripts;
using AAEmu.UnitTests.Utils.Mocks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NetCoreServer;

namespace AAEmu.UnitTests.Services.WebApi;

[NotInParallel]
public class CommandControllerTests
{
    private TestEnvironment _environment;

    [Before(Test)]
    public void Setup()
    {
        _environment = new TestEnvironment();
    }

    [After(Test)]
    public void Teardown()
    {
        _environment.Dispose();
        _environment = null;
    }

    [Test]
    public async Task ExecuteCommand_SystemActor_RunsAddBotWithoutWorldLookup()
    {
        var response = _environment.Execute("@system", "2");

        await Assert.That(response.Status).IsEqualTo(200);
        await Assert.That(CommandManager.Instance.GetCommandKeys()).Contains("addbot");
        await Assert.That(_environment.BotManager.GetBot(2)).IsNotNull();
    }

    [Test]
    public async Task ExecuteCommand_SystemActor_UsesLowestIdQualifiedBotAndIsFreshPerRequest()
    {
        var world = new WorldInstance(
            new WorldTemplate { Id = 7, Name = "anchored_world" },
            0,
            true,
            41);
        _environment.SetMainWorld(world);
        var higherIdPosition = new WorldSpawnPosition
        {
            WorldId = 7,
            ZoneId = 402,
            X = 901.5f,
            Y = 902.5f,
            Z = 903.5f,
            Roll = 0.4f,
            Pitch = 0.5f,
            Yaw = 0.6f
        };
        var lowestIdPosition = new WorldSpawnPosition
        {
            WorldId = 7,
            ZoneId = 401,
            X = 101.5f,
            Y = 202.5f,
            Z = 303.5f,
            Roll = 0.1f,
            Pitch = 0.2f,
            Yaw = 0.3f
        };
        var higherIdBot = _environment.MakeAnchoredBot(20, world, higherIdPosition);
        var lowestIdBot = _environment.MakeAnchoredBot(10, world, lowestIdPosition);
        _environment.AddActiveBot(higherIdBot);
        _environment.AddActiveBot(lowestIdBot);
        var captureCommand = new CaptureActorCommand();
        captureCommand.OnLoad();

        var firstResponse = _environment.Execute("captureactor", SystemActor.ActorName, "first");
        var secondResponse = _environment.Execute("captureactor", SystemActor.ActorName, "second");

        await Assert.That(firstResponse.Status).IsEqualTo(200);
        await Assert.That(secondResponse.Status).IsEqualTo(200);
        await Assert.That(captureCommand.Actors).Count().IsEqualTo(2);
        await Assert.That(captureCommand.Actors[0]).IsNotSameReferenceAs(captureCommand.Actors[1]);
        foreach (var actor in captureCommand.Actors)
        {
            await Assert.That(actor).IsTypeOf<SystemActor>();
            await Assert.That(actor.Name).IsEqualTo(SystemActor.ActorName);
            await Assert.That(actor.AccessLevel).IsEqualTo(100);
            await Assert.That(actor.AccountId).IsEqualTo(0u);
            await Assert.That(actor.Connection).IsNull();
            await Assert.That(actor.ParentWorld).IsSameReferenceAs(world);
            await Assert.That(actor.Transform.InstanceId).IsEqualTo(world.Id);
            await Assert.That(actor.Transform.ZoneId).IsEqualTo(lowestIdPosition.ZoneId);
            await Assert.That(actor.Transform.World.Position.X).IsEqualTo(lowestIdPosition.X);
            await Assert.That(actor.Transform.World.Position.Y).IsEqualTo(lowestIdPosition.Y);
            await Assert.That(actor.Transform.World.Position.Z).IsEqualTo(lowestIdPosition.Z);
            await Assert.That(actor.Transform.World.Rotation.X).IsEqualTo(lowestIdPosition.Roll);
            await Assert.That(actor.Transform.World.Rotation.Y).IsEqualTo(lowestIdPosition.Pitch);
            await Assert.That(actor.Transform.World.Rotation.Z).IsEqualTo(lowestIdPosition.Yaw);
        }

        await Assert.That(lowestIdBot.ParentWorld).IsSameReferenceAs(world);
        await Assert.That(lowestIdBot.Transform.World.Position.X).IsEqualTo(lowestIdPosition.X);
        await Assert.That(world.GetCharacterCount()).IsEqualTo(0);
    }

    [Test]
    public async Task ExecuteCommand_SystemActor_ExcludesInvalidAndZoneZeroActiveBots()
    {
        var world = new WorldInstance(new WorldTemplate { Id = 8, Name = "qualified_world" }, 0, true, 42);
        _environment.SetMainWorld(world);
        var zoneZeroBot = _environment.MakeAnchoredBot(1, world,
            new WorldSpawnPosition { WorldId = 8, ZoneId = 0, X = 1f, Y = 2f, Z = 3f });
        var worldlessBot = _environment.MakeAnchoredBot(2, world,
            new WorldSpawnPosition { WorldId = 8, ZoneId = 501, X = 4f, Y = 5f, Z = 6f });
        BotTestFixture.SetPrivateField<WorldInstance>(worldlessBot, "_parentWorld", null);
        var nonFiniteBot = _environment.MakeAnchoredBot(3, world,
            new WorldSpawnPosition { WorldId = 8, ZoneId = 502, X = float.NaN, Y = 8f, Z = 9f });
        var mismatchedInstanceBot = _environment.MakeAnchoredBot(4, world,
            new WorldSpawnPosition { WorldId = 8, ZoneId = 503, X = 10f, Y = 11f, Z = 12f });
        BotTestFixture.SetPrivateField(mismatchedInstanceBot.Transform, "_instanceId", 999u);
        var qualifiedPosition = new WorldSpawnPosition
        {
            WorldId = 8,
            ZoneId = 504,
            X = 13f,
            Y = 14f,
            Z = 15f,
            Roll = 0.7f,
            Pitch = 0.8f,
            Yaw = 0.9f
        };
        var qualifiedBot = _environment.MakeAnchoredBot(5, world, qualifiedPosition);
        _environment.AddActiveBot(qualifiedBot);
        _environment.AddActiveBot(mismatchedInstanceBot);
        _environment.AddActiveBot(nonFiniteBot);
        _environment.AddActiveBot(worldlessBot);
        _environment.AddActiveBot(zoneZeroBot);
        var captureCommand = new CaptureActorCommand();
        captureCommand.OnLoad();

        var response = _environment.Execute("captureactor", SystemActor.ActorName, "probe");

        var actor = captureCommand.Actors.Single();
        await Assert.That(response.Status).IsEqualTo(200);
        await Assert.That(actor.ParentWorld).IsSameReferenceAs(world);
        await Assert.That(actor.Transform.ZoneId).IsEqualTo(qualifiedPosition.ZoneId);
        await Assert.That(actor.Transform.World.Position.X).IsEqualTo(qualifiedPosition.X);
    }

    [Test]
    public async Task ExecuteCommand_SystemActorWithoutQualifiedBot_IgnoresZoneZeroMainWorld()
    {
        var t057Origin = new WorldSpawnPosition { ZoneId = 0, X = 0f, Y = 0f, Z = 0f };
        var world = new WorldInstance(
            new WorldTemplate { Id = 1, Name = "main_world", SpawnPosition = t057Origin },
            0,
            true,
            WorldManager.DefaultInstanceId);
        _environment.SetMainWorld(world);
        var captureCommand = new CaptureActorCommand();
        captureCommand.OnLoad();

        var response = _environment.Execute("captureactor", SystemActor.ActorName, "probe");

        var actor = captureCommand.Actors.Single();
        await Assert.That(response.Status).IsEqualTo(200);
        await Assert.That(actor.ParentWorld).IsNull();
        await Assert.That(actor.Transform.ZoneId).IsEqualTo(0u);
        await Assert.That(actor.AccessLevel).IsEqualTo(100);
        await Assert.That(actor.AccountId).IsEqualTo(0u);
        await Assert.That(actor.Connection).IsNull();
        await Assert.That(world.GetCharacterCount()).IsEqualTo(0);
    }

    [Test]
    public async Task ExecuteCommand_UnknownActor_ReturnsExisting400Error()
    {
        var response = _environment.Execute("Nobody", "2");

        await Assert.That(response.Status).IsEqualTo(400);
        await Assert.That(response.Body).Contains("Character \\\"Nobody\\\" not found");
    }

    private sealed class TestEnvironment : IDisposable
    {
        private readonly ServiceProvider _serviceProvider;
        public BotManager BotManager { get; }
        public WorldManager WorldManager { get; }

        public TestEnvironment()
        {
            ResetSingletons();

            var taskManager = Mock.Of<ITaskManager>();
            taskManager.Schedule(Any<AAEmu.Game.Models.Tasks.Task>(), Any<TimeSpan?>(), Any<TimeSpan?>(), Any<int>())
                .Returns(true);
            var timeProvider = new FakeTimeProvider();
            var botHost = new BotHost(taskManager.Object, timeProvider);
            var botCombatManager = new FakeBotCombatManager();
            var botArchetypeManager = new FakeBotArchetypeManager();
            Character.UsedCharacterObjIds[2] = 1002;
            BotManager = new BotManager(
                characterLoader: id => BotTestFixture.MakeBot(id, Vector3.Zero),
                onlineLookup: _ => null,
                fullLoader: _ => { },
                onBotSpawn: _ => { },
                saveAndRemove: _ => { },
                leaveWorld: _ => { },
                setWorld: _ => { },
                prepareCharacter: _ => false,
                spawn: _ => { });

            var accountManager = Mock.Of<IAccountManager>();
            accountManager.GetAccountDetails(Any<uint>()).Returns(new AccountDetails());
            WorldManager = new WorldManager(
                Mock.Of<ITickManager>().Object,
                Mock.Of<IWorldIdManager>().Object,
                new Lazy<IZoneManager>(() => Mock.Of<IZoneManager>().Object),
                new Lazy<IIndunManager>(() => Mock.Of<IIndunManager>().Object),
                new Lazy<IFamilyManager>(() => Mock.Of<IFamilyManager>().Object));
            var characterManager = new CharacterManager(
                WorldManager,
                accountManager.Object,
                Mock.Of<INameManager>().Object,
                Mock.Of<ICharacterIdManager>().Object,
                Mock.Of<IFactionManager>().Object,
                Mock.Of<ISkillManager>().Object,
                Mock.Of<IItemManager>().Object,
                Mock.Of<IHousingManager>().Object,
                Mock.Of<IFamilyManager>().Object,
                Mock.Of<IMailManager>().Object,
                taskManager.Object);
            var zoneManager = new ZoneManager(WorldManager);
            BotTestFixture.SetPrivateField(zoneManager, "_zones", new Dictionary<uint, Zone>());
            var accessLevelManager = new AccessLevelManager(Options.Create(new AppConfiguration
            {
                AccessLevel = new Dictionary<string, int>
                {
                    ["addbot"] = 100,
                    ["captureactor"] = 100
                }
            }));
            accessLevelManager.Load();
            var commandManager = new CommandManager();

            var services = new ServiceCollection();
            services.AddSingleton(taskManager.Object);
            services.AddSingleton<ITaskManager>(taskManager.Object);
            services.AddSingleton<TimeProvider>(timeProvider);
            services.AddSingleton(botHost);
            services.AddSingleton<IBotHost>(botHost);
            services.AddSingleton<BotConfig>(new BotConfig());
            services.AddSingleton(BotManager);
            services.AddSingleton<IBotManager>(BotManager);
            services.AddSingleton<BotCombatManager>(botCombatManager);
            services.AddSingleton<IBotCombatManager>(botCombatManager);
            services.AddSingleton<BotArchetypeManager>(botArchetypeManager);
            services.AddSingleton<IBotArchetypeManager>(botArchetypeManager);
            services.AddSingleton(WorldManager);
            services.AddSingleton(zoneManager);
            services.AddSingleton<IZoneManager>(zoneManager);
            services.AddSingleton(characterManager);
            services.AddSingleton(accessLevelManager);
            services.AddSingleton(commandManager);
            _serviceProvider = services.BuildServiceProvider();
            SingletonContainer.ServiceProvider = _serviceProvider;

            new AAEmu.Game.Scripts.Commands.AddBot().OnLoad();
        }

        public HttpResponse Execute(string actor, string arguments)
        {
            return Execute("addbot", actor, arguments);
        }

        public HttpResponse Execute(string command, string actor, string arguments)
        {
            var request = new HttpRequest("POST", $"/api/commands/{command}", "HTTP/1.1");
            request.SetBody(JsonSerializer.Serialize(new { character = actor, arguments }));
            var matches = Regex.Matches(request.Url, "/api/commands/([^/]+)");
            return new CommandController().ExecuteCommand(request, matches);
        }

        public void SetMainWorld(WorldInstance world)
        {
            var worlds = (ConcurrentDictionary<uint, WorldInstance>)typeof(WorldManager)
                .GetField("_worlds", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(WorldManager);
            worlds[world.Id] = world;
            WorldManager.MainWorld = world;
        }

        public Character MakeAnchoredBot(uint id, WorldInstance world, WorldSpawnPosition position)
        {
            var bot = BotTestFixture.MakeBot(id, Vector3.Zero);
            bot.Transform.InstanceId = world.Id;
            BotTestFixture.SetPrivateField(bot.Transform, "_zoneId", position.ZoneId);
            bot.Transform.Local.SetPosition(position.X, position.Y, position.Z);
            bot.Transform.Local.SetRotation(position.Roll, position.Pitch, position.Yaw);
            bot.ParentWorld = world;
            return bot;
        }

        public void AddActiveBot(Character bot)
        {
            BotTestFixture.GetDictionary<Character>(BotManager, "ActiveBots")[bot.Id] = bot;
        }

        public void Dispose()
        {
            _serviceProvider.Dispose();
            SingletonContainer.ServiceProvider = null;
            ResetSingletons();
        }

        private static void ResetSingletons()
        {
            Character.UsedCharacterObjIds.Remove(2);
            BotTestFixture.ResetSingleton<CommandManager>();
            BotTestFixture.ResetSingleton<AccessLevelManager>();
            BotTestFixture.ResetSingleton<CharacterManager>();
            BotTestFixture.ResetSingleton<WorldManager>();
            BotTestFixture.ResetSingleton<ZoneManager>();
            BotTestFixture.ResetSingleton<BotManager>();
            BotTestFixture.ResetSingleton<BotCombatManager>();
            BotTestFixture.ResetSingleton<BotArchetypeManager>();
            BotTestFixture.ResetSingleton<BotHost>();
            BotTestFixture.ResetSingleton<BotConfig>();
        }
    }

    private sealed class CaptureActorCommand : ICommand
    {
        public List<Character> Actors { get; } = [];
        public string[] CommandNames { get; set; } = ["captureactor"];

        public void OnLoad()
        {
            CommandManager.Instance.Register(CommandNames, this);
        }

        public string GetCommandLineHelp()
        {
            return string.Empty;
        }

        public string GetCommandHelpText()
        {
            return string.Empty;
        }

        public void Execute(Character character, string[] args, IMessageOutput messageOutput)
        {
            Actors.Add(character);
        }
    }
}
