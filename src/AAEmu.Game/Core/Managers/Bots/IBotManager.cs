using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Bots;

namespace AAEmu.Game.Core.Managers.Bots;

public interface IBotManager
{
    Character SpawnBot(uint characterId);
    SpawnResult SpawnBot(uint characterId, out Character bot);
    bool DespawnBot(uint characterId);
    void DespawnAllBots();
    void Stop();
    Character GetBot(uint characterId);
    List<Character> GetAllBots();
    BotMovementState GetBotState(uint characterId);
    BotMovementBroadcaster GetBroadcaster(uint characterId);
    bool IsMovementTaskRunning(uint characterId);
    void MoveBotTo(Character bot, float x, float y, float z);
    void StopImmediately(Character bot);
    void SetFollowTarget(Character bot, Character target, float followDistance = 2.0f);
    void StopFollow(Character bot);
    void SetBotDestination(Character bot, float x, float y, float z, bool run = true);
    void StopBot(Character bot);
}
