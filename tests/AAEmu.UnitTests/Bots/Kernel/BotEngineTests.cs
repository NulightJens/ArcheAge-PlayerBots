using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.UnitTests.Bots.Sim;

namespace AAEmu.UnitTests.Bots.Kernel;

public class BotEngineTests
{
    [Test]
    public async Task BotActionResult_ContainsNoUndeclaredAliases()
    {
        var names = Enum.GetNames<BotActionResult>();

        await Assert.That(names).DoesNotContain("Failed");
        await Assert.That(names).DoesNotContain("Useless");
    }

    [Test]
    public async Task DoNextAction_ExecutesExactlyOneSuccessfulAction()
    {
        var executions = 0;
        var action = new EngineTestAction("hit", execute: _ =>
        {
            executions++;
            return BotActionResult.Success;
        });
        var engine = CreateEngine(new TestStrategy("combat", [new BotNextAction("hit", BotRelevance.Normal)]), [action]);

        var result = engine.DoNextAction(CreateContext(engine), minimal: false);

        await Assert.That(result).IsTrue();
        await Assert.That(executions).IsEqualTo(1);
    }

    [Test]
    public async Task DoNextAction_HonoursTriggerCheckInterval()
    {
        var checks = 0;
        var trigger = new EngineTestTrigger("pulse", 500, _ =>
        {
            checks++;
            return true;
        });
        var strategy = new TestStrategy("triggered", triggers: [
            new BotTriggerNode(trigger, [new BotNextAction("pulse action", BotRelevance.Normal)])]);
        var action = new EngineTestAction("pulse action");
        var engine = CreateEngine(strategy, [action]);
        var sim = new BotSim();
        var now = sim.Time.GetUtcNow().UtcDateTime;

        engine.DoNextAction(CreateContext(engine, sim, now), minimal: false);
        engine.DoNextAction(CreateContext(engine, sim, now.AddMilliseconds(100)), minimal: false);

        await Assert.That(checks).IsEqualTo(1);
    }

    [Test]
    public async Task DoNextAction_SameActionNameAcrossStrategies_UsesHigherRelevance()
    {
        var action = new EngineTestAction("shared");
        var low = new TestStrategy("low", [new BotNextAction("shared", 5f)]);
        var high = new TestStrategy("high", [new BotNextAction("shared", 20f)]);
        var engine = CreateEngine([low, high], [action]);

        engine.DoNextAction(CreateContext(engine), minimal: false);

        await Assert.That(engine.LastActionLog[^1].Relevance).IsEqualTo(20f);
    }

    [Test]
    public async Task DoNextAction_PossibleAction_PushesPrerequisiteBeforeSelf()
    {
        var executionOrder = new List<string>();
        var prerequisite = new EngineTestAction("prepare", execute: _ =>
        {
            executionOrder.Add("prepare");
            return BotActionResult.Success;
        });
        var main = new EngineTestAction("main", possible: _ => true,
            prerequisites: [new BotNextAction("prepare", BotRelevance.Normal)]);
        var engine = CreateEngine(
            new TestStrategy("combat", [new BotNextAction("main", BotRelevance.Normal)]),
            [main, prerequisite]);

        engine.DoNextAction(CreateContext(engine), minimal: false);

        await Assert.That(executionOrder).IsEquivalentTo(["prepare"]);
        await Assert.That(engine.Queue.Count).IsEqualTo(1);
        var self = engine.Queue.Pop();
        await Assert.That(self.Node.Name).IsEqualTo("main");
        await Assert.That(self.Relevance).IsEqualTo(10.001f);
    }

    [Test]
    public async Task DoNextAction_ImpossibleActionWithoutPrerequisites_UsesAlternativeAtEpsilon()
    {
        var executed = string.Empty;
        var main = new EngineTestAction("main", possible: _ => false,
            alternatives: [new BotNextAction("fallback", BotRelevance.Default)]);
        var fallback = new EngineTestAction("fallback", execute: _ =>
        {
            executed = "fallback";
            return BotActionResult.Success;
        });
        var engine = CreateEngine(new TestStrategy("combat", [new BotNextAction("main", BotRelevance.Normal)]), [main, fallback]);

        engine.DoNextAction(CreateContext(engine), minimal: false);

        await Assert.That(executed).IsEqualTo("fallback");
        await Assert.That(engine.LastActionLog[^1].Relevance).IsEqualTo(10.003f);
    }

