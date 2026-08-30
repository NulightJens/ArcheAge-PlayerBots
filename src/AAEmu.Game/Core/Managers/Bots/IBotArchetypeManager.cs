using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Bots;

namespace AAEmu.Game.Core.Managers.Bots;

public interface IBotArchetypeManager
{
    void OnBotSpawn(Character bot);
    void RemoveState(uint characterId);
    BotArchetypeState GetState(Character bot);
    void CheckForUpdates(Character bot);
    BotArchetypeDefinition GetEffectiveDefinition(BotArchetypeState state);
    void ForceReevaluate(Character bot);
    void RerollArchetype(Character bot);
    void ClearArchetypeSkills(Character bot);
}
