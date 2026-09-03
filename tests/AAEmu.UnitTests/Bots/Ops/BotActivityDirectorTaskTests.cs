using System.Numerics;
using AAEmu.Game.Bots.Ops;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.World;
using AAEmu.UnitTests.Utils.Mocks;
using Microsoft.Extensions.Time.Testing;

namespace AAEmu.UnitTests.Bots.Ops;

[NotInParallel]
public sealed class BotActivityDirectorTaskTests
{
    [Test]
    public async Task DisabledOrInvalidConfiguration_FailsClosedWithoutSchedulingWork()
    {
        var time = MakeTime();
        var manager = new RecordingBotManager();
        var disabled = new BotActivityDirectorTask(new BotConfig(), manager, time);
        var invalid = new BotActivityDirectorTask(
            DirectorConfig(zoneId: 0),
            manager,
            time);

        var disabledStarted = disabled.TryStart();
        disabled.Execute();
        var invalidStarted = invalid.TryStart();
        invalid.Execute();

        await Assert.That(disabledStarted).IsFalse();
        await Assert.That(disabled.Snapshot.Enabled).IsFalse();
        await Assert.That(disabled.Snapshot.Valid).IsTrue();
        await Assert.That(disabled.Snapshot.Reason).IsEqualTo("disabled");
        await Assert.That(invalidStarted).IsFalse();
        await Assert.That(invalid.Snapshot.Enabled).IsTrue();
        await Assert.That(invalid.Snapshot.Valid).IsFalse();
        await Assert.That(invalid.Snapshot.Reason).IsEqualTo("zone_zero");
        await Assert.That(manager.SpawnCalls).IsEmpty();
    }

    [Test]
    public async Task BootstrapAndSteadyState_SelectOrderedIdentitiesOneAtATimeTowardTarget()
    {
        var time = MakeTime();
        var manager = new RecordingBotManager();
        manager.SpawnedBots[9] = MakeLocatedBot(9, 137);
        manager.SpawnedBots[7] = MakeLocatedBot(7, 137);
        manager.SpawnedBots[8] = MakeLocatedBot(8, 137);
        var director = new BotActivityDirectorTask(
            DirectorConfig(characterIds: [9, 7, 8], minimum: 1, target: 2, maximum: 3),
            manager,
            time);

        try
        {
            await Assert.That(director.TryStart()).IsTrue();

            director.Execute();
            await Assert.That(string.Join(",", manager.SpawnCalls)).IsEqualTo("9");
            await Assert.That(director.Snapshot.LiveQualified).IsEqualTo(1);
            await Assert.That(director.Snapshot.SuccessCount).IsEqualTo(1L);

            director.Execute();
            await Assert.That(string.Join(",", manager.SpawnCalls)).IsEqualTo("9,7");
            await Assert.That(director.Snapshot.LiveQualified).IsEqualTo(2);
            await Assert.That(director.Snapshot.SuccessCount).IsEqualTo(2L);

            director.Execute();
            await Assert.That(string.Join(",", manager.SpawnCalls)).IsEqualTo("9,7");
            await Assert.That(director.Snapshot.LastResult).IsEqualTo("steady");
            await Assert.That(director.Snapshot.LastReason).IsEqualTo("target_satisfied");
            await Assert.That(director.Snapshot.MinimumPopulation).IsEqualTo(1);
            await Assert.That(director.Snapshot.TargetPopulation).IsEqualTo(2);
            await Assert.That(director.Snapshot.MaximumPopulation).IsEqualTo(3);
        }
        finally
        {
            director.Stop();
        }
    }

