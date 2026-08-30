using System;
using System.Numerics;
using System.Threading;
using AAEmu.Game.Bots.Body;
using AAEmu.Game.Bots.Social;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Models;
using AAEmu.Game.Models.Game.Units.Movements;
using AAEmu.Game.Models.StaticValues;
using NLog;
#if PLAYERBOTS_AAEMU_3_0
using ModelManager = AAEmu.Game.Core.Managers.AAEmu.Game.Core.Managers.ModelManager;
#endif

namespace AAEmu.Game.Models.Tasks.Bots;

public class BotMovementTask : AAEmu.Game.Models.Tasks.Task, IBotMover
{
    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();
    private readonly Character _bot;
    private readonly BotMovementState _state;
    private readonly IBotMovementBroadcaster _broadcaster;
    private readonly Action<BotMovementTask> _onCancel;
    private readonly Func<bool, float> _baseSpeed;
    private readonly Func<float, float, float> _groundHeight;
    private readonly BotConfig _config;
    private readonly TimeProvider _time;
    private BotCombatState _combatState;

    private const float TickInterval = 0.1f;
    private const float FallbackRunSpeed = 5.4f;
    private const float FallbackWalkSpeed = 1.8f;

    private float _cachedRunSpeed = FallbackRunSpeed;
    private float _cachedWalkSpeed = FallbackWalkSpeed;
    private uint _cachedModelId;
    private GameStanceType _cachedStance = GameStanceType.Combat;

    public BotMovementTask(Character bot, BotMovementState state, BotMovementBroadcaster broadcaster)
        : this(bot, state, broadcaster, null, null, null, null, null)
    {
    }

    internal BotMovementTask(
        Character bot,
        BotMovementState state,
        IBotMovementBroadcaster broadcaster,
        Func<bool, float> baseSpeed,
        Func<float, float, float> groundHeight,
        BotConfig config = null,
        TimeProvider time = null)
        : this(bot, state, broadcaster, null, baseSpeed, groundHeight, config, time)
    {
    }

    internal BotMovementTask(
        Character bot,
        BotMovementState state,
        BotMovementBroadcaster broadcaster,
        Action<BotMovementTask> onCancel,
        Func<float, float, float> groundHeight = null)
        : this(bot, state, broadcaster, onCancel, null, groundHeight, null, null)
    {
    }

    private BotMovementTask(
        Character bot,
        BotMovementState state,
        IBotMovementBroadcaster broadcaster,
        Action<BotMovementTask> onCancel,
        Func<bool, float> baseSpeed,
        Func<float, float, float> groundHeight,
        BotConfig config,
        TimeProvider time)
    {
        _bot = bot ?? throw new ArgumentNullException(nameof(bot));
        _state = state ?? throw new ArgumentNullException(nameof(state));
        _broadcaster = broadcaster ?? throw new ArgumentNullException(nameof(broadcaster));
        _onCancel = onCancel;
        _baseSpeed = baseSpeed ?? (running => GetBaseSpeed(running));
        _groundHeight = groundHeight ?? ((x, y) => _bot.ParentWorld.GetHeight(x, y));
        _config = config ?? BotConfig.Instance;
        _time = time ?? TimeProvider.System;
    }

    public override void OnCancel()
    {
        _onCancel?.Invoke(this);
    }

    internal void BindCombatState(BotCombatState combatState)
    {
        _combatState = combatState ?? throw new ArgumentNullException(nameof(combatState));
    }

    public void SetDestination(Character bot, Vector3 position, bool run = true, float tolerance = 0.5f)
    {
        if (!ReferenceEquals(bot, _bot))
            return;
        if (_state.Destination is { } current && Vector3.Distance(current, position) <= tolerance)
            return;

        _state.Destination = position;
        _state.IsRunning = run;
        _state.FallVelocity = 0;
        Logger.Trace($"BOT id={bot.Id} obj={bot.ObjId} ev=destination pos=({position.X}, {position.Y}, {position.Z}) run={run}");
    }

    public void StopIfMoving(Character bot)
    {
        if (!ReferenceEquals(bot, _bot) || _state.Destination == null)
            return;

        StopImmediately(bot);
    }

