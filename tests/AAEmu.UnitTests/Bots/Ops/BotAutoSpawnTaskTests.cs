using AAEmu.Game.Bots.Ops;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Duels;
using AAEmu.Game.Models.Game.Units;
using AAEmu.UnitTests.Utils.Mocks;

namespace AAEmu.UnitTests.Bots.Ops;

[NotInParallel]
public class BotAutoSpawnTaskTests
{
    [Test]
    public async Task Execute_SpawnsEachConfiguredId_AppliesStateAndContinuesAfterFailures()
    {
        var botManager = new FakeBotManager();
        botManager.Bots[2] = BotTestFixture.MakeBot(2, default);
        botManager.Bots[4] = BotTestFixture.MakeBot(4, default);
        botManager.Results[3] = SpawnResult.LoadFailed;
        botManager.ThrowOnIds.Add(4);
        var combatManager = new RecordingBotCombatManager();
        var logLines = new List<string>();
        var task = new BotAutoSpawnTask([2, 3, 4], "grind", botManager, combatManager, logLines.Add);

        task.Execute();

        await Assert.That(botManager.SpawnCalls).IsEquivalentTo([2u, 3u, 4u]);
        await Assert.That(combatManager.ForcedStates).IsEquivalentTo([(2u, (BotCombatStateType?)BotCombatStateType.Grinding)]);
        await Assert.That(logLines).Count().IsEqualTo(3);
        await Assert.That(logLines[0]).Contains("BOT ev=autospawn id=2 result=Ok");
        await Assert.That(logLines[1]).Contains("BOT ev=autospawn id=3 result=LoadFailed");
        await Assert.That(logLines[2]).Contains("BOT ev=autospawn id=4 result=Exception");
    }

    private sealed class FakeBotManager : IBotManager
    {
        public Dictionary<uint, Character> Bots { get; } = [];
        public Dictionary<uint, SpawnResult> Results { get; } = [];
        public HashSet<uint> ThrowOnIds { get; } = [];
        public List<uint> SpawnCalls { get; } = [];

        public Character SpawnBot(uint characterId)
        {
            return SpawnBot(characterId, out var bot) == SpawnResult.Ok ? bot : null;
        }

        public SpawnResult SpawnBot(uint characterId, out Character bot)
        {
            SpawnCalls.Add(characterId);
            if (ThrowOnIds.Contains(characterId))
                throw new InvalidOperationException("simulated spawn failure");

            var result = Results.GetValueOrDefault(characterId, SpawnResult.Ok);
            bot = result == SpawnResult.Ok ? Bots.GetValueOrDefault(characterId) : null;
            return result;
        }

        public bool DespawnBot(uint characterId) => false;
        public void DespawnAllBots() { }
        public void Stop() { }
        public Character GetBot(uint characterId) => Bots.GetValueOrDefault(characterId);
        public List<Character> GetAllBots() => Bots.Values.ToList();
        public BotMovementState GetBotState(uint characterId) => null;
        public BotMovementBroadcaster GetBroadcaster(uint characterId) => null;
        public bool IsMovementTaskRunning(uint characterId) => false;
        public void MoveBotTo(Character bot, float x, float y, float z) { }
        public void StopImmediately(Character bot) { }
        public void SetFollowTarget(Character bot, Character target, float followDistance = 2.0f) { }
        public void StopFollow(Character bot) { }
        public void SetBotDestination(Character bot, float x, float y, float z, bool run = true) { }
        public void StopBot(Character bot) { }
    }

    private sealed class RecordingBotCombatManager : IBotCombatManager
    {
        public List<(uint Id, BotCombatStateType? State)> ForcedStates { get; } = [];

        public void StartListening(Character bot) { }
        public void StopListening(Character bot) { }
        public void EnableCombat(Character bot, uint? targetTypeFilter = null, int? killGoal = null) { }
        public void DisableCombat(Character bot) { }
        public bool IsCombatEnabled(Character bot) => false;
        public BotCombatState GetState(Character bot) => null;
        public bool IsTaskRunning(uint characterId) => false;
        public void ResetCombat(Character bot) { }
        public void ResetBot(Character bot) { }
        public void StartDuel(Character bot, Unit opponent) { }
        public void EndDuel(Character bot) { }
        public bool OnDuelRequested(Character bot, Character challenger) => false;
        public void OnDuelStarted(Duel duel) { }
        public void OnDuelEnded(Duel duel) { }
        public void SetForcedState(Character bot, BotCombatStateType? state) => ForcedStates.Add((bot.Id, state));
    }
}
