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
#if !PLAYERBOTS_AAEMU_3_0
    internal const double NativeTurnWindowSeconds = 6;
    private DateTime _lastPositionProgressAt;
    private DateTime _turnWindowStartedAt;
    private Vector3? _turnTarget;
    private float? _bestTurnError;
#endif

    public BotStuckWatch(BotMovementState state, BotConfig config = null)
    {
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _config = config ?? BotConfig.Instance;
    }

    public bool Update(DateTime now, Vector3 position, bool hasDestination
#if !PLAYERBOTS_AAEMU_3_0
        , float? nativeYaw = null
#endif
        )
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
#if !PLAYERBOTS_AAEMU_3_0
            _lastPositionProgressAt = now;
            _turnWindowStartedAt = now;
            _turnTarget = null;
            _bestTurnError = null;
#endif
            return false;
        }

        if (Vector3.Distance(_state.LastPos.Value, position) >= (float)Math.Max(0, _config.StuckMinMeters))
        {
            Reset(position, now);
            return false;
        }

#if !PLAYERBOTS_AAEMU_3_0
        // A native client turns before walking. Count only a new best alignment
        // toward the unchanged steering target, within a bounded window after real
        // positional progress or a recovery attempt. Total stall age stays intact.
        if (nativeYaw is { } yaw && float.IsFinite(yaw) &&
            (_state.SteeringDestination ?? _state.Destination) is { } target &&
            float.IsFinite(target.X) && float.IsFinite(target.Y))
        {
            // The normal movement protocol uses yaw zero along +Y (see native
            // CSMoveUnit rotation and the shared NotFacingTargetTrigger).
            var desired = MathF.Atan2(position.X - target.X, target.Y - position.Y);
            var error = MathF.Abs(MathF.IEEERemainder(desired - yaw, 2 * MathF.PI));
            if (_turnTarget != target || !_bestTurnError.HasValue)
            {
                _turnTarget = target;
                _bestTurnError = error;
            }
            else if (_bestTurnError.Value - error >= .075f)
            {
                _bestTurnError = error;
                if (now >= _turnWindowStartedAt &&
                    now - _turnWindowStartedAt <= TimeSpan.FromSeconds(NativeTurnWindowSeconds))
                    _state.LastMoveAt = now;
            }
        }
        else { _turnTarget = null; _bestTurnError = null; }
#endif

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
#if !PLAYERBOTS_AAEMU_3_0
        _lastPositionProgressAt = now;
        _turnWindowStartedAt = now;
        _turnTarget = null;
        _bestTurnError = null;
#endif
    }

#if !PLAYERBOTS_AAEMU_3_0
    internal TimeSpan NoPositionProgressFor(DateTime now)
    {
        var since = _lastPositionProgressAt == DateTime.MinValue ? _state.LastMoveAt : _lastPositionProgressAt;
        return now >= since ? now - since : TimeSpan.Zero;
    }

    internal void ObserveRecoveryAttempt(DateTime now)
    {
        // Preserve attempts and total stall age, but let the mover try the nudge
        // before another brain tick replaces it with the opposite side/reset.
        _state.LastMoveAt = now;
        _turnWindowStartedAt = now;
        _turnTarget = null;
        _bestTurnError = null;
    }
#endif

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
