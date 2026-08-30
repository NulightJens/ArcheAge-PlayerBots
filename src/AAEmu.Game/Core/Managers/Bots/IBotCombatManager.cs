using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Duels;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Core.Managers.Bots;

public interface IBotCombatManager
{
    void StartListening(Character bot);
    void StopListening(Character bot);
    void EnableCombat(Character bot, uint? targetTypeFilter = null, int? killGoal = null);
    void DisableCombat(Character bot);
    bool IsCombatEnabled(Character bot);
    BotCombatState GetState(Character bot);
    bool IsTaskRunning(uint characterId);
    void ResetCombat(Character bot);
    void ResetBot(Character bot);
    void StartDuel(Character bot, Unit opponent);
    void EndDuel(Character bot);
    bool OnDuelRequested(Character bot, Character challenger);
    void OnDuelStarted(Duel duel);
    void OnDuelEnded(Duel duel);
    void SetForcedState(Character bot, BotCombatStateType? state);
}
