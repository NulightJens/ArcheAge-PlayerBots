using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
using AAEmu.Commons.Utils;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Core.Managers.Id;
using AAEmu.Game.Core.Managers.UnitManagers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models;
using AAEmu.Game.Models.Account;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Items;
using AAEmu.Game.Models.Game.World;
using AAEmu.Game.Services.WebApi.Controllers;
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
            var worldManager = new WorldManager(
                Mock.Of<ITickManager>().Object,
                Mock.Of<IWorldIdManager>().Object,
                new Lazy<IZoneManager>(() => Mock.Of<IZoneManager>().Object),
                new Lazy<IIndunManager>(() => Mock.Of<IIndunManager>().Object),
                new Lazy<IFamilyManager>(() => Mock.Of<IFamilyManager>().Object));
            var characterManager = new CharacterManager(
                worldManager,
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
            var accessLevelManager = new AccessLevelManager(Options.Create(new AppConfiguration
            {
                AccessLevel = new Dictionary<string, int> { ["addbot"] = 100 }
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
            services.AddSingleton(worldManager);
            services.AddSingleton(characterManager);
            services.AddSingleton(accessLevelManager);
            services.AddSingleton(commandManager);
            _serviceProvider = services.BuildServiceProvider();
            SingletonContainer.ServiceProvider = _serviceProvider;

            new AAEmu.Game.Scripts.Commands.AddBot().OnLoad();
        }

        public HttpResponse Execute(string actor, string arguments)
        {
            var request = new HttpRequest("POST", "/api/commands/addbot", "HTTP/1.1");
            request.SetBody(JsonSerializer.Serialize(new { character = actor, arguments }));
            var matches = Regex.Matches(request.Url, "/api/commands/([^/]+)");
            return new CommandController().ExecuteCommand(request, matches);
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
            BotTestFixture.ResetSingleton<BotManager>();
            BotTestFixture.ResetSingleton<BotCombatManager>();
            BotTestFixture.ResetSingleton<BotArchetypeManager>();
            BotTestFixture.ResetSingleton<BotHost>();
            BotTestFixture.ResetSingleton<BotConfig>();
        }
    }
}
