using System.Numerics;
using AAEmu.Game.Bots.Blackboard;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Team;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Bots.Social;

public static class BotSocialValues
{
    public static readonly ValueKey<uint> TeamId = new("social_team_id");
    public static readonly ValueKey<uint> MasterId = new("social_master_id");
    public static readonly ValueKey<int> FormationSlot = new("social_formation_slot");
    public static readonly ValueKey<uint> MainTankId = new("social_main_tank_id");
    public static readonly ValueKey<uint> AssistTargetObjId = new("social_assist_target_obj_id");
    public static readonly ValueKey<MemberRole> Role = new("social_member_role");
}

public sealed class BotSocialState
{
    private readonly BotRuntime _runtime;
    private readonly object _sync = new();
    private readonly ManualValue<uint> _teamId = new(0);
    private readonly ManualValue<uint> _masterId = new(0);
    private readonly ManualValue<int> _formationSlot = new(-1);
    private readonly ManualValue<uint> _mainTankId = new(0);
    private readonly ManualValue<uint> _assistTargetObjId = new(0);
    private readonly ManualValue<MemberRole> _role = new(MemberRole.Undecided);
    private Team _team;
    private Character _master;
    private Character _committedHealRecipient;
    private float _committedHealSearchLeash;
    private float _committedHealMinimumPercent;
    private float _committedHealMaximumPercent;
    private int _healRecipientSelectionScans;
    private BotMovementOrder _movementOrder = BotMovementOrder.Stay;
    private BotCombatOrder _combatOrder = BotCombatOrder.Passive;

    internal BotSocialState(BotRuntime runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        runtime.Blackboard.Register(BotSocialValues.TeamId, _teamId);
        runtime.Blackboard.Register(BotSocialValues.MasterId, _masterId);
        runtime.Blackboard.Register(BotSocialValues.FormationSlot, _formationSlot);
        runtime.Blackboard.Register(BotSocialValues.MainTankId, _mainTankId);
        runtime.Blackboard.Register(BotSocialValues.AssistTargetObjId, _assistTargetObjId);
        runtime.Blackboard.Register(BotSocialValues.Role, _role);
    }

    public uint TeamId { get { lock (_sync) return _teamId.Value; } }
    public uint MasterId { get { lock (_sync) return _masterId.Value; } }
    public int FormationSlot { get { lock (_sync) return _formationSlot.Value; } }
    public uint MainTankId { get { lock (_sync) return _mainTankId.Value; } }
    public uint AssistTargetObjId { get { lock (_sync) return _assistTargetObjId.Value; } }
    public MemberRole Role { get { lock (_sync) return _role.Value; } }
    public BotMovementOrder MovementOrder { get { lock (_sync) return _movementOrder; } }
    public BotCombatOrder CombatOrder { get { lock (_sync) return _combatOrder; } }
    internal int HealRecipientSelectionScans { get { lock (_sync) return _healRecipientSelectionScans; } }

    internal Character Master
    {
        get { lock (_sync) return _master; }
    }

    internal void UpdateTeam(
        Team team,
        Character master,
        int formationSlot,
        uint mainTankId,
        MemberRole role)
    {
        lock (_sync)
        {
            var teamId = team?.Id ?? 0;
            var joinedParty = _teamId.Value == 0 && teamId != 0;
            if (!ReferenceEquals(_team, team) ||
                _committedHealRecipient != null && team?.IsMember(_committedHealRecipient.Id) != true)
                ClearCommittedHealRecipientUnsafe();
            _team = team;
            _teamId.Value = teamId;
            _master = master;
            _masterId.Value = master?.Id ?? 0;
            _formationSlot.Value = formationSlot;
            _mainTankId.Value = mainTankId;
            _role.Value = role;
            if (joinedParty)
                _combatOrder = BotCombatOrder.Assist;
            if (_movementOrder == BotMovementOrder.Follow)
                _runtime.MovementState.FollowTarget = master;
        }

        if (master == null)
            SafeHold();
    }

    internal Character ResolveLowestHealthMember(float maxRange)
    {
        Team team;
        lock (_sync)
            team = _team;

        if (team == null || _runtime.Bot.Transform == null || !float.IsFinite(maxRange) || maxRange < 0f)
            return null;

        Character best = null;
        var botPosition = _runtime.Bot.Transform.World.Position;
        foreach (var member in team.Members)
        {
            var candidate = member?.Character;
            if (candidate?.Transform == null || candidate.IsDead || candidate.MaxHp <= 0 ||
                candidate.Hp >= candidate.MaxHp || !candidate.IsBot && !candidate.IsOnline)
                continue;
            if ((_runtime.Bot.ParentWorld != null || candidate.ParentWorld != null) &&
                !ReferenceEquals(_runtime.Bot.ParentWorld, candidate.ParentWorld))
                continue;
            if (_runtime.Bot.Transform.InstanceId != candidate.Transform.InstanceId ||
                Vector3.Distance(botPosition, candidate.Transform.World.Position) > maxRange)
                continue;

            if (best == null ||
                (long)candidate.Hp * best.MaxHp < (long)best.Hp * candidate.MaxHp ||
                (long)candidate.Hp * best.MaxHp == (long)best.Hp * candidate.MaxHp && candidate.Id < best.Id)
                best = candidate;
        }

        return best;
    }

