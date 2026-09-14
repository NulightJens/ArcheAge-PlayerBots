#if !PLAYERBOTS_AAEMU_3_0
namespace AAEmu.Game.Bots.Host;

public interface IBotHostObserver
{
    void OnRegistered(BotRuntime runtime);
    void OnTick(BotHostTickSnapshot snapshot);
    void OnRetired(BotRuntime runtime);
}
#endif
