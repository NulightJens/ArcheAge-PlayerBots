using System.Numerics;
using AAEmu.Game.Bots.Body;
using AAEmu.Game.Bots.Content.Actions;
using AAEmu.Game.Bots.Content.Strategies;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Units;
using AAEmu.UnitTests.Utils.Mocks;
using Microsoft.Extensions.Time.Testing;

namespace AAEmu.UnitTests.Bots.Content;

[NotInParallel]
public sealed class StrategyEquivalenceTests
{
    private static readonly DateTime s_now = new(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);

    private const string RecordedFollowTrace = """
0:hp=100
relaxed-stance
1:hp=100
2:hp=100
3:hp=100
4:hp=100
5:hp=100
6:hp=100
7:hp=100
8:hp=100
9:hp=100
10:hp=100
11:hp=100
12:hp=100
13:hp=100
14:hp=100
15:hp=100
16:hp=100
17:hp=100
18:hp=100
19:hp=100
20:state=Following->Combat
20:hp=100
ev=assist target=1003
21:hp=100
22:hp=100
23:hp=100
24:hp=100
25:hp=100
ev=defend target=1004 state=following
26:hp=100
ev=assist target=1003
27:hp=100
28:hp=100
29:hp=100
30:hp=100
31:hp=100
32:hp=100
33:hp=100
34:hp=100
35:hp=100
36:hp=100
37:hp=100
38:hp=100
39:hp=100
40:hp=100
41:hp=100
42:hp=100
43:hp=100
44:hp=100
45:hp=100
46:state=Combat->Following
46:hp=100
stop-immediately
relaxed-stance
relaxed-stance
47:hp=100
48:hp=100
49:hp=100
50:hp=100
51:hp=100
52:hp=100
53:hp=100
54:hp=100
55:hp=100
56:hp=100
57:hp=100
58:hp=100
59:hp=100
""";

    private const string RecordedRestTrace = """
0:hp=50
stop-if-moving
1:hp=60
stop-if-moving
2:hp=70
stop-if-moving
3:hp=80
stop-if-moving
4:hp=90
stop-if-moving
5:state=Resting->Idle
5:hp=100
stop-if-moving
ev=rest_complete
""";

