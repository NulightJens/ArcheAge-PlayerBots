#if !PLAYERBOTS_AAEMU_3_0
using System.Runtime.CompilerServices;
using AAEmu.Game.Models.Game.Char;

namespace AAEmu.Game.Bots.Host;

public static class BotDrivers
{
    private static readonly ConditionalWeakTable<Character, BotRuntime> Runtimes = new();
    private static readonly object Sync = new();

    public static BotRuntime Runtime(Character bot) =>
        bot != null && Runtimes.TryGetValue(bot, out var runtime) && !runtime.Retired ? runtime : null;

    public static IBotDriver For(Character bot) => Runtime(bot)?.Driver ?? ServerDriver.Instance;

    internal static void Register(BotRuntime runtime)
    {
        lock (Sync)
        {
            Runtimes.Remove(runtime.Bot);
            Runtimes.Add(runtime.Bot, runtime);
        }
    }

    internal static void Unregister(BotRuntime runtime)
    {
        lock (Sync)
            if (Runtimes.TryGetValue(runtime.Bot, out var current) && ReferenceEquals(current, runtime))
                Runtimes.Remove(runtime.Bot);
    }

    public static void ReleaseBeforeLeave(Character bot)
    {
        var runtime = Runtime(bot);
        if (runtime == null) return;
        lock (runtime.SyncRoot)
            if (!runtime.Retired && runtime.Driver.Owns(bot)) runtime.Driver.Release(runtime);
    }
}
#endif
