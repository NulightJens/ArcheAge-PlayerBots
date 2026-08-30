namespace AAEmu.Game.Core.Managers.Bots;

internal interface IBotArchetypeStore
{
    (string archetypeName, bool isFinal) Get(uint characterId);
    void Save(uint characterId, string archetypeName, bool isFinal);
    void Delete(uint characterId);
}