    private const string RecordedSearchTrace = """
0:hp=0
destination:1.9775,0.2989,0.0000:run=True:tol=0.5
1:hp=0
destination:3.2481,1.0048,0.0000:run=True:tol=0.5
2:hp=0
destination:4.3221,2.0878,0.0000:run=True:tol=0.5
3:hp=0
destination:5.1171,3.5008,0.0000:run=True:tol=0.5
4:hp=0
destination:5.5608,5.1805,0.0000:run=True:tol=0.5
5:hp=0
destination:5.5945,7.0499,0.0000:run=True:tol=0.5
6:hp=0
destination:5.1747,9.0212,0.0000:run=True:tol=0.5
7:hp=0
destination:4.2758,10.9981,0.0000:run=True:tol=0.5
8:hp=0
destination:2.8909,12.8795,0.0000:run=True:tol=0.5
9:hp=0
destination:1.0328,14.5634,0.0000:run=True:tol=0.5
10:hp=0
destination:-1.2659,15.9498,0.0000:run=True:tol=0.5
11:hp=0
destination:-3.9533,16.9450,0.0000:run=True:tol=0.5
12:hp=0
destination:-6.9594,17.4644,0.0000:run=True:tol=0.5
13:hp=0
destination:-10.1979,17.4368,0.0000:run=True:tol=0.5
14:hp=0
destination:-13.5686,16.8064,0.0000:run=True:tol=0.5
15:hp=0
destination:-16.9601,15.5357,0.0000:run=True:tol=0.5
16:hp=0
destination:-20.2533,13.6075,0.0000:run=True:tol=0.5
17:hp=0
destination:-23.3251,11.0264,0.0000:run=True:tol=0.5
18:hp=0
destination:-26.0518,7.8194,0.0000:run=True:tol=0.5
19:hp=0
destination:-28.3138,4.0360,0.0000:run=True:tol=0.5
20:hp=0
destination:-29.9989,-0.2522,0.0000:run=True:tol=0.5
21:hp=0
destination:-29.6244,-4.7324,0.0000:run=True:tol=0.5
22:hp=0
destination:-28.5845,-9.1063,0.0000:run=True:tol=0.5
23:hp=0
destination:-26.9027,-13.2756,0.0000:run=True:tol=0.5
24:hp=0
destination:-24.6168,-17.1469,0.0000:run=True:tol=0.5
25:hp=0
destination:-21.7779,-20.6330,0.0000:run=True:tol=0.5
26:hp=0
destination:-18.4500,-23.6558,0.0000:run=True:tol=0.5
27:hp=0
destination:-14.7078,-26.1473,0.0000:run=True:tol=0.5
28:hp=0
destination:-10.6352,-28.0516,0.0000:run=True:tol=0.5
29:hp=0
destination:-6.3238,-29.3259,0.0000:run=True:tol=0.5
30:hp=0
destination:-1.8704,-29.9416,0.0000:run=True:tol=0.5
31:hp=0
destination:2.6250,-29.8849,0.0000:run=True:tol=0.5
32:hp=0
destination:7.0615,-29.1571,0.0000:run=True:tol=0.5
33:hp=0
destination:11.3394,-27.7744,0.0000:run=True:tol=0.5
34:hp=0
destination:15.3626,-25.7680,0.0000:run=True:tol=0.5
35:hp=0
destination:19.0408,-23.1829,0.0000:run=True:tol=0.5
36:hp=0
destination:22.2914,-20.0771,0.0000:run=True:tol=0.5
37:hp=0
destination:25.0414,-16.5205,0.0000:run=True:tol=0.5
38:hp=0
destination:27.2290,-12.5929,0.0000:run=True:tol=0.5
39:hp=0
destination:28.8051,-8.3824,0.0000:run=True:tol=0.5
40:hp=0
destination:29.7343,-3.9837,0.0000:run=True:tol=0.5
41:hp=0
destination:29.9958,0.5045,0.0000:run=True:tol=0.5
42:hp=0
destination:29.5835,4.9813,0.0000:run=True:tol=0.5
43:hp=0
destination:28.5070,9.3463,0.0000:run=True:tol=0.5
44:hp=0
destination:26.7902,13.5014,0.0000:run=True:tol=0.5
45:hp=0
destination:24.4717,17.3533,0.0000:run=True:tol=0.5
46:hp=0
destination:21.6037,20.8154,0.0000:run=True:tol=0.5
47:hp=0
destination:18.2505,23.8101,0.0000:run=True:tol=0.5
48:hp=0
destination:14.4874,26.2700,0.0000:run=True:tol=0.5
49:hp=0
destination:10.3990,28.1400,0.0000:run=True:tol=0.5
50:hp=0
destination:6.0770,29.3781,0.0000:run=True:tol=0.5
51:state=Searching->Idle
51:hp=0
stop-immediately
relaxed-stance
ev=search_give_up
""";

    [Test]
    public async Task Strategies_ExposeTheR3LegacyStatePaths()
    {
        await Assert.That(new FollowStrategy().Name).IsEqualTo("follow");
        await Assert.That(new RestStrategy().Name).IsEqualTo("rest");
        await Assert.That(new SearchStrategy().Name).IsEqualTo("search");
        await Assert.That(new FollowStrategy().DefaultActions.Select(action => action.Name)).Contains("follow tick");
        await Assert.That(new RestStrategy().DefaultActions.Select(action => action.Name)).Contains("rest tick");
        await Assert.That(new SearchStrategy().DefaultActions.Select(action => action.Name)).Contains("search tick");
    }

    [Test]
    public async Task FollowStrategy_ReproducesRecordedLegacyTrace()
    {
        await AssertRecordedTrace(RunStrategyFollowTrace(), RecordedFollowTrace);
    }

    [Test]
    public async Task FollowAction_DefendsAgainstAttackerThroughContextSeam()
    {
        var bot = BotTestFixture.MakeBot(10, Vector3.Zero);
        var leader = BotTestFixture.MakeBot(11, new Vector3(2, 0, 0));
        var attacker = BotTestFixture.MakeBot(12, new Vector3(1, 0, 0));
        var combat = new BotCombatState { CurrentState = BotCombatStateType.Following };
        var runtime = new BotRuntime(bot, new BotMovementState { FollowTarget = leader }, combat,
            config: new BotConfig { UseEngine = false });
        var action = new FollowAction(new TraceMover());
        var context = new BotContext(bot, runtime, runtime.Blackboard, s_now,
            new BotConfig { UseEngine = false }, BotEngineKind.NonCombat)
        {
            Defender = () => attacker
        };

        action.Execute(context, default);

        await Assert.That(combat.CurrentState).IsEqualTo(BotCombatStateType.Combat);
        await Assert.That(combat.Target).IsEqualTo(attacker);
    }