    [Test]
    public async Task DoNextAction_NotUsefulActionWithPrerequisites_PushesNothingAndLogsNotUseful()
    {
        var prerequisiteRan = false;
        var prerequisite = new EngineTestAction("prepare", execute: _ =>
        {
            prerequisiteRan = true;
            return BotActionResult.Success;
        });
        var main = new EngineTestAction("main", useful: _ => false,
            prerequisites: [new BotNextAction("prepare", BotRelevance.Normal)]);
        var engine = CreateEngine(
            new TestStrategy("combat", [new BotNextAction("main", BotRelevance.Normal)]),
            [main, prerequisite]);

        var executed = engine.DoNextAction(CreateContext(engine), minimal: false);

        await Assert.That(executed).IsFalse();
        await Assert.That(prerequisiteRan).IsFalse();
        await Assert.That(engine.Queue.Count).IsEqualTo(0);
        await Assert.That(engine.LastActionLog[^1].Action).IsEqualTo("main");
        await Assert.That(engine.LastActionLog[^1].Result).IsEqualTo(BotActionResult.NotUseful);
    }

    [Test]
    public async Task DoNextAction_VetoedActionWithPrerequisites_PushesNothingAndLogsVetoed()
    {
        var prerequisiteRan = false;
        var prerequisite = new EngineTestAction("prepare", execute: _ =>
        {
            prerequisiteRan = true;
            return BotActionResult.Success;
        });
        var main = new EngineTestAction("main",
            prerequisites: [new BotNextAction("prepare", BotRelevance.Normal)]);
        var engine = CreateEngine(
            new TestStrategy("combat", [new BotNextAction("main", BotRelevance.Normal)], multipliers: [new EngineTestMultiplier(0f)]),
            [main, prerequisite]);

        var executed = engine.DoNextAction(CreateContext(engine), minimal: false);

        await Assert.That(executed).IsFalse();
        await Assert.That(prerequisiteRan).IsFalse();
        await Assert.That(engine.Queue.Count).IsEqualTo(0);
        await Assert.That(engine.LastActionLog[^1].Action).IsEqualTo("main");
        await Assert.That(engine.LastActionLog[^1].Result).IsEqualTo(BotActionResult.Vetoed);
    }

    [Test]
    public async Task DoNextAction_ImpossibleActionWithPrerequisites_UsesAlternativeNotPrerequisite()
    {
        var executionOrder = new List<string>();
        var prerequisite = new EngineTestAction("prepare", execute: _ =>
        {
            executionOrder.Add("prepare");
            return BotActionResult.Success;
        });
        var fallback = new EngineTestAction("fallback", execute: _ =>
        {
            executionOrder.Add("fallback");
            return BotActionResult.Success;
        });
        var main = new EngineTestAction("main", possible: _ => false,
            prerequisites: [new BotNextAction("prepare", BotRelevance.Normal)],
            alternatives: [new BotNextAction("fallback", BotRelevance.Default)]);
        var engine = CreateEngine(
            new TestStrategy("combat", [new BotNextAction("main", BotRelevance.Normal)]),
            [main, prerequisite, fallback]);

        engine.DoNextAction(CreateContext(engine), minimal: false);

        await Assert.That(executionOrder).IsEquivalentTo(["fallback"]);
        await Assert.That(engine.Queue.Count).IsEqualTo(0);
        await Assert.That(engine.LastActionLog.Any(entry => entry.Action == "main" && entry.Result == BotActionResult.Impossible)).IsTrue();
    }

    [Test]
    public async Task DoNextAction_SuccessPushesContinuersAtSameRelevance()
    {
        var action = new EngineTestAction("start", continuers: [new BotNextAction("continue", BotRelevance.Default)]);
        var continuer = new EngineTestAction("continue");
        var engine = CreateEngine(new TestStrategy("combat", [new BotNextAction("start", BotRelevance.Normal)]), [action, continuer]);

        engine.DoNextAction(CreateContext(engine), minimal: false);

        await Assert.That(engine.Queue.Count).IsEqualTo(1);
        var basket = engine.Queue.Pop();
        await Assert.That(basket.Node.Name).IsEqualTo("continue");
        await Assert.That(basket.Relevance).IsEqualTo(BotRelevance.Normal);
    }

