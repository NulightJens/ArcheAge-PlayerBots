using System.Numerics;
using AAEmu.Game.Bots.Blackboard;
using AAEmu.Game.Bots.Body;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Tasks.Bots;
using AAEmu.UnitTests.Utils.Mocks;
using Microsoft.Extensions.Time.Testing;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace AAEmu.UnitTests.Bots.Host;

[NotInParallel]
public class BotKillCreditTests
{
    [Test]
    public async Task AuthoritativeKill_TwoBotsObserveOneNpc_CreditsOnlyKiller()
    {
        var host = MakeHost();
        var killer = MakeRuntime(10);
        var observer = MakeRuntime(11);
        var victim = MakeNpc(700, 42);
        killer.CombatState.Target = victim;
        observer.CombatState.Target = victim;

        host.Register(killer);
        host.Register(observer);
        try
        {
            killer.Bot.Events.OnKill(victim, new OnKillArgs { Killer = killer.Bot, Victim = victim });

            await Assert.That(killer.CombatState.KillCount).IsEqualTo(1);
            await Assert.That(observer.CombatState.KillCount).IsEqualTo(0);
            await Assert.That(host.Metrics.Snapshot().CreditedKills).IsEqualTo(1L);
        }
        finally
        {
            host.Unregister(killer.Bot.Id);
            host.Unregister(observer.Bot.Id);
        }
    }

    [Test]
    public async Task AuthoritativeKill_RequiresBotKillerEligibleNpcAndMatchingFilter()
    {
        var host = MakeHost();
        var runtime = MakeRuntime(20);
        runtime.CombatState.TargetTypeFilter = 42;
        var other = BotTestFixture.MakeBot(21, Vector3.Zero);

        host.Register(runtime);
        try
        {
            var characterVictim = BotTestFixture.MakeBot(22, Vector3.One);
            characterVictim.Hp = 0;
            runtime.Bot.Events.OnKill(characterVictim,
                new OnKillArgs { Killer = runtime.Bot, Victim = characterVictim });
            var wrongTemplateVictim = MakeNpc(701, 41);
            runtime.Bot.Events.OnKill(wrongTemplateVictim,
                new OnKillArgs { Killer = runtime.Bot, Victim = wrongTemplateVictim });
            var wrongKillerVictim = MakeNpc(702, 42);
            runtime.Bot.Events.OnKill(wrongKillerVictim,
                new OnKillArgs { Killer = other, Victim = wrongKillerVictim });

            await Assert.That(runtime.CombatState.KillCount).IsEqualTo(0);

            var eligibleVictim = MakeNpc(705, 42);
            runtime.Bot.Events.OnKill(eligibleVictim,
                new OnKillArgs { Killer = runtime.Bot, Victim = eligibleVictim });

            await Assert.That(runtime.CombatState.KillCount).IsEqualTo(1);
        }
        finally
        {
            host.Unregister(runtime.Bot.Id);
        }
    }

    [Test]
    public async Task AuthoritativeKill_SummonKillingBlow_DoesNotCreditBotOwner()
    {
        var host = MakeHost();
        var runtime = MakeRuntime(25);
        var summon = new Mate { OwnerObjId = runtime.Bot.ObjId };
        var victim = MakeNpc(706, 42);

        host.Register(runtime);
        try
        {
            summon.Events.OnKill(victim, new OnKillArgs { Killer = summon, Victim = victim });

            await Assert.That(runtime.CombatState.KillCount).IsEqualTo(0);
        }
        finally
        {
            host.Unregister(runtime.Bot.Id);
        }
    }

    [Test]
    public async Task AuthoritativeKill_MalformedAndDuplicateDelivery_CreditsVictimOnce()
    {
        var host = MakeHost();
        var runtime = MakeRuntime(30);
        var victim = MakeNpc(710, 42);

        host.Register(runtime);
        try
        {
            runtime.Bot.Events.OnKill(runtime.Bot, new OnKillArgs { Target = runtime.Bot });
            var args = new OnKillArgs { Killer = runtime.Bot, Victim = victim };
            runtime.Bot.Events.OnKill(victim, args);
            runtime.Bot.Events.OnKill(victim, args);

            await Assert.That(runtime.CombatState.KillCount).IsEqualTo(1);
        }
        finally
        {
            host.Unregister(runtime.Bot.Id);
        }
    }

