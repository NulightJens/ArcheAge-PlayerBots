using System;
using System.Numerics;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Units;
using NLog;

namespace AAEmu.Game.Models.Game.Bots
{
    public enum BotCombatStateType
    {
        Idle,
        Grinding,
        Questing,
        Roaming,
        Following,
        Combat,
        Dueling,
        Resting,
        Searching   // Added for stealth search
    }

    public class BotCombatState
    {
        private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();

        internal uint BotId { get; set; }

        // ---- Core ----
        public bool IsActive { get; set; }
        internal BotDiagnostics Diagnostics { get; } = new();
        public BotCombatStateType CurrentState { get; set; } = BotCombatStateType.Idle;
        public BotCombatStateType PreviousState { get; set; } = BotCombatStateType.Idle;
        public BotCombatStateType BaseActivity { get; private set; } = BotCombatStateType.Idle;

        // ---- Forced state override (null = automatic) ----
        public BotCombatStateType? ForcedState { get; set; }

        // ---- Target ----
        public Unit Target { get; set; }
        public bool SentRelaxedAfterCombat { get; set; }

        // ---- Rest ----
        public bool IsResting { get; set; }

        // ---- Kill tracking ----
        private int _killCount;
        public int KillCount
        {
            get => Volatile.Read(ref _killCount);
            set => Volatile.Write(ref _killCount, value);
        }
        public uint? TargetTypeFilter { get; set; }
        public int? KillGoal { get; set; }

        // ---- Respawn ----
        public bool RespawnScheduled { get; set; }
        public bool ShouldRespawn { get; set; }

        // ---- Combat timing ----
        public DateTime LastCombatTime { get; set; } = DateTime.UtcNow;
        public float LastFacingAngle { get; set; } = float.MinValue;

        // ---- Duel ----
        public bool InDuel { get; set; }
        public Unit DuelOpponent { get; set; }
        public bool WasCombatActive { get; set; }

        // ---- Pending duel request ----
        public bool DuelRequestPending { get; set; }
        public Character DuelChallenger { get; set; }

        // ---- Combo ----
        public uint LastComboSkill { get; set; }
        public DateTime LastComboSkillTime { get; set; }
        public double ComboWindowMs { get; set; } = 2000;

        // ---- Roaming ----
        public Vector3? RoamDestination { get; set; }
        // ---- Archetype ----
        public string ActiveArchetype { get; set; }

        // ---- Stealth search ----
        public Unit LostTarget { get; set; }
        public Vector3? LastKnownTargetPosition { get; set; }
        public DateTime SearchStartTime { get; set; }
        public bool IsSearching { get; set; }
        public float SearchRadius { get; set; } = 0f;      // Current search radius (0-30m)
        public float SearchAngle { get; set; } = 0f;       // Current angle in radians
        public DateTime LastRestHealTick { get; set; }

        // ---- Methods ----

        public void SetCombo(uint skillId)
        {
            LastComboSkill = skillId;
            LastComboSkillTime = DateTime.UtcNow;
        }

        public void ClearCombo()
        {
            LastComboSkill = 0;
            LastComboSkillTime = DateTime.MinValue;
        }

        internal int CreditKill()
        {
            return Interlocked.Increment(ref _killCount);
        }

        public bool IsComboActive(uint skillId)
        {
            if (LastComboSkill != skillId) return false;
            if ((DateTime.UtcNow - LastComboSkillTime).TotalMilliseconds > ComboWindowMs) return false;
            return true;
        }

