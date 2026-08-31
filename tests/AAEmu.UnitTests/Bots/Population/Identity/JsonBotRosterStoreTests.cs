using AAEmu.Game.Bots.Population.Identity;

namespace AAEmu.UnitTests.Bots.Population.Identity;

[NotInParallel]
public class JsonBotRosterStoreTests
{
    [Test]
    public async Task CreateReadUpdate_RoundTripsAcrossRestartsInCharacterIdOrder()
    {
        var path = NewRosterPath();
        var authoritativeIds = new HashSet<uint> { 7, 42 };
        var firstProcess = new JsonBotRosterStore(path, authoritativeIds.Contains);

        firstProcess.Create(Entry(42, false, "artisan", 301, "Dormant"));
        firstProcess.Create(Entry(7, true, "ranger", 102, "Resident"));

        var secondProcess = new JsonBotRosterStore(path, authoritativeIds.Contains);
        var created = secondProcess.Read();

        await Assert.That(created.SchemaVersion).IsEqualTo(BotRosterSnapshot.CurrentSchemaVersion);
        await Assert.That(created.Entries).Count().IsEqualTo(2);
        await Assert.That(created.Entries[0]).IsEqualTo(Entry(7, true, "ranger", 102, "Resident"));
        await Assert.That(created.Entries[1]).IsEqualTo(Entry(42, false, "artisan", 301, "Dormant"));

        secondProcess.Update(Entry(42, true, "merchant", 411, "Visiting"));

        var thirdProcess = new JsonBotRosterStore(path, authoritativeIds.Contains);
        var updated = thirdProcess.Read();
        await Assert.That(updated.Entries).Count().IsEqualTo(2);
        await Assert.That(updated.Entries[0].Identity.CharacterId).IsEqualTo(7u);
        await Assert.That(updated.Entries[1]).IsEqualTo(Entry(42, true, "merchant", 411, "Visiting"));

        var json = File.ReadAllText(path);
        await Assert.That(json).Contains($"\"schemaVersion\": \"{BotRosterSnapshot.CurrentSchemaVersion}\"");
        await Assert.That(json.IndexOf("\"characterId\": 7", StringComparison.Ordinal))
            .IsLessThan(json.IndexOf("\"characterId\": 42", StringComparison.Ordinal));
    }

    [Test]
    public async Task Identity_UsesOnlyNonZeroAuthoritativeCharacterId()
    {
        var first = new BotIdentity(17);
        var same = new BotIdentity(17);
        var different = new BotIdentity(18);

        await Assert.That(first).IsEqualTo(same);
        await Assert.That(first).IsNotEqualTo(different);
        Assert.Throws<ArgumentOutOfRangeException>(() => new BotIdentity(0));
    }

    [Test]
    public async Task Create_RejectsDuplicateWithoutChangingPersistedRoster()
    {
        var path = NewRosterPath();
        var store = new JsonBotRosterStore(path, id => id == 12);
        store.Create(Entry(12, true, "first", 4, "Resident"));
        var before = File.ReadAllText(path);

        Assert.Throws<InvalidOperationException>(() =>
            store.Create(Entry(12, false, "duplicate", 9, "Dormant")));

        await Assert.That(File.ReadAllText(path)).IsEqualTo(before);
        await Assert.That(store.Read().Entries).Count().IsEqualTo(1);
    }

    [Test]
    public void Read_RejectsDuplicatePersistedIdentities()
    {
        var path = NewRosterPath();
        WriteDocument(path, $$"""
        {
          "schemaVersion": "{{BotRosterSnapshot.CurrentSchemaVersion}}",
          "entries": [
            { "characterId": 31, "enabled": true, "profile": "one", "homeZoneId": 1, "desiredLifeState": "Resident" },
            { "characterId": 31, "enabled": false, "profile": "two", "homeZoneId": 2, "desiredLifeState": "Dormant" }
          ]
        }
        """);

        var store = new JsonBotRosterStore(path, id => id == 31);
        Assert.Throws<InvalidDataException>(() => store.Read());
    }

    [Test]
    public void Read_RejectsUnknownSchemaVersion()
    {
        var path = NewRosterPath();
        WriteDocument(path, """
        {
          "schemaVersion": "playerbots.bot-roster.v999",
          "entries": []
        }
        """);

        var store = new JsonBotRosterStore(path, _ => true);
        Assert.Throws<InvalidDataException>(() => store.Read());
    }

    [Test]
    public void ReadAndCreate_RejectForeignCharacterIdentities()
    {
        var persistedPath = NewRosterPath();
        WriteDocument(persistedPath, $$"""
        {
          "schemaVersion": "{{BotRosterSnapshot.CurrentSchemaVersion}}",
          "entries": [
            { "characterId": 99, "enabled": true, "profile": "foreign", "homeZoneId": 1, "desiredLifeState": "Resident" }
          ]
        }
        """);

        var persisted = new JsonBotRosterStore(persistedPath, id => id == 7);
        Assert.Throws<InvalidDataException>(() => persisted.Read());

        var newPath = NewRosterPath();
        var fresh = new JsonBotRosterStore(newPath, id => id == 7);
        Assert.Throws<InvalidDataException>(() =>
            fresh.Create(Entry(99, true, "foreign", 1, "Resident")));
    }

    [Test]
    public void Update_RejectsIdentityNotAlreadyInRoster()
    {
        var store = new JsonBotRosterStore(NewRosterPath(), id => id == 8);
        Assert.Throws<KeyNotFoundException>(() =>
            store.Update(Entry(8, true, "new", 1, "Resident")));
    }

    private static BotRosterEntry Entry(
        uint characterId,
        bool enabled,
        string profile,
        uint homeZoneId,
        string desiredLifeState)
    {
        return new BotRosterEntry(
            new BotIdentity(characterId),
            enabled,
            profile,
            homeZoneId,
            desiredLifeState);
    }

    private static string NewRosterPath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "playerbots-t040",
            Guid.NewGuid().ToString("N"),
            "bot-roster.json");
    }

    private static void WriteDocument(string path, string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
    }
}
