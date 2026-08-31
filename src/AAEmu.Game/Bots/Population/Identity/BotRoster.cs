using System.Collections.ObjectModel;

namespace AAEmu.Game.Bots.Population.Identity;

/// <summary>
/// Stable PlayerBots identity anchored to the authoritative AAEmu character row.
/// </summary>
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

/// <summary>
/// Persisted policy for one bot identity. Profile and desired-life-state values are
/// stable tokens owned by their respective population policy layers.
/// </summary>
public sealed record BotRosterEntry
{
    public BotRosterEntry(
        BotIdentity identity,
        bool enabled,
        string profile,
        uint homeZoneId,
        string desiredLifeState)
    {
        if (string.IsNullOrWhiteSpace(profile))
            throw new ArgumentException("A roster profile is required.", nameof(profile));
        if (string.IsNullOrWhiteSpace(desiredLifeState))
            throw new ArgumentException("A desired life state is required.", nameof(desiredLifeState));

        Identity = identity;
        Enabled = enabled;
        Profile = profile;
        HomeZoneId = homeZoneId;
        DesiredLifeState = desiredLifeState;
    }

    public BotIdentity Identity { get; }
    public bool Enabled { get; }
    public string Profile { get; }
    public uint HomeZoneId { get; }
    public string DesiredLifeState { get; }
}

/// <summary>
/// Immutable, character-id ordered view of the versioned roster.
/// </summary>
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
}
