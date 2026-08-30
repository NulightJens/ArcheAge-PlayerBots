using System;
using System.Numerics;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Models;
using AAEmu.Game.Models.Game.Units.Movements;
using AAEmu.Game.Models.StaticValues;
using AAEmu.Game.Utils;

namespace AAEmu.Game.Models.Game.Bots
{
    public class BotMovementBroadcaster : IBotMovementBroadcaster
    {
        private readonly Character _bot;
        private readonly TimeProvider _time;

        private Vector3 _lastPosition;
        private Vector3 _lastVelocity;
        private GameStanceType _lastStance;
        private MoveTypeAlertness _lastAlertness;
        private MoveTypeFlags _lastFlags;
        private byte _lastActorFlags;
        private DateTime _lastSendTime;

        private const double MinSendIntervalMs = 50;

        internal Action<UnitMoveType> MoveTypeSink { get; set; }

        public BotMovementBroadcaster(Character bot, TimeProvider time = null)
        {
            _bot = bot ?? throw new ArgumentNullException(nameof(bot));
            _time = time ?? TimeProvider.System;
            _lastPosition = bot.Transform.World.Position;
            _lastVelocity = Vector3.Zero;
            _lastStance = GameStanceType.Relaxed;
            _lastAlertness = MoveTypeAlertness.Idle;
            _lastFlags = MoveTypeFlags.Stopping;
            _lastActorFlags = 0;
            _lastSendTime = _time.GetUtcNow().UtcDateTime;
            MoveTypeSink = moveType => _bot.BroadcastPacket(new SCOneUnitMovementPacket(_bot.ObjId, moveType), true);
        }

        public void SendMove(Vector3 pos, Vector3 velocity, bool isInBattle)
        {
            float speed = velocity.Length();
            bool running = speed > 3.0f;

            byte actorFlags = running ? (byte)4 : (byte)5;
            MoveTypeFlags flags = MoveTypeFlags.Moving;
            GameStanceType stance = isInBattle ? GameStanceType.Combat : GameStanceType.Relaxed;
            MoveTypeAlertness alertness = isInBattle ? MoveTypeAlertness.Combat : MoveTypeAlertness.Idle;

            if (IsStateUnchanged(pos, velocity, stance, alertness, flags, actorFlags))
                return;

            UpdateLastState(pos, velocity, stance, alertness, flags, actorFlags);
            BuildAndBroadcast(pos, velocity, stance, alertness, flags, actorFlags, isStop: false);
        }

        public void SendStop(Vector3 pos, bool isInBattle)
        {
            SendStationary(pos, isInBattle);
        }

        public void SendFall(Vector3 pos, float fallVelocity, bool isInBattle)
        {
            Vector3 velocity = new Vector3(0, 0, fallVelocity);
            byte actorFlags = 0;
            MoveTypeFlags flags = MoveTypeFlags.Moving;
            GameStanceType stance = isInBattle ? GameStanceType.Combat : GameStanceType.Relaxed;
            MoveTypeAlertness alertness = isInBattle ? MoveTypeAlertness.Combat : MoveTypeAlertness.Idle;

            if (IsStateUnchanged(pos, velocity, stance, alertness, flags, actorFlags))
                return;

            UpdateLastState(pos, velocity, stance, alertness, flags, actorFlags);
            BuildAndBroadcast(pos, velocity, stance, alertness, flags, actorFlags, isStop: false);
        }

        public void SendJump(Vector3 pos, Vector3 velocity, bool isInBattle)
        {
            // Actor movement encodes falling as positive VelZ, while world-space Z is upward.
            var packetVelocity = new Vector3(velocity.X, velocity.Y, -velocity.Z);
            // DeltaMovement is client input, not velocity. Advertising full-forward input for
            // an idle jump makes the client blend the run cycle over the vertical arc. Keep it
            // neutral unless the bot is genuinely carrying horizontal movement through the jump.
            var hasHorizontalMovement = MathF.Abs(velocity.X) > 0.01f || MathF.Abs(velocity.Y) > 0.01f;
            byte actorFlags = (byte)MoveTypeActorFlags.Jumping;
            var flags = MoveTypeFlags.Jumping;
            var stance = StanceFor(isInBattle);
            var alertness = isInBattle ? MoveTypeAlertness.Combat : MoveTypeAlertness.Idle;

            if (IsStateUnchanged(pos, packetVelocity, stance, alertness, flags, actorFlags))
                return;

            UpdateLastState(pos, packetVelocity, stance, alertness, flags, actorFlags);
            BuildAndBroadcast(pos, packetVelocity, stance, alertness, flags, actorFlags,
                isStop: !hasHorizontalMovement);
        }

        public void SendTeleport(Vector3 pos, bool isInBattle)
        {
            _bot.Transform.ResetFinalizeTransform();
            _bot.Transform.Local.SetPosition(pos.X, pos.Y, pos.Z);
            _bot.Transform.FinalizeTransform();

            SendStationary(pos, isInBattle);
            _lastPosition = pos - new Vector3(1, 1, 1);
        }