    [Test]
    public async Task PopulationCount_IgnoresManualBotsAndSeparatesConfiguredWrongZone()
    {
        var time = MakeTime();
        var manager = new RecordingBotManager();
        manager.AddLive(MakeLocatedBot(7, 137));
        manager.AddLive(MakeLocatedBot(8, 999));
        manager.AddLive(MakeLocatedBot(99, 137));
        manager.SpawnedBots[9] = MakeLocatedBot(9, 137);
        var director = new BotActivityDirectorTask(
            DirectorConfig(characterIds: [7, 8, 9], minimum: 1, target: 3, maximum: 3),
            manager,
            time);

        try
        {
            director.TryStart();
            director.Execute();

            await Assert.That(director.Snapshot.LiveQualified).IsEqualTo(2);
            await Assert.That(director.Snapshot.LiveWrongZone).IsEqualTo(1);
            await Assert.That(string.Join(",", manager.SpawnCalls)).IsEqualTo("9");
            await Assert.That(manager.DespawnCalls).IsEmpty();
        }
        finally
        {
            director.Stop();
        }
    }

    [Test]
    public async Task SpawnFailure_CoolsIdentityUntilBoundedBackoffExpires()
    {
        var time = MakeTime();
        var manager = new RecordingBotManager();
        manager.Results[7] = SpawnResult.LoadFailed;
        var director = new BotActivityDirectorTask(
            DirectorConfig(characterIds: [7], minimum: 1, target: 1, maximum: 1, retryBackoffMs: 1000),
            manager,
            time);

        try
        {
            director.TryStart();
            director.Execute();
            director.Execute();

            await Assert.That(string.Join(",", manager.SpawnCalls)).IsEqualTo("7");
            await Assert.That(director.Snapshot.Cooldown).IsEqualTo(1);
            await Assert.That(director.Snapshot.FailureCount).IsEqualTo(1L);
            await Assert.That(director.Snapshot.LastReason).IsEqualTo("eligible_identities_cooling_down");

            time.Advance(TimeSpan.FromSeconds(1));
            director.Execute();

            await Assert.That(string.Join(",", manager.SpawnCalls)).IsEqualTo("7,7");
            await Assert.That(director.Snapshot.FailureCount).IsEqualTo(2L);
        }
        finally
        {
            director.Stop();
        }
    }

    [Test]
    public async Task WrongZoneAndWrongInstanceSpawns_UseOnlyNormalCleanupAndCoolDown()
    {
        var time = MakeTime();
        var manager = new RecordingBotManager();
        manager.SpawnedBots[7] = MakeLocatedBot(7, 999);
        manager.SpawnedBots[8] = MakeLocatedBot(8, 137, defaultInstance: false);
        var director = new BotActivityDirectorTask(
            DirectorConfig(characterIds: [7, 8], minimum: 1, target: 1, maximum: 2),
            manager,
            time);

        try
        {
            director.TryStart();
            director.Execute();
            director.Execute();

            await Assert.That(string.Join(",", manager.SpawnCalls)).IsEqualTo("7,8");
            await Assert.That(string.Join(",", manager.DespawnCalls)).IsEqualTo("7,8");
            await Assert.That(manager.DirectGameplayCommandCalls).IsEqualTo(0);
            await Assert.That(director.Snapshot.SuccessCount).IsEqualTo(0L);
            await Assert.That(director.Snapshot.FailureCount).IsEqualTo(2L);
            await Assert.That(director.Snapshot.Cooldown).IsEqualTo(2);
            await Assert.That(director.Snapshot.LastReason).Contains("spawn_wrongworld_cleanup_succeeded");
        }
        finally
        {
            director.Stop();
        }
    }

    [Test]
    public async Task LoweredMaximum_ReportsOverCapacityWithoutShrinkingExistingPopulation()
    {
        var time = MakeTime();
        var manager = new RecordingBotManager();
        manager.AddLive(MakeLocatedBot(7, 137));
        manager.AddLive(MakeLocatedBot(8, 137));
        manager.AddLive(MakeLocatedBot(9, 137));
        var config = DirectorConfig(characterIds: [7, 8, 9], minimum: 1, target: 3, maximum: 3);
        var director = new BotActivityDirectorTask(config, manager, time);

        try
        {
            director.TryStart();
            config.ActivityDirectorTargetPopulation = 2;
            config.ActivityDirectorMaximumPopulation = 2;
            director.Execute();

            await Assert.That(director.Snapshot.LiveQualified).IsEqualTo(3);
            await Assert.That(director.Snapshot.LastResult).IsEqualTo("over_capacity");
            await Assert.That(manager.SpawnCalls).IsEmpty();
            await Assert.That(manager.DespawnCalls).IsEmpty();
        }
        finally
        {
            director.Stop();
        }
    }

