#if !PLAYERBOTS_AAEMU_3_0
using Microsoft.Extensions.Hosting;

namespace AAEmu.Game.Bots.Host;

/// <summary>Normal server shutdown must run hosted cleanup before the process returns.</summary>
public sealed class BotGracefulShutdown(IHostApplicationLifetime lifetime) : IHostedService
{
    private readonly IHostApplicationLifetime _lifetime = lifetime;
    private static BotGracefulShutdown _current;
    private static int _exitCode;
    private int _requested;
    public static int ExitCode => Volatile.Read(ref _exitCode);

    public System.Threading.Tasks.Task StartAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref _current, this, null) != null)
            throw new InvalidOperationException("A shutdown lifetime is already registered.");
        Volatile.Write(ref _exitCode, 0);
        return System.Threading.Tasks.Task.CompletedTask;
    }

    public System.Threading.Tasks.Task StopAsync(CancellationToken cancellationToken)
    {
        Interlocked.CompareExchange(ref _current, null, this);
        return System.Threading.Tasks.Task.CompletedTask;
    }

    public static void Request(int exitCode)
    {
        var current = Volatile.Read(ref _current) ??
            throw new InvalidOperationException("Server lifetime is unavailable; process retained.");
        if (Interlocked.CompareExchange(ref current._requested, 1, 0) != 0) return;
        Volatile.Write(ref _exitCode, exitCode);
        // Signals the host asynchronously. GameService.StopAsync performs native
        // bot logout before its final save and before stopping task/network managers.
        current._lifetime.StopApplication();
    }
}
#endif
