using System.Numerics;
using AAEmu.Game.Models.Game.Char;

namespace AAEmu.Game.Models.Game.Bots
{
    public class BotMovementState
    {
        public Vector3? Destination { get; set; }
        public bool IsRunning { get; set; } = true;
        public float FallVelocity { get; set; }
        public bool IsMoving { get; set; }
        public bool IsFalling { get; set; }
        public bool JumpRequested { get; set; }
        public bool IsJumping { get; set; }
        /// <summary>World-space vertical velocity in metres per second; positive is upward.</summary>
        public float JumpVerticalVelocity { get; set; }
        public DateTime NextJumpAllowedAt { get; set; }
        public DateTime NextAmbientJumpAt { get; set; }
        public DateTime NextObstacleJumpProbeAt { get; set; }
        public uint AmbientJumpSequence { get; set; }
        public Vector3? LastPos { get; set; }
        public DateTime LastMoveAt { get; set; }
        public int Attempts { get; set; }
        internal int Running;
        internal BotDiagnostics Diagnostics { get; } = new();

        // Follow feature
        public Character FollowTarget { get; set; }
        public float FollowDistance { get; set; } = 2.0f; // how close to stand
        public int FormationSlot { get; set; } = -1;
        public int FormationColumns { get; set; }
        public int FormationMemberCount { get; set; }
        public float FormationSpacing { get; set; } = 2.5f;
    }
}