    [Test]
    public async Task NormalSelfLogout_IsRefilledAndCountedWithoutForcedState()
    {
        var time = MakeTime();
        var manager = new RecordingBotManager();
        manager.SpawnedBots[7] = MakeLocatedBot(7, 137);
        var director = new BotActivityDirectorTask(
            DirectorConfig(characterIds: [7], minimum: 1, target: 1, maximum: 1),
            manager,
            time);

        try
        {
            director.TryStart();
            director.Execute();
            manager.RemoveLive(7);
            time.Advance(TimeSpan.FromSeconds(5));
            director.Execute();

            await Assert.That(string.Join(",", manager.SpawnCalls)).IsEqualTo("7,7");
            await Assert.That(director.Snapshot.SuccessCount).IsEqualTo(2L);
            await Assert.That(director.Snapshot.RefillCount).IsEqualTo(1L);
            await Assert.That(director.Snapshot.LastResult).IsEqualTo("refill_succeeded");
            await Assert.That(manager.DirectGameplayCommandCalls).IsEqualTo(0);
        }
        finally
        {
            director.Stop();
        }
    }

    [Test]
    public async Task OverlapAndStop_AreSerializedIdempotentAndExposeImmutableSnapshotFields()
    {
        var time = MakeTime();
        var manager = new RecordingBotManager { BlockSpawn = true };
        manager.SpawnedBots[7] = MakeLocatedBot(7, 137);
        var logs = new List<string>();
        var director = new BotActivityDirectorTask(
            DirectorConfig(characterIds: [7], minimum: 1, target: 1, maximum: 1),
            manager,
            time,
            logs.Add);
        director.TryStart();

        var firstTick = System.Threading.Tasks.Task.Run(director.Execute);
        await Assert.That(manager.SpawnEntered.Wait(TimeSpan.FromSeconds(5))).IsTrue();
        var inFlight = director.Snapshot;
        director.Execute();
        var overlap = director.Snapshot;
        var stopTask = System.Threading.Tasks.Task.Run(director.Stop);
        await System.Threading.Tasks.Task.Delay(50);

        await Assert.That(inFlight.InFlight).IsEqualTo(1);
        await Assert.That(inFlight.AttemptCount).IsEqualTo(1L);
        await Assert.That(overlap.LastReason).IsEqualTo("overlap");
        await Assert.That(stopTask.IsCompleted).IsFalse();

        manager.ReleaseSpawn.Set();
        await firstTick;
        await stopTask;
        director.Stop();
        director.Execute();

        var stopped = director.Snapshot;
        await Assert.That(string.Join(",", manager.SpawnCalls)).IsEqualTo("7");
        await Assert.That(stopped.StartedAt).IsNotNull();
        await Assert.That(stopped.StoppedAt).IsNotNull();
        await Assert.That(stopped.LastTickAt).IsNotNull();
        await Assert.That(stopped.TickCount).IsEqualTo(1L);
        await Assert.That(stopped.EligibleIdentities).IsEqualTo(1);
        await Assert.That(logs).Contains(message =>
            message.Contains("enabled=true valid=true zone=137 min=1 target=1 max=1") &&
            message.Contains("eligible=1 live_qualified=") &&
            message.Contains("attempts=1 successes=1 failures=0 refills=0") &&
            message.Contains("started_at=") && message.Contains("stopped_at=") && message.Contains("tick_at="));
    }

