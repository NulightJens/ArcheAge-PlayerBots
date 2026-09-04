using System.Collections.ObjectModel;

namespace AAEmu.Game.Bots.Population.Identity;

/// <summary>A bot identity backed by an AAEmu character.</summary>
public readonly record struct BotIdentity
{
    public BotIdentity(uint characterId)
    {
        if (characterId == 0)
            throw new ArgumentOutOfRangeException(nameof(characterId), "A bot identity requires a non-zero AAEmu character id.");

        CharacterId = characterId;
    }

    public uint CharacterId { get; }
}

/// <summary>Persisted settings for one bot.</summary>
public sealed record BotRosterEntry
{
    public BotRosterEntry(
        BotIdentity identity,
        bool enabled,
        string profile,
        uint homeZoneId)
    {
        if (string.IsNullOrWhiteSpace(profile))
            throw new ArgumentException("A roster profile is required.", nameof(profile));
        Identity = identity;
        Enabled = enabled;
        Profile = profile;
        HomeZoneId = homeZoneId;
    }

    public BotIdentity Identity { get; }
    public bool Enabled { get; }
    public string Profile { get; }
    public uint HomeZoneId { get; }
}

/// <summary>An immutable roster ordered by character ID.</summary>
public sealed class BotRosterSnapshot
{
    public const string CurrentSchemaVersion = "playerbots.bot-roster.v1";

    internal BotRosterSnapshot(IEnumerable<BotRosterEntry> entries)
    {
        var ordered = entries.OrderBy(entry => entry.Identity.CharacterId).ToArray();
        Entries = new ReadOnlyCollection<BotRosterEntry>(ordered);
    }

    public string SchemaVersion => CurrentSchemaVersion;
    public IReadOnlyList<BotRosterEntry> Entries { get; }
}

public interface IBotRosterStore
{
    BotRosterSnapshot Read();
    BotRosterEntry Create(BotRosterEntry entry);
    BotRosterEntry Update(BotRosterEntry entry);
    bool RemoveForCreationRollback(BotIdentity identity);
}
