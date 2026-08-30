using System.Numerics;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Bots.Blackboard;
using AAEmu.Game.Bots.Content.Strategies;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Models.Game.DoodadObj;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.World;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.UnitTests.Bots.Sim;
using AAEmu.UnitTests.Utils.Mocks;

namespace AAEmu.UnitTests.Bots.Kernel;

[NotInParallel]
public class LegacyBridgeTests
{
    [Before(Test)]
    public void ResetContent()
    {
        BotTestFixture.ResetBotContentRegistry();
    }

    [Test]
    public async Task Runtime_ExposesThreeEngineSlots_WithLegacyAndCombatEngines()
    {
        var sim = new BotSim();
        var bot = sim.AddBot(1);

        await Assert.That(bot.Runtime.Engines).Count().IsEqualTo(3);
        await Assert.That(bot.Runtime.Engines[(int)BotEngineKind.Combat]).IsNotNull();
        await Assert.That(bot.Runtime.Engines[(int)BotEngineKind.Combat].HasStrategy("combat-base")).IsTrue();
        await Assert.That(bot.Runtime.Engines[(int)BotEngineKind.NonCombat]).IsNotNull();
        await Assert.That(bot.Runtime.Engines[(int)BotEngineKind.NonCombat].HasStrategy("legacy")).IsTrue();
        await Assert.That(bot.Runtime.Engines[(int)BotEngineKind.Dead]).IsNull();
    }

    [Test]
    public async Task CombatEngine_ContainsBaseTriggers()
    {
        var sim = new BotSim();
        var bot = sim.AddBot(1);
        var triggerNames = bot.Runtime.Engines[(int)BotEngineKind.Combat].TriggerNodes
            .Select(node => node.Trigger.Name);

        await Assert.That(triggerNames).IsEquivalentTo([
            "in-hostile-area",
            "not-facing-target",
            "stuck",
            "target-invalid",
            "target-stealthed"]);
    }

    [Test]
    public async Task BotSim_CombatTickInsideHostileArea_ExecutesAvoidHazard()
    {
        var config = BotConfig.Instance;
        var previousUseEngine = config.UseEngine;
        var previousPercent = config.ActivityPercent;
        try
        {
            config.UseEngine = true;
            config.ActivityPercent = 100;
            var sim = new BotSim();
            var bot = sim.AddBot(1, BotCombatStateType.Combat);
            AddHostileSphere(bot);
            bot.Runtime.Schedule.NextBrainAt = sim.Time.GetUtcNow().UtcDateTime;

            sim.Tick();

            var engine = bot.Runtime.Engines[(int)BotEngineKind.Combat];
            await Assert.That(engine.LastActionLog.Any(log => log.Action == "avoid-hazard" &&
                log.Result == BotActionResult.Success)).IsTrue();
        }
        finally
        {
            config.UseEngine = previousUseEngine;
            config.ActivityPercent = previousPercent;
        }
    }

    [Test]
    public async Task BotSim_GrindingTickInsideHostileArea_ExecutesAvoidHazardOnNonCombatEngine()
    {
        var config = BotConfig.Instance;
        var previousUseEngine = config.UseEngine;
        var previousPercent = config.ActivityPercent;
        try
        {
            config.UseEngine = true;
            config.ActivityPercent = 100;
            var sim = new BotSim();
            var bot = sim.AddBot(1, BotCombatStateType.Grinding);
            AddHostileSphere(bot);
            bot.Runtime.Schedule.NextBrainAt = sim.Time.GetUtcNow().UtcDateTime;

            sim.Tick();

            var engine = bot.Runtime.Engines[(int)BotEngineKind.NonCombat];
            await Assert.That(engine.LastActionLog.Any(log => log.Action == "avoid-hazard" &&
                log.Result == BotActionResult.Success)).IsTrue();
        }
        finally
        {
            config.UseEngine = previousUseEngine;
            config.ActivityPercent = previousPercent;
        }
    }