    private static FakeTimeProvider MakeTime() =>
        new(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero));

    private static BotConfig DirectorConfig(
        uint zoneId = 137,
        List<uint> characterIds = null,
        int minimum = 1,
        int target = 1,
        int maximum = 2,
        int retryBackoffMs = 30000) =>
        new()
        {
            ActivityDirectorEnabled = true,
            ActivityDirectorZoneId = zoneId,
            ActivityDirectorCharacterIds = characterIds ?? [7, 8],
            ActivityDirectorMinimumPopulation = minimum,
            ActivityDirectorTargetPopulation = target,
            ActivityDirectorMaximumPopulation = maximum,
            ActivityDirectorInitialDelayMs = 2000,
            ActivityDirectorReconciliationIntervalMs = 5000,
            ActivityDirectorRetryBackoffMs = retryBackoffMs
        };

    private static Character MakeLocatedBot(uint id, uint zoneId, bool defaultInstance = true)
    {
        var bot = BotTestFixture.MakeBot(id, Vector3.Zero);
        var instanceId = defaultInstance ? WorldManager.DefaultInstanceId : WorldManager.DefaultInstanceId + 1;
        var templateId = defaultInstance ? WorldManager.DefaultWorldTemplateId : WorldManager.DefaultWorldTemplateId + 1;
        var world = new WorldInstance(
            new WorldTemplate { Id = templateId, Name = $"director-world-{instanceId}" },
            0,
            true,
            instanceId);
        BotTestFixture.SetPrivateField(bot, "_parentWorld", world);
        BotTestFixture.SetPrivateField(bot.Transform, "_instanceId", instanceId);
        BotTestFixture.SetPrivateField(bot.Transform, "_zoneId", zoneId);
        return bot;
    }

    private sealed class RecordingBotManager : IBotManager
    {
        private readonly Dictionary<uint, Character> _live = [];

        public Dictionary<uint, Character> SpawnedBots { get; } = [];
        public Dictionary<uint, SpawnResult> Results { get; } = [];
        public List<uint> SpawnCalls { get; } = [];
        public List<uint> DespawnCalls { get; } = [];
        public ManualResetEventSlim SpawnEntered { get; } = new(false);
        public ManualResetEventSlim ReleaseSpawn { get; } = new(false);
        public bool BlockSpawn { get; set; }
        public int DirectGameplayCommandCalls { get; private set; }

        public void AddLive(Character bot) => _live[bot.Id] = bot;
        public void RemoveLive(uint characterId) => _live.Remove(characterId);

        public Character SpawnBot(uint characterId) =>
            SpawnBot(characterId, out var bot) == SpawnResult.Ok ? bot : null;

        public SpawnResult SpawnBot(uint characterId, out Character bot)
        {
            SpawnCalls.Add(characterId);
            SpawnEntered.Set();
            if (BlockSpawn)
                ReleaseSpawn.Wait(TimeSpan.FromSeconds(5));

            var result = Results.GetValueOrDefault(characterId, SpawnResult.Ok);
            bot = result == SpawnResult.Ok ? SpawnedBots.GetValueOrDefault(characterId) : null;
            if (result == SpawnResult.Ok && bot != null)
                _live[characterId] = bot;
            return result;
        }

        public bool DespawnBot(uint characterId)
        {
            DespawnCalls.Add(characterId);
            return _live.Remove(characterId);
        }

        public void DespawnAllBots() => _live.Clear();
        public void Stop() { }
        public Character GetBot(uint characterId) => _live.GetValueOrDefault(characterId);
        public List<Character> GetAllBots() => _live.Values.ToList();
        public BotMovementState GetBotState(uint characterId) => null;
        public BotMovementBroadcaster GetBroadcaster(uint characterId) => null;
        public bool IsMovementTaskRunning(uint characterId) => false;
        public void MoveBotTo(Character bot, float x, float y, float z) => DirectGameplayCommandCalls++;
        public void StopImmediately(Character bot) => DirectGameplayCommandCalls++;
        public void SetFollowTarget(Character bot, Character target, float followDistance = 2) => DirectGameplayCommandCalls++;
        public void StopFollow(Character bot) => DirectGameplayCommandCalls++;
        public void SetBotDestination(Character bot, float x, float y, float z, bool run = true) => DirectGameplayCommandCalls++;
        public void StopBot(Character bot) => DirectGameplayCommandCalls++;
    }
}
