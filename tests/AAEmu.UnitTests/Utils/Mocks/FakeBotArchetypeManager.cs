using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;

namespace AAEmu.UnitTests.Utils.Mocks;

public sealed class FakeBotArchetypeManager : BotArchetypeManager
{
    public Dictionary<uint, BotArchetypeState> States { get; } = [];
    public List<uint> RemoveStateCalls { get; } = [];
    public bool ThrowOnSpawn { get; set; }
    public bool ReloadResult { get; set; } = true;
    public int ReloadCalls { get; private set; }

    public override bool Reload()
    {
        ReloadCalls++;
        return ReloadResult;
    }

    public override void OnBotSpawn(Character bot)
    {
        if (ThrowOnSpawn)
            throw new InvalidOperationException("archetype setup failed");

        States[bot.Id] = new BotArchetypeState();
    }

    public override BotArchetypeState GetState(Character bot)
    {
        States.TryGetValue(bot.Id, out var state);
        return state;
    }

    public override void RemoveState(uint characterId)
    {
        RemoveStateCalls.Add(characterId);
        States.Remove(characterId);
    }
}