    [Test]
    public async Task DoNextAction_MultiplierVetoesOrScalesRelevanceAtPopTime()
    {
        var multiplier = new EngineTestMultiplier(1f);
        var executions = 0;
        var action = new EngineTestAction("multiplied", execute: _ =>
        {
            executions++;
            return BotActionResult.Success;
        });
        var engine = CreateEngine(new TestStrategy("multiplied", multipliers: [multiplier]), [action]);
        var sim = new BotSim();
        var bot = sim.AddBot(1);
        var now = sim.Time.GetUtcNow().UtcDateTime;
        engine.Queue.Push(new BotActionBasket(
            new BotActionNode(action),
            BotRelevance.Normal,
            skipPrerequisites: false,
            default,
            now));
        multiplier.Value = 0f;

        engine.DoNextAction(new BotContext(bot.Bot, bot.Runtime, bot.Runtime.Blackboard,
            now.AddMilliseconds(1), new BotConfig(), engine.Kind), minimal: false);

        await Assert.That(executions).IsEqualTo(0);
        await Assert.That(multiplier.Calls).IsEqualTo(1);
        await Assert.That(engine.LastActionLog[^1].Result).IsEqualTo(BotActionResult.Vetoed);

        multiplier.Value = 0.5f;
        engine.Queue.Push(new BotActionBasket(
            new BotActionNode(action),
            BotRelevance.Normal,
            skipPrerequisites: false,
            default,
            now.AddMilliseconds(2)));
        engine.DoNextAction(new BotContext(bot.Bot, bot.Runtime, bot.Runtime.Blackboard,
            now.AddMilliseconds(2), new BotConfig(), engine.Kind), minimal: false);

        await Assert.That(executions).IsEqualTo(1);
        await Assert.That(engine.LastActionLog[^1].Relevance).IsEqualTo(5f);
    }

    [Test]
    public async Task DoNextAction_MultiplierDoesNotScaleStoredRelevanceBeforeDedupe()
    {
        var multiplier = new EngineTestMultiplier(0.5f);
        var high = new EngineTestAction("high");
        var low = new EngineTestAction("low");
        var engine = CreateEngine([
            new TestStrategy("high", [new BotNextAction("high", 20f)], multipliers: [multiplier]),
            new TestStrategy("low", [new BotNextAction("low", 10f)])], [high, low]);

        engine.DoNextAction(CreateContext(engine), minimal: false);

        var remaining = engine.Queue.Pop();
        await Assert.That(remaining.Node.Name).IsEqualTo("low");
        await Assert.That(remaining.Relevance).IsEqualTo(10f);
        await Assert.That(multiplier.Calls).IsEqualTo(1);
    }

    [Test]
    public async Task DoNextAction_DoesNotEvaluateMultiplierForBasketThatIsNeverPopped()
    {
        var multiplier = new EngineTestMultiplier(1f);
        var selected = new EngineTestAction("selected");
        var neverPopped = new EngineTestAction("never popped");
        var engine = CreateEngine([
            new TestStrategy("selected", [new BotNextAction("selected", 20f)], multipliers: [multiplier]),
            new TestStrategy("never", [new BotNextAction("never popped", 10f)])], [selected, neverPopped]);

        engine.DoNextAction(CreateContext(engine), minimal: false);

        await Assert.That(multiplier.Calls).IsEqualTo(1);
        await Assert.That(engine.Queue.Count).IsEqualTo(1);
    }

    [Test]
    public async Task DoNextAction_MinimalOnlyExecutesCommandRelevance()
    {
        var normalExecutions = 0;
        var commandExecutions = 0;
        var normal = new EngineTestAction("normal", execute: _ =>
        {
            normalExecutions++;
            return BotActionResult.Success;
        });
        var command = new EngineTestAction("command", execute: _ =>
        {
            commandExecutions++;
            return BotActionResult.Success;
        });
        var strategy = new TestStrategy("minimal", [
            new BotNextAction("normal", BotRelevance.Normal),
            new BotNextAction("command", BotRelevance.Command)]);
        var engine = CreateEngine(strategy, [normal, command]);

        engine.DoNextAction(CreateContext(engine), minimal: true);

        await Assert.That(normalExecutions).IsEqualTo(0);
        await Assert.That(commandExecutions).IsEqualTo(1);
    }