    [Test]
    public async Task BotSim_InescapableHostileArea_AllowsLegacyTickBySecondBrainTick()
    {
        var config = BotConfig.Instance;
        var previousUseEngine = config.UseEngine;
        var previousPercent = config.ActivityPercent;
        var previousCombatDelay = config.ReactDelayCombatMs;
        var previousMovingDelay = config.ReactDelayMovingMs;
        var previousIdleMin = config.ReactDelayIdleMinMs;
        var previousIdleMax = config.ReactDelayIdleMaxMs;
        var previousStuckSeconds = config.StuckSeconds;
        BotSim sim = null;
        try
        {
            config.UseEngine = true;
            config.ActivityPercent = 100;
            config.ReactDelayCombatMs = 100;
            config.ReactDelayMovingMs = 100;
            config.ReactDelayIdleMinMs = 100;
            config.ReactDelayIdleMaxMs = 100;
            config.StuckSeconds = 60;
            sim = new BotSim();
            var bot = sim.AddBot(1, BotCombatStateType.Grinding, runLegacyBrain: true);
            AddHostileSphere(bot, radius: float.MaxValue);
            bot.Runtime.Schedule.NextBrainAt = sim.Time.GetUtcNow().UtcDateTime;

            sim.Advance(2000);

            await Assert.That(bot.Brain.FullStepTimes.Count).IsGreaterThanOrEqualTo(2);
            var secondBrainTick = bot.Brain.FullStepTimes[1];

            await Assert.That(bot.Runtime.Engines[(int)BotEngineKind.NonCombat].LastActionLog.Any(log =>
                log.Time <= secondBrainTick && log.Action == "legacy tick" && log.Result == BotActionResult.Success)).IsTrue();
        }
        finally
        {
            sim?.Reset();
            config.UseEngine = previousUseEngine;
            config.ActivityPercent = previousPercent;
            config.ReactDelayCombatMs = previousCombatDelay;
            config.ReactDelayMovingMs = previousMovingDelay;
            config.ReactDelayIdleMinMs = previousIdleMin;
            config.ReactDelayIdleMaxMs = previousIdleMax;
            config.StuckSeconds = previousStuckSeconds;
        }
    }

    [Test]
    public async Task BotSim_InescapableHostileArea_WithStuckTrigger_AllowsLegacyTickBySecondBrainTick()
    {
        var config = BotConfig.Instance;
        var previousUseEngine = config.UseEngine;
        var previousPercent = config.ActivityPercent;
        var previousCombatDelay = config.ReactDelayCombatMs;
        var previousMovingDelay = config.ReactDelayMovingMs;
        var previousIdleMin = config.ReactDelayIdleMinMs;
        var previousIdleMax = config.ReactDelayIdleMaxMs;
        var previousStuckSeconds = config.StuckSeconds;
        BotSim sim = null;
        try
        {
            config.UseEngine = true;
            config.ActivityPercent = 100;
            config.ReactDelayCombatMs = 100;
            config.ReactDelayMovingMs = 100;
            config.ReactDelayIdleMinMs = 100;
            config.ReactDelayIdleMaxMs = 100;
            config.StuckSeconds = 0.3;
            sim = new BotSim();
            var bot = sim.AddBot(2, BotCombatStateType.Grinding, runLegacyBrain: true);
            AddHostileSphere(bot, radius: float.MaxValue);
            bot.Runtime.Schedule.NextBrainAt = sim.Time.GetUtcNow().UtcDateTime;

            sim.Advance(2000);

            await Assert.That(bot.Brain.FullStepTimes.Count).IsGreaterThanOrEqualTo(2);
            var secondBrainTick = bot.Brain.FullStepTimes[1];
            await Assert.That(bot.Runtime.Engines[(int)BotEngineKind.NonCombat].LastActionLog.Any(log =>
                log.Time <= secondBrainTick && log.Action == "legacy tick" && log.Result == BotActionResult.Success)).IsTrue();
        }
        finally
        {
            sim?.Reset();
            config.UseEngine = previousUseEngine;
            config.ActivityPercent = previousPercent;
            config.ReactDelayCombatMs = previousCombatDelay;
            config.ReactDelayMovingMs = previousMovingDelay;
            config.ReactDelayIdleMinMs = previousIdleMin;
            config.ReactDelayIdleMaxMs = previousIdleMax;
            config.StuckSeconds = previousStuckSeconds;
        }
    }

