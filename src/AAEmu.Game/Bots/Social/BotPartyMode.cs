#if !PLAYERBOTS_AAEMU_3_0
using System.Globalization;
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Models.Game.Bots;
using AAEmu.Game.Models.Game.Team;

namespace AAEmu.Game.Bots.Social;

internal static class BotPartyMode
{
    internal static bool TryParse(string[] args, out bool support, out uint id)
    {
        support = false; id = 0;
        if (args is not { Length: 2 } || args[0] is not ("support" or "independent")) return false;
        support = args[0] == "support";
        return uint.TryParse(args[1], NumberStyles.None, CultureInfo.InvariantCulture, out id) && id > 0;
    }

    internal static bool TrySet(BotConfig config, IBotHost host, Team team, uint id, bool support,
        Action<Team, uint> promote, out string reason)
    {
        var existing = config.ConfiguredQuestGroups();
        var matching = existing.Where(g => g?.Contains(id) == true).ToArray();
        if (!support)
        {
            if (matching.Length != 1) { reason = "Requires one configured party containing this bot."; return false; }
            config.PartyQuestSupportLeaders = (config.PartyQuestSupportLeaders ?? [])
                .Where(leader => !matching[0].Contains(leader)).ToArray();
            reason = "mode=independent members=" + string.Join(',', matching[0]);
            return true;
        }
        var members = team?.Members.Where(m => m != null).Select(m => m.Character).ToArray() ?? [];
        if (id == 0 || team is not { IsParty: true } || members.Length != 3 ||
            members.Any(m => m == null || m.Id == 0 || !m.IsBot || m.IsDead || m.Transform == null || m.ParentWorld == null) ||
            members.Select(m => m.Id).Distinct().Count() != 3 ||
            members.All(m => m.Id != id))
        { reason = "Requires three living native party bots, including the selected leader."; return false; }
        var runtimes = members.Select(m => host.GetRuntime(m.Id)).ToArray();
        if (runtimes.Any(r => r == null || r.Retired || r.Social.TeamId != team.Id) ||
            runtimes.Where((r, i) => !ReferenceEquals(r.Bot, members[i])).Any() ||
            members.Any(m => !ReferenceEquals(m.ParentWorld, members[0].ParentWorld) ||
                m.Transform.InstanceId != members[0].Transform.InstanceId))
        { reason = "All party members must be admitted in the same world and instance."; return false; }
        var ids = members.Select(m => m.Id).Order().ToArray();
        var overlaps = existing.Where(g => g != null && g.Intersect(ids).Any()).ToArray();
        if (overlaps.Length > 1 || overlaps.Length == 1 && !overlaps[0].Order().SequenceEqual(ids))
        { reason = "Existing quest-party membership overlaps this native party; configuration retained."; return false; }
        if (team.OwnerId != id)
        {
            promote(team, id);
            if (team.OwnerId != id) { reason = "Native leader promotion did not complete; mode retained."; return false; }
        }
        if (overlaps.Length == 0)
        {
            config.PartyQuestGroups = [.. existing, ids];
            config.PartyQuestBotIds = [];
        }
        config.PartyQuestSupportLeaders = [.. (config.PartyQuestSupportLeaders ?? [])
            .Where(leader => !ids.Contains(leader)), id];
        config.PartyQuestEnabled = true;
        foreach (var runtime in runtimes) runtime.TeamHooks.Refresh(team);
        reason = $"mode=support leader={id} members={string.Join(',', ids)} native=true";
        return true;
    }
}
#endif
