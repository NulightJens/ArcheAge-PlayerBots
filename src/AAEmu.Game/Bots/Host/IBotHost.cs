namespace AAEmu.Game.Bots.Host;

public interface IBotHost
{
    BotHostMetrics Metrics { get; }
    int RuntimeCount { get; }
    TimeProvider TimeProvider { get; }
    Func<int> Roll { get; }
    BotRuntime GetRuntime(uint botId);
    void Register(BotRuntime runtime);
    // Optional drivers call this under the runtime's ownership lock.
    void StepMovement(BotRuntime runtime, DateTime now, AAEmu.Game.Models.Game.Bots.BotConfig config) =>
        throw new NotSupportedException("This host does not provide driver movement.");
    void Unregister(uint botId);
    void Unregister(BotRuntime runtime);
    void Start();
    void Stop();
}
