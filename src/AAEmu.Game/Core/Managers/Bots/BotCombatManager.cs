using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading;
using AAEmu.Commons.Utils;
using AAEmu.Game.Bots.Blackboard;
using AAEmu.Game.Bots.Body;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Duels;
using AAEmu.Game.Models.Game.Models;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Static;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.Movements;
using AAEmu.Game.Models.Game.Units.Static;
using AAEmu.Game.Models.Tasks.Bots;
using AAEmu.Game.Models.StaticValues;
using NLog;

namespace AAEmu.Game.Core.Managers.Bots
{
    public class BotCombatManager : Singleton<BotCombatManager>, IBotCombatManager
    {
        private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();

        private readonly ConcurrentDictionary<uint, BotCombatState> _combatStates = new();
        private readonly ConcurrentDictionary<uint, BotCombatTask> _combatTasks = new();
#if PLAYERBOTS_AAEMU_3_0
        private readonly ConcurrentDictionary<uint, EventHandler<OnDamagedArgs>> _damageHandlers = new();
#endif
        private readonly ITaskManager _taskManager;
        private readonly IDuelManager _duelManager;
        private readonly Lazy<IBotManager> _botManager;
        private readonly IBotArchetypeManager _botArchetypeManager;
        private readonly IBotHost _botHost;

        internal BotCombatManager()
        {
            _taskManager = null;
            _duelManager = null;
            _botManager = new Lazy<IBotManager>(() => BotManager.Instance);
            _botArchetypeManager = null;
            _botHost = null;
        }

        public BotCombatManager(
            ITaskManager taskManager,
            IDuelManager duelManager,
            Lazy<IBotManager> botManager,
            IBotArchetypeManager botArchetypeManager,
            IBotHost botHost = null)
        {
            _taskManager = taskManager ?? throw new ArgumentNullException(nameof(taskManager));
            _duelManager = duelManager ?? throw new ArgumentNullException(nameof(duelManager));
            _botManager = botManager ?? throw new ArgumentNullException(nameof(botManager));
            _botArchetypeManager = botArchetypeManager ?? throw new ArgumentNullException(nameof(botArchetypeManager));
            _botHost = botHost;
        }

        private ITaskManager Scheduler => _taskManager ?? TaskManager.Instance;
        private IDuelManager Duel => _duelManager ?? DuelManager.Instance;
        private IBotManager Bots => _botManager.Value;
        private IBotArchetypeManager Archetypes => _botArchetypeManager ?? BotArchetypeManager.Instance;
        private IBotHost Host => _botHost ?? BotHost.Instance;

        // ---- Public API ----

        public virtual void StartListening(Character bot)
        {
            if (bot == null) return;
            var runtime = Host.GetRuntime(bot.Id);
            var state = _combatStates.GetOrAdd(bot.Id, _ => runtime?.CombatState ?? new BotCombatState { BotId = bot.Id });
            state.WasCombatActive = state.IsActive;
#if PLAYERBOTS_AAEMU_3_0
            StartDamageTracking(bot);
#endif
            EnsureTask(bot, state);
        }

        public virtual void StopListening(Character bot)
        {
            if (bot == null)
                return;

            if (_combatTasks.TryRemove(bot.Id, out var task))
            {
                task.Cancelled = true;
            }
#if PLAYERBOTS_AAEMU_3_0
            StopDamageTracking(bot);
#endif
            Host.Unregister(bot.Id);
            _combatStates.TryRemove(bot.Id, out _);
        }

#if PLAYERBOTS_AAEMU_3_0
        private void StartDamageTracking(Character bot)
        {
            var handler = _damageHandlers.GetOrAdd(bot.Id, _ => (_, args) =>
            {
                if (args?.Attacker == null || ReferenceEquals(args.Attacker, bot))
                    return;

                var aggro = bot.AggroTable.GetOrAdd(args.Attacker.ObjId,
                    _ => new Aggro(args.Attacker));
                aggro.AddAggro(AggroKind.Damage, Math.Max(1, args.Amount));
            });
            bot.Events.OnDamaged -= handler;
            bot.Events.OnDamaged += handler;
        }

        private void StopDamageTracking(Character bot)
        {
            if (_damageHandlers.TryRemove(bot.Id, out var handler))
                bot.Events.OnDamaged -= handler;
            bot.AggroTable.Clear();
        }
#endif

        public void EnableCombat(Character bot, uint? targetTypeFilter = null, int? killGoal = null)
        {
            if (bot == null) return;
            var runtime = Host.GetRuntime(bot.Id);
            var state = _combatStates.GetOrAdd(bot.Id, _ => runtime?.CombatState ?? new BotCombatState { BotId = bot.Id });
            state.IsActive = true;
            state.TargetTypeFilter = targetTypeFilter;
            state.KillGoal = killGoal;
            state.KillCount = 0;
            state.Target = null;
            state.ShouldRespawn = false;
            state.LastCombatTime = DateTime.UtcNow;
            state.LastFacingAngle = float.MinValue;

            if (!state.IsForced && state.CurrentState == BotCombatStateType.Idle)
                state.TransitionTo(BotCombatStateType.Grinding);

            EnsureTask(bot, state);
            Logger.Trace($"BOT id={bot.Id} ev=combat_enabled");
        }

