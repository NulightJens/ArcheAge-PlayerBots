using System.Collections.Concurrent;
using AAEmu.Game.Bots.Content;
using AAEmu.Game.Bots.Content.Strategies;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.UnitTests.Bots.Sim;
using AAEmu.UnitTests.Utils.Mocks;

namespace AAEmu.UnitTests.Bots.Kernel;

[NotInParallel]
public class BotEngineStrategyTests
{
    [Before(Test)]
    public void ResetContent()
    {
        BotTestFixture.ResetBotContentRegistry();
    }

    [Test]
    public async Task Init_DrainsQueueAndRebuildsStrategyNodes()
    {
        var strategy = new TestStrategy("one", [new BotNextAction("action", BotRelevance.Normal)]);
        var action = new EngineTestAction("action");
        var engine = new BotEngine(BotEngineKind.Combat, new BotConfig(), [strategy], [action]);
        engine.Queue.Push(new BotActionBasket(
            new BotActionNode(action),
            BotRelevance.Normal,
            skipPrerequisites: false,
            default,
            new DateTime(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc)));

        engine.Init();

        await Assert.That(engine.Queue.Count).IsEqualTo(0);
        await Assert.That(engine.TriggerNodes).IsEmpty();
        await Assert.That(strategy.InitTriggersCalls).IsEqualTo(2);
        await Assert.That(strategy.InitMultipliersCalls).IsEqualTo(2);
    }

    [Test]
    public async Task AddStrategy_SiblingGroupRemovesExistingSibling()
    {
        var first = new TestStrategy("a", siblingGroup: "life");
        var second = new TestStrategy("b", siblingGroup: "life");
        var engine = new BotEngine(BotEngineKind.NonCombat, new BotConfig(), [first]);

        engine.AddStrategy(second);

        await Assert.That(engine.HasStrategy("a")).IsFalse();
        await Assert.That(engine.HasStrategy("b")).IsTrue();
    }

    [Test]
    public async Task LegacyStrategy_CanCoexistWithFollowStrategy()
    {
        var engine = new BotEngine(BotEngineKind.NonCombat, new BotConfig(), [new LegacyStrategy()]);

        engine.AddStrategy(new FollowStrategy());

        await Assert.That(engine.HasStrategy("legacy")).IsTrue();
        await Assert.That(engine.HasStrategy("follow")).IsTrue();
    }

    [Test]
    public async Task ToggleStrategy_AddsAndRemovesRegisteredStrategy()
    {
        LegacyContent.Register();
        var engine = new BotEngine(BotEngineKind.Combat, new BotConfig());

        engine.ToggleStrategy("legacy");
        var added = engine.HasStrategy("legacy");
        engine.ToggleStrategy("legacy");

        await Assert.That(added).IsTrue();
        await Assert.That(engine.HasStrategy("legacy")).IsFalse();
    }

    [Test]
    public async Task DoNextAction_DeduplicatesSameTriggerInstanceAcrossStrategies()
    {
        var checks = 0;
        var trigger = new EngineTestTrigger("shared", 0, _ =>
        {
            checks++;
            return true;
        });
        var first = new TestStrategy("first", triggers: [
            new BotTriggerNode(trigger, [new BotNextAction("one", BotRelevance.Normal)])]);
        var second = new TestStrategy("second", triggers: [
            new BotTriggerNode(trigger, [new BotNextAction("two", BotRelevance.Normal)])]);
        var engine = new BotEngine(BotEngineKind.Combat, new BotConfig(), [first, second], [
            new EngineTestAction("one"), new EngineTestAction("two")]);
        var sim = new BotSim();
        var bot = sim.AddBot(1);
        var context = new BotContext(bot.Bot, bot.Runtime, bot.Runtime.Blackboard,
            sim.Time.GetUtcNow().UtcDateTime, new BotConfig(), engine.Kind);

        engine.DoNextAction(context, minimal: false);

        await Assert.That(checks).IsEqualTo(1);
    }

