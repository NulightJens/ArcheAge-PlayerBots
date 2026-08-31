using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using AAEmu.Game.Bots.Blackboard;
using AAEmu.Game.Bots.Body;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.Bots;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.AI.Bots;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Models;
using AAEmu.Game.Models.Game.NPChar;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Skills.Static;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Game.Units.Static;
using AAEmu.Game.Models.StaticValues;
using NLog;

namespace AAEmu.Game.Models.Tasks.Bots;

public class BotCombatTask : Task
{
    internal const float MaximumQuestTargetSearchRadius = 100f;
    internal const float MaximumQuestTargetSurfaceOffset = 15f;

    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();
    private readonly Character _bot;
    private readonly BotCombatState _state;
    private readonly BotMovementState _movementState;
    private readonly BotMovementBroadcaster _broadcaster;
    private readonly Action<BotCombatTask> _onCancel;
    private readonly Func<Character, bool> _handler;
    private readonly Func<Character, float, List<Npc>> _nearbyNpcs;
    private readonly BotBlackboard _blackboard;
    private readonly TimeProvider _timeProvider;
    private readonly Func<float, float, float> _heightProvider;
    private DateTime _lastHealTick;

    public BotCombatTask(Character bot, BotCombatState state, BotMovementBroadcaster broadcaster)
        : this(bot, state, broadcaster, null, null, null, null, null, null)
    {
    }

    internal BotCombatTask(
        Character bot,
        BotCombatState state,
        BotMovementBroadcaster broadcaster,
        Action<BotCombatTask> onCancel,
        Func<Character, bool> handler = null,
        Func<Character, float, List<Npc>> nearbyNpcs = null,
        BotBlackboard blackboard = null,
        TimeProvider timeProvider = null,
        Func<float, float, float> heightProvider = null)
    {
        _bot = bot;
        _state = state;
        _movementState = BotManager.Instance.GetBotState(bot.Id) ?? new BotMovementState();
        _broadcaster = broadcaster;
        _onCancel = onCancel;
        _handler = handler;
        _nearbyNpcs = nearbyNpcs ?? ((character, radius) => WorldManager.GetAround<Npc>(character, radius, true));
        _blackboard = blackboard ?? WorldValues.Create(bot, _nearbyNpcs);
        _timeProvider = timeProvider ?? TimeProvider.System;
        _heightProvider = heightProvider ?? ((x, y) => _bot.ParentWorld.GetHeight(x, y));
        _lastHealTick = Now;
    }

    public override void OnCancel()
    {
        _onCancel?.Invoke(this);
    }

    internal uint BotId => _bot.Id;
    internal BotCombatState State => _state;
    internal BotBlackboard Blackboard => _blackboard;
    internal BotHostMetrics HostMetrics { get; set; }
    private DateTime Now => _timeProvider.GetUtcNow().UtcDateTime;