    [Test]
    public async Task RelaxedFlag_ResetsOnCombatEntryRestCompletionAndSearchExit()
    {
        var entryState = new BotCombatState
        {
            CurrentState = BotCombatStateType.Following,
            SentRelaxedAfterCombat = true
        };
        entryState.TransitionTo(BotCombatStateType.Combat);
        await Assert.That(entryState.SentRelaxedAfterCombat).IsFalse();

        var bot = MakeHealthBot(20, Vector3.Zero);
        bot.Hp = 100;
        bot.MaxHp = 100;
        var leader = MakeHealthBot(21, new Vector3(2, 0, 0));
        var mover = new TraceMover();
        var combat = new BotCombatState
        {
            CurrentState = BotCombatStateType.Combat,
            Target = leader,
            SentRelaxedAfterCombat = false
        };
        var runtime = new BotRuntime(bot, new BotMovementState { FollowTarget = leader }, combat,
            config: new BotConfig { UseEngine = false });
        var context = new BotContext(bot, runtime, runtime.Blackboard, s_now,
            new BotConfig { UseEngine = false }, BotEngineKind.Combat, mover: mover);

        new FollowAction(mover).Execute(context, default);

        await Assert.That(combat.SentRelaxedAfterCombat).IsTrue();
        await Assert.That(mover.TakeEvents()).Contains("relaxed-stance");

        combat.CurrentState = BotCombatStateType.Resting;
        combat.IsResting = true;
        combat.SentRelaxedAfterCombat = true;
        new RestAction(mover).Execute(context, default);
        await Assert.That(combat.SentRelaxedAfterCombat).IsFalse();

        combat.CurrentState = BotCombatStateType.Searching;
        combat.LastKnownTargetPosition = null;
        combat.SentRelaxedAfterCombat = false;
        new SearchAction(mover).Execute(context, default);
        await Assert.That(combat.SentRelaxedAfterCombat).IsFalse();

        combat.SentRelaxedAfterCombat = true;
        new SearchAction(mover).Execute(context, default);
        await Assert.That(combat.SentRelaxedAfterCombat).IsTrue();
    }

    [Test]
    public async Task RestStrategy_ReproducesRecordedLegacyTrace()
    {
        await AssertRecordedTrace(RunStrategyRestTrace(), RecordedRestTrace);
    }

    [Test]
    public async Task SearchStrategy_ReproducesRecordedLegacyTraceThroughTimeout()
    {
        await AssertRecordedTrace(RunStrategySearchTrace(), RecordedSearchTrace);
    }

    private static async Task AssertRecordedTrace(Trace trace, string recordedTrace)
    {
        await Assert.That(string.Join('\n', trace.Lines)).IsEqualTo(recordedTrace.Trim());
    }

    private static Trace RunStrategyFollowTrace()
    {
        var bot = BotTestFixture.MakeBot(1, Vector3.Zero);
        bot.Hp = 100;
        bot.MaxHp = 100;
        var leader = BotTestFixture.MakeBot(2, new Vector3(2, 0, 0));
        var enemy = BotTestFixture.MakeBot(3, new Vector3(3, 0, 0));
        var attacker = BotTestFixture.MakeBot(4, new Vector3(1, 0, 0));
        var leaderStealthed = false;
        var buffs = Mock.Of<IBuffs>();
        buffs.HasEffectsMatchingCondition(Any<Func<Buff, bool>>()).Returns(_ => leaderStealthed);
        leader.Buffs = buffs.Object;
        var movement = new BotMovementState { FollowTarget = leader, FollowDistance = 2f };
        var combat = new BotCombatState { CurrentState = BotCombatStateType.Following };
        var runtime = new BotRuntime(bot, movement, combat, config: new BotConfig { UseEngine = false });
        var mover = new TraceMover();
        var events = new List<string>();
        var time = new FakeTimeProvider(new DateTimeOffset(s_now));
        var action = new FollowAction(mover);
        var trace = new Trace();
        for (var tick = 0; tick < 60; tick++)
        {
            if (tick == 20)
            {
                leader.IsInBattle = true;
                leader.CurrentTarget = enemy;
            }
            if (tick == 40)
            {
                leaderStealthed = true;
            }
            if (tick == 46)
            {
                leaderStealthed = false;
                leader.IsInBattle = false;
                leader.CurrentTarget = null;
            }

            var context = new BotContext(bot, runtime, runtime.Blackboard, time.GetUtcNow().UtcDateTime,
                new BotConfig { UseEngine = false }, BotEngineKind.NonCombat, mover: mover)
            {
                EventSink = events.Add,
                Defender = () => tick == 25 ? attacker : null
            };
            var before = combat.CurrentState;
            action.Execute(context, default);
            trace.Add(tick, combat, mover, events, before, bot.Hp);
            events.Clear();
            time.Advance(TimeSpan.FromMilliseconds(100));
        }

        return trace;
    }

