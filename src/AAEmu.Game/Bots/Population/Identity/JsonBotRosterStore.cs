using System.Text;
using System.Text.Json;

namespace AAEmu.Game.Bots.Population.Identity;

/// <summary>Stores bot policy only for characters that still exist in AAEmu.</summary>
public sealed class JsonBotRosterStore : IBotRosterStore
{
    private sealed class RosterDocument
    {
        public string SchemaVersion { get; set; }
        public List<RosterEntryDocument> Entries { get; set; }
    }

    private sealed class RosterEntryDocument
    {
        public uint CharacterId { get; set; }
        public bool Enabled { get; set; }
        public string Profile { get; set; }
        public uint HomeZoneId { get; set; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly object _gate = new();
    private readonly string _path;
    private readonly Func<uint, bool> _authoritativeCharacterExists;

    public JsonBotRosterStore(string path, Func<uint, bool> authoritativeCharacterExists)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("A roster path is required.", nameof(path));

        _path = Path.GetFullPath(path);
        _authoritativeCharacterExists = authoritativeCharacterExists
            ?? throw new ArgumentNullException(nameof(authoritativeCharacterExists));
    }

    public BotRosterSnapshot Read()
    {
        lock (_gate)
            return ReadCore();
    }

    public BotRosterEntry Create(BotRosterEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (_gate)
        {
            var snapshot = ReadCore();
            EnsureAuthoritative(entry.Identity.CharacterId);
            if (snapshot.Entries.Any(existing => existing.Identity == entry.Identity))
                throw new InvalidOperationException($"Bot character id {entry.Identity.CharacterId} already exists in the roster.");

            WriteCore(snapshot.Entries.Append(entry));
            return entry;
        }
    }

    public BotRosterEntry Update(BotRosterEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (_gate)
        {
            var snapshot = ReadCore();
            EnsureAuthoritative(entry.Identity.CharacterId);
            if (!snapshot.Entries.Any(existing => existing.Identity == entry.Identity))
                throw new KeyNotFoundException($"Bot character id {entry.Identity.CharacterId} is not in the roster.");

            WriteCore(snapshot.Entries.Select(existing => existing.Identity == entry.Identity ? entry : existing));
            return entry;
        }
    }

    /// <summary>Removes roster state when identity creation fails.</summary>
    public bool RemoveForCreationRollback(BotIdentity identity)
    {
        lock (_gate)
        {
            var snapshot = ReadCore();
            if (!snapshot.Entries.Any(existing => existing.Identity == identity))
                return true;

            WriteCore(snapshot.Entries.Where(existing => existing.Identity != identity));
            return true;
        }
    }

    private BotRosterSnapshot ReadCore()
    {
        if (!File.Exists(_path))
            return new BotRosterSnapshot([]);

        RosterDocument document;
        try
        {
            document = JsonSerializer.Deserialize<RosterDocument>(File.ReadAllText(_path), JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The bot roster is not valid JSON.", exception);
        }

        if (document == null)
            throw new InvalidDataException("The bot roster document is empty.");
        if (!string.Equals(document.SchemaVersion, BotRosterSnapshot.CurrentSchemaVersion, StringComparison.Ordinal))
            throw new InvalidDataException($"Unsupported bot roster schema version '{document.SchemaVersion ?? "<missing>"}'.");
        if (document.Entries == null)
            throw new InvalidDataException("The bot roster entries collection is missing.");

        var identities = new HashSet<uint>();
        var entries = new List<BotRosterEntry>(document.Entries.Count);
        foreach (var persisted in document.Entries)
        {
            if (!identities.Add(persisted.CharacterId))
                throw new InvalidDataException($"Duplicate bot character id {persisted.CharacterId} in the roster.");

            EnsureAuthoritative(persisted.CharacterId);
            try
            {
                entries.Add(new BotRosterEntry(
                    new BotIdentity(persisted.CharacterId),
                    persisted.Enabled,
                    persisted.Profile,
                    persisted.HomeZoneId));
            }
            catch (ArgumentException exception)
            {
                throw new InvalidDataException(
                    $"Bot roster entry {persisted.CharacterId} is invalid.",
                    exception);
            }
        }

        return new BotRosterSnapshot(entries);
    }

    private void WriteCore(IEnumerable<BotRosterEntry> entries)
    {
        var ordered = entries.OrderBy(entry => entry.Identity.CharacterId).ToArray();
        foreach (var entry in ordered)
            EnsureAuthoritative(entry.Identity.CharacterId);

        var document = new RosterDocument
        {
            SchemaVersion = BotRosterSnapshot.CurrentSchemaVersion,
            Entries = ordered.Select(entry => new RosterEntryDocument
            {
                CharacterId = entry.Identity.CharacterId,
                Enabled = entry.Enabled,
                Profile = entry.Profile,
                HomeZoneId = entry.HomeZoneId
            }).ToList()
        };

        var parent = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(parent))
            Directory.CreateDirectory(parent);

        var json = JsonSerializer.Serialize(document, JsonOptions).ReplaceLineEndings("\n") + "\n";
        File.WriteAllText(_path, json, new UTF8Encoding(false));
    }

    private void EnsureAuthoritative(uint characterId)
    {
        if (characterId == 0 || !_authoritativeCharacterExists(characterId))
            throw new InvalidDataException($"Bot character id {characterId} is not an authoritative AAEmu character identity.");
    }
}