    public override void Execute()
    {
        if (Interlocked.CompareExchange(ref _movementState.Running, 1, 0) != 0)
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
            Volatile.Write(ref _movementState.Running, 0);
        }
    }

    internal virtual void Step()
    {
        ExecuteCore();
    }

    internal virtual void StepMinimal()
    {
        if (_bot == null || _bot.ParentWorld == null)
        {
            Cancelled = true;
            return;
        }

        HandleDeadBot();
    }

    private void ExecuteCore()
    {
        if (_bot == null || _bot.ParentWorld == null)
        {
            Cancelled = true;
            return;
        }

        if (_bot.IsDead)
        {
            HandleDeadBot();
            return;
        }

        BotArchetypeManager.Instance.CheckForUpdates(_bot);
        var archetypeState = BotArchetypeManager.Instance.GetState(_bot);
        if (archetypeState != null)
        {
            var def = BotArchetypeManager.Instance.GetEffectiveDefinition(archetypeState);
            _state.ActiveArchetype = def?.Name;
        }

        UpdateBot();

    }

    private void HandleDeadBot()
    {
        if (!_bot.IsDead)
            return;

        if (!_state.RespawnScheduled && !_state.ShouldRespawn)
        {
            _state.RespawnScheduled = true;
            var respawnTask = new RespawnTask(_bot);
            TaskManager.Instance.Schedule(respawnTask, TimeSpan.FromSeconds(BotConfig.Instance.RespawnDelaySeconds));
        }
        else if (_state.ShouldRespawn)
        {
            RespawnBot();
        }
    }

    private void UpdateBot()
    {
        if (_state.ShouldRevertToForced())
        {
            _state.TransitionTo(_state.ForcedState!.Value);
        }

        switch (_state.CurrentState)
        {
            case BotCombatStateType.Idle: UpdateIdle(); break;
            case BotCombatStateType.Grinding: UpdateGrinding(); break;
            case BotCombatStateType.Questing: UpdateQuesting(); break;
            case BotCombatStateType.Roaming: UpdateRoaming(); break;
            case BotCombatStateType.Following: UpdateFollowing(); break;
            case BotCombatStateType.Combat: UpdateCombat(); break;
            case BotCombatStateType.Dueling: UpdateDueling(); break;
            case BotCombatStateType.Resting: UpdateResting(); break;
            case BotCombatStateType.Searching: UpdateSearching(); break;
        }
    }

    // ---- State handlers ----

    private void UpdateIdle()
    {
        if (_state.IsActive && !_state.IsForced)
        {
            if (_state.TargetTypeFilter.HasValue)
                _state.TransitionTo(BotCombatStateType.Questing);
            else
                _state.TransitionTo(BotCombatStateType.Grinding);
        }
        RelaxOnce();
    }

    private void UpdateGrinding()
    {
        if (!_state.IsActive)
        {
            _state.TransitionTo(BotCombatStateType.Idle);
            return;
        }
        if (ShouldRest())
        {
            _state.TransitionTo(BotCombatStateType.Resting);
            return;
        }

        if (_state.LastKnownTargetPosition.HasValue && !_state.IsSearching)
        {
            BeginSearch(_state.LastKnownTargetPosition.Value);
            return;
        }

        if (_state.Target == null || _state.Target.IsDead)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            if (_state.KillGoal.HasValue && _state.KillCount >= _state.KillGoal.Value)
            {
                Logger.Trace($"BOT id={_bot.Id} ev=kill_goal count={_state.KillCount}/{_state.KillGoal}");
                _state.KillGoal = null;
                _state.SetForcedState(null);
                _state.TransitionTo(BotCombatStateType.Idle);
                BotManager.Instance.StopImmediately(_bot);
                BotCombatManager.SendRelaxedStance(_bot);
                return;
            }

            var hostileNpcIds = _blackboard.Get(BotValues.NearbyHostileNpcIds, now);
            Npc closest = null;
            var closestDistance = float.MaxValue;
            foreach (var npcId in hostileNpcIds)
            {
                var npc = _bot.ParentWorld.GetNpc(npcId);
                if (npc == null)
                    continue;
                if (npc.IsDead) continue;
                if (!_bot.CanAttack(npc)) continue;
                if (_state.TargetTypeFilter.HasValue && npc.TemplateId != _state.TargetTypeFilter.Value)
                    continue;
                if (IsStealthed(npc))
                    continue;
                var botPosition = _bot.Transform.World.Position;
                var npcPosition = npc.Transform.World.Position;
                var distance = Vector3.Distance(botPosition, npcPosition);
                if (_state.TargetTypeFilter.HasValue)
                {
                    var navigationSurfaceZ = _heightProvider(npcPosition.X, npcPosition.Y);
                    if (!IsWithinNavigableQuestTargetVolume(
                            botPosition,
                            npcPosition,
                            navigationSurfaceZ,
                            QuestTargetSearchRadius()))
                        continue;
                }
                if (distance < closestDistance)
                {
                    closest = npc;
                    closestDistance = distance;
                }
            }

            if (closest != null)
            {
                _state.Target = closest;
                _state.LastFacingAngle = float.MinValue;
                _state.TransitionTo(BotCombatStateType.Combat);
                Logger.Trace($"BOT id={_bot.Id} ev=target target={closest.ObjId} distance={closestDistance:F1}");
            }
            else
            {
                if ((now - _state.LastCombatTime).TotalSeconds > BotConfig.Instance.IdleStanceDelaySeconds)
                    RelaxOnce();
            }
        }
        else
        {
            if (IsStealthed(_state.Target))
            {
                _state.LastKnownTargetPosition = _state.Target.Transform.World.Position;
                _state.Target = null;
                BeginSearch(_state.LastKnownTargetPosition.Value);
                BotManager.Instance.StopImmediately(_bot);
                return;
            }
            _state.TransitionTo(BotCombatStateType.Combat);
        }
    }

    private void UpdateQuesting()
    {
        if (!_state.TargetTypeFilter.HasValue)
        {
            _state.TransitionTo(BotCombatStateType.Grinding);
            return;
        }
        UpdateGrinding();
    }

    private void UpdateRoaming()
    {
        if (_state.RoamDestination is { } roamDestination)
        {
            BotManager.Instance.SetBotDestinationIfChanged(_bot, roamDestination, run: true);
        }
        if (TryDefend(out var attacker))
        {
            Logger.Trace($"BOT id={_bot.Id} ev=defend target={attacker.ObjId} state=roaming");
        }
        RelaxOnce();
    }

    private void UpdateFollowing()
    {
        var followTarget = _movementState?.FollowTarget;
        if (followTarget == null)
        {
            _state.TransitionTo(BotCombatStateType.Idle);
            return;
        }

        if (followTarget.IsInBattle && followTarget.CurrentTarget is Unit unit)
        {
            if (!IsStealthed(followTarget) && _state.Target != unit)
            {
                _state.Target = unit;
                _state.TransitionTo(BotCombatStateType.Combat);
                Logger.Trace($"BOT id={_bot.Id} ev=assist target={_state.Target.ObjId}");
            }
        }
        else
        {
            if (_state.CurrentState == BotCombatStateType.Combat && _state.Target != null)
            {
                _state.Target = null;
                _state.TransitionTo(BotCombatStateType.Following);
                BotManager.Instance.StopImmediately(_bot);
                BotCombatManager.SendRelaxedStance(_bot);
            }
            RelaxOnce();
        }

        if (TryDefend(out var playerAttacker))
            Logger.Trace($"BOT id={_bot.Id} ev=defend target={playerAttacker.ObjId} state=following");
    }

    private void UpdateCombat()
    {
        if (_state.Target == null)
        {
            ExitTemporaryState();
            return;
        }

        if (_state.Target.IsDead)
        {
            ExitTemporaryState();
            return;
        }

        if (_state.TargetTypeFilter.HasValue && _state.Target is Npc questTarget)
        {
            var botPosition = _bot.Transform.World.Position;
            var targetPosition = questTarget.Transform.World.Position;
            var navigationSurfaceZ = _heightProvider(targetPosition.X, targetPosition.Y);
            if (!IsWithinNavigableQuestTargetVolume(
                    botPosition,
                    targetPosition,
                    navigationSurfaceZ,
                    QuestTargetSearchRadius()))
            {
                Logger.Trace(
                    $"BOT id={_bot.Id} ev=quest_target_rejected target={questTarget.ObjId} " +
                    $"distance={Vector3.Distance(botPosition, targetPosition):F1} " +
                    $"surface_offset={Math.Abs(navigationSurfaceZ - targetPosition.Z):F1}");
                _state.Target = null;
                _bot.CurrentTarget = null;
                _state.TransitionTo(BotCombatStateType.Questing);
                BotManager.Instance.StopImmediately(_bot);
                return;
            }
        }

        if (TryEnforceNonlethalFloor())
            return;

        if (IsStealthed(_state.Target))
        {
            _state.LastKnownTargetPosition = _state.Target.Transform.World.Position;
            _state.Target = null;
            BeginSearch(_state.LastKnownTargetPosition.Value);
            BotManager.Instance.StopImmediately(_bot);
            Logger.Trace($"BOT id={_bot.Id} ev=target_lost reason=stealth pos={_state.LastKnownTargetPosition.Value}");
            return;
        }

        UpdateFight(_state.Target, useInjectedHandler: true);
    }

    internal static bool IsWithinNavigableQuestTargetVolume(
        Vector3 botPosition,
        Vector3 targetPosition,
        float navigationSurfaceZ,
        float searchRadius)
    {
        if (searchRadius <= 0f || Vector3.Distance(botPosition, targetPosition) > searchRadius)
            return false;

        // A zero/negative height is the host's sentinel for maps without usable
        // height data. When height data exists, reject cave/flying fixtures that
        // the simple heightmap mover would project onto a different surface.
        return navigationSurfaceZ <= 0f ||
               Math.Abs(navigationSurfaceZ - targetPosition.Z) <= MaximumQuestTargetSurfaceOffset;
    }

    private static float QuestTargetSearchRadius() =>
        Math.Min(MaximumQuestTargetSearchRadius, Math.Max(0f, (float)BotConfig.Instance.SearchRadius));

    private void UpdateDueling()
    {
        if (_state.DuelOpponent == null || _state.DuelOpponent.IsDead)
        {
            BotCombatManager.Instance.EndDuel(_bot);
            _state.InDuel = false;
            _state.DuelOpponent = null;
            return;
        }

        if (IsStealthed(_state.DuelOpponent))
        {
            _state.LastKnownTargetPosition = _state.DuelOpponent.Transform.World.Position;
            _state.Target = null;
            BeginSearch(_state.LastKnownTargetPosition.Value);
            BotManager.Instance.StopImmediately(_bot);
            Logger.Trace($"BOT id={_bot.Id} ev=duel_target_lost reason=stealth pos={_state.LastKnownTargetPosition.Value}");
            return;
        }

        UpdateFight(_state.DuelOpponent, useInjectedHandler: false);
    }

    private void UpdateSearching()
    {
        if (!_state.LastKnownTargetPosition.HasValue)
        {
            _state.IsSearching = false;
            _state.SearchRadius = 0f;
            _state.SearchAngle = 0f;
            ExitTemporaryState(resetRelaxedAfterCombat: false);
            return;
        }

        if ((Now - _state.SearchStartTime).TotalSeconds > 50)
        {
            Logger.Trace($"BOT id={_bot.Id} ev=search_give_up");
            _state.LastKnownTargetPosition = null;
            _state.IsSearching = false;
            _state.SearchRadius = 0f;
            _state.SearchAngle = 0f;
            ExitTemporaryState(resetRelaxedAfterCombat: false);
            return;
        }

        var targetPosition = _state.LastKnownTargetPosition.Value;
        var currentPosition = _bot.Transform.World.Position;
        var distanceToLast = Vector3.Distance(currentPosition, targetPosition);
        HostMetrics?.RecordWorldScan(BotWorldScanKind.Search);
        var nearbyCharacters = WorldManager.GetAround<Character>(_bot, 30f, true);
        Unit foundTarget = null;

        foreach (var character in nearbyCharacters)
        {
            if (character == _bot)
                continue;
            if (_state.InDuel ? character != _state.DuelOpponent : !_bot.CanAttack(character))
                continue;

            var distanceToCharacter = Vector3.Distance(currentPosition, character.Transform.World.Position);
            if (distanceToCharacter <= 2f || !IsStealthed(character))
            {
                foundTarget = character;
                break;
            }
        }

        if (foundTarget != null)
        {
            _state.Target = foundTarget;
            _state.LastKnownTargetPosition = null;
            _state.IsSearching = false;
            _state.SearchRadius = 0f;
            _state.SearchAngle = 0f;
            if (_state.InDuel)
            {
                _state.DuelOpponent = foundTarget;
                _state.TransitionTo(BotCombatStateType.Dueling);
            }
            else
            {
                _state.TransitionTo(BotCombatStateType.Combat);
            }

            if (UpdateFight(foundTarget, useInjectedHandler: false))
                _state.LastSkillTime = Now;
            Logger.Trace($"BOT id={_bot.Id} ev=target_found target={foundTarget.ObjId}");
            return;
        }

        if (distanceToLast > 1f)
        {
            BotManager.Instance.SetBotDestinationIfChanged(_bot, targetPosition, run: true);
            return;
        }

        _state.SearchAngle += 0.15f;
        var elapsed = (float)(Now - _state.SearchStartTime).TotalSeconds;
        var currentRadius = Math.Min(30f, 2f + elapsed / 20f * 28f);
        _state.SearchRadius = currentRadius;
        var destination = new Vector3(
            targetPosition.X + (float)Math.Cos(_state.SearchAngle) * currentRadius,
            targetPosition.Y + (float)Math.Sin(_state.SearchAngle) * currentRadius,
            targetPosition.Z);
        var groundZ = _bot.ParentWorld.GetHeight(destination.X, destination.Y);
        if (groundZ > 0)
            destination.Z = groundZ;
        BotManager.Instance.SetBotDestinationIfChanged(_bot, destination, run: true);
    }

    private void UpdateResting()
    {
        var restHealInterval = (float)BotConfig.Instance.RestHealInterval;
        var restHealPercent = BotConfig.Instance.RestHealPercentPerTick;
        if ((Now - _lastHealTick).TotalSeconds >= restHealInterval)
        {
            if (HpPercent(_bot) < 100)
            {
                var healAmount = Math.Max(1, (int)(_bot.MaxHp * restHealPercent / 100f));
                _bot.Hp = Math.Min(_bot.MaxHp, _bot.Hp + healAmount);
                _bot.BroadcastPacket(new SCUnitPointsPacket(_bot.ObjId, _bot.Hp, _bot.Mp
#if PLAYERBOTS_AAEMU_3_0
                    , _bot.HighAbilityRsc
#endif
                ), true);
            }
            _lastHealTick = Now;
        }

        if (_bot.Hp >= _bot.MaxHp)
        {
            _state.IsResting = false;
            _state.RestorePreviousState();
            _state.RevertToForcedState();
            _state.SentRelaxedAfterCombat = false;
            Logger.Trace($"BOT id={_bot.Id} ev=rest_complete");
        }

        if (TryDefend(out var attacker))
            Logger.Trace($"BOT id={_bot.Id} ev=rest_interrupted target={attacker.ObjId}");
        else if (_bot.IsInBattle && _bot.Hp < _bot.MaxHp)
        {
            var firstAggro = _bot.AggroTable.Values.FirstOrDefault();
            if (firstAggro?.Owner != null && !firstAggro.Owner.IsDead)
            {
                _state.Target = firstAggro.Owner;
                _state.TransitionTo(BotCombatStateType.Combat);
                _state.SentRelaxedAfterCombat = false;
                Logger.Trace($"BOT id={_bot.Id} ev=rest_interrupted target={_state.Target.ObjId}");
            }
        }
        else
            BotManager.Instance.StopIfMoving(_bot);
    }

    // ---- Helpers ----

    private void RelaxOnce()
    {
        if (_state.SentRelaxedAfterCombat)
            return;

        BotCombatManager.SendRelaxedStance(_bot);
        _state.SentRelaxedAfterCombat = true;
    }

    private bool RunHandler(Unit target, bool useInjectedHandler)
    {
        var handled = useInjectedHandler && _handler != null && _handler(_bot);
        return handled || BasicCombat.Execute(_bot, _state, target);
    }

    private bool UpdateFight(Unit target, bool useInjectedHandler)
    {
        if (!RunHandler(target, useInjectedHandler))
            return false;

        _bot.IsInBattle = true;
        target.IsInBattle = true;
        return true;
    }

    private void ExitTemporaryState()
    {
        ExitTemporaryState(resetRelaxedAfterCombat: true);
    }

    private void ExitTemporaryState(bool resetRelaxedAfterCombat)
    {
        _state.StopAtTargetHpPercent = null;
        _state.NonlethalFloorReached = null;
        _state.RestorePreviousState();
        _state.RevertToForcedState();
        BotManager.Instance.StopImmediately(_bot);
        BotCombatManager.SendRelaxedStance(_bot);
        if (resetRelaxedAfterCombat)
            _state.SentRelaxedAfterCombat = false;
    }

    private void BeginSearch(Vector3 position)
    {
        _state.LastKnownTargetPosition = position;
        _state.SearchStartTime = Now;
        _state.IsSearching = true;
        _state.SearchRadius = 0f;
        _state.SearchAngle = 0f;
        _state.TransitionTo(BotCombatStateType.Searching);
    }

    private bool ShouldRest()
    {
        if (_state.InDuel) return false;
        var hpPercent = HpPercent(_bot);
        return hpPercent <= BotConfig.Instance.RestThresholdPercent;
    }

    private static int HpPercent(Unit unit)
    {
        return (int)((float)unit.Hp / unit.MaxHp * 100);
    }

    internal static bool HasReachedHpFloor(Unit target, byte stopPercent)
    {
        return target != null && target.MaxHp > 0 &&
               (long)target.Hp * 100 <= (long)target.MaxHp * stopPercent;
    }

    internal bool TryEnforceNonlethalFloor()
    {
        if (_state.CurrentState != BotCombatStateType.Combat ||
            _state.Target == null ||
            _state.StopAtTargetHpPercent is not { } stopPercent ||
            !HasReachedHpFloor(_state.Target, stopPercent))
        {
            return false;
        }

        var target = _state.Target;
        var onFloorReached = _state.NonlethalFloorReached;
        _state.Target = null;
        _bot.CurrentTarget = null;
        ExitTemporaryState();
        Logger.Info($"BOT id={_bot.Id} ev=nonlethal_floor target={target.ObjId} " +
                    $"hp={target.Hp}/{target.MaxHp} floor_pct={stopPercent}");
        if (onFloorReached != null)
        {
            try
            {
                onFloorReached();
            }
            catch (Exception exception)
            {
                Logger.Error(exception,
                    $"BOT id={_bot.Id} ev=nonlethal_floor_callback_failed target={target.ObjId}");
            }
        }
        return true;
    }

    private bool TryDefend(out Unit attacker)
    {
        if (!DefendRules.IsBeingAttackedByPlayer(_bot, out attacker))
            return false;

        _state.Target = attacker;
        _state.TransitionTo(BotCombatStateType.Combat);
        return true;
    }

    private void RespawnBot()
    {
        if (_bot.IsDead)
        {
            _bot.Hp = _bot.MaxHp;
            _bot.Mp = _bot.MaxMp;
            _bot.PostUpdateCurrentHp(_bot, 0, _bot.Hp, KillReason.Unknown);
            _bot.BroadcastPacket(new SCUnitPointsPacket(_bot.ObjId, _bot.Hp, _bot.Mp
#if PLAYERBOTS_AAEMU_3_0
                , _bot.HighAbilityRsc
#endif
            ), true);
            _bot.BroadcastPacket(new SCCharacterResurrectedPacket(
                _bot.ObjId,
                _bot.Transform.World.Position.X,
                _bot.Transform.World.Position.Y,
                _bot.Transform.World.Position.Z,
                _bot.Transform.World.Rotation.Z
            ), true);
#if !PLAYERBOTS_AAEMU_3_0
            _bot.Buffs.RemoveBuff((uint)BuffConstants.WeakenedBody);
            _bot.Buffs.RemoveBuff((uint)BuffConstants.RespawnCooldown);
            _bot.Buffs.RemoveBuff((uint)BuffConstants.WarZoneLeech);
#endif
            _bot.DiedInPvp = false;
            _bot.DiedInPvpWarZone = false;
            _bot.ClearAllAggro();
            _state.SentRelaxedAfterCombat = false;
            Logger.Info($"Bot '{_bot.Name}' respawned at {_bot.Transform.World.Position}");
        }

        _state.RespawnScheduled = false;
        _state.ShouldRespawn = false;
    }

    private class RespawnTask : Task
    {
        private readonly Character _bot;
        public RespawnTask(Character bot) { _bot = bot; }
        public override void Execute()
        {
            if (BotManager.Instance.GetBot(_bot.Id) != _bot) return;

            var state = BotCombatManager.Instance.GetState(_bot);
            if (state != null)
            {
                state.ShouldRespawn = true;
                state.RespawnScheduled = false;
            }
        }
    }

    // ---- Stealth detection helper ----
    private static bool IsStealthed(Unit unit)
    {
        if (unit == null) return false;
        return unit.Buffs.HasEffectsMatchingCondition(e => e.Template.Stealth);
    }
}
