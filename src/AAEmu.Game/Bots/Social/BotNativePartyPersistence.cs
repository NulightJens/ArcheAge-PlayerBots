#if !PLAYERBOTS_AAEMU_3_0
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Team;
using NLog;

namespace AAEmu.Game.Bots.Social;

internal sealed class BotNativePartyPersistence(IBotHost host) : IBotPartyPersistenceGateway
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private BotPartyPersistence _store;
    private string _selectedPath;
    private string _lastStatus;

    internal void Update(BotConfig config, DateTimeOffset now)
    {
        if (!config.PartyQuestEnabled || string.IsNullOrWhiteSpace(config.PartyQuestStateFile)) return;
        try
        {
            if (_selectedPath != config.PartyQuestStateFile)
            {
                _selectedPath = config.PartyQuestStateFile;
                _store = null;
                _store = new BotPartyPersistence(_selectedPath, this);
            }
            if (_store == null) return;
            _store.Tick(config.ConfiguredQuestGroups(), now);
            if (_lastStatus != _store.Status)
            {
                _lastStatus = _store.Status;
                Logger.Info("BOT party persistence {0}", _lastStatus);
            }
        }
        catch (Exception e)
        {
            // An unreadable file is retained and disabled until explicitly reconfigured or restarted.
            _store = null;
            Logger.Error(e, "BOT party persistence disabled; existing state retained");
        }
    }

    public SavedBotParty Capture(uint[] members)
    {
        var ids = members.Order().ToArray();
        var manager = TeamManager.Instance;
        var team = manager.GetActiveTeamByUnit(ids[0]);
        if (team == null) return null;
        lock (team.SyncLock)
        {
            var native = team.Members.Where(m => m?.Character != null).ToArray();
            if (!team.IsParty || native.Length != 3 || native.Any(m => !m.Character.IsBot) ||
                !native.Select(m => m.Character.Id).Order().SequenceEqual(ids) ||
                ids.Any(id => manager.GetActiveTeamByUnit(id) != team)) return null;
            return new SavedBotParty(team.OwnerId, ids,
                ids.Select(id => native.Single(m => m.Character.Id == id).Role).ToArray(),
                team.LootingRule.LootMethod, team.LootingRule.MinimumGrade,
                team.LootingRule.LootMaster, team.LootingRule.RollForBindOnPickup);
        }
    }

    public bool TryRestore(SavedBotParty saved, out string reason)
    {
        reason = "waiting_for_members";
        var members = saved.Members.Select(host.GetRuntime).ToArray();
        if (members.Any(r => r == null || r.Retired || !r.Bot.IsBot || r.Bot.IsDead ||
            r.Bot.Transform == null || r.Bot.ParentWorld == null ||
            !ReferenceEquals(host.GetRuntime(r.Bot.Id), r)) ||
            members.Any(r => !ReferenceEquals(r.Bot.ParentWorld, members[0].Bot.ParentWorld) ||
                r.Bot.Transform.InstanceId != members[0].Bot.Transform.InstanceId)) return false;
        var manager = TeamManager.Instance;
        var owner = members.Single(r => r.Bot.Id == saved.Owner).Bot;
        var existing = saved.Members.Select(manager.GetActiveTeamByUnit).Where(t => t != null).Distinct().ToArray();
        if (existing.Length > 1 || existing.Any(t => !t.IsParty || t.OwnerId != saved.Owner ||
            t.Members.Any(m => m?.Character != null && (!m.Character.IsBot || !saved.Members.Contains(m.Character.Id)))) ||
            members.Any(r => r.Bot.InParty && manager.GetActiveTeamByUnit(r.Bot.Id) == null))
        { reason = "foreign_native_membership"; return false; }
        foreach (var member in members.Where(r => r.Bot.Id != saved.Owner))
        {
            var team = manager.GetActiveTeamByUnit(owner.Id);
            var targetTeam = manager.GetActiveTeamByUnit(member.Bot.Id);
            if (targetTeam != null && targetTeam != team) { reason = "native_membership_changed"; return false; }
            if (targetTeam == null) manager.AskToJoin(owner, member.Bot.Name, team?.Id ?? 0, true, member.Bot);
        }
        var party = manager.GetActiveTeamByUnit(owner.Id);
        if (party == null || saved.Members.Any(id => !party.IsMember(id)))
        { reason = "native_join_incomplete"; return false; }
        for (var i = 0; i < saved.Members.Length; i++)
            manager.SetTeamMemberRole(members[i].Bot, party.Id, saved.Members[i], saved.Roles[i]);
        manager.ChangeLootingRule(owner, party.Id, 15, saved.LootMethod, saved.MinimumGrade,
            saved.LootMaster, saved.RollForBindOnPickup);
        foreach (var member in members) member.TeamHooks.Refresh(party);
        reason = "restored";
        return true;
    }
}
#endif