    [Test]
    public async Task BotSim_BodyActionsUseRuntimeScopedMover()
    {
        var config = BotConfig.Instance;
        var previousUseEngine = config.UseEngine;
        var previousPercent = config.ActivityPercent;
        try
        {
            config.UseEngine = true;
            config.ActivityPercent = 100;
            var sim = new BotSim();
            var bot = sim.AddBot(901, BotCombatStateType.Grinding);
            AddHostileSphere(bot);
            bot.Runtime.Schedule.NextBrainAt = sim.Time.GetUtcNow().UtcDateTime;

            sim.Tick();

            await Assert.That(bot.Runtime.MovementState.Destination).IsNotNull();
        }
        finally
        {
            config.UseEngine = previousUseEngine;
            config.ActivityPercent = previousPercent;
        }
    }

    [Test]
    public async Task BotSim_StuckBot_ReachesUnstickWithinAdvanceWindow()
    {
        var config = BotConfig.Instance;
        var previousUseEngine = config.UseEngine;
        var previousPercent = config.ActivityPercent;
        var previousStuckSeconds = config.StuckSeconds;
        try
        {
            config.UseEngine = true;
            config.ActivityPercent = 100;
            config.StuckSeconds = 0.3;
            var sim = new BotSim();
            var bot = sim.AddBot(1, BotCombatStateType.Grinding);
            var target = BotTestFixture.MakeBot(2, new Vector3(10, 0, 0));
            var world = BotTestFixture.MakeWorld();
            BotTestFixture.SetPrivateField(target, "_parentWorld", world);
            BotTestFixture.SetPrivateField(bot.Bot, "_parentWorld", world);
            bot.Runtime.CombatState.Target = target;
            bot.Runtime.MovementState.Destination = new Vector3(10, 0, 0);
            bot.Runtime.Schedule.NextBrainAt = sim.Time.GetUtcNow().UtcDateTime;

            sim.Advance(1000);

            await Assert.That(bot.Runtime.Engines[(int)BotEngineKind.NonCombat].LastActionLog.Any(log =>
                log.Action == "unstick" && log.Result == BotActionResult.Success)).IsTrue();
        }
        finally
        {
            config.UseEngine = previousUseEngine;
            config.ActivityPercent = previousPercent;
            config.StuckSeconds = previousStuckSeconds;
        }
    }

    [Test]
    public async Task BotSim_StepsOnlyStateSelectedEnginePerTick()
    {
        var config = BotConfig.Instance;
        var previousUseEngine = config.UseEngine;
        var previousPercent = config.ActivityPercent;
        try
        {
            config.UseEngine = true;
            config.ActivityPercent = 100;
            var sim = new BotSim();
            var combat = sim.AddBot(1, BotCombatStateType.Combat);
            var nonCombat = sim.AddBot(2, BotCombatStateType.Grinding);
            var invalidCombatTarget = BotTestFixture.MakeBot(3, new Vector3(10, 0, 0));
            var invalidNonCombatTarget = BotTestFixture.MakeBot(4, new Vector3(10, 0, 0));
            invalidCombatTarget.Hp = 0;
            invalidNonCombatTarget.Hp = 0;
            combat.Runtime.CombatState.Target = invalidCombatTarget;
            nonCombat.Runtime.CombatState.Target = invalidNonCombatTarget;
            combat.Runtime.Schedule.NextBrainAt = sim.Time.GetUtcNow().UtcDateTime;
            nonCombat.Runtime.Schedule.NextBrainAt = sim.Time.GetUtcNow().UtcDateTime;
            var combatEngine = combat.Runtime.Engines[(int)BotEngineKind.Combat];
            var combatNonCombatEngine = combat.Runtime.Engines[(int)BotEngineKind.NonCombat];
            var nonCombatEngine = nonCombat.Runtime.Engines[(int)BotEngineKind.NonCombat];
            var nonCombatCombatEngine = nonCombat.Runtime.Engines[(int)BotEngineKind.Combat];

            sim.Tick();

            await Assert.That(combatEngine.ActionLogCount).IsGreaterThan(0);
            await Assert.That(combatNonCombatEngine.ActionLogCount).IsEqualTo(0);
            await Assert.That(nonCombatEngine.ActionLogCount).IsGreaterThan(0);
            await Assert.That(nonCombatCombatEngine.ActionLogCount).IsEqualTo(0);
        }
        finally
        {
            config.UseEngine = previousUseEngine;
            config.ActivityPercent = previousPercent;
        }
    }

