#if !PLAYERBOTS_AAEMU_3_0
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Bots.Social;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Utils.Scripts;

namespace AAEmu.Game.Scripts.Commands;

internal sealed class BotQuestPartyHandler(ICommand command)
{
    public void Execute(Character character, string[] args, IMessageOutput output)
    {
        var config = BotConfig.Instance;
        if (BotPartyMode.TryParse(args, out var support, out var modeId))
        {
            var team = TeamManager.Instance.GetActiveTeamByUnit(modeId);
            if (BotPartyMode.TrySet(config, BotHost.Instance, team, modeId, support,
                (party, leaderId) =>
                {
                    var owner = BotHost.Instance.GetRuntime(party.OwnerId)?.Bot;
                    if (owner != null) TeamManager.Instance.MakeTeamOwner(owner, party.Id, leaderId);
                }, out var reason))
                CommandManager.SendNormalText(command, output, reason);
            else CommandManager.SendErrorText(command, output, reason);
            return;
        }
        if (args is ["status"])
        {
            foreach (var id in config.ConfiguredQuestGroups().Where(group => group != null).SelectMany(group => group).Distinct())
            {
                var runtime = BotHost.Instance.GetRuntime(id);
                CommandManager.SendNormalText(command, output, runtime == null ? $"bot={id} unavailable" :
                    $"bot={id} name={runtime.Bot.Name} party={runtime.Social.TeamId} state={runtime.PartyQuestReason} " +
                    $"regroup={runtime.PartyRegroupDetail} navigation={runtime.MovementState.LastNavigationDecision?.Reason} " +
                    $"recovery={runtime.MovementState.SafeRecovery.Reason} failures={runtime.MovementState.SafeRecovery.Failures} " +
                    $"last_teleport={runtime.MovementState.SafeRecovery.LastTeleportAt:O}");
            }
            foreach (var statusParty in config.ConfiguredQuestGroups().Where(g => g != null).SelectMany(g => g)
                .Select(id => TeamManager.Instance.GetActiveTeamByUnit(id)).Where(t => t != null).Distinct())
            {
                lock (statusParty.SyncLock)
                    CommandManager.SendNormalText(command, output,
                        $"native_party={statusParty.Id} owner={statusParty.OwnerId} roles=" +
                        string.Join(',', statusParty.Members.Where(m => m?.Character != null)
                            .OrderBy(m => m.Character.Id).Select(m => $"{m.Character.Id}:{m.Role}")) +
                        $" loot={statusParty.LootingRule.LootMethod} grade={statusParty.LootingRule.MinimumGrade}" +
                        $" loot_master={statusParty.LootingRule.LootMaster} roll_bound={statusParty.LootingRule.RollForBindOnPickup}");
            }
            return;
        }
        if (args.Length != 4 || args[0] != "create" || args.Skip(1).Any(a => !uint.TryParse(a, out _)))
        { CommandManager.SendDefaultHelpText(command, output); return; }
        var ids = args.Skip(1).Select(uint.Parse).ToArray();
        var members = ids.Select(id => BotHost.Instance.GetRuntime(id)).ToArray();
        if (ids.Distinct().Count() != 3 || members.Any(r => r == null || !r.Bot.IsBot || r.Bot.IsDead || r.Bot.InParty) ||
            members.Any(r => !ReferenceEquals(r.Bot.ParentWorld, members[0].Bot.ParentWorld)))
        { CommandManager.SendErrorText(command, output, "Requires three living, admitted bots in the same world without existing parties."); return; }
        var existing = config.ConfiguredQuestGroups();
        if (existing.Where(group => group != null).SelectMany(group => group).Intersect(ids).Any())
        { CommandManager.SendErrorText(command, output, "A bot is already configured in a quest party; existing groups retained."); return; }
        config.PartyQuestGroups = [.. existing, ids];
        config.PartyQuestBotIds = [];
        config.PartyQuestEnabled = true;
        var leader = members[0].Bot;
        foreach (var member in members.Skip(1))
        {
            var team = TeamManager.Instance.GetActiveTeamByUnit(leader.Id);
            TeamManager.Instance.AskToJoin(leader, member.Bot.Name, team?.Id ?? 0, true, member.Bot);
        }
        var party = TeamManager.Instance.GetActiveTeamByUnit(leader.Id);
        if (party == null || members.Any(r => !party.IsMember(r.Bot.Id)))
        {
            CommandManager.SendErrorText(command, output, "Native party formation incomplete; configured members are held. Existing membership retained.");
            return;
        }
        foreach (var member in members) member.TeamHooks.Refresh(party);
        CommandManager.SendNormalText(command, output, $"party={party.Id} members={string.Join(',', ids)} native=true coordination=enabled");
    }
}
#endif
