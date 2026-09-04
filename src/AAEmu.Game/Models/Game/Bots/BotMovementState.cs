using System.Numerics;
using AAEmu.Game.Bots.Navigation;
using AAEmu.Game.Models.Game.Char;

namespace AAEmu.Game.Models.Game.Bots
{
    public class BotMovementState
    {
        public Vector3? Destination { get; set; }
        /// <summary>
        /// The behavior-owned final destination while <see cref="Destination"/> advances
        /// through collision-aware local and road waypoints.
        /// </summary>
        public Vector3? TravelDestination { get; internal set; }
        public string TravelMode { get; internal set; } = "direct";
        public int TravelWaypointCount => TravelWaypoints.Count + (Destination.HasValue ? 1 : 0);
        internal Queue<Vector3> TravelWaypoints { get; } = new();
        /// <summary>The short-horizon point currently driving smooth route steering.</summary>
        public Vector3? SteeringDestination { get; internal set; }
        /// <summary>Retained horizontal speed for acceleration and braking between route ticks.</summary>
        public float TravelSpeed { get; internal set; }
        /// <summary>Approximate path distance remaining, updated without re-planning the route.</summary>
        public float TravelRemainingDistance { get; internal set; }
        internal Vector3 TravelDirection { get; set; }
        public NavigationDecision? LastNavigationDecision { get; internal set; }
        internal Vector3? ApprovedNavigationDestination { get; set; }
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
