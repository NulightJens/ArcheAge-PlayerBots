using AAEmu.Game.Core.Managers.Bots;

namespace AAEmu.UnitTests.Utils.Mocks;

internal sealed class InMemoryBotArchetypeStore : IBotArchetypeStore
{
    private readonly Dictionary<uint, (string archetypeName, bool isFinal)> _plans = [];

    public (string archetypeName, bool isFinal) Get(uint characterId)
    {
        return _plans.TryGetValue(characterId, out var plan) ? plan : (null, false);
    }

    public void Save(uint characterId, string archetypeName, bool isFinal)
    {
        _plans[characterId] = (archetypeName, isFinal);
    }

    public void Delete(uint characterId)
    {
        _plans.Remove(characterId);
    }
}