    internal Character CommitLowestHealthMember(
        float searchLeash,
        float minimumHealthPercent = 0f,
        float maximumHealthPercent = 100f)
    {
        lock (_sync)
        {
            if (!IsValidSelectionBand(searchLeash, minimumHealthPercent, maximumHealthPercent) ||
                _team == null || _runtime.Bot.Transform == null)
            {
                ClearCommittedHealRecipientUnsafe();
                return null;
            }

            if (IsHealRecipientValidUnsafe(
                    _committedHealRecipient,
                    searchLeash,
                    minimumHealthPercent,
                    maximumHealthPercent))
            {
                _committedHealSearchLeash = searchLeash;
                _committedHealMinimumPercent = minimumHealthPercent;
                _committedHealMaximumPercent = maximumHealthPercent;
                return _committedHealRecipient;
            }

            ClearCommittedHealRecipientUnsafe();
            Character best = null;
            _healRecipientSelectionScans++;
            foreach (var member in _team.Members)
            {
                var candidate = member?.Character;
                if (!IsHealRecipientValidUnsafe(candidate, searchLeash, minimumHealthPercent, maximumHealthPercent))
                    continue;

                if (best == null ||
                    (long)candidate.Hp * best.MaxHp < (long)best.Hp * candidate.MaxHp ||
                    (long)candidate.Hp * best.MaxHp == (long)best.Hp * candidate.MaxHp && candidate.Id < best.Id)
                    best = candidate;
            }

            _committedHealRecipient = best;
            _committedHealSearchLeash = searchLeash;
            _committedHealMinimumPercent = minimumHealthPercent;
            _committedHealMaximumPercent = maximumHealthPercent;
            return best;
        }
    }

    internal Character ResolveCommittedHealRecipient()
    {
        lock (_sync)
        {
            if (IsHealRecipientValidUnsafe(
                    _committedHealRecipient,
                    _committedHealSearchLeash,
                    _committedHealMinimumPercent,
                    _committedHealMaximumPercent))
                return _committedHealRecipient;

            ClearCommittedHealRecipientUnsafe();
            return null;
        }
    }

    internal void ClearCommittedHealRecipient()
    {
        lock (_sync)
            ClearCommittedHealRecipientUnsafe();
    }

    public bool IsAuthorized(uint requesterId, uint teamId, uint masterId)
    {
        lock (_sync)
        {
            return requesterId != 0 && requesterId == _masterId.Value &&
                   requesterId == masterId && teamId != 0 && teamId == _teamId.Value;
        }
    }

    public bool IsMasterAvailable(Character character)
    {
        lock (_sync)
        {
            if (_teamId.Value == 0 || _master == null || !ReferenceEquals(character, _master) ||
                !character.IsOnline || character.IsDead)
                return false;
            if (_runtime.Bot.ParentWorld != null && character.ParentWorld != null &&
                !ReferenceEquals(_runtime.Bot.ParentWorld, character.ParentWorld))
                return false;
            return _runtime.Bot.Transform != null && character.Transform != null &&
                   _runtime.Bot.Transform.InstanceId == character.Transform.InstanceId;
        }
    }

    public Unit ResolveMasterTarget(uint targetObjId)
    {
        lock (_sync)
        {
            return targetObjId != 0 && _master?.CurrentTarget is Unit target && target.ObjId == targetObjId
                ? target
                : null;
        }
    }

    public bool GuardLeader()
    {
        Character master;
        uint teamId;
        lock (_sync)
        {
            master = _master;
            teamId = _teamId.Value;
        }

        if (teamId == 0 || IsMasterAvailable(master))
            return true;
        SafeHold();
        return false;
    }

    public void ApplyFollow()
    {
        lock (_sync)
        {
            _movementOrder = BotMovementOrder.Follow;
            _runtime.MovementState.FollowTarget = _master;
            _runtime.MovementState.FormationSlot = _formationSlot.Value;
            _runtime.MovementState.FormationColumns = 0;
            _runtime.MovementState.FormationMemberCount = 0;
            _runtime.CombatState.SetForcedState(BotCombatStateType.Following);
            if (_runtime.CombatState.CurrentState is not BotCombatStateType.Combat and not BotCombatStateType.Dueling)
                _runtime.CombatState.TransitionTo(BotCombatStateType.Following);
        }
    }