    private static void AddHostileSphere(BotSim.SimBot bot, float radius = 5f)
    {
        var target = BotTestFixture.MakeBot(2, new Vector3(10, 0, 0));
        target.Hp = 100;
        target.MaxHp = 100;
        var world = BotTestFixture.MakeWorld();
        BotTestFixture.SetPrivateField(target, "_parentWorld", world);
        BotTestFixture.SetPrivateField(bot.Bot, "_parentWorld", world);
        bot.Bot.Transform.Local.SetRotationDegree(0f, 0f, -90f);
        bot.Runtime.CombatState.Target = target;

        var owner = new Doodad();
        owner.Transform.Local.SetPosition(System.Numerics.Vector3.Zero);
        var hazard = new AreaTrigger
        {
            Owner = owner,
            Caster = bot.Bot,
            Shape = new AreaShape { Type = AreaShapeType.Sphere, Value1 = radius },
            TargetRelation = SkillTargetRelation.Hostile
        };
        var blackboard = bot.Runtime.Blackboard;
        blackboard.Register(
            BotValues.HostileAreaTriggersNearby,
            HazardValues.Create(bot.Bot, () => [hazard], TimeSpan.Zero));
    }

    [Test]
    public async Task UseEngine_ActiveTickRunsLegacyBrainThroughEngine()
    {
        var config = BotConfig.Instance;
        var previousUseEngine = config.UseEngine;
        var previousPercent = config.ActivityPercent;
        try
        {
            config.UseEngine = true;
            config.ActivityPercent = 100;
            var sim = new BotSim();
            var bot = sim.AddBot(1);
            bot.Runtime.Schedule.NextBrainAt = sim.Time.GetUtcNow().UtcDateTime;

            sim.Tick();

            await Assert.That(bot.Brain.FullStepTimes).Count().IsEqualTo(1);
            await Assert.That(bot.Brain.MinimalStepTimes).IsEmpty();
            await Assert.That(bot.Runtime.Engines[(int)BotEngineKind.NonCombat].LastActionLog[^1].Action)
                .IsEqualTo("legacy tick");
        }
        finally
        {
            config.UseEngine = previousUseEngine;
            config.ActivityPercent = previousPercent;
        }
    }

    [Test]
    public async Task UseEngine_InactiveTickRunsMinimalLegacyBrainBeforeEngine()
    {
        var config = BotConfig.Instance;
        var previousUseEngine = config.UseEngine;
        var previousPercent = config.ActivityPercent;
        try
        {
            config.UseEngine = true;
            config.ActivityPercent = 0;
            var sim = new BotSim();
            var bot = sim.AddBot(1);
            bot.Runtime.Schedule.NextBrainAt = sim.Time.GetUtcNow().UtcDateTime;

            sim.Tick();

            await Assert.That(bot.Brain.FullStepTimes).IsEmpty();
            await Assert.That(bot.Brain.MinimalStepTimes).Count().IsEqualTo(1);
            await Assert.That(bot.Runtime.Engines[(int)BotEngineKind.NonCombat].LastActionLog).IsEmpty();
        }
        finally
        {
            config.UseEngine = previousUseEngine;
            config.ActivityPercent = previousPercent;
        }
    }

