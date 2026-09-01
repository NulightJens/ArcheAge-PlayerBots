using System.Collections.Concurrent;
using System.Drawing;
using System.Numerics;
using System.Reflection;
using AAEmu.Commons.Utils;
using AAEmu.Game.Bots.Ops;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Core.Managers.Id;
using AAEmu.Game.Core.Managers.UnitManagers;
using AAEmu.Game.Core.Managers.World;
using System.Globalization;
using AAEmu.Game.Models.Game.AI.v2.AiCharacters;
using AAEmu.Game.Models.Game.AI.v2.Behaviors.Common;
using AAEmu.Game.Models.Game.AI.v2.Framework;
using AAEmu.Game.Models.Game.Chat;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.World;
using AAEmu.Game.Models.Game.World.Transform;
using AAEmu.Game.Models.Game.World.Zones;
using AAEmu.Game.Scripts.Commands;
using AAEmu.Game.Utils.Scripts;
using AAEmu.UnitTests.Utils.Mocks;
using Microsoft.Extensions.DependencyInjection;

namespace AAEmu.UnitTests.Game.Scripts.Commands;

[NotInParallel]
public class SpawnPassiveNpcCommandTests
{
    private ServiceProvider _serviceProvider;

    [After(Test)]
    public void Teardown()
    {
        _serviceProvider?.Dispose();
        _serviceProvider = null;
        SingletonContainer.ServiceProvider = null;
        BotTestFixture.ResetSingleton<NpcManager>();
        BotTestFixture.ResetSingleton<WorldManager>();
        BotTestFixture.ResetSingleton<ZoneManager>();
        BotTestFixture.ResetSingleton<CharacterManager>();
        BotTestFixture.ResetSingleton<BotManager>();
    }

    [Test]
    public async Task TryParse_TemplateOnly_UsesSafeDefaultDistance()
    {
        var parsed = SpawnPassiveNpcCommand.TryParse(["11180"], out var templateId, out var distance);

        await Assert.That(parsed).IsTrue();
        await Assert.That(templateId).IsEqualTo(11180u);
        await Assert.That(distance).IsEqualTo(SpawnPassiveNpcCommand.DefaultDistance);
    }

