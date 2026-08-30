using System.Numerics;
using AAEmu.Game.Bots.Body;
using AAEmu.Game.Bots.Body.Positioning;
using AAEmu.Game.Bots.Content.Triggers;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.UnitTests.Bots.Body.Positioning;
using AAEmu.UnitTests.Utils.Mocks;

namespace AAEmu.UnitTests.Bots.Body;

public class BotStuckWatchTests
{
    private static readonly DateTime Now = new(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);

    [Test]
    public async Task Update_RequiresDestinationAndThreeSecondsWithoutProgress()
    {
        var state = new BotMovementState { Destination = new Vector3(10, 0, 0) };
        var watch = new BotStuckWatch(state, new BotConfig { StuckMinMeters = 0.3, StuckSeconds = 3 });

        await Assert.That(watch.Update(Now, Vector3.Zero, true)).IsFalse();
        await Assert.That(watch.Update(Now.AddSeconds(2.9), Vector3.Zero, true)).IsFalse();
        await Assert.That(watch.Update(Now.AddSeconds(3), Vector3.Zero, true)).IsTrue();
    }

    [Test]
    public async Task Update_RealProgress_ResetsAttemptsAndAge()
    {
        var state = new BotMovementState { Destination = new Vector3(10, 0, 0), Attempts = 3 };
        var watch = new BotStuckWatch(state, new BotConfig { StuckMinMeters = 0.3, StuckSeconds = 3 });
        watch.Update(Now, Vector3.Zero, true);
        watch.Update(Now.AddSeconds(3), Vector3.Zero, true);

        var stuck = watch.Update(Now.AddSeconds(3.1), new Vector3(1, 0, 0), true);

        await Assert.That(stuck).IsFalse();
        await Assert.That(state.Attempts).IsEqualTo(0);
        await Assert.That(state.LastPos).IsEqualTo(new Vector3(1, 0, 0));
        await Assert.That(state.LastMoveAt).IsEqualTo(Now.AddSeconds(3.1));
    }

    [Test]
    public async Task Unstick_NudgesAlternatingSidesThenTeleportsAndResets()
    {
        var mover = new RecordingMover();
        var state = new BotMovementState { Destination = new Vector3(10, 0, 0) };
        var config = new BotConfig { StuckMinMeters = 0.3, StuckSeconds = 3, StuckNudgeMeters = 2, StuckTeleportAttempts = 5 };
        var watch = new BotStuckWatch(state, config);
        watch.Update(Now, Vector3.Zero, true);
        watch.Update(Now.AddSeconds(3), Vector3.Zero, true);
        var context = CreateContext(state, config, Now.AddSeconds(3));
        var metrics = new BotHostMetrics();
        context.Runtime.HostMetrics = metrics;
        var action = new UnstickAction(watch, mover, config);

        for (var i = 0; i < 4; i++)
        {
            await Assert.That(action.Execute(context, default)).IsEqualTo(AAEmu.Game.Bots.Kernel.BotActionResult.Success);
        }

        await Assert.That(mover.Destinations[0].Position).IsEqualTo(new Vector3(0, 2, 0));
        await Assert.That(mover.Destinations[1].Position).IsEqualTo(new Vector3(0, -2, 0));
        await Assert.That(mover.Destinations[2].Position).IsEqualTo(new Vector3(0, 2, 0));
        await Assert.That(mover.Destinations[3].Position).IsEqualTo(new Vector3(0, -2, 0));

        await Assert.That(action.Execute(context, default)).IsEqualTo(AAEmu.Game.Bots.Kernel.BotActionResult.Success);

        await Assert.That(mover.Teleports.Single()).IsEqualTo(new Vector3(10, 0, 0));
        await Assert.That(state.Attempts).IsEqualTo(0);
        await Assert.That(state.LastPos).IsEqualTo(Vector3.Zero);
        await Assert.That(metrics.Snapshot().StuckNudges).IsEqualTo(4L);
        await Assert.That(metrics.Snapshot().StuckTeleports).IsEqualTo(1L);
    }

    [Test]
    public async Task StuckTrigger_IsPureAndDoesNotAdvanceAnInitialisedStuckWatch()
    {
        var state = new BotMovementState
        {
            Destination = new Vector3(10, 0, 0),
            Attempts = 3
        };
        var bot = BotTestFixture.MakeBot(1, Vector3.Zero);
        var runtime = new BotRuntime(bot, state, new BotCombatState(),
            config: new BotConfig { UseEngine = false, StuckSeconds = 3 });
        var context = new BotContext(bot, runtime, runtime.Blackboard, Now.AddSeconds(3), new BotConfig(), BotEngineKind.Combat);
        var trigger = new StuckTrigger();
        var watch = runtime.StuckWatch;
        watch.Update(Now, Vector3.Zero, true);
        watch.Update(Now.AddSeconds(3), Vector3.Zero, true);
        var lastPosition = state.LastPos;
        var lastMoveAt = state.LastMoveAt;
        var attempts = state.Attempts;

        await Assert.That(trigger.IsActive(context)).IsTrue();
        await Assert.That(trigger.IsActive(context)).IsTrue();
        await Assert.That(state.Attempts).IsEqualTo(attempts);
        await Assert.That(state.LastPos).IsEqualTo(lastPosition);
        await Assert.That(state.LastMoveAt).IsEqualTo(lastMoveAt);
    }

    private static BotContext CreateContext(BotMovementState state, BotConfig config, DateTime now)
    {
        var bot = BotTestFixture.MakeBot(1, Vector3.Zero);
        bot.IsBot = true;
        bot.Hp = 100;
        bot.MaxHp = 100;
        var runtime = new BotRuntime(bot, state, new BotCombatState(), config: new BotConfig { UseEngine = false });
        return new BotContext(bot, runtime, runtime.Blackboard, now, config, AAEmu.Game.Bots.Kernel.BotEngineKind.Combat);
    }
}
