using System.Numerics;
using AAEmu.Game.Bots.Kernel;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using NLog;

namespace AAEmu.Game.Bots.Body;

public sealed class BotStuckWatch
{
    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();
    private readonly BotMovementState _state;
    private readonly BotConfig _config;

    public BotStuckWatch(BotMovementState state, BotConfig config = null)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _config = config ?? BotConfig.Instance;
    }

    public bool Update(DateTime now, Vector3 position, bool hasDestination)
    {
        if (!hasDestination)
        {
            Reset(position, now);
            return false;
        }

        if (!_state.LastPos.HasValue || _state.LastMoveAt == DateTime.MinValue)
        {
            _state.LastPos = position;
            _state.LastMoveAt = now;
            return false;
        }

        if (Vector3.Distance(_state.LastPos.Value, position) >= (float)Math.Max(0, _config.StuckMinMeters))
        {
            Reset(position, now);
            return false;
        }

        return IsStale(now);
    }

    public bool IsStuck(DateTime now, Vector3 position, bool hasDestination)
    {
        if (!hasDestination || !_state.LastPos.HasValue || _state.LastMoveAt == DateTime.MinValue)
            return false;
        if (Vector3.Distance(_state.LastPos.Value, position) >= (float)Math.Max(0, _config.StuckMinMeters))
            return false;

        return IsStale(now);
    }

    public void Reset(Vector3 position, DateTime now)
    {
        _state.LastPos = position;
        _state.LastMoveAt = now;
        _state.Attempts = 0;
    }

    private bool IsStale(DateTime now)
    {
        return now >= _state.LastMoveAt && now - _state.LastMoveAt >=
            TimeSpan.FromSeconds(Math.Max(0, _config.StuckSeconds));
    }

    internal static void LogUnstick(Character bot, int attempt, string mode)
    {
        Logger.Info($"BOT id={bot?.Id} ev=unstick attempt={attempt} mode={mode}");
    }
}