    private static Trace RunStrategyRestTrace()
    {
        var bot = MakeHealthBot(5, Vector3.Zero);
        bot.Hp = 50;
        bot.MaxHp = 100;
        var combat = new BotCombatState { CurrentState = BotCombatStateType.Resting, IsResting = true };
        var runtime = new BotRuntime(bot, new BotMovementState(), combat, config: new BotConfig { UseEngine = false });
        var config = new BotConfig { UseEngine = false, RestHealInterval = 1, RestHealPercentPerTick = 10 };
        var mover = new TraceMover();
        var events = new List<string>();
        var time = new FakeTimeProvider(new DateTimeOffset(s_now));
        var action = new RestAction(mover);
        var trace = new Trace();
        for (var tick = 0; tick <= 5; tick++)
        {
            var context = new BotContext(bot, runtime, runtime.Blackboard, time.GetUtcNow().UtcDateTime,
                config, BotEngineKind.NonCombat, mover: mover)
            {
                EventSink = events.Add
            };
            var before = combat.CurrentState;
            action.Execute(context, default);
            trace.Add(tick, combat, mover, events, before, bot.Hp);
            events.Clear();
            time.Advance(TimeSpan.FromSeconds(1));
        }

        return trace;
    }

    private static Trace RunStrategySearchTrace()
    {
        var bot = BotTestFixture.MakeBot(6, Vector3.Zero);
        var combat = new BotCombatState
        {
            CurrentState = BotCombatStateType.Searching,
            LastKnownTargetPosition = Vector3.Zero,
            SearchStartTime = s_now,
            IsSearching = true
        };
        var runtime = new BotRuntime(bot, new BotMovementState(), combat, config: new BotConfig { UseEngine = false });
        var mover = new TraceMover();
        var events = new List<string>();
        var time = new FakeTimeProvider(new DateTimeOffset(s_now));
        var action = new SearchAction(mover, static _ => [], static (_, _) => false);
        var trace = new Trace();
        for (var tick = 0; tick <= 51; tick++)
        {
            var context = new BotContext(bot, runtime, runtime.Blackboard, time.GetUtcNow().UtcDateTime,
                new BotConfig { UseEngine = false }, BotEngineKind.NonCombat, mover: mover)
            {
                EventSink = events.Add
            };
            var before = combat.CurrentState;
            action.Execute(context, default);
            trace.Add(tick, combat, mover, events, before, bot.Hp);
            events.Clear();
            time.Advance(TimeSpan.FromSeconds(1));
        }

        return trace;
    }

    private static HealthCharacterMock MakeHealthBot(uint id, Vector3 position)
    {
        var bot = new HealthCharacterMock { Id = id, ObjId = 1000 + id, Name = $"bot{id}", IsBot = true };
        bot.Transform.Local.SetPosition(position);
        return bot;
    }

    private sealed class Trace
    {
        public List<string> Lines { get; } = [];

        public void Add(int tick, BotCombatState state, TraceMover mover, List<string> events,
            BotCombatStateType before, int hp)
        {
            if (before != state.CurrentState)
                Lines.Add($"{tick}:state={before}->{state.CurrentState}");
            Lines.Add($"{tick}:hp={hp}");
            Lines.AddRange(mover.TakeEvents());
            Lines.AddRange(events);
        }
    }

    private sealed class TraceMover : IBotMover
    {
        private readonly List<string> _events = [];

        public void SetDestination(Character bot, Vector3 position, bool run, float tolerance) =>
            _events.Add($"destination:{position.X:F4},{position.Y:F4},{position.Z:F4}:run={run}:tol={tolerance:F1}");

        public void StopIfMoving(Character bot) => _events.Add("stop-if-moving");
        public void StopImmediately(Character bot) => _events.Add("stop-immediately");
        public void Face(Character bot, float angle) => _events.Add($"face:{angle:F4}");
        public void Teleport(Character bot, Vector3 position) => _events.Add("teleport");
        public void Follow(Character bot, Character target, float distance) => _events.Add($"follow:{distance:F1}");
        public void StopFollow(Character bot) => _events.Add("stop-follow");
        public void SendRelaxedStance(Character bot) => _events.Add("relaxed-stance");

        public List<string> TakeEvents()
        {
            var result = _events.ToList();
            _events.Clear();
            return result;
        }
    }

    private sealed class HealthCharacterMock : CharacterMock
    {
        public override int MaxHp { get; set; }
    }
}
