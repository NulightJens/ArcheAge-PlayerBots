using System.Collections.Concurrent;
using System.Numerics;
using AAEmu.Game.Bots.Blackboard;
using AAEmu.Game.Bots.Content.Rotations;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Scripts.Commands;
using AAEmu.Game.Utils.Scripts;
using AAEmu.UnitTests.Bots.Sim;
using AAEmu.UnitTests.Utils.Mocks;
using Microsoft.Extensions.Time.Testing;

namespace AAEmu.UnitTests.Bots.Ops;

[NotInParallel]
public class BotOpsCommandTests
{
    private BotManager _botManager;
    private BotHost _host;

    [Before(Test)]
    public void Setup()
    {
        BotTestFixture.RegisterTaskManager();
        BotTestFixture.ResetSingleton<BotRotationManager>();
        _botManager = new BotManager(_ => null, onlineLookup: _ => null);
        BotTestFixture.RegisterSingletons(_botManager);
        _host = BotHost.Instance;
    }

    [After(Test)]
    public void Teardown()
    {
        BotTestFixture.ResetTaskManager();
        BotTestFixture.ResetSingleton<BotRotationManager>();
    }

    [Test]
    public async Task TryBotStrategy_ParsesAllEngineOperationAndNames()
    {
        var parsed = BotCommandArgs.TryBotStrategy(
            ["all", "nc", "+legacy,other"],
            out var target,
            out var kind,
            out var operation,
            out var names,
            out var error);

        await Assert.That(parsed).IsTrue();
        await Assert.That(target).IsEqualTo("all");
        await Assert.That(kind).IsEqualTo(BotEngineKind.NonCombat);
        await Assert.That(operation).IsEqualTo('+');
        await Assert.That(names).IsEquivalentTo(["legacy", "other"]);
        await Assert.That(error).IsNull();
    }

    [Test]
    public async Task BotStrategy_AddRemoveToggleAndList_RoundTripsAgainstBotSimRuntime()
    {
        var bot = AddBot(1);
        var command = new BotStrategyCommand();

        var removed = Execute(command, "1", "nc", "-legacy");
        var added = Execute(command, "1", "nc", "+legacy");
        var toggled = Execute(command, "1", "nc", "~legacy");
        var listed = Execute(command, "1", "nc", "?");

        await Assert.That(bot.Runtime.Engines[(int)BotEngineKind.NonCombat].HasStrategy("legacy")).IsFalse();
        await Assert.That(removed.Messages.Single()).Contains("removed");
        await Assert.That(added.Messages.Single()).Contains("added");
        await Assert.That(toggled.Messages.Single()).Contains("removed");
        await Assert.That(listed.Messages.Single()).Contains("Active strategies");
        await Assert.That(listed.Messages.Single()).DoesNotContain("legacy");
    }

    [Test]
    public async Task BotStrategy_HelpExplainsPendingActionsAreDrained()
    {
        await Assert.That(new BotStrategyCommand().GetCommandHelpText()).Contains("drain");
    }

    [Test]
    public async Task BotStrategy_ListShowsPresentStrategy()
    {
        AddBot(1);
        var listed = Execute(new BotStrategyCommand(), "1", "nc", "?");

        await Assert.That(listed.Messages.Single()).Contains("legacy");
    }

    [Test]
    public async Task BotStrategy_ReportsCombatStrategyAndMissingDeadEngine()
    {
        AddBot(1);
        var command = new BotStrategyCommand();

        var combat = Execute(command, "1", "co", "?");
        var dead = Execute(command, "1", "de", "?");

        await Assert.That(combat.Messages.Single()).Contains("Active strategies");
        await Assert.That(combat.Messages.Single()).Contains("combat-base");
        var nonCombat = Execute(command, "1", "nc", "?");
        await Assert.That(nonCombat.Messages.Single()).Contains("body-base");
        await Assert.That(dead.Messages.Single()).Contains("no Dead engine");
    }

    [Test]
    public async Task TryBotStrategy_RejectsMalformedOperationAndName()
    {
        var parsed = BotCommandArgs.TryBotStrategy(["1", "nc", "+"],
            out _, out _, out _, out _, out var error);

        await Assert.That(parsed).IsFalse();
        await Assert.That(error).IsEqualTo("help");
    }

    [Test]
    public async Task BotStrategy_AllAppliesToEveryBot_AndUnknownNameListsRegistry()
    {
        var first = AddBot(1);
        var second = AddBot(2);
        var command = new BotStrategyCommand();

        var removed = Execute(command, "all", "nc", "-legacy");
        var unknown = Execute(command, "1", "nc", "+missing");

        await Assert.That(first.Runtime.Engines[(int)BotEngineKind.NonCombat].HasStrategy("legacy")).IsFalse();
        await Assert.That(second.Runtime.Engines[(int)BotEngineKind.NonCombat].HasStrategy("legacy")).IsFalse();
        await Assert.That(removed.Messages.Count()).IsEqualTo(2);
        await Assert.That(unknown.Messages.Single()).Contains("legacy");
    }