    public void ApplyStay()
    {
        lock (_sync)
        {
            _movementOrder = BotMovementOrder.Stay;
            _runtime.MovementState.FollowTarget = null;
            _runtime.MovementState.FormationSlot = -1;
            _runtime.MovementState.FormationColumns = 0;
            _runtime.MovementState.FormationMemberCount = 0;
            _runtime.MovementState.Destination = null;
            _runtime.CombatState.SetForcedState(BotCombatStateType.Idle);
            if (_runtime.CombatState.CurrentState is not BotCombatStateType.Combat and not BotCombatStateType.Dueling)
                _runtime.CombatState.TransitionTo(BotCombatStateType.Idle);
        }
    }

    public void ApplyAttack(Unit target)
    {
        ArgumentNullException.ThrowIfNull(target);
        lock (_sync)
        {
            _combatOrder = BotCombatOrder.Assist;
            _assistTargetObjId.Value = target.ObjId;
            _runtime.CombatState.Target = target;
            _runtime.Bot.CurrentTarget = target;
            _runtime.CombatState.IsActive = true;
            _runtime.CombatState.TransitionTo(BotCombatStateType.Combat);
        }
    }

    public void ApplyPassive()
    {
        lock (_sync)
        {
            _combatOrder = BotCombatOrder.Passive;
            _assistTargetObjId.Value = 0;
            _runtime.CombatState.Target = null;
            _runtime.Bot.CurrentTarget = null;
            var next = _movementOrder == BotMovementOrder.Follow && _master != null
                ? BotCombatStateType.Following
                : BotCombatStateType.Idle;
            _runtime.CombatState.SetForcedState(next);
            _runtime.CombatState.TransitionTo(next);
        }
    }

    public void ApplyRole(MemberRole role)
    {
        lock (_sync)
            _role.Value = role;
    }

    public void SafeHold()
    {
        lock (_sync)
        {
            ClearCommittedHealRecipientUnsafe();
            _movementOrder = BotMovementOrder.Stay;
            _combatOrder = BotCombatOrder.Passive;
            _assistTargetObjId.Value = 0;
            _runtime.MovementState.FollowTarget = null;
            _runtime.MovementState.FormationSlot = -1;
            _runtime.MovementState.FormationColumns = 0;
            _runtime.MovementState.FormationMemberCount = 0;
            _runtime.MovementState.Destination = null;
            _runtime.CombatState.Target = null;
            _runtime.Bot.CurrentTarget = null;
            _runtime.CombatState.SetForcedState(BotCombatStateType.Idle);
            _runtime.CombatState.TransitionTo(BotCombatStateType.Idle);
        }
    }

    internal void ClearTeam()
    {
        SafeHold();
        lock (_sync)
        {
            _team = null;
            _teamId.Value = 0;
            _master = null;
            _masterId.Value = 0;
            _formationSlot.Value = -1;
            _mainTankId.Value = 0;
            _role.Value = MemberRole.Undecided;
        }
    }

    private bool IsHealRecipientValidUnsafe(
        Character candidate,
        float searchLeash,
        float minimumHealthPercent,
        float maximumHealthPercent)
    {
        if (candidate?.Transform == null || candidate.IsDead || candidate.MaxHp <= 0 ||
            candidate.Hp >= candidate.MaxHp || !candidate.IsBot && !candidate.IsOnline ||
            _runtime.Bot.Transform == null)
            return false;
        if ((_runtime.Bot.ParentWorld != null || candidate.ParentWorld != null) &&
            !ReferenceEquals(_runtime.Bot.ParentWorld, candidate.ParentWorld))
            return false;
        if (_runtime.Bot.Transform.InstanceId != candidate.Transform.InstanceId)
            return false;

        var healthPercent = candidate.Hp * 100f / candidate.MaxHp;
        if (healthPercent < minimumHealthPercent || healthPercent > maximumHealthPercent)
            return false;

        var offset = _runtime.Bot.Transform.World.Position - candidate.Transform.World.Position;
        return offset.LengthSquared() <= searchLeash * searchLeash;
    }

    private static bool IsValidSelectionBand(float searchLeash, float minimumHealthPercent, float maximumHealthPercent)
    {
        return float.IsFinite(searchLeash) && searchLeash >= 0f &&
               float.IsFinite(minimumHealthPercent) && float.IsFinite(maximumHealthPercent) &&
               minimumHealthPercent >= 0f && maximumHealthPercent <= 100f &&
               minimumHealthPercent <= maximumHealthPercent;
    }

    private void ClearCommittedHealRecipientUnsafe()
    {
        _committedHealRecipient = null;
        _committedHealSearchLeash = 0f;
        _committedHealMinimumPercent = 0f;
        _committedHealMaximumPercent = 0f;
    }
}