    public void StopImmediately(Character bot)
    {
        if (!ReferenceEquals(bot, _bot))
            return;

        ResetMovementState(_state);
        Logger.Trace($"BOT id={bot.Id} obj={bot.ObjId} ev=stop_immediately");
        if (bot.Transform == null)
            return;

        _broadcaster.SendStop(bot.Transform.World.Position, bot.IsInBattle);
        bot.Transform.FinalizeTransform();
    }

    public void Face(Character bot, float angle)
    {
        if (!ReferenceEquals(bot, _bot) || bot.Transform == null)
            return;

        _broadcaster.SendFaceTarget(bot.Transform.World.Position, angle - 90f, bot.IsInBattle);
        bot.Transform.FinalizeTransform();
    }

    public void Teleport(Character bot, Vector3 position)
    {
        if (!ReferenceEquals(bot, _bot))
            return;

        _broadcaster.SendTeleport(position, bot.IsInBattle);
        ResetMovementState(_state);
        Logger.Trace($"BOT id={bot.Id} obj={bot.ObjId} ev=teleport pos=({position.X}, {position.Y}, {position.Z})");
    }

    public void Follow(Character bot, Character target, float distance)
    {
        if (!ReferenceEquals(bot, _bot) || target == null)
            return;

        _state.FollowTarget = target;
        _state.FollowDistance = distance;
        _state.Destination = null;
        Logger.Trace($"BOT id={bot.Id} obj={bot.ObjId} ev=follow target={target.Id}");
    }

    public void StopFollow(Character bot)
    {
        if (!ReferenceEquals(bot, _bot))
            return;

        _state.FollowTarget = null;
        StopImmediately(bot);
    }

    public void SendRelaxedStance(Character bot)
    {
        if (!ReferenceEquals(bot, _bot) || bot.Transform == null)
            return;

        _broadcaster.SendRelaxedStance(bot.Transform.World.Position);
    }

    /// <summary>
    /// Queues one jump for the next mover tick. This is intentionally separate from
    /// IBotMover so existing positioning actions do not start jumping implicitly.
    /// </summary>
    public bool RequestJump()
    {
        if (!_config.JumpEnabled || _bot.Transform == null || _bot.ParentWorld == null || _bot.IsDead ||
            _bot.SkillTask != null || _state.JumpRequested || _state.IsJumping || IsMovementImpaired())
            return false;

        var now = _time.GetUtcNow().UtcDateTime;
        if (now < _state.NextJumpAllowedAt)
            return false;

        var position = _bot.Transform.World.Position;
        var groundZ = GetGroundHeight(position.X, position.Y);
        if (position.Z > groundZ + 0.15f)
            return false;

        _state.JumpRequested = true;
        return true;
    }

    public override void Execute()
    {
        if (Interlocked.CompareExchange(ref _state.Running, 1, 0) != 0)
            return;

        try
        {
            Step();
        }
        catch (Exception e)
        {
            _state.Diagnostics.RecordError(e);
            Logger.Error(e, $"BOT id={_bot?.Id} ev=tick_error task={GetType().Name}");
        }
        finally
        {
            Volatile.Write(ref _state.Running, 0);
        }
    }

    internal virtual void Step()
    {
        ExecuteCore();
    }