    [Test]
    public async Task LastActionLog_KeepsOnlyLast32Entries()
    {
        var action = new EngineTestAction("tick");
        var engine = new BotEngine(BotEngineKind.Combat, new BotConfig(),
            [new TestStrategy("tick", [new BotNextAction("tick", BotRelevance.Normal)])], [action]);
        var sim = new BotSim();
        var bot = sim.AddBot(1);
        var now = sim.Time.GetUtcNow().UtcDateTime;

        for (var i = 0; i < 40; i++)
        {
            engine.DoNextAction(new BotContext(bot.Bot, bot.Runtime, bot.Runtime.Blackboard,
                now.AddMilliseconds(i), new BotConfig(), engine.Kind), minimal: false);
        }

        await Assert.That(engine.LastActionLog).Count().IsEqualTo(32);
        await Assert.That(engine.LastActionLog[^1].Action).IsEqualTo("tick");
        await Assert.That(engine.LastActionLog[^1].Result).IsEqualTo(BotActionResult.Success);
    }

    [Test]
    public async Task ConcurrentTicksAndStrategyToggles_KeepEngineConsistent()
    {
        LegacyContent.Register();
        var action = new EngineTestAction("tick");
        var strategy = new BlockingStrategy();
        var engine = new BotEngine(BotEngineKind.Combat, new BotConfig(), [strategy], [action]);
        var sim = new BotSim();
        var bot = sim.AddBot(1);
        var context = new BotContext(bot.Bot, bot.Runtime, bot.Runtime.Blackboard,
            sim.Time.GetUtcNow().UtcDateTime, new BotConfig(), engine.Kind);
        var exceptions = new ConcurrentBag<Exception>();
        var expectedLegacy = false;
        var toggleStarted = new ManualResetEventSlim(false);

        var ticking = System.Threading.Tasks.Task.Run(() =>
        {
            for (var i = 0; i < 1000; i++)
            {
                try
                {
                    engine.DoNextAction(context, minimal: false);
                }
                catch (Exception exception)
                {
                    exceptions.Add(exception);
                }
            }
        });
        await Assert.That(strategy.Entered.Wait(TimeSpan.FromSeconds(2))).IsTrue();
        var toggling = System.Threading.Tasks.Task.Run(() =>
        {
            toggleStarted.Set();
            for (var i = 0; i < 100; i++)
            {
                try
                {
                    engine.ToggleStrategy("legacy");
                    expectedLegacy = !expectedLegacy;
                }
                catch (Exception exception)
                {
                    exceptions.Add(exception);
                }
            }
        });

        await Assert.That(toggleStarted.Wait(TimeSpan.FromSeconds(2))).IsTrue();
        strategy.Release.Set();
        await System.Threading.Tasks.Task.WhenAll(ticking, toggling);
        engine.Init();

        await Assert.That(exceptions).IsEmpty();
        await Assert.That(engine.HasStrategy("legacy")).IsEqualTo(expectedLegacy);
        await Assert.That(engine.Queue.Count).IsEqualTo(0);
    }

    [Test]
    public void ContentRegistry_RejectsWritesAfterFreeze()
    {
        LegacyContent.Register();

        Assert.Throws<InvalidOperationException>(() =>
            BotContentRegistry.RegisterAction("late", static () => new EngineTestAction("late")));
    }

    private sealed class BlockingStrategy : IBotStrategy
    {
        private int _blocked;

        public ManualResetEventSlim Entered { get; } = new(false);
        public ManualResetEventSlim Release { get; } = new(false);
        public string Name => "base";
        public string SiblingGroup => null;

        public IReadOnlyList<BotNextAction> DefaultActions
        {
            get
            {
                if (Interlocked.Exchange(ref _blocked, 1) == 0)
                {
                    Entered.Set();
                    Release.Wait();
                }

                return [new BotNextAction("tick", BotRelevance.Normal)];
            }
        }

        public void InitTriggers(List<BotTriggerNode> triggers)
        {
        }

        public void InitMultipliers(List<IBotMultiplier> multipliers)
        {
        }
    }
}
