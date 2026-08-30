using System.Diagnostics;

namespace AAEmu.Game.Bots.Kernel;

public static class BotContentRegistry
{
    private static readonly object s_syncRoot = new();
    private static readonly Dictionary<string, Func<IBotStrategy>> s_strategyFactories = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Func<IBotTrigger>> s_triggerFactories = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, Func<IBotAction>> s_actionFactories = new(StringComparer.OrdinalIgnoreCase);
    private static int s_frozen;

    public static IReadOnlyCollection<string> StrategyNames => GetNames(s_strategyFactories);
    public static IReadOnlyCollection<string> TriggerNames => GetNames(s_triggerFactories);
    public static IReadOnlyCollection<string> ActionNames => GetNames(s_actionFactories);

    public static void Freeze()
    {
        lock (s_syncRoot)
            Volatile.Write(ref s_frozen, 1);
    }

    [Conditional("DEBUG")]
    internal static void ResetForTests()
    {
        lock (s_syncRoot)
        {
            s_strategyFactories.Clear();
            s_triggerFactories.Clear();
            s_actionFactories.Clear();
            Volatile.Write(ref s_frozen, 0);
        }
    }

    public static void RegisterStrategy(string name, Func<IBotStrategy> factory)
    {
        Register(s_strategyFactories, name, factory);
    }

    public static void RegisterTrigger(string name, Func<IBotTrigger> factory)
    {
        Register(s_triggerFactories, name, factory);
    }

    public static void RegisterAction(string name, Func<IBotAction> factory)
    {
        Register(s_actionFactories, name, factory);
    }

    public static bool TryCreateStrategy(string name, out IBotStrategy strategy)
    {
        return TryCreate(s_strategyFactories, name, out strategy);
    }

    public static bool TryCreateTrigger(string name, out IBotTrigger trigger)
    {
        return TryCreate(s_triggerFactories, name, out trigger);
    }

    public static bool TryCreateAction(string name, out IBotAction action)
    {
        return TryCreate(s_actionFactories, name, out action);
    }

    private static void Register<T>(Dictionary<string, Func<T>> factories, string name, Func<T> factory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(factory);
        lock (s_syncRoot)
        {
            if (Volatile.Read(ref s_frozen) != 0)
                throw new InvalidOperationException("Bot content registration is frozen.");
            factories[name] = factory;
        }
    }

    private static bool TryCreate<T>(Dictionary<string, Func<T>> factories, string name, out T value)
    {
        if (Volatile.Read(ref s_frozen) == 0)
        {
            lock (s_syncRoot)
                return TryCreateCore(factories, name, out value);
        }

        return TryCreateCore(factories, name, out value);
    }

    private static bool TryCreateCore<T>(Dictionary<string, Func<T>> factories, string name, out T value)
    {
        if (name != null && factories.TryGetValue(name, out var factory))
        {
            value = factory();
            return value != null;
        }

        value = default;
        return false;
    }

    private static IReadOnlyCollection<string> GetNames<T>(Dictionary<string, Func<T>> factories)
    {
        if (Volatile.Read(ref s_frozen) != 0)
            return factories.Keys;

        lock (s_syncRoot)
            return new List<string>(factories.Keys);
    }
}
