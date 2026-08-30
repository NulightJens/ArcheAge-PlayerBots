using AAEmu.Game.Bots.Ops;

namespace AAEmu.UnitTests.Bots.Ops;

public class WorldGeometryWriterTests
{
    [Test]
    public async Task Serialize_PreservesWorldGeometryShape()
    {
        var document = new WorldGeometryDocument
        {
            Worlds =
            [
                new WorldGeometryWorld { Id = 0, Name = "main_world" }
            ],
            ZoneGroups =
            [
                new WorldGeometryZoneGroup
                {
                    Id = 10,
                    Name = "West",
                    X = 1,
                    Y = 2,
                    Width = 3,
                    Height = 4,
                    FactionId = 1,
                    TargetId = 20
                },
                new WorldGeometryZoneGroup
                {
                    Id = 11,
                    Name = "East",
                    X = 5,
                    Y = 6,
                    Width = 7,
                    Height = 8,
                    FactionId = 2,
                    TargetId = 21
                }
            ],
            Zones =
            [
                new WorldGeometryZone
                {
                    Id = 100,
                    Name = "Zone A",
                    ZoneKey = 101,
                    GroupId = 10,
                    FactionId = 1,
                    Closed = false
                }
            ],
            Roads =
            [
                new WorldGeometryRoad
                {
                    WorldId = 0,
                    Id = "road-a",
                    Points =
                    [
                        new WorldGeometryPoint { X = 1, Y = 2, Z = 3 },
                        new WorldGeometryPoint { X = 4, Y = 5, Z = 6 }
                    ]
                }
            ]
        };

        var expected = """
{
  "worlds": [
    {
      "id": 0,
      "name": "main_world"
    }
  ],
  "zoneGroups": [
    {
      "id": 10,
      "name": "West",
      "x": 1,
      "y": 2,
      "w": 3,
      "h": 4,
      "factionId": 1,
      "targetId": 20
    },
    {
      "id": 11,
      "name": "East",
      "x": 5,
      "y": 6,
      "w": 7,
      "h": 8,
      "factionId": 2,
      "targetId": 21
    }
  ],
  "zones": [
    {
      "id": 100,
      "name": "Zone A",
      "zoneKey": 101,
      "groupId": 10,
      "factionId": 1,
      "closed": false
    }
  ],
  "roads": [
    {
      "worldId": 0,
      "id": "road-a",
      "points": [
        {
          "x": 1,
          "y": 2,
          "z": 3
        },
        {
          "x": 4,
          "y": 5,
          "z": 6
        }
      ]
    }
  ],
  "npcSpawnCounts": []
}
""";

        await Assert.That(WorldGeometryWriter.Serialize(document)).IsEqualTo(expected);
    }
}