    private void ExecuteCore()
    {
        if (_bot.ParentWorld == null)
        {
            Cancelled = true;
            return;
        }

        if (_bot.IsDead)
        {
            if (_state.IsMoving || _state.IsJumping || _state.JumpRequested)
                StopAndClear(_bot.Transform.World.Position, forceFinalize: true);

            return;
        }

        if (IsMovementImpaired())
        {
            if (_state.Destination != null || _state.IsMoving || _state.IsFalling || _state.IsJumping || _state.JumpRequested)
                StopAndClear(_bot.Transform.World.Position, forceFinalize: true);

            return;
        }

        if (_bot.SkillTask != null)
        {
            if (_state.Destination != null || _state.IsMoving || _state.IsJumping || _state.JumpRequested)
                StopAndClear(_bot.Transform.World.Position, forceFinalize: true);

            return;
        }

        var followTarget = _state.FollowTarget;
        var followMovementActive = _combatState == null ||
                                   _combatState.CurrentState == BotCombatStateType.Following;
        if (followTarget != null && followMovementActive)
        {
            var targetPosition = _state.FormationSlot >= 0
                ? BotFormation.PositionFor(followTarget, _state)
                : followTarget.Transform.World.Position;
            var follow = BotMovementMath.ComputeFollowDestination(
                _bot.Transform.World.Position,
                targetPosition,
                _state.FormationSlot >= 0 ? 0.35f : _state.FollowDistance);
            _state.Destination = follow.Destination;
            if (follow.Destination.HasValue)
                _state.IsRunning = follow.Run;
        }

        var now = _time.GetUtcNow().UtcDateTime;
        var currentPosition = _bot.Transform.World.Position;
        var groundZ = GetGroundHeight(currentPosition.X, currentPosition.Y);
        TryStartJump(now, currentPosition, groundZ, _state.Destination);

        if (_state.Destination is { } destination)
        {
            var planarCurrent = new Vector3(currentPosition.X, currentPosition.Y, 0f);
            var planarDestination = new Vector3(destination.X, destination.Y, 0f);
            var distance = Vector3.Distance(planarCurrent, planarDestination);
            if (distance < 0.5f)
            {
                currentPosition.X = destination.X;
                currentPosition.Y = destination.Y;
                _state.Destination = null;
                if (!_state.IsJumping)
                {
                    currentPosition.Z = GetGroundHeight(currentPosition.X, currentPosition.Y);
                    _bot.Transform.Local.SetPosition(currentPosition.X, currentPosition.Y, currentPosition.Z);
                    StopAndClear(currentPosition, forceFinalize: false);
                    return;
                }
            }
            else
            {
                var finalSpeed = _baseSpeed(_state.IsRunning) * GetDirectionalMultiplier(destination, currentPosition) * _bot.MoveSpeedMul;
                var movement = BotMovementMath.StepTowards(planarCurrent, planarDestination, finalSpeed, TickInterval);
                currentPosition.X = movement.Next.X;
                currentPosition.Y = movement.Next.Y;
                if (movement.Arrived)
                    _state.Destination = null;

                groundZ = GetGroundHeight(currentPosition.X, currentPosition.Y);
                var airborne = AdvanceJump(ref currentPosition, groundZ);
                _bot.Transform.Local.SetPosition(currentPosition.X, currentPosition.Y, currentPosition.Z);

                var combatWithTarget = _bot.IsInBattle && _bot.CurrentTarget != null;
                var facingTarget = combatWithTarget
                    ? _bot.CurrentTarget.Transform.World.Position
                    : destination;
                var targetAngle = BotMovementMath.ComputeFacingDegrees(currentPosition, facingTarget);
                _bot.Transform.Local.SetRotationDegree(0f, 0f, targetAngle);

                var direction = planarDestination - new Vector3(currentPosition.X, currentPosition.Y, 0f);
                var moveDirection = direction.LengthSquared() < 1e-6f
                    ? Vector3.Zero
                    : Vector3.Normalize(direction);
                var velocity = BotMovementMath.ComputeVelocity(
                    moveDirection,
                    _bot.Transform.World.Rotation.Z,
                    finalSpeed,
                    combatWithTarget);

                if (airborne)
                    _broadcaster.SendJump(currentPosition, new Vector3(velocity.X, velocity.Y, _state.JumpVerticalVelocity), _bot.IsInBattle);
                else
                    _broadcaster.SendMove(currentPosition, velocity, _bot.IsInBattle);
                _bot.Transform.FinalizeTransform();
                _state.IsMoving = true;
                _state.IsFalling = airborne && _state.JumpVerticalVelocity < 0f;

                if (_state.Destination is null && !airborne)
                    StopAndClear(currentPosition, forceFinalize: false);

                return;
            }
        }

        groundZ = GetGroundHeight(currentPosition.X, currentPosition.Y);
        if (_state.IsJumping)
        {
            var airborne = AdvanceJump(ref currentPosition, groundZ);
            _bot.Transform.Local.SetHeight(currentPosition.Z);
            if (airborne)
            {
                _broadcaster.SendJump(currentPosition, new Vector3(0f, 0f, _state.JumpVerticalVelocity), _bot.IsInBattle);
                _bot.Transform.FinalizeTransform();
                _state.IsMoving = true;
                _state.IsFalling = _state.JumpVerticalVelocity < 0f;
            }
            else
            {
                StopAndClear(currentPosition, forceFinalize: false);
            }

            return;
        }

        if (currentPosition.Z > groundZ + 0.1f || _state.FallVelocity > 0f)
        {
            var gravity = BotMovementMath.ApplyGravity(currentPosition.Z, groundZ, _state.FallVelocity, TickInterval);
            if (gravity.Landed)
            {
                _state.FallVelocity = gravity.NewFallVelocity;
                _state.IsFalling = false;
                _bot.Transform.Local.SetHeight(gravity.NewZ);
                StopAndClear(new Vector3(currentPosition.X, currentPosition.Y, gravity.NewZ), forceFinalize: false);
                return;
            }

            _state.FallVelocity = gravity.NewFallVelocity;
            _state.IsFalling = gravity.Falling;
            _bot.Transform.Local.SetHeight(gravity.NewZ);
            _broadcaster.SendFall(new Vector3(currentPosition.X, currentPosition.Y, gravity.NewZ), _state.FallVelocity, _bot.IsInBattle);
            _bot.Transform.FinalizeTransform();
            _state.IsMoving = true;
        }
        else if (_state.IsMoving || _state.IsFalling)
        {
            _broadcaster.SendStop(currentPosition, _bot.IsInBattle);
            _state.IsMoving = false;
            _state.IsFalling = false;
            _state.FallVelocity = 0;
            _bot.Transform.FinalizeTransform();
        }
    }

