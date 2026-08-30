namespace AAEmu.Game.Bots.Host;

public interface IBotHost
{
    BotHostMetrics Metrics { get; }
    int RuntimeCount { get; }
    TimeProvider TimeProvider { get; }
    Func<int> Roll { get; }
    BotRuntime GetRuntime(uint botId);
    void Register(BotRuntime runtime);
    void Unregister(uint botId);
    void Unregister(BotRuntime runtime);
    void Start();
    void Stop();
}