        public void DisableCombat(Character bot)
        {
            if (bot == null) return;
            DetachCombatTask(bot.Id);
            if (_combatStates.TryGetValue(bot.Id, out var state))
            {
                state.IsActive = false;
                state.Target = null;
                state.IsResting = false;
                state.SetForcedState(null);
                state.TransitionTo(BotCombatStateType.Idle);
                Bots.StopImmediately(bot);
                SendRelaxedStance(bot);
                Logger.Trace($"BOT id={bot.Id} ev=combat_disabled");
            }
        }

        public bool IsCombatEnabled(Character bot)
        {
            return bot != null && _combatStates.TryGetValue(bot.Id, out var state) && state.IsActive;
        }

        public virtual BotCombatState GetState(Character bot)
        {
            if (bot == null) return null;
            _combatStates.TryGetValue(bot.Id, out var state);
            return state;
        }

        public bool IsTaskRunning(uint characterId)
        {
            return _combatTasks.TryGetValue(characterId, out var task) && !task.Cancelled;
        }

        public void ResetCombat(Character bot)
        {
            if (bot == null) return;
            var state = GetState(bot);
            if (state == null) return;
            state.Target = null;
            state.StopAtTargetHpPercent = null;
            state.IsResting = false;
            Host.GetRuntime(bot.Id)?.Blackboard.InvalidateAll();
            state.LastFacingAngle = float.MinValue;
            state.LastCombatTime = DateTime.UtcNow;
            state.LastKnownTargetPosition = null;
            state.IsSearching = false;
            state.SearchRadius = 0f;
            state.SearchAngle = 0f;
            if (state.CurrentState == BotCombatStateType.Combat || state.CurrentState == BotCombatStateType.Dueling || state.CurrentState == BotCombatStateType.Searching)
                state.TransitionTo(BotCombatStateType.Idle);
            Logger.Trace($"BOT id={bot.Id} ev=combat_reset");
        }

        public virtual void ResetBot(Character bot)
        {
            if (bot == null)
                return;

            var state = GetState(bot);
            var wasActive = state?.IsActive ?? false;
            var forcedState = state?.ForcedState;
            var targetTypeFilter = state?.TargetTypeFilter;
            var killGoal = state?.KillGoal;

            Bots.StopImmediately(bot);
            ResetCombat(bot);
            Archetypes.ForceReevaluate(bot);

            if (wasActive)
            {
                EnableCombat(bot, targetTypeFilter, killGoal);
                if (forcedState is { } forced)
                    SetForcedState(bot, forced);
            }
        }

        public void StartDuel(Character bot, Unit opponent)
        {
            if (bot == null || opponent == null) return;
            var state = _combatStates.GetOrAdd(bot.Id, _ => new BotCombatState { BotId = bot.Id });
            state.WasCombatActive = state.IsActive;
            state.IsActive = true;
            state.InDuel = true;
            state.DuelOpponent = opponent;
            state.Target = opponent;
            state.IsResting = false;
            state.ShouldRespawn = false;
            state.TransitionTo(BotCombatStateType.Dueling);

            bot.IsInBattle = true;
            opponent.IsInBattle = true;

            EnsureTask(bot, state);
            Logger.Info($"Bot '{bot.Name}' entered duel against '{opponent.Name}'");
        }

        public void EndDuel(Character bot)
        {
            if (bot == null) return;
            if (_combatStates.TryGetValue(bot.Id, out var state))
            {
                if (!state.InDuel)
                    return;

                state.InDuel = false;
                state.DuelOpponent = null;
                state.Target = null;
                state.LastFacingAngle = float.MinValue;
                state.IsActive = state.WasCombatActive;

                state.RestorePreviousState();
                state.RevertToForcedState();

                if (!state.IsActive)
                {
                    Bots.StopImmediately(bot);
                    SendRelaxedStance(bot);
                    DetachCombatTask(bot.Id);
                }
                else
                {
                    state.Target = null;
                    Host.GetRuntime(bot.Id)?.Blackboard.InvalidateAll();
                    state.LastKnownTargetPosition = null;
                    state.IsSearching = false;
                    state.SearchRadius = 0f;
                    state.SearchAngle = 0f;
                }
                Logger.Info($"Bot '{bot.Name}' duel ended. Restored combat state: {state.IsActive}");
            }
        }

        // ---- Hook called from DuelManager ----
        public bool OnDuelRequested(Character bot, Character challenger)
        {
            if (bot == null || challenger == null)
                return false;

            if (!_combatStates.TryGetValue(bot.Id, out var state))
                return false;

            if (bot.Expedition != null)
            {
                Logger.Info($"Bot '{bot.Name}' is in an expedition and refused the duel request.");
                Duel.DuelCancel(challenger.Id, ErrorMessageType.TargetRejectedDuel);
                return true;
            }

            if (state.DuelRequestPending)
                return true;

            state.DuelRequestPending = true;
            state.DuelChallenger = challenger;
            var delay = Random.Shared.Next(1000, 5000);
            var acceptTask = new DuelAcceptTask(bot, challenger, state);
            Scheduler.Schedule(acceptTask, TimeSpan.FromMilliseconds(delay));
            Logger.Info($"Bot '{bot.Name}' received duel request from '{challenger.Name}', will accept in {delay}ms");
            return true;
        }

