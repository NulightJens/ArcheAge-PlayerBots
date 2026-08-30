using AAEmu.Game.Bots.Host;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.Team;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Bots.Social;

public sealed class BotTeamHooks : IDisposable
{
    private readonly BotRuntime _runtime;
    private Character _master;
    private bool _disposed;

    internal BotTeamHooks(BotRuntime runtime)
    {
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        var events = runtime.Bot.Events;
        events.OnTeamJoin += OnTeamJoin;
        events.OnTeamLeave += OnTeamLeave;
        events.OnTeamKick += OnTeamLeave;
        events.OnTeamChanged += OnTeamChanged;
        events.OnDisconnect += OnBotDisconnect;
    }

    public void Refresh(Team team)
    {
        if (_disposed)
            return;
        if (team == null || !team.IsMember(_runtime.Bot.Id))
        {
            DetachMaster();
            _runtime.Social.ClearTeam();
            return;
        }

        Character master = null;
        var formationSlot = 0;
        var ownSlot = -1;
        var mainTankId = 0u;
        var role = MemberRole.Undecided;
        for (var i = 0; i < team.Members.Length; i++)
        {
            var member = team.Members[i];
            if (member?.Character == null)
                continue;

            var character = member.Character;
            if (character.Id == team.OwnerId && !character.IsBot)
                master = character;
            if (mainTankId == 0 && member.Role == MemberRole.Tank)
                mainTankId = character.Id;
            if (character.Id == _runtime.Bot.Id)
            {
                ownSlot = formationSlot;
                role = member.Role;
            }
            if (character.IsBot)
                formationSlot++;
        }

        if (mainTankId == 0)
            mainTankId = team.OwnerId;
        AttachMaster(master);
        _runtime.Social.UpdateTeam(team, master, ownSlot, mainTankId, role);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        var events = _runtime.Bot.Events;
        events.OnTeamJoin -= OnTeamJoin;
        events.OnTeamLeave -= OnTeamLeave;
        events.OnTeamKick -= OnTeamLeave;
        events.OnTeamChanged -= OnTeamChanged;
        events.OnDisconnect -= OnBotDisconnect;
        DetachMaster();
        _runtime.Social.ClearTeam();
    }

    private void AttachMaster(Character master)
    {
        if (ReferenceEquals(_master, master))
            return;
        DetachMaster();
        _master = master;
        if (_master == null)
            return;
        _master.Events.OnDisconnect += OnMasterDisconnect;
        _master.Events.OnDeath += OnMasterDeath;
    }

    private void DetachMaster()
    {
        if (_master == null)
            return;
        _master.Events.OnDisconnect -= OnMasterDisconnect;
        _master.Events.OnDeath -= OnMasterDeath;
        _master = null;
    }

    private void OnTeamJoin(object sender, OnTeamJoinArgs args) => Refresh(args.Team);

    private void OnTeamChanged(object sender, OnTeamChangedArgs args) => Refresh(args.Team);

    private void OnTeamLeave(object sender, OnTeamLeaveArgs args)
    {
        DetachMaster();
        _runtime.Social.ClearTeam();
    }

    private void OnBotDisconnect(object sender, OnDisconnectArgs args)
    {
        DetachMaster();
        _runtime.Social.SafeHold();
    }

    private void OnMasterDisconnect(object sender, OnDisconnectArgs args)
    {
        DetachMaster();
        _runtime.Social.SafeHold();
    }

    private void OnMasterDeath(object sender, OnDeathArgs args) => _runtime.Social.SafeHold();
}
