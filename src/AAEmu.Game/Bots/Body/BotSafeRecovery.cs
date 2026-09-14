#if !PLAYERBOTS_AAEMU_3_0
using System.Numerics;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using NLog;

namespace AAEmu.Game.Bots.Body;

/// <summary>Last-resort rollback to recently occupied ground, never a quest destination.</summary>
internal sealed class BotSafeRecovery
{
    private readonly Queue<(Vector3 Position, object World, uint Instance, DateTime At)> _ground = new();
    private Vector3? _lastSample;
    private Vector3 _failureOrigin;
    private string _failureKey;
    private DateTime? _firstFailure;
    private DateTime _lastFailure;
    internal DateTime? LastTeleportAt { get; private set; }
    internal int Failures { get; private set; }
    internal string Reason { get; private set; } = "not_required";
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    internal void ObserveGround(Character bot, BotMovementState state, DateTime now, float groundHeight)
    {
        if (!Eligible(bot, state) || !float.IsFinite(groundHeight)) return;
        var position = bot.Transform.World.Position;
        if (!Finite(position) || MathF.Abs(position.Z - groundHeight) > 0.5f) return;
        if (_lastSample.HasValue && Vector3.Distance(_lastSample.Value, position) < 2f) return;
        _lastSample = position;
        _ground.Enqueue((position, bot.ParentWorld, bot.Transform.InstanceId, now));
        while (_ground.Count > 16) _ground.Dequeue();
        if (_firstFailure.HasValue && Vector3.Distance(_failureOrigin, position) >= 4f)
            ClearFailures();
    }

    internal bool FailedRoute(BotRuntime runtime, BotConfig config, DateTime now, string key,
        IBotMover mover, Func<float, float, float> height = null)
    {
        if (!config.StuckTeleportEnabled) { Reason = "disabled"; return false; }
        var bot = runtime.Bot;
        var state = runtime.MovementState;
        if (!Eligible(bot, state) || runtime.CombatState.Target != null || runtime.CombatState.IsActive)
        { Reason = "unsafe_actor_state"; return false; }
        if (_failureKey != key || !_firstFailure.HasValue ||
            Vector3.Distance(_failureOrigin, bot.Transform.World.Position) >= 4f)
        {
            ClearFailures();
            _failureKey = key;
            _firstFailure = now;
            _failureOrigin = bot.Transform.World.Position;
        }
        // Repeated calls in one recovery cycle cannot manufacture escalation.
        if (Failures == 0 || now - _lastFailure >= TimeSpan.FromSeconds(30))
        { Failures++; _lastFailure = now; }
        if (Failures < Math.Max(3, config.StuckTeleportAttempts) ||
            now - _firstFailure.Value < TimeSpan.FromSeconds(Math.Max(90, config.StuckTeleportSeconds)))
        { Reason = "normal_recovery_first"; return false; }
        if (LastTeleportAt.HasValue && now - LastTeleportAt.Value < TimeSpan.FromMinutes(10))
        { Reason = "teleport_cooldown"; return false; }
        var origin = bot.Transform.World.Position;
        foreach (var sample in _ground.Reverse())
        {
            var distance = Vector3.Distance(origin, sample.Position);
            if (!ReferenceEquals(sample.World, bot.ParentWorld) || sample.Instance != bot.Transform.InstanceId ||
                now < sample.At || now - sample.At > TimeSpan.FromMinutes(15) ||
                distance < 2f || distance > 8f || MathF.Abs(origin.Z - sample.Position.Z) > 1.5f)
                continue;
            float ground;
            try { ground = (height ?? ((x, y) => bot.ParentWorld.GetHeight(x, y)))(sample.Position.X, sample.Position.Y); }
            catch { continue; }
            if (!float.IsFinite(ground) || MathF.Abs(ground - sample.Position.Z) > 0.5f) continue;
            mover.StopImmediately(bot);
            mover.Teleport(bot, sample.Position);
            LastTeleportAt = now;
            runtime.HostMetrics?.RecordStuckRecovery(teleport: true);
            Logger.Warn($"BOT id={bot.Id} ev=recovery_teleport reason={key} failures={Failures} " +
                $"from={origin} to={sample.Position} distance={distance:F2} policy=previously_occupied_ground");
            ClearFailures();
            _ground.Clear();
            _lastSample = null;
            Reason = "returned_to_recent_ground";
            return true;
        }
        Reason = "no_verified_recent_ground";
        return false;
    }

    private void ClearFailures() { Failures = 0; _firstFailure = null; _failureKey = null; }
    private static bool Eligible(Character bot, BotMovementState state) =>
        bot?.ParentWorld != null && bot.Transform != null && !bot.IsDead && !bot.IsInBattle &&
        bot.Transform.Parent == null && bot.Transform.StickyParent == null &&
        state.Climb == null && !state.IsFalling && !state.IsJumping;
    private static bool Finite(Vector3 p) => float.IsFinite(p.X) && float.IsFinite(p.Y) && float.IsFinite(p.Z);
}
#endif
