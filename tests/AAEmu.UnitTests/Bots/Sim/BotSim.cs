using System.Numerics;
using System.Diagnostics;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Tasks.Bots;
using AAEmu.UnitTests.Utils.Mocks;
using Microsoft.Extensions.Time.Testing;

namespace AAEmu.UnitTests.Bots.Sim;

public sealed class BotSim
{
    private readonly List<SimBot> _bots = [];
    private readonly List<uint> _legacyBotIds = [];

    public BotSim(int seed = 12345)
    {
        Time = new FakeTimeProvider(new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero));
        var random = new Random(seed);
        Host = new BotHost(new NoopTaskManager(), Time, random.Next);
    }

    public FakeTimeProvider Time { get; }
    public BotHost Host { get; }
    public IReadOnlyList<SimBot> Bots => _bots;

    public SimBot AddBot(uint id, BotCombatStateType state = BotCombatStateType.Idle, bool runLegacyBrain = false)
    {
        var bot = new SimCharacter { Id = id, ObjId = 1000 + id, Name = $"bot{id}" };
        bot.Transform.Local.SetPosition(Vector3.Zero);
        bot.IsBot = true;
        bot.Hp = 100;
        bot.MaxHp = 100;
        var movementState = new BotMovementState();
        var combatState = new BotCombatState { BotId = id };
        if (state != BotCombatStateType.Idle)
            combatState.TransitionTo(state);
        var broadcaster = new BotMovementBroadcaster(bot, Time);
        var mover = new SimMover(bot, movementState, broadcaster);
        if (runLegacyBrain)
        {
            BotTestFixture.SetPrivateField(bot, "_parentWorld", BotTestFixture.MakeWorld());
            BotTestFixture.GetDictionary<BotMovementState>(BotManager.Instance, "_botStates")[id] = movementState;
            _legacyBotIds.Add(id);
        }

        var brain = new SimBrain(bot, combatState, broadcaster, Time)
        {
            RunLegacyBehavior = runLegacyBrain
        };
        var runtime = new BotRuntime(bot, movementState, combatState, broadcaster, mover, brain);
        var simulated = new SimBot(runtime, mover, brain);
        _bots.Add(simulated);
        Host.Register(runtime);
        return simulated;
    }

    public void Reset()
    {
        var states = BotTestFixture.GetDictionary<BotMovementState>(BotManager.Instance, "_botStates");
        foreach (var id in _legacyBotIds)
            states.TryRemove(id, out _);

        _legacyBotIds.Clear();
        foreach (var runtime in Host.GetRuntimeSnapshot())
            Host.Unregister(runtime.Bot.Id);
        _bots.Clear();
    }

    public void Advance(int milliseconds)
    {
        for (var elapsed = 0; elapsed < milliseconds; elapsed += 100)
        {
            Time.Advance(TimeSpan.FromMilliseconds(100));
            Host.HostTask.Execute();
        }
    }

    public void Tick()
    {
        Host.HostTask.Execute();
    }

    public sealed class SimBot
    {
        internal SimBot(BotRuntime runtime, SimMover mover, SimBrain brain)
        {
            Runtime = runtime;
            Mover = mover;
            Brain = brain;
        }

        public BotRuntime Runtime { get; }
        public Character Bot => Runtime.Bot;
        public SimMover Mover { get; }
        public SimBrain Brain { get; }
    }

    public sealed class SimMover(CharacterMock bot, BotMovementState state, BotMovementBroadcaster broadcaster)
        : BotMovementTask(bot, state, broadcaster)
    {
        public int StepCount { get; private set; }
        public int CancelCount { get; private set; }

        internal override void Step() => StepCount++;
        public override void OnCancel() => CancelCount++;
    }

    public sealed class SimBrain(CharacterMock bot, BotCombatState state, BotMovementBroadcaster broadcaster, FakeTimeProvider time)
        : BotCombatTask(bot, state, broadcaster, onCancel: null, timeProvider: time)
    {
        private readonly FakeTimeProvider _time = time;

        public List<DateTime> FullStepTimes { get; } = [];
        public List<DateTime> MinimalStepTimes { get; } = [];
        public int CancelCount { get; private set; }
        public bool ThrowOnFull { get; set; }
        public int SpinMilliseconds { get; set; }
        public bool RunLegacyBehavior { get; set; }
        public ManualResetEventSlim Entered { get; } = new(false);
        public ManualResetEventSlim Release { get; } = new(true);

        internal override void Step()
        {
            FullStepTimes.Add(_time.GetUtcNow().UtcDateTime);
            Entered.Set();
            Release.Wait();
            if (SpinMilliseconds > 0)
            {
                var start = Stopwatch.GetTimestamp();
                while (Stopwatch.GetElapsedTime(start).TotalMilliseconds < SpinMilliseconds)
                {
                }
            }
            if (ThrowOnFull)
                throw new InvalidOperationException("simulated brain failure");
            if (RunLegacyBehavior)
                base.Step();
        }

        internal override void StepMinimal()
        {
            MinimalStepTimes.Add(_time.GetUtcNow().UtcDateTime);
        }

        public override void OnCancel() => CancelCount++;
    }

    private sealed class SimCharacter : CharacterMock
    {
        public override int MaxHp { get; set; }
    }

    private sealed class NoopTaskManager : ITaskManager
    {
        public void Initialize()
        {
        }

        public void Start()
        {
        }

        public void Stop()
        {
        }

        public bool Schedule(AAEmu.Game.Models.Tasks.Task task, TimeSpan? startTime = null, TimeSpan? repeatInterval = null, int count = -1)
        {
            return true;
        }

        public bool CronSchedule(AAEmu.Game.Models.Tasks.Task task, string cronExpression, TimeSpan? startDelay = null, int count = -1)
        {
            return true;
        }

        public bool Cancel(AAEmu.Game.Models.Tasks.Task task)
        {
            task.Cancelled = true;
            return true;
        }
    }
}