    [Test]
    public async Task DoNextAction_RespectsIterationsPerTickBound()
    {
        var executions = 0;
        var loop = new EngineTestAction("loop", execute: _ =>
        {
            executions++;
            return BotActionResult.Failure;
        }, alternatives: [new BotNextAction("loop", BotRelevance.Normal)]);
        var config = new BotConfig { IterationsPerTick = 3 };
        var engine = CreateEngine(new TestStrategy("loop", [new BotNextAction("loop", BotRelevance.Normal)]), [loop], config);

        engine.DoNextAction(CreateContext(engine, config: config), minimal: false);

        await Assert.That(executions).IsEqualTo(3);
    }

    [Test]
    public async Task DoNextAction_MinimalSkipsLowRelevanceTriggers()
    {
        var checks = 0;
        var trigger = new EngineTestTrigger("low pulse", 0, _ =>
        {
            checks++;
            return true;
        });
        var strategy = new TestStrategy("minimal trigger", triggers: [
            new BotTriggerNode(trigger, [new BotNextAction("low action", BotRelevance.Normal)])]);
        var engine = CreateEngine(strategy, [new EngineTestAction("low action")]);

        engine.DoNextAction(CreateContext(engine), minimal: true);

        await Assert.That(checks).IsEqualTo(0);
    }

    [Test]
    public async Task DoNextAction_MinimalUsesTwoIterationsPerQueuedBasket()
    {
        var executions = 0;
        var loop = new EngineTestAction("minimal loop", execute: _ =>
        {
            executions++;
            return BotActionResult.Failure;
        }, alternatives: [new BotNextAction("minimal loop", BotRelevance.Command)]);
        var config = new BotConfig { IterationsPerTick = 10 };
        var engine = CreateEngine(new TestStrategy("minimal loop", [new BotNextAction("minimal loop", BotRelevance.Command)]), [loop], config);

        engine.DoNextAction(CreateContext(engine, config: config), minimal: true);

        await Assert.That(executions).IsEqualTo(2);
    }

    [Test]
    public async Task DoNextAction_HotPathAllocatesOnlyBasketsAndActionLogEntries()
    {
        const int tickCount = 1000;
        const long basketSize = 64;
        const long actionLogEntrySize = 32;
        var possibleCalls = 0;
        var driver = new EngineTestAction("driver",
            possible: _ => possibleCalls++ % 2 == 0,
            prerequisites: [new BotNextAction("prepare", BotRelevance.High)],
            alternatives: [new BotNextAction("fallback", BotRelevance.Default)],
            continuers: [new BotNextAction("follow", BotRelevance.Normal)]);
        var prepare = new EngineTestAction("prepare");
        var fallback = new EngineTestAction("fallback");
        var follow = new EngineTestAction("follow");
        var engine = CreateEngine(new TestStrategy("allocation", [new BotNextAction("driver", BotRelevance.Normal)]),
            [driver, prepare, fallback, follow]);
        var sim = new BotSim();
        var bot = sim.AddBot(1);
        var context = new BotContext(bot.Bot, bot.Runtime, bot.Runtime.Blackboard,
            sim.Time.GetUtcNow().UtcDateTime, new BotConfig(), engine.Kind);

        for (var i = 0; i < 20; i++)
            engine.DoNextAction(context, minimal: false);

        var beforePushes = engine.PushCount;
        var beforeLogEntries = engine.ActionLogCount;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < tickCount; i++)
            engine.DoNextAction(context, minimal: false);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        var countedBaskets = engine.PushCount - beforePushes;
        var countedLogEntries = engine.ActionLogCount - beforeLogEntries;
        var bound = countedBaskets * basketSize + countedLogEntries * actionLogEntrySize;
        Console.WriteLine($"hot-path allocations={allocated} bound={bound} baskets={countedBaskets} basket-size={basketSize} log-entries={countedLogEntries} log-entry-size={actionLogEntrySize} possible-calls={possibleCalls}");

