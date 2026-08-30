using System.Text.Json;
using System.Text.Json.Serialization;

namespace AAEmu.Game.Bots.Ops;

public sealed class WorldGeometryDocument
{
    public List<WorldGeometryWorld> Worlds { get; set; } = [];
    public List<WorldGeometryZoneGroup> ZoneGroups { get; set; } = [];
    public List<WorldGeometryZone> Zones { get; set; } = [];
    public List<WorldGeometryRoad> Roads { get; set; } = [];
    public List<WorldGeometryNpcSpawnCount> NpcSpawnCounts { get; set; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string RoadsNote { get; set; }
}

public sealed class WorldGeometryWorld
{
    public uint Id { get; set; }
    public string Name { get; set; }
}

public sealed class WorldGeometryZoneGroup
{
    public uint Id { get; set; }
    public string Name { get; set; }
    public float X { get; set; }
    public float Y { get; set; }

    [JsonPropertyName("w")]
    public float Width { get; set; }

    [JsonPropertyName("h")]
    public float Height { get; set; }

    public uint FactionId { get; set; }
    public uint TargetId { get; set; }
}

public sealed class WorldGeometryZone
{
    public uint Id { get; set; }
    public string Name { get; set; }
    public uint ZoneKey { get; set; }
    public uint GroupId { get; set; }
    public uint FactionId { get; set; }
    public bool Closed { get; set; }
}

public sealed class WorldGeometryRoad
{
    public uint WorldId { get; set; }
    public string Id { get; set; }
    public List<WorldGeometryPoint> Points { get; set; } = [];
}

public sealed class WorldGeometryPoint
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
}

public sealed class WorldGeometryNpcSpawnCount
{
    public uint ZoneGroupId { get; set; }
    public int Count { get; set; }
}

public static class WorldGeometryWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static string Serialize(WorldGeometryDocument document)
    {
        // LF on every OS: the artifact is committed to git and the exact-JSON test literal is eol=lf (review V0 B1).
        return JsonSerializer.Serialize(document ?? throw new ArgumentNullException(nameof(document)), JsonOptions).ReplaceLineEndings("\n");
    }

    public static void Write(string path, WorldGeometryDocument document)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("An output path is required.", nameof(path));

        var fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, Serialize(document));
    }
}