    [Test]
    public async Task TryParse_DecimalDistanceUnderGermanCulture_UsesInvariantCulture()
    {
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            var parsed = SpawnPassiveNpcCommand.TryParse(["11180", "14.5"], out _, out var distance);

            await Assert.That(parsed).IsTrue();
            await Assert.That(distance).IsEqualTo(14.5f);
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    [Arguments("0")]
    [Arguments("abc")]
    [Arguments("NaN")]
    [Arguments("4.9")]
    [Arguments("100.1")]
    [Test]
    public async Task TryParse_InvalidInput_IsRejected(string value)
    {
        string[] args = value is "0" or "abc" ? [value] : ["11180", value];

        await Assert.That(SpawnPassiveNpcCommand.TryParse(args, out _, out _)).IsFalse();
    }

    [Test]
    public async Task ApplyPassiveAi_DetachesExistingAiAndRegistersDummyBehavior()
    {
        var npc = new Npc();
        var previousAi = new DummyAiCharacter { Owner = npc };
        previousAi.Start();
        previousAi.GoToIdle();
        npc.Ai = previousAi;
        NpcAi registeredAi = null;

        var passiveAi = SpawnPassiveNpcCommand.ApplyPassiveAi(npc, ai => registeredAi = ai);

        await Assert.That(previousAi.Owner).IsNull();
        await Assert.That(npc.Ai).IsSameReferenceAs(passiveAi);
        await Assert.That(registeredAi).IsSameReferenceAs(passiveAi);
        await Assert.That(passiveAi.Owner).IsSameReferenceAs(npc);
        await Assert.That(passiveAi.GetCurrentBehavior()).IsTypeOf<DummyBehavior>();
        await Assert.That(npc.CurrentTarget).IsNull();
    }

    [Test]
    public async Task Execute_SystemActorWithoutQualifiedBot_StopsAtWorldGuardForT057ZoneZeroOrigin()
    {
        var t057Origin = new WorldSpawnPosition { ZoneId = 0, X = 0f, Y = 0f, Z = 0f };
        var worldManager = CreateWorldManager();
        var world = new WorldInstance(
            new WorldTemplate { Id = 1, Name = "main_world", SpawnPosition = t057Origin },
            0,
            true,
            WorldManager.DefaultInstanceId);
        SetMainWorld(worldManager, world);
        var npcManager = CreateNpcManager();
        var botManager = new BotManager(_ => null);
        RegisterSingletons(worldManager, npcManager, botManager);
        var actor = SystemActor.Create();
        var messages = new List<string>();
        var output = Mock.Of<IMessageOutput>();
        output.SendMessage(Any<ChatType>(), Any<string>(), Any<Color?>())
            .Callback((ChatType _, string message, Color? _) => messages.Add(message));

        new SpawnPassiveNpcCommand().Execute(actor, ["11180"], output.Object);

        await Assert.That(actor.ParentWorld).IsNull();
        await Assert.That(actor.Transform.ZoneId).IsEqualTo(0u);
        await Assert.That(messages).Contains(
            "|cFFFFFFFF[spawnpassive]|r |cFFFF0000The command character is not in a world instance.|r");
        await Assert.That(messages).DoesNotContain("|cFFFFFFFF[spawnpassive]|r |cFFFF0000NPC 11180 does not exist.|r");
    }

    [Test]
    public async Task Execute_SystemActorWithQualifiedAnchor_AdvancesPastWorldGuard()
    {
        var worldManager = CreateWorldManager();
        var world = new WorldInstance(new WorldTemplate { Id = 9, Name = "bot_world" }, 0, true, 43);
        SetMainWorld(worldManager, world);
        var npcManager = CreateNpcManager();
        var botManager = new BotManager(_ => null);
        RegisterSingletons(worldManager, npcManager, botManager);
        var anchorPosition = new WorldSpawnPosition
        {
            WorldId = 9,
            ZoneId = 601,
            X = 101.5f,
            Y = 202.5f,
            Z = 303.5f,
            Roll = 0.1f,
            Pitch = 0.2f,
            Yaw = 0.3f
        };
        var bot = BotTestFixture.MakeBot(10, Vector3.Zero);
        bot.Transform.InstanceId = world.Id;
        BotTestFixture.SetPrivateField(bot.Transform, "_zoneId", anchorPosition.ZoneId);
        bot.Transform.Local.SetPosition(anchorPosition.X, anchorPosition.Y, anchorPosition.Z);
        bot.Transform.Local.SetRotation(anchorPosition.Roll, anchorPosition.Pitch, anchorPosition.Yaw);
        bot.ParentWorld = world;
        BotTestFixture.GetDictionary<AAEmu.Game.Models.Game.Char.Character>(botManager, "ActiveBots")[bot.Id] = bot;
        var actor = SystemActor.Create();
        var messages = new List<string>();
        var output = Mock.Of<IMessageOutput>();
        output.SendMessage(Any<ChatType>(), Any<string>(), Any<Color?>())
            .Callback((ChatType _, string message, Color? _) => messages.Add(message));

        new SpawnPassiveNpcCommand().Execute(actor, ["11180"], output.Object);

        await Assert.That(actor.ParentWorld).IsSameReferenceAs(world);
        await Assert.That(actor.Transform.InstanceId).IsEqualTo(world.Id);
        await Assert.That(actor.Transform.ZoneId).IsEqualTo(anchorPosition.ZoneId);
        await Assert.That(messages).Contains("|cFFFFFFFF[spawnpassive]|r |cFFFF0000NPC 11180 does not exist.|r");
        await Assert.That(messages).DoesNotContain(
            "|cFFFFFFFF[spawnpassive]|r |cFFFF0000The command character is not in a world instance.|r");
    }

    [Test]
    public async Task Execute_CharacterWithoutWorld_KeepsExactWorldGuard()
    {
        var messages = new List<string>();
        var output = Mock.Of<IMessageOutput>();
        output.SendMessage(Any<ChatType>(), Any<string>(), Any<Color?>())
            .Callback((ChatType _, string message, Color? _) => messages.Add(message));

        new SpawnPassiveNpcCommand().Execute(
            new AAEmu.Game.Models.Game.Char.Character(new AAEmu.Game.Models.Game.Units.UnitCustomModelParams()),
            ["11180"],
            output.Object);

        await Assert.That(messages).Contains(
            "|cFFFFFFFF[spawnpassive]|r |cFFFF0000The command character is not in a world instance.|r");
    }

    private void RegisterSingletons(WorldManager worldManager, NpcManager npcManager, BotManager botManager)
    {
        BotTestFixture.ResetSingleton<NpcManager>();
        BotTestFixture.ResetSingleton<WorldManager>();
        BotTestFixture.ResetSingleton<ZoneManager>();
        BotTestFixture.ResetSingleton<CharacterManager>();
        BotTestFixture.ResetSingleton<BotManager>();
        var zoneManager = new ZoneManager(worldManager);
        BotTestFixture.SetPrivateField(zoneManager, "_zones", new Dictionary<uint, Zone>());
        var taskManager = Mock.Of<ITaskManager>();
        var characterManager = new CharacterManager(
            worldManager,
            Mock.Of<IAccountManager>().Object,
            Mock.Of<INameManager>().Object,
            Mock.Of<ICharacterIdManager>().Object,
            Mock.Of<IFactionManager>().Object,
            Mock.Of<ISkillManager>().Object,
            Mock.Of<IItemManager>().Object,
            Mock.Of<IHousingManager>().Object,
            Mock.Of<IFamilyManager>().Object,
            Mock.Of<IMailManager>().Object,
            taskManager.Object);
        var services = new ServiceCollection();
        services.AddSingleton(worldManager);
        services.AddSingleton(zoneManager);
        services.AddSingleton<IZoneManager>(zoneManager);
        services.AddSingleton(characterManager);
        services.AddSingleton(npcManager);
        services.AddSingleton(botManager);
        _serviceProvider = services.BuildServiceProvider();
        SingletonContainer.ServiceProvider = _serviceProvider;
    }

    private static NpcManager CreateNpcManager()
    {
        return new NpcManager(
            Mock.Of<IObjectIdManager>().Object,
            Mock.Of<IModelManager>().Object,
            Mock.Of<IFactionManager>().Object,
            Mock.Of<IItemManager>().Object,
            Mock.Of<IAIManager>().Object);
    }

    private static WorldManager CreateWorldManager()
    {
        return new WorldManager(
            Mock.Of<ITickManager>().Object,
            Mock.Of<IWorldIdManager>().Object,
            new Lazy<IZoneManager>(() => Mock.Of<IZoneManager>().Object),
            new Lazy<IIndunManager>(() => Mock.Of<IIndunManager>().Object),
            new Lazy<IFamilyManager>(() => Mock.Of<IFamilyManager>().Object));
    }

    private static void SetMainWorld(WorldManager worldManager, WorldInstance world)
    {
        var worlds = (ConcurrentDictionary<uint, WorldInstance>)typeof(WorldManager)
            .GetField("_worlds", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(worldManager);
        worlds[world.Id] = world;
        worldManager.MainWorld = world;
    }
}