    [Test]
    public async Task UseEngine_CapturesBrainBeforeDetach_AndStepsCapturedBrainOnce()
    {
        var config = BotConfig.Instance;
        var previousUseEngine = config.UseEngine;
        var previousPercent = config.ActivityPercent;
        try
        {
            config.UseEngine = true;
            config.ActivityPercent = 100;
            var sim = new BotSim();
            var bot = sim.AddBot(1);
            var engine = bot.Runtime.Engines[(int)BotEngineKind.NonCombat];
            var gate = new CaptureGateAction();
            engine.RegisterAction(gate);
            engine.AddStrategy(new TestStrategy("capture gate", [new BotNextAction("capture gate", BotRelevance.High)]));
            bot.Runtime.Schedule.NextBrainAt = sim.Time.GetUtcNow().UtcDateTime;

            var tick = System.Threading.Tasks.Task.Run(sim.Host.HostTask.Execute);
            await Assert.That(gate.Entered.Wait(TimeSpan.FromSeconds(2))).IsTrue();
            lock (bot.Runtime.SyncRoot)
                bot.Runtime.Brain = null;
            gate.Release.Set();
            await System.Threading.Tasks.Task.WhenAll(tick);

            await Assert.That(bot.Brain.FullStepTimes).Count().IsEqualTo(1);
            await Assert.That(bot.Runtime.CombatState.Diagnostics.LastError).IsNull();
            await Assert.That(bot.Runtime.Metrics.BrainSteps).IsEqualTo(1L);
        }
        finally
        {
            config.UseEngine = previousUseEngine;
            config.ActivityPercent = previousPercent;
        }
    }

    [Test]
    public async Task UseEngine_DefaultsToTrue()
    {
        var config = new BotConfig();

        await Assert.That(config.UseEngine).IsTrue();
    }

    [Test]
    public async Task Runtime_UsesInjectedConfig_AndSkipsEngineWhenDisabled()
    {
        var disabledBot = BotTestFixture.MakeBot(2, System.Numerics.Vector3.Zero);
        var disabled = new BotRuntime(
            disabledBot,
            new BotMovementState(),
            new BotCombatState(),
            config: new BotConfig { UseEngine = false });
        var enabledBot = BotTestFixture.MakeBot(3, System.Numerics.Vector3.Zero);
        var enabled = new BotRuntime(
            enabledBot,
            new BotMovementState(),
            new BotCombatState(),
            config: new BotConfig { UseEngine = true });

        await Assert.That(disabled.Engines.All(engine => engine == null)).IsTrue();
        await Assert.That(enabled.Engines[(int)BotEngineKind.NonCombat]).IsNotNull();
    }

    [Test]
    public async Task UseEngine_OnAndOff_KeepThirtySecondFiveBotCadenceEquivalent()
    {
        var config = BotConfig.Instance;
        var previousUseEngine = config.UseEngine;
        var previousPercent = config.ActivityPercent;
        try
        {
            foreach (var activityPercent in new[] { 100, 50, 0 })
            {
                config.ActivityPercent = activityPercent;
                var engineRun = RunSimulation(config, true);
                var legacyRun = RunSimulation(config, false);

                await Assert.That(engineRun).IsEquivalentTo(legacyRun);
                if (activityPercent == 0)
                    await Assert.That(engineRun.All(bot => bot.minimal > 0 && bot.full == 0)).IsTrue();
                else if (activityPercent == 50)
                    await Assert.That(engineRun.Any(bot => bot.minimal > 0)).IsTrue();
            }
        }
        finally
        {
            config.UseEngine = previousUseEngine;
            config.ActivityPercent = previousPercent;
        }
    }

    private static List<(int full, int minimal, List<TimeSpan> cadence)> RunSimulation(BotConfig config, bool useEngine)
    {
        config.UseEngine = useEngine;
        var sim = new BotSim(12345);
        for (uint id = 1; id <= 5; id++)
            sim.AddBot(id);

        sim.Advance(30000);

        return sim.Bots.Select(bot =>
        {
            var times = bot.Brain.FullStepTimes;
            return (
                times.Count,
                bot.Brain.MinimalStepTimes.Count,
                times.Zip(times.Skip(1), (first, second) => second - first).ToList());
        }).ToList();
    }

    private sealed class CaptureGateAction : IBotAction
    {
        public ManualResetEventSlim Entered { get; } = new(false);
        public ManualResetEventSlim Release { get; } = new(false);
        public string Name => "capture gate";
        public bool IsUseful(BotContext context)
        {
            Entered.Set();
            Release.Wait();
            return false;
        }

        public bool IsPossible(BotContext context) => true;
        public BotActionResult Execute(BotContext context, BotEvent ev) => BotActionResult.Success;
    }
}