    private void TryStartJump(DateTime now, Vector3 position, float groundZ, Vector3? destination)
    {
        if (!_config.JumpEnabled)
        {
            _state.JumpRequested = false;
            ResetAmbientJumpSchedule();
            return;
        }

        if (_state.IsJumping || now < _state.NextJumpAllowedAt || position.Z > groundZ + 0.15f)
            return;

        var requested = _state.JumpRequested;
        var ambient = !requested && ShouldAmbientJump(now, destination);
        var obstacle = !requested && !ambient && ShouldJumpTerrainStep(now, position, groundZ, destination);
        if (!requested && !ambient && !obstacle)
            return;

        _state.JumpRequested = false;
        _state.IsJumping = true;
        _state.IsFalling = false;
        _state.FallVelocity = 0f;
        _state.JumpVerticalVelocity = (float)_config.JumpLaunchSpeed;
        _state.NextJumpAllowedAt = now.AddMilliseconds(_config.JumpCooldownMs);
        ScheduleAmbientJump(now);
        Logger.Trace($"BOT id={_bot.Id} obj={_bot.ObjId} ev=jump reason={(requested ? "requested" : ambient ? "ambient" : "terrain_step")}");
    }

    private bool AdvanceJump(ref Vector3 position, float groundZ)
    {
        if (!_state.IsJumping)
        {
            position.Z = groundZ;
            return false;
        }

        var jump = BotMovementMath.ApplyJump(
            position.Z,
            groundZ,
            _state.JumpVerticalVelocity,
            TickInterval);
        position.Z = jump.NewZ;
        _state.JumpVerticalVelocity = jump.NewVerticalVelocity;
        if (!jump.Landed)
            return true;

        _state.IsJumping = false;
        _state.IsFalling = false;
        _state.JumpVerticalVelocity = 0f;
        return false;
    }

    private bool ShouldAmbientJump(DateTime now, Vector3? destination)
    {
        if (!_config.AmbientJumpEnabled || _bot.IsInBattle || _state.FollowTarget == null || destination == null)
        {
            ResetAmbientJumpSchedule();
            return false;
        }

        if (_state.NextAmbientJumpAt == DateTime.MinValue)
        {
            ScheduleAmbientJump(now);
            return false;
        }

        return now >= _state.NextAmbientJumpAt;
    }

