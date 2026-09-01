using System.Collections.Concurrent;
using System.Drawing;
using System.Reflection;
using AAEmu.Commons.Utils;
using AAEmu.Game.Bots.Ops;
using AAEmu.Game.Core.Managers;
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
    public async Task Execute_SystemActorFromActiveWorld_IsNotRejectedByWorldGuard()
    {
        var spawnPosition = new WorldSpawnPosition { ZoneId = 0, X = 101.5f, Y = 202.5f, Z = 303.5f };
        var worldManager = CreateWorldManager();
        var world = new WorldInstance(
            new WorldTemplate { Id = 1, Name = "main_world", SpawnPosition = spawnPosition },
            0,
            true,
            WorldManager.DefaultInstanceId);
        SetMainWorld(worldManager, world);
        var npcManager = new NpcManager(
            Mock.Of<IObjectIdManager>().Object,
            Mock.Of<IModelManager>().Object,
            Mock.Of<IFactionManager>().Object,
            Mock.Of<IItemManager>().Object,
            Mock.Of<IAIManager>().Object);
        RegisterSingletons(worldManager, npcManager);
        var actor = SystemActor.Create();
        var messages = new List<string>();
        var output = Mock.Of<IMessageOutput>();
        output.SendMessage(Any<ChatType>(), Any<string>(), Any<Color?>())
            .Callback((ChatType _, string message, Color? _) => messages.Add(message));

        new SpawnPassiveNpcCommand().Execute(actor, ["11180"], output.Object);

        await Assert.That(actor.ParentWorld).IsSameReferenceAs(worldManager.MainWorld);
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

    private void RegisterSingletons(WorldManager worldManager, NpcManager npcManager)
    {
        BotTestFixture.ResetSingleton<NpcManager>();
        BotTestFixture.ResetSingleton<WorldManager>();
        var services = new ServiceCollection();
        services.AddSingleton(worldManager);
        services.AddSingleton(npcManager);
        _serviceProvider = services.BuildServiceProvider();
        SingletonContainer.ServiceProvider = _serviceProvider;
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
