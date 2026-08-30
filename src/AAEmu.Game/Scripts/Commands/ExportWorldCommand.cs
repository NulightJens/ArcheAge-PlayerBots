using System.Numerics;

using AAEmu.Commons.IO;
using AAEmu.Game.Bots.Ops;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.World;
using AAEmu.Game.IO;
using AAEmu.Game.Models.CryEngine.Readers;
using AAEmu.Game.Models.Game;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.World;
using AAEmu.Game.Models.Game.World.Transform;
using AAEmu.Game.Models.Game.World.Zones;
using AAEmu.Game.Utils.Scripts;

using NLog;

namespace AAEmu.Game.Scripts.Commands;

public sealed class ExportWorldCommand : ICommand
{
    private static Logger Logger { get; } = LogManager.GetCurrentClassLogger();

    public string[] CommandNames { get; set; } = ["exportworld"];

    public void OnLoad()
    {
        CommandManager.Instance.Register(CommandNames, this);
    }

    public string GetCommandLineHelp()
    {
        return "[outDir]";
    }

    public string GetCommandHelpText()
    {
        return "Exports world geometry and loaded roads to world-geometry.json.";
    }

    public void Execute(Character character, string[] args, IMessageOutput messageOutput)
    {
        try
        {
            var outputDirectory = args is { Length: > 0 } && !string.IsNullOrWhiteSpace(args[0])
                ? args[0]
                : Path.Combine(FileManager.AppPath, "Export");
            var outputPath = Path.Combine(outputDirectory, "world-geometry.json");
            var document = BuildDocument();

            WorldGeometryWriter.Write(outputPath, document);
            CommandManager.SendNormalText(this, messageOutput,
                $"Wrote {Path.GetFullPath(outputPath)} (worlds={document.Worlds.Count}, zones={document.Zones.Count}, roads={document.Roads.Count}).");
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to export world geometry.");
            CommandManager.SendErrorText(this, messageOutput, $"Export failed: {ex.Message}");
        }
    }

    private static WorldGeometryDocument BuildDocument()
    {
        var worldManager = WorldManager.Instance;
        var zoneManager = ZoneManager.Instance;
        var worlds = worldManager.WorldTemplates.Values.OrderBy(world => world.Id).ToList();
        var zones = zoneManager.GetAllZones().OrderBy(zone => zone.Id).ToList();
        var groups = zoneManager.GetAllZoneGroups().OrderBy(group => group.Id).ToList();

        var document = new WorldGeometryDocument
        {
            Worlds = worlds.Select(world => new WorldGeometryWorld
            {
                Id = world.Id,
                Name = world.Name
            }).ToList(),
            ZoneGroups = groups.Select(group => new WorldGeometryZoneGroup
            {
                Id = group.Id,
                Name = group.Name,
                X = group.X,
                Y = group.Y,
                Width = group.Width,
                Height = group.Hight,
                FactionId = (uint)(zones.FirstOrDefault(zone => zone.GroupId == group.Id)?.FactionId ?? 0),
                TargetId = group.TargetId
            }).ToList(),
            Zones = zones.Select(zone => new WorldGeometryZone
            {
                Id = zone.Id,
                Name = zone.Name,
                ZoneKey = zone.ZoneKey,
                GroupId = zone.GroupId,
                FactionId = (uint)zone.FactionId,
                Closed = zone.Closed
            }).ToList(),
            NpcSpawnCounts = LoadNpcSpawnCounts(worldManager, zones)
        };

        document.Roads = LoadRoads(worlds, out var roadsNote);
        document.RoadsNote = roadsNote;
        return document;
    }

    private static List<WorldGeometryNpcSpawnCount> LoadNpcSpawnCounts(WorldManager worldManager, IReadOnlyList<Zone> zones)
    {
        var counts = new Dictionary<uint, int>();
        foreach (var world in worldManager.GetWorlds().OrderBy(world => world.Id))
        {
            if (world.SpawnManager == null)
                continue;

            foreach (var spawner in world.SpawnManager.GetAllSpawners().Values.SelectMany(spawners => spawners))
            {
                var zoneGroupId = zones.FirstOrDefault(zone => zone.ZoneKey == spawner.Position?.ZoneId)?.GroupId ?? 0;
                if (zoneGroupId == 0)
                    continue;

                counts[zoneGroupId] = counts.GetValueOrDefault(zoneGroupId) + 1;
            }
        }

        return counts.OrderBy(pair => pair.Key)
            .Select(pair => new WorldGeometryNpcSpawnCount { ZoneGroupId = pair.Key, Count = pair.Value })
            .ToList();
    }