    private bool ShouldJumpTerrainStep(DateTime now, Vector3 position, float groundZ, Vector3? destination)
    {
        if (!_config.ObstacleJumpEnabled || destination == null || now < _state.NextObstacleJumpProbeAt)
            return false;

        _state.NextObstacleJumpProbeAt = now.AddMilliseconds(_config.ObstacleJumpProbeIntervalMs);
        var direction = new Vector2(destination.Value.X - position.X, destination.Value.Y - position.Y);
        if (direction.LengthSquared() < 1e-4f)
            return false;

        direction = Vector2.Normalize(direction);
        var probeDistance = (float)_config.ObstacleJumpProbeDistance;
        var probeGroundZ = GetGroundHeight(
            position.X + direction.X * probeDistance,
            position.Y + direction.Y * probeDistance);
        var rise = probeGroundZ - groundZ;
        return rise >= (float)_config.ObstacleJumpMinRise && rise <= (float)_config.ObstacleJumpMaxRise;
    }

    private void ScheduleAmbientJump(DateTime now)
    {
        if (!_config.AmbientJumpEnabled)
        {
            ResetAmbientJumpSchedule();
            return;
        }

        var minMs = _config.AmbientJumpMinIntervalMs;
        var rangeMs = _config.AmbientJumpMaxIntervalMs - minMs;
        uint hash;
        unchecked
        {
            _state.AmbientJumpSequence++;
            hash = _bot.Id * 747_796_405u + _state.AmbientJumpSequence * 2_891_336_453u;
            hash = (hash >> ((int)(hash >> 28) + 4)) ^ hash;
            hash *= 277_803_737u;
            hash = (hash >> 22) ^ hash;
        }

        var fraction = hash / (double)uint.MaxValue;
        _state.NextAmbientJumpAt = now.AddMilliseconds(minMs + rangeMs * fraction);
    }

    private void ResetAmbientJumpSchedule()
    {
        _state.NextAmbientJumpAt = DateTime.MinValue;
    }

    private float GetBaseSpeed(bool running)
    {
        var currentStance = _bot.IsInBattle ? GameStanceType.Combat : GameStanceType.Relaxed;
        if (_cachedModelId != _bot.ModelId || _cachedStance != currentStance)
        {
            _cachedModelId = _bot.ModelId;
            _cachedStance = currentStance;
            _cachedRunSpeed = FallbackRunSpeed;
            _cachedWalkSpeed = FallbackWalkSpeed;

            var model = ModelManager.Instance.GetActorModel(_bot.ModelId);
            if (model != null && model.Stances.TryGetValue(currentStance, out var stance))
            {
                var resolved = BotMovementMath.ResolveSpeed(
                    stance.AiMoveSpeedRun,
                    stance.AiMoveSpeedWalk,
                    stance.MaxSpeed);
                _cachedRunSpeed = resolved.Run;
                _cachedWalkSpeed = resolved.Walk;
            }
        }

        return running ? _cachedRunSpeed : _cachedWalkSpeed;
    }

    private float GetDirectionalMultiplier(Vector3 destination, Vector3 currentPosition)
    {
        if (!_bot.IsInBattle || _bot.CurrentTarget == null)
            return 1.0f;

        var directionToTarget = _bot.CurrentTarget.Transform.World.Position - currentPosition;
        var moveDirection = destination - currentPosition;
        return BotMovementMath.DirectionalMultiplier(moveDirection, directionToTarget);
    }

    private bool IsMovementImpaired()
    {
        return _bot.Buffs.HasEffectsMatchingCondition(e =>
            e.Template.Root || e.Template.Stun || e.Template.Knockdown ||
            e.Template.Sleep || e.Template.Psychokinesis);
    }

    private float GetGroundHeight(float x, float y)
    {
        return _groundHeight(x, y);
    }

    private void StopAndClear(Vector3 position, bool forceFinalize)
    {
        _state.Destination = null;
        _state.IsMoving = false;
        _state.IsFalling = false;
        _state.FallVelocity = 0;
        _state.JumpRequested = false;
        _state.IsJumping = false;
        _state.JumpVerticalVelocity = 0;
        _broadcaster.SendStop(position, _bot.IsInBattle);
        if (forceFinalize)
            _bot.Transform.ResetFinalizeTransform();
        _bot.Transform.FinalizeTransform();
    }

    private static void ResetMovementState(BotMovementState state)
    {
        state.Destination = null;
        state.IsMoving = false;
        state.IsFalling = false;
        state.FallVelocity = 0;
        state.JumpRequested = false;
        state.IsJumping = false;
        state.JumpVerticalVelocity = 0;
    }
}