        await Assert.That(allocated).IsLessThanOrEqualTo(bound);
    }

    private static BotEngine CreateEngine(
        TestStrategy strategy,
        IReadOnlyList<IBotAction> actions,
        BotConfig config = null)
    {
        return CreateEngine([strategy], actions, config);
    }

    private static BotEngine CreateEngine(
        IReadOnlyList<TestStrategy> strategies,
        IReadOnlyList<IBotAction> actions,
        BotConfig config = null)
    {
        return new BotEngine(BotEngineKind.Combat, config ?? new BotConfig(), strategies, actions);
    }

    private static BotContext CreateContext(BotEngine engine, BotSim sim = null, DateTime? now = null, BotConfig config = null)
    {
        sim ??= new BotSim();
        var bot = sim.Bots.Count == 0 ? sim.AddBot(1) : sim.Bots[0];
        var contextNow = now ?? sim.Time.GetUtcNow().UtcDateTime;
        return new BotContext(bot.Bot, bot.Runtime, bot.Runtime.Blackboard, contextNow, config ?? new BotConfig(), engine.Kind);
    }
}

internal sealed class EngineTestAction : IBotAction
{
    private readonly Func<BotContext, bool> _useful;
    private readonly Func<BotContext, bool> _possible;
    private readonly Func<BotContext, BotActionResult> _execute;

    public EngineTestAction(
        string name,
        Func<BotContext, bool> useful = null,
        Func<BotContext, bool> possible = null,
        Func<BotContext, BotActionResult> execute = null,
        IReadOnlyList<BotNextAction> prerequisites = null,
        IReadOnlyList<BotNextAction> alternatives = null,
        IReadOnlyList<BotNextAction> continuers = null)
    {
        Name = name;
        _useful = useful ?? (_ => true);
        _possible = possible ?? (_ => true);
        _execute = execute ?? (_ => BotActionResult.Success);
        Prerequisites = prerequisites ?? [];
        Alternatives = alternatives ?? [];
        Continuers = continuers ?? [];
    }

    public string Name { get; }
    public IReadOnlyList<BotNextAction> Prerequisites { get; }
    public IReadOnlyList<BotNextAction> Alternatives { get; }
    public IReadOnlyList<BotNextAction> Continuers { get; }
    public bool IsUseful(BotContext context) => _useful(context);
    public bool IsPossible(BotContext context) => _possible(context);
    public BotActionResult Execute(BotContext context, BotEvent ev) => _execute(context);
}

internal sealed class EngineTestTrigger : IBotTrigger
{
    private readonly Func<BotContext, bool> _active;

    public EngineTestTrigger(string name, int checkIntervalMs, Func<BotContext, bool> active)
    {
        Name = name;
        CheckIntervalMs = checkIntervalMs;
        _active = active;
    }

    public string Name { get; }
    public int CheckIntervalMs { get; }
    public BotEvent Event => new(Name);
    public bool IsActive(BotContext context) => _active(context);
}

internal sealed class EngineTestMultiplier(float value) : IBotMultiplier
{
    public float Value { get; set; } = value;
    public int Calls { get; private set; }

    public float GetValue(IBotAction action, BotContext context)
    {
        Calls++;
        return Value;
    }
}

internal sealed class TestStrategy : IBotStrategy
{
    public TestStrategy(
        string name,
        IReadOnlyList<BotNextAction> defaultActions = null,
        string siblingGroup = null,
        IReadOnlyList<BotTriggerNode> triggers = null,
        IReadOnlyList<IBotMultiplier> multipliers = null)
    {
        Name = name;
        SiblingGroup = siblingGroup;
        DefaultActions = defaultActions ?? [];
        Triggers = triggers ?? [];
        Multipliers = multipliers ?? [];
    }

    public string Name { get; }
    public string SiblingGroup { get; }
    public IReadOnlyList<BotNextAction> DefaultActions { get; }
    public IReadOnlyList<BotTriggerNode> Triggers { get; }
    public IReadOnlyList<IBotMultiplier> Multipliers { get; }
    public int InitTriggersCalls { get; private set; }
    public int InitMultipliersCalls { get; private set; }

    public void InitTriggers(List<BotTriggerNode> triggers)
    {
        InitTriggersCalls++;
        foreach (var trigger in Triggers)
            triggers.Add(trigger);
    }

    public void InitMultipliers(List<IBotMultiplier> multipliers)
    {
        InitMultipliersCalls++;
        foreach (var multiplier in Multipliers)
            multipliers.Add(multiplier);
    }
}
