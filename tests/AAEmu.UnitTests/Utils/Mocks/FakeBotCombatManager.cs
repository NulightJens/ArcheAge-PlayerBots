using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;

namespace AAEmu.UnitTests.Utils.Mocks;

public sealed class FakeBotCombatManager : BotCombatManager
{
    public Dictionary<uint, BotCombatState> States { get; } = [];
    public List<uint> StartListeningCalls { get; } = [];
    public List<uint> StopListeningCalls { get; } = [];
    public List<uint> ResetBotCalls { get; } = [];

    public override void StartListening(Character bot)
    {
        StartListeningCalls.Add(bot.Id);
        States.TryAdd(bot.Id, new BotCombatState());
    }

    public override BotCombatState GetState(Character bot)
    {
        States.TryGetValue(bot.Id, out var state);
        return state;
    }

    public override void StopListening(Character bot)
    {
        StopListeningCalls.Add(bot.Id);
        States.Remove(bot.Id);
    }

    public override void ResetBot(Character bot)
    {
        ResetBotCalls.Add(bot.Id);
    }
}