    private static List<WorldGeometryRoad> LoadRoads(IReadOnlyList<WorldTemplate> worlds, out string roadsNote)
    {
        var roads = new List<WorldGeometryRoad>();
        var filesFound = 0;
        var filesRead = 0;
        var readFailures = 0;

        foreach (var world in worlds)
        {
            var worldFolder = Path.Combine("game", "worlds", world.Name);
            var roadFiles = ClientFileManager.GetFilesInDirectory(worldFolder, "*road*.bai", true)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            filesFound += roadFiles.Length;

            var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var roadFile in roadFiles)
            {
                using var stream = ClientFileManager.GetFileStream(roadFile) ?? Stream.Null;
                if (ReferenceEquals(stream, Stream.Null))
                {
                    readFailures++;
                    continue;
                }

                try
                {
                    var location = GetBaiLocation(roadFile);
                    var reader = new RoadMissionReader(stream, location.ZoneKey)
                    {
                        ReaderPointOffset = GetBaiOffset(world, location)
                    };
                    reader.ReadFile();
                    filesRead++;

                    for (var index = 0; index < reader.RoadList.Count; index++)
                    {
                        var road = reader.RoadList[index];
                        if (road.RoadNodeList.Count == 0)
                            continue;

                        var baseId = string.IsNullOrWhiteSpace(road.Name)
                            ? $"{Path.GetFileName(roadFile)}-{index}"
                            : road.Name;
                        var roadId = MakeUniqueId(baseId, usedIds);
                        roads.Add(new WorldGeometryRoad
                        {
                            WorldId = world.Id,
                            Id = roadId,
                            Points = road.RoadNodeList.Select(node => new WorldGeometryPoint
                            {
                                X = node.Pos.X,
                                Y = node.Pos.Y,
                                Z = node.Pos.Z
                            }).ToList()
                        });
                    }
                }
                catch (Exception ex)
                {
                    readFailures++;
                    Logger.Warn(ex, "Failed to read road BAI file {0}.", roadFile);
                }
            }
        }

        if (roads.Count > 0)
        {
            roadsNote = null;
            return roads;
        }

        roadsNote = filesFound == 0
            ? "No *road*.bai files were available from the configured client sources. RoadMissionReader and WaypointSurfaceNavigationReader data are not retained by the runtime loader."
            : $"No road polylines were decoded from {filesFound} road BAI file(s); {filesRead} read successfully and {readFailures} failed. WaypointSurfaceNavigationReader retains link indices without point positions, so no waypoint polyline was available.";
        return roads;
    }

    private static string MakeUniqueId(string baseId, HashSet<string> usedIds)
    {
        if (usedIds.Add(baseId))
            return baseId;

        for (var suffix = 2; ; suffix++)
        {
            var candidate = $"{baseId}#{suffix}";
            if (usedIds.Add(candidate))
                return candidate;
        }
    }

    private static Vector3 GetBaiOffset(WorldTemplate world, BaiLocation location)
    {
        if (location.ZoneKey != 0 && world.XmlWorld.Zones.TryGetValue(location.ZoneKey, out var xmlWorldZone))
            return new Vector3(xmlWorldZone.OriginX * 1024f, xmlWorldZone.OriginY * 1024f, 0f);

        return new Vector3(location.PathBlockX * 256f, location.PathBlockY * 256f, 0f);
    }

    private static BaiLocation GetBaiLocation(string file)
    {
        var parts = file.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        var containerIndex = Array.FindLastIndex(parts, part => part.Equals("zone", StringComparison.OrdinalIgnoreCase) ||
                                                                  part.Equals("paths", StringComparison.OrdinalIgnoreCase));
        if (containerIndex < 0 || containerIndex + 1 >= parts.Length)
            return new BaiLocation(0, 0, 0);

        var folder = parts[containerIndex + 1];
        if (uint.TryParse(folder, out var zoneKey))
            return new BaiLocation(zoneKey, 0, 0);

        var coordinates = folder.Split('_');
        if (coordinates.Length == 2 && uint.TryParse(coordinates[0], out var pathBlockX) && uint.TryParse(coordinates[1], out var pathBlockY))
            return new BaiLocation(0, pathBlockX, pathBlockY);

        return new BaiLocation(0, 0, 0);
    }

    private readonly record struct BaiLocation(uint ZoneKey, uint PathBlockX, uint PathBlockY);
}