        /// <summary>
        /// Transitions to a new state. Automatically manages IsActive based on the target state:
        /// - Grinding/Questing → IsActive = true
        /// - Idle → IsActive = false
        /// - Other states → IsActive unchanged
        /// </summary>
        public void TransitionTo(BotCombatStateType newState)
        {
            if (CurrentState == newState) return;

            if (newState is BotCombatStateType.Idle or BotCombatStateType.Grinding or BotCombatStateType.Questing or
                BotCombatStateType.Roaming or BotCombatStateType.Following)
                BaseActivity = newState;

            PreviousState = CurrentState;
            CurrentState = newState;

            if (newState is BotCombatStateType.Combat or BotCombatStateType.Dueling)
                SentRelaxedAfterCombat = false;

            ApplyActiveRule(newState);
            Logger.Info($"BOT id={BotId} ev=transition from={PreviousState} to={CurrentState}");
            // For Combat, Dueling, Roaming, Following, Resting, Searching: keep current IsActive
        }

        public void RestorePreviousState()
        {
            TransitionTo(BaseActivity);
        }

        private void ApplyActiveRule(BotCombatStateType state)
        {
            if (state is BotCombatStateType.Grinding or BotCombatStateType.Questing)
                IsActive = true;
            else if (state == BotCombatStateType.Idle)
                IsActive = false;
        }

        // ---- Stalker mode (Darkrunner) ----
        public bool IsStalking { get; set; }
        public DateTime StalkerStartTime { get; set; }
        public int StalkerStage { get; set; } // 0=entry, 1=circling, 2=exit

        // ---- Combo lock ----
        public bool IsComboLocked { get; set; }
        public DateTime ComboLockStartTime { get; set; }
        public uint PendingComboFollowUp { get; set; } // The skill we're waiting to use
        internal const double DefaultComboLockDurationMs = 2000;
        public double ComboLockDurationMs { get; private set; } = DefaultComboLockDurationMs;

        // ---- Skill timing ----
        public DateTime LastSkillTime { get; set; } = DateTime.MinValue;
        public DateTime LastGapCloserTime { get; set; } = DateTime.MinValue;

        // ---- Triple Slash combo tracking (3 hits: 18131 -> 18132 -> 18134) ----
        public int TripleSlashStage { get; set; } // 0 = idle, 1 = used first hit, 2 = used second hit

        // ---- Triple Slash combo timing ----
        public DateTime LastTripleSlashTime { get; set; } = DateTime.MinValue;

        // ---- Forced state helpers ----
        public bool IsForced => ForcedState.HasValue;

        public void BeginCombo(uint opener, uint followUp, double lockMs = DefaultComboLockDurationMs,
            DateTime? now = null)
        {
            var timestamp = now ?? DateTime.UtcNow;
            LastComboSkill = opener;
            LastComboSkillTime = timestamp;
            PendingComboFollowUp = followUp;
            IsComboLocked = true;
            ComboLockStartTime = timestamp;
            ComboLockDurationMs = lockMs;
            LastSkillTime = timestamp;
        }

        public void EndCombo()
        {
            IsComboLocked = false;
            PendingComboFollowUp = 0;
            ComboLockDurationMs = DefaultComboLockDurationMs;
        }

        internal static bool IsTemporary(BotCombatStateType state)
        {
            return state is BotCombatStateType.Combat or BotCombatStateType.Dueling or BotCombatStateType.Searching or BotCombatStateType.Resting;
        }

        public bool ShouldRevertToForced()
        {
            return IsForced && !IsTemporary(CurrentState) && CurrentState != ForcedState;
        }

        /// <summary>
        /// Sets the forced state. If null, automatic behavior resumes.
        /// </summary>
        public void SetForcedState(BotCombatStateType? state)
        {
            ForcedState = state;
            if (state.HasValue && CurrentState != state.Value && CurrentState != BotCombatStateType.Combat && CurrentState != BotCombatStateType.Dueling)
            {
                // Immediately transition if not in a temporary state
                TransitionTo(state.Value);
            }
        }

        /// <summary>
        /// Called after a temporary state (Combat/Dueling) ends to return to forced state if set.
        /// </summary>
        public void RevertToForcedState()
        {
            if (ForcedState.HasValue && CurrentState != ForcedState.Value)
            {
                TransitionTo(ForcedState.Value);
            }
        }
    }
}
