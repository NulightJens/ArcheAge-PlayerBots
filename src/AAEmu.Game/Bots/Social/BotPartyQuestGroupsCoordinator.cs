#if !PLAYERBOTS_AAEMU_3_0
using AAEmu.Game.Bots.Host;
using AAEmu.Game.Models.Game.Bots;

namespace AAEmu.Game.Bots.Social;

/// <summary>Each declared trio owns independent anchors, trails and quest waits.</summary>
internal sealed class BotPartyQuestGroupsCoordinator
{
    private readonly Dictionary<string, BotPartyQuestCoordinator> _groups = [];
    private readonly Dictionary<uint, (BotPartyQuestCoordinator Coordinator, BotConfig Config)> _owners = [];
    private readonly HashSet<uint> _invalid = [];
    private readonly Func<BotPartyQuestCoordinator> _factory;
    internal BotPartyQuestGroupsCoordinator(Func<BotPartyQuestCoordinator> factory = null)
        => _factory = factory ?? (() => new());

    internal void Update(BotRuntime[] runtimes, BotConfig config, DateTimeOffset now)
    {
        _owners.Clear(); _invalid.Clear();
        // Keep the ordinary disabled host tick allocation-free. Existing
        // coordinators still need one cleanup pass when coordination is disabled.
        if (!config.PartyQuestEnabled && _groups.Count == 0) return;
        UpdateGroups(runtimes, config, now);
    }

    private void UpdateGroups(BotRuntime[] runtimes, BotConfig config, DateTimeOffset now)
    {
        var declared = config.PartyQuestEnabled ? config.ConfiguredQuestGroups() : [];
        var counts = declared.Where(g => g != null).SelectMany(g => g).GroupBy(id => id)
            .ToDictionary(g => g.Key, g => g.Count());
        var retained = new HashSet<string>();
        foreach (var ids in declared)
        {
            if (ids == null) continue;
            if (ids.Length != 3 || ids.Any(id => id == 0 || counts[id] != 1))
            { foreach (var id in ids) _invalid.Add(id); continue; }
            var key = string.Join(",", ids.OrderBy(id => id));
            retained.Add(key);
            if (!_groups.TryGetValue(key, out var coordinator))
                _groups[key] = coordinator = _factory();
            var groupConfig = config.ForQuestGroup(ids);
            coordinator.Update(runtimes, groupConfig, now);
            foreach (var id in ids) _owners[id] = (coordinator, groupConfig);
        }
        foreach (var key in _groups.Keys.Where(key => !retained.Contains(key)).ToArray())
        {
            var disabled = config.ForQuestGroup([]); disabled.PartyQuestEnabled = false;
            _groups[key].Update(runtimes, disabled, now);
            _groups.Remove(key);
        }
    }

    internal bool Step(BotRuntime runtime, BotConfig config, DateTimeOffset now)
    {
        if (_invalid.Contains(runtime.Bot.Id))
        {
            runtime.PartyQuestSuppressBrain = true;
            runtime.PartyQuestReason = "invalid_party_group_configuration";
            runtime.Mover?.StopImmediately(runtime.Bot);
            runtime.QuestLifecycleController.PauseForParty(runtime, now);
            return false;
        }
        if (_owners.TryGetValue(runtime.Bot.Id, out var owner))
            return owner.Coordinator.Step(runtime, owner.Config, now);
        runtime.PartyQuestSuppressBrain = false;
        runtime.PartyQuestReason = "disabled";
        return true;
    }
}
#endif