    [Test]
    public async Task BotValues_FiltersAndShowsBlackboardSnapshot()
    {
        var bot = AddBot(1);
        var key = new ValueKey<int>("test value");
        bot.Runtime.Blackboard.Register(key, new ManualValue<int>(42));
        bot.Runtime.Blackboard.Get(key, _host.TimeProvider.GetUtcNow().UtcDateTime);

        var output = Execute(new BotValuesCommand(), "1", "test");

        await Assert.That(output.Messages.Single()).Contains("test value=42");
    }

    [Test]
    public async Task BotActions_ShowsLastActionRing()
    {
        var bot = AddBot(1);
        var now = _host.TimeProvider.GetUtcNow().UtcDateTime;
        var context = new BotContext(
            bot.Bot,
            bot.Runtime,
            bot.Runtime.Blackboard,
            now,
            BotConfig.Instance,
            BotEngineKind.NonCombat,
            bot.Brain);
        bot.Runtime.Engines[(int)BotEngineKind.NonCombat].DoNextAction(context, minimal: false);

        var output = Execute(new BotActionsCommand(), "1");

        await Assert.That(output.Messages.Single()).Contains("legacy tick");
        await Assert.That(output.Messages.Single()).Contains("Success");
    }

    [Test]
    public async Task BotActions_CombatSelectorShowsCombatRing()
    {
        var bot = AddBot(1);
        var now = _host.TimeProvider.GetUtcNow().UtcDateTime;
        var context = new BotContext(
            bot.Bot,
            bot.Runtime,
            bot.Runtime.Blackboard,
            now,
            BotConfig.Instance,
            BotEngineKind.Combat,
            bot.Brain);
        bot.Runtime.Engines[(int)BotEngineKind.Combat].DoNextAction(context, minimal: false);

        var output = Execute(new BotActionsCommand(), "1", "co");

        await Assert.That(output.Messages.Single()).Contains("legacy tick");
        await Assert.That(output.Messages.Single()).Contains("Success");
    }

    [Test]
    public async Task BotRotation_SetAndShow_RoundTripsAgainstBotRuntime()
    {
        var bot = AddBot(1);
        var manager = BotRotationManager.Instance;
        var json = """
                   {
                     "id": "test.rotation",
                     "archetype": "Test",
                     "meta": { "role": "damage", "range": "melee" },
                     "skills": {},
                     "default": [{ "action": "autoAttack", "rel": 11.0, "weight": 1.0 }],
                     "rules": []
                   }
                   """;
        await Assert.That(manager.LoadRotations(json, "test.rotation")).IsTrue();

        var command = new BotRotationCommand();
        var set = Execute(command, "1", "set", "test.rotation");
        var show = Execute(command, "1", "show").Messages.ToArray();

        await Assert.That(set.Messages.Single()).Contains("rotation set to 'test.rotation'");
        await Assert.That(show[0]).Contains("rotation=test.rotation");
        await Assert.That(show[1]).Contains("filler=1");
        await Assert.That(show[2]).Contains("rotation rows won: (none)");
        await Assert.That(bot.Runtime.RotationOverrideId).IsEqualTo("test.rotation");
    }

    private BotSim.SimBot AddBot(uint id)
    {
        var bot = BotTestFixture.MakeBot(id, Vector3.Zero);
        bot.MaxHp = 100;
        bot.Hp = 100;
        BotTestFixture.GetPrivateField<ConcurrentDictionary<uint, Character>>(_botManager, "ActiveBots")[id] = bot;

        var movementState = new BotMovementState();
        var combatState = new BotCombatState { BotId = id };
        var broadcaster = new BotMovementBroadcaster(bot, _host.TimeProvider);
        var mover = new BotSim.SimMover(bot, movementState, broadcaster);
        var brain = new BotSim.SimBrain(bot, combatState, broadcaster, (FakeTimeProvider)_host.TimeProvider);
        var runtime = new BotRuntime(bot, movementState, combatState, broadcaster, mover, brain);
        _host.Register(runtime);

        return new BotSim.SimBot(runtime, mover, brain);
    }

    private static CharacterMessageOutput Execute(ICommand command, params string[] args)
    {
        var output = new CharacterMessageOutput(new CharacterMock());
        command.Execute(new CharacterMock(), args, output);
        return output;
    }
}
