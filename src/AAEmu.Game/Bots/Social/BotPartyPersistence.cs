#if !PLAYERBOTS_AAEMU_3_0
using System.Text.Json;
using AAEmu.Game.Models.Game.Team;

namespace AAEmu.Game.Bots.Social;

internal sealed record SavedBotParty(uint Owner, uint[] Members, MemberRole[] Roles,
    LootingRuleMethod LootMethod, byte MinimumGrade, uint LootMaster, bool RollForBindOnPickup)
{
    internal string Key => string.Join(',', Members.Order());
    internal bool Valid => Members is { Length: 3 } && Roles is { Length: 3 } &&
        Members.All(id => id != 0) && Members.Distinct().Count() == 3 && Members.Contains(Owner) &&
        Roles.All(role => Enum.IsDefined(role)) && Enum.IsDefined(LootMethod) &&
        LootMethod != LootingRuleMethod.Public && (LootMaster == 0 || Members.Contains(LootMaster));
}

internal interface IBotPartyPersistenceGateway
{
    SavedBotParty Capture(uint[] members);
    // Must preserve missing members and foreign native memberships.
    bool TryRestore(SavedBotParty party, out string reason);
}

/// <summary>Only startup records are restored. Later disbands remain native decisions.</summary>
internal sealed class BotPartyPersistence
{
    private sealed record Document(int Version, SavedBotParty[] Parties);
    private readonly string _path;
    private readonly IBotPartyPersistenceGateway _native;
    private readonly Dictionary<string, SavedBotParty> _saved = [];
    private readonly HashSet<string> _pending = [];
    private readonly HashSet<string> _verifyRestore = [];
    private readonly Dictionary<string, int> _attempts = [];
    private DateTimeOffset _next;
    internal string Status { get; private set; } = "ready";

    internal BotPartyPersistence(string path, IBotPartyPersistenceGateway native)
    {
        _path = Path.GetFullPath(path);
        _native = native;
        if (!File.Exists(_path)) return;
        var doc = JsonSerializer.Deserialize<Document>(File.ReadAllText(_path));
        if (doc is not { Version: 1, Parties: not null } || doc.Parties.Any(p => p == null || !p.Valid) ||
            doc.Parties.SelectMany(p => p.Members).GroupBy(id => id).Any(g => g.Count() != 1))
            throw new InvalidDataException("Invalid party state; existing file retained.");
        foreach (var party in doc.Parties) { _saved.Add(party.Key, party); _pending.Add(party.Key); }
    }

    internal void Tick(uint[][] groups, DateTimeOffset now)
    {
        if (now < _next) return;
        _next = now.AddSeconds(5);
        var counts = groups.Where(g => g != null).SelectMany(g => g).GroupBy(id => id)
            .ToDictionary(g => g.Key, g => g.Count());
        foreach (var ids in groups)
        {
            if (ids is not { Length: 3 } || ids.Any(id => id == 0 || counts[id] != 1)) continue;
            var key = string.Join(',', ids.Order());
            var current = _native.Capture(ids);
            if (_pending.Contains(key) && (current == null || _verifyRestore.Contains(key)))
            {
                if (_attempts.GetValueOrDefault(key) >= 6) { Status = $"blocked:{key}:restore_attempt_limit"; continue; }
                if (!_native.TryRestore(_saved[key], out var reason))
                {
                    // Missing admissions are expected during startup; native attempts alone consume the budget.
                    if (reason != "waiting_for_members") _attempts[key] = _attempts.GetValueOrDefault(key) + 1;
                    Status = $"waiting:{key}:{reason}";
                    continue;
                }
                _verifyRestore.Add(key);
                current = _native.Capture(ids);
                if (current == null || JsonSerializer.Serialize(current) != JsonSerializer.Serialize(_saved[key]))
                {
                    _attempts[key] = _attempts.GetValueOrDefault(key) + 1;
                    Status = $"waiting:{key}:native_state_mismatch";
                    continue;
                }
            }
            if (current == null || !current.Valid) continue;
            _pending.Remove(key);
            _verifyRestore.Remove(key);
            if (!_saved.TryGetValue(key, out var prior) || JsonSerializer.Serialize(prior) != JsonSerializer.Serialize(current))
            {
                // Validate the whole replacement before touching its retained predecessor.
                var replacement = _saved.Where(p => p.Key != key).Select(p => p.Value).Append(current).ToArray();
                if (replacement.SelectMany(p => p.Members).GroupBy(id => id).Any(g => g.Count() > 1))
                { Status = $"blocked:{key}:saved_membership_overlap"; continue; }
                Save(replacement);
                _saved[key] = current;
            }
            Status = $"ready:{key}";
        }
    }

    private void Save(SavedBotParty[] parties)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var suffix = Guid.NewGuid().ToString("N");
        var staged = _path + ".pending-" + suffix;
        File.WriteAllText(staged, JsonSerializer.Serialize(new Document(1, parties), new JsonSerializerOptions { WriteIndented = true }));
        if (File.Exists(_path)) File.Copy(_path, _path + ".previous-" + suffix);
        File.Move(staged, _path, true);
    }
}
#endif
