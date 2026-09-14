#if !PLAYERBOTS_AAEMU_3_0
namespace AAEmu.Game.Bots.Host;

public sealed partial class BotHost
{
    private readonly object _observerLock = new();
    private IBotHostObserver[] _observers = [];

    public IReadOnlyList<IBotHostObserver> Observers => Array.AsReadOnly(Volatile.Read(ref _observers));

    public void RegisterObserver(IBotHostObserver observer)
    {
        ArgumentNullException.ThrowIfNull(observer);
        lock (_observerLock)
        {
            if (_observers.Contains(observer)) return;
            Volatile.Write(ref _observers, [.. _observers, observer]);
        }
    }

    public void UnregisterObserver(IBotHostObserver observer)
    {
        lock (_observerLock)
            Volatile.Write(ref _observers, _observers.Where(o => !ReferenceEquals(o, observer)).ToArray());
    }

    private void NotifyRegistered(BotRuntime runtime)
    {
        foreach (var observer in Volatile.Read(ref _observers))
            try { observer.OnRegistered(runtime); }
            catch (Exception error) { Logger.Error(error, "BOT observer registration failed"); }
    }

    private void NotifyRetired(BotRuntime runtime)
    {
        foreach (var observer in Volatile.Read(ref _observers))
            try { observer.OnRetired(runtime); }
            catch (Exception error) { Logger.Error(error, "BOT observer retirement failed"); }
    }

    internal void CaptureObservers(BotRuntime runtime, DateTimeOffset now)
    {
        var observers = Volatile.Read(ref _observers);
        if (observers.Length == 0 || now < runtime.NextObservationAt) return;
        runtime.NextObservationAt = now.AddSeconds(2);
        BotHostTickSnapshot snapshot;
        try { snapshot = BotHostTickSnapshot.Capture(runtime, now); }
        catch (Exception error) { Logger.Error(error, "BOT observation capture failed"); return; }
        if (snapshot == null) return;
        foreach (var observer in observers)
            try { observer.OnTick(snapshot); }
            catch (Exception error) { Logger.Error(error, "BOT observer tick failed"); }
    }
}
#endif