        public void OnDuelStarted(Duel duel)
        {
            ArgumentNullException.ThrowIfNull(duel);

            if (GetState(duel.Challenger) != null)
                StartDuel(duel.Challenger, duel.Challenged);
            if (GetState(duel.Challenged) != null)
                StartDuel(duel.Challenged, duel.Challenger);
        }

        public void OnDuelEnded(Duel duel)
        {
            ArgumentNullException.ThrowIfNull(duel);

            if (GetState(duel.Challenger) != null)
                EndDuel(duel.Challenger);
            if (GetState(duel.Challenged) != null)
                EndDuel(duel.Challenged);
        }

        // ---- Forced state control ----
        public void SetForcedState(Character bot, BotCombatStateType? state)
        {
            if (bot == null) return;
            var combatState = GetState(bot);
            if (combatState == null) return;
            combatState.SetForcedState(state);
            if (!state.HasValue && combatState.IsActive && combatState.CurrentState == BotCombatStateType.Idle)
                combatState.TransitionTo(BotCombatStateType.Grinding);
            Logger.Trace($"BOT id={bot.Id} ev=forced_state state={state?.ToString() ?? "auto"}");
        }

        private void RemoveCombatTask(BotCombatTask task)
        {
            if (_combatTasks.TryGetValue(task.BotId, out var current) && ReferenceEquals(current, task))
            {
                _combatTasks.TryRemove(task.BotId, out _);
                Host.Unregister(task.BotId);
            }
        }

        private void EnsureTask(Character bot, BotCombatState state)
        {
            if (_combatTasks.TryGetValue(bot.Id, out var existing))
            {
                if (!existing.Cancelled && ReferenceEquals(existing.State, state))
                    return;

                // A bot respawn can replace the manager/runtime state before an
                // older brain has been retired. Never leave that brain consuming
                // stale combat controls (targets, forced state, health floors).
                DetachCombatTask(bot.Id);
            }

            var broadcaster = Bots.GetBroadcaster(bot.Id);
            var runtime = Host.GetRuntime(bot.Id);
            var task = new BotCombatTask(
                bot,
                state,
                broadcaster,
                RemoveCombatTask,
                blackboard: runtime?.Blackboard,
                timeProvider: Host.TimeProvider);
            if (!_combatTasks.TryAdd(bot.Id, task))
                return;

            if (runtime == null)
            {
                var movementState = Bots.GetBotState(bot.Id) ?? new BotMovementState();
                var mover = broadcaster == null ? null : new BotMovementTask(bot, movementState, broadcaster);
                runtime = new BotRuntime(bot, movementState, state, broadcaster, mover, task, task.Blackboard);
                Host.Register(runtime);
            }
            else
            {
                runtime.Brain = task;
            }
        }

        private void DetachCombatTask(uint botId)
        {
            if (!_combatTasks.TryRemove(botId, out var task))
                return;

            // Detach the brain BEFORE marking it cancelled: the host retires a runtime when either task is
            // cancelled, so publishing Cancelled first would let a concurrent tick retire the mover too.
            var runtime = Host.GetRuntime(botId);
            if (runtime != null)
            {
                lock (runtime.SyncRoot)
                {
                    if (runtime.Brain == task)
                        runtime.Brain = null;
                    task.Cancelled = true;
                }
            }
            else
            {
                task.Cancelled = true;
            }
        }

        // ---- Internal helpers ----

        internal static void SendRelaxedStance(Character bot, IBotMover mover = null)
        {
            if (bot == null)
                return;

            if (mover != null)
            {
                mover.SendRelaxedStance(bot);
                return;
            }

            if (bot.Transform != null)
                BotManager.Instance.GetBroadcaster(bot.Id)?.SendRelaxedStance(bot.Transform.World.Position);
        }

        // ---- Internal Duel Accept Task ----
        internal sealed class DuelAcceptTask : global::AAEmu.Game.Models.Tasks.Task
        {
            private readonly Character _bot;
            private readonly Character _challenger;
            private readonly BotCombatState _state;

            public DuelAcceptTask(Character bot, Character challenger, BotCombatState state)
            {
                _bot = bot;
                _challenger = challenger;
                _state = state;
            }

            public override void Execute()
            {
                if (BotManager.Instance.GetBot(_bot.Id) != _bot) return;

                if (_state.DuelRequestPending && _state.DuelChallenger == _challenger)
                {
                    DuelManager.Instance.DuelAccepted(_bot, _challenger.Id);
                    _state.DuelRequestPending = false;
                    _state.DuelChallenger = null;
                    Logger.Trace($"BOT id={_bot.Id} ev=duel_auto_accept opponent={_challenger.Id}");
                }
                else
                {
                    _state.DuelRequestPending = false;
                    _state.DuelChallenger = null;
                }
            }
        }

    }
}