        public void SendFaceTarget(Vector3 pos, float rotationZ, bool isInBattle)
        {
            _bot.Transform.Local.SetRotationDegree(0f, 0f, rotationZ);

            var zeroVelocity = Vector3.Zero;
            var actorFlags = (byte)0;
            var flags = MoveTypeFlags.Stopping;
            var stance = StanceFor(isInBattle);
            var alertness = isInBattle ? MoveTypeAlertness.Combat : MoveTypeAlertness.Idle;

            if (IsStateUnchanged(pos, zeroVelocity, stance, alertness, flags, actorFlags))
                return;

            UpdateLastState(pos, zeroVelocity, stance, alertness, flags, actorFlags);
            BuildAndBroadcast(pos, zeroVelocity, stance, alertness, flags, actorFlags, isStop: true);
        }

        public void SendRelaxedStance(Vector3 pos)
        {
            SendStationary(pos, false);

            _bot.BroadcastPacket(new SCUnitModelPostureChangedPacket(_bot, 0, false), true);
        }

        // ---- Private helpers ----

        private void SendStationary(Vector3 pos, bool isInBattle)
        {
            var zeroVelocity = Vector3.Zero;
            var actorFlags = (byte)0;
            var flags = MoveTypeFlags.Stopping;
            var stance = StanceFor(isInBattle);
            var alertness = isInBattle ? MoveTypeAlertness.Combat : MoveTypeAlertness.Idle;

            UpdateLastState(pos, zeroVelocity, stance, alertness, flags, actorFlags);
            BuildAndBroadcast(pos, zeroVelocity, stance, alertness, flags, actorFlags, isStop: true);
        }

        private static GameStanceType StanceFor(bool isInBattle)
        {
            return isInBattle ? GameStanceType.Combat : GameStanceType.Relaxed;
        }

        private bool IsStateUnchanged(Vector3 pos, Vector3 velocity, GameStanceType stance,
            MoveTypeAlertness alertness, MoveTypeFlags flags, byte actorFlags)
        {
            if ((_time.GetUtcNow().UtcDateTime - _lastSendTime).TotalMilliseconds < MinSendIntervalMs)
                return true;

            bool posChanged = Vector3.Distance(pos, _lastPosition) > 0.01f;
            bool velChanged = Vector3.Distance(velocity, _lastVelocity) > 0.01f;
            bool stanceChanged = stance != _lastStance;
            bool alertnessChanged = alertness != _lastAlertness;
            bool flagsChanged = flags != _lastFlags;
            bool actorFlagsChanged = actorFlags != _lastActorFlags;

            if (posChanged || velChanged || stanceChanged || alertnessChanged || flagsChanged || actorFlagsChanged)
                return false;

            return true;
        }

        private void UpdateLastState(Vector3 pos, Vector3 velocity, GameStanceType stance,
            MoveTypeAlertness alertness, MoveTypeFlags flags, byte actorFlags)
        {
            _lastPosition = pos;
            _lastVelocity = velocity;
            _lastStance = stance;
            _lastAlertness = alertness;
            _lastFlags = flags;
            _lastActorFlags = actorFlags;
            _lastSendTime = _time.GetUtcNow().UtcDateTime;
        }

        private void BuildAndBroadcast(Vector3 pos, Vector3 velocity, GameStanceType stance,
            MoveTypeAlertness alertness, MoveTypeFlags flags, byte actorFlags, bool isStop)
        {
            var moveType = BuildMoveType(pos, velocity, stance, alertness, flags, actorFlags, isStop);
            MoveTypeSink(moveType);
        }

        internal UnitMoveType BuildMoveType(Vector3 pos, Vector3 velocity, GameStanceType stance,
            MoveTypeAlertness alertness, MoveTypeFlags flags, byte actorFlags, bool isStop)
        {
            var moveType = (UnitMoveType)MoveType.GetType(MoveTypeEnum.Unit);
            moveType.X = pos.X;
            moveType.Y = pos.Y;
            moveType.Z = pos.Z;

            moveType.VelX = (short)Math.Clamp(velocity.X * 1000f, (float)short.MinValue, (float)short.MaxValue);
            moveType.VelY = (short)Math.Clamp(velocity.Y * 1000f, (float)short.MinValue, (float)short.MaxValue);
            moveType.VelZ = (short)Math.Clamp(velocity.Z * 1000f, (float)short.MinValue, (float)short.MaxValue);

            var (rx, ry, rz) = _bot.Transform.Local.ToRollPitchYawSBytesMovement();
            moveType.RotationX = rx;
            moveType.RotationY = ry;
            moveType.RotationZ = rz;

            moveType.ActorFlags = actorFlags;
            moveType.Flags = flags;

            // Critical fix: DeltaMovement must be zero for stop packets
            if (isStop)
                moveType.DeltaMovement = new sbyte[3] { 0, 0, 0 };
            else
                moveType.DeltaMovement = new sbyte[3] { 0, 127, 0 };

            moveType.Stance = stance;
            moveType.Alertness = alertness;
            var now = _time.GetUtcNow().UtcDateTime;
            moveType.Time = (uint)(now - now.Date).TotalMilliseconds;

            return moveType;
        }
    }
}