    [Test]
    public async Task RegisterUnregisterAndReset_DoNotRetainOrDuplicateKillSubscription()
    {
        var host = MakeHost();
        var runtime = MakeRuntime(40);
        var baselineHandlers = runtime.Bot.Events.OnKill.GetInvocationList().Length;
        var manager = MakeCombatManager(host);

        host.Register(runtime);
        host.Register(runtime);
        manager.StartListening(runtime.Bot);
        try
        {
            var registeredHandlers = runtime.Bot.Events.OnKill.GetInvocationList().Length;
            manager.ResetCombat(runtime.Bot);
            manager.ResetCombat(runtime.Bot);

            await Assert.That(registeredHandlers).IsEqualTo(baselineHandlers + 1);
            await Assert.That(runtime.Bot.Events.OnKill.GetInvocationList().Length).IsEqualTo(registeredHandlers);

            host.Unregister(runtime.Bot.Id);
            await Assert.That(runtime.Bot.Events.OnKill.GetInvocationList().Length).IsEqualTo(baselineHandlers);

            host.Register(runtime);
            var victim = MakeNpc(720, 42);
            runtime.Bot.Events.OnKill(victim, new OnKillArgs { Killer = runtime.Bot, Victim = victim });

            await Assert.That(runtime.Bot.Events.OnKill.GetInvocationList().Length).IsEqualTo(baselineHandlers + 1);
            await Assert.That(runtime.CombatState.KillCount).IsEqualTo(1);
        }
        finally
        {
            manager.StopListening(runtime.Bot);
            host.Unregister(runtime.Bot.Id);
        }

        await Assert.That(runtime.Bot.Events.OnKill.GetInvocationList().Length).IsEqualTo(baselineHandlers);
    }

    [Test]
    public async Task DuplicateAuthoritativeKill_CompletesGoalExactlyOnceAndLeavesBotStopped()
    {
        var host = MakeHost();
        var runtime = MakeRuntime(50);
        BotTestFixture.SetPrivateField(runtime.Bot, "_parentWorld", BotTestFixture.MakeWorld());
        runtime.CombatState.IsActive = true;
        runtime.CombatState.CurrentState = BotCombatStateType.Grinding;
        runtime.CombatState.ForcedState = BotCombatStateType.Grinding;
        runtime.CombatState.InDuel = true;
        runtime.CombatState.KillGoal = 1;
        var victim = MakeNpc(730, 42);
        var blackboard = WorldValues.Create(runtime.Bot, (_, _) => [], (_, _) => [], config: new BotConfig());
        var task = new BotCombatTask(
            runtime.Bot,
            runtime.CombatState,
            new BotMovementBroadcaster(runtime.Bot),
            onCancel: null,
            blackboard: blackboard);
        var logs = new MemoryTarget { Layout = "${message}" };
        var previousConfiguration = LogManager.Configuration;
        var configuration = new LoggingConfiguration();
        configuration.LoggingRules.Add(new LoggingRule(
            "AAEmu.Game.Models.Tasks.Bots.BotCombatTask",
            LogLevel.Trace,
            logs));
        LogManager.Configuration = configuration;
        LogManager.ReconfigExistingLoggers();

        host.Register(runtime);
        try
        {
            var args = new OnKillArgs { Killer = runtime.Bot, Victim = victim };
            runtime.Bot.Events.OnKill(victim, args);
            runtime.Bot.Events.OnKill(victim, args);
            task.Step();
            task.Step();

            await Assert.That(runtime.CombatState.KillCount).IsEqualTo(1);
            await Assert.That(logs.Logs.Count(message => message.Contains("ev=kill_goal"))).IsEqualTo(1);
            await Assert.That(runtime.CombatState.KillGoal).IsNull();
            await Assert.That(runtime.CombatState.ForcedState).IsNull();
            await Assert.That(runtime.CombatState.CurrentState).IsEqualTo(BotCombatStateType.Idle);
            await Assert.That(runtime.CombatState.IsActive).IsFalse();
        }
        finally
        {
            host.Unregister(runtime.Bot.Id);
            LogManager.Configuration = previousConfiguration;
            LogManager.ReconfigExistingLoggers();
        }
    }

    private static BotHost MakeHost()
    {
        var taskManager = Mock.Of<ITaskManager>();
        taskManager.Schedule(Any<AAEmu.Game.Models.Tasks.Task>(), Any<TimeSpan?>(), Any<TimeSpan?>(), Any<int>())
            .Returns(true);
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 8, 27, 12, 0, 0, TimeSpan.Zero));
        return new BotHost(taskManager.Object, time);
    }

    private static BotCombatManager MakeCombatManager(BotHost host)
    {
        var taskManager = Mock.Of<ITaskManager>();
        var duelManager = Mock.Of<IDuelManager>();
        var botManager = Mock.Of<IBotManager>();
        var archetypeManager = Mock.Of<IBotArchetypeManager>();
        return new BotCombatManager(
            taskManager.Object,
            duelManager.Object,
            new Lazy<IBotManager>(() => botManager.Object),
            archetypeManager.Object,
            host);
    }

    private static BotRuntime MakeRuntime(uint id)
    {
        var bot = BotTestFixture.MakeBot(id, Vector3.Zero);
        bot.Hp = 100;
        bot.MaxHp = 100;
        var state = new BotCombatState { BotId = id };
        return new BotRuntime(bot, new BotMovementState(), state, config: new BotConfig { UseEngine = false });
    }

    private static Npc MakeNpc(uint objId, uint templateId)
    {
        return new Npc
        {
            ObjId = objId,
            TemplateId = templateId,
            Hp = 0,
            MaxHp = 100
        };
    }
}
