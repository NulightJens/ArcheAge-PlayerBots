using System.Numerics;
using AAEmu.Game.Models.Game.AI.AStar;
using AAEmu.Game.Models.Game.Char;

namespace AAEmu.Game.Bots.Navigation;

public sealed record BotTravelRoute(
    string Mode,
    IReadOnlyList<Vector3> Waypoints,
    int RoadSteps,
    string Detail);

/// <summary>Combines transfer roads with AAEmu's local pathfinder.</summary>
public static class BotTravelRoutePlanner
{
    private const float RoadMinimumTravelDistance = 18f;
    private const float RoadMaximumProjectionDistance = 200f;
    private const float RoadWaypointSegmentLength = 8f;
    private const float DuplicatePointTolerance = 0.35f;
    private const float MaximumRouteStretch = 5f;
#if !PLAYERBOTS_AAEMU_3_0
    private static readonly IWorldRoadGraphProvider RoadGraphs =
        new TransferRoadGraphProvider(new AaemuTransferRoadSnapshotProvider());
    private static readonly WorldRoadRoutePlanner RoadPlanner = new(new WorldRoadRoutePlannerOptions
    {
        MaximumProjectionDistance = RoadMaximumProjectionDistance,
        MaximumProjectionVerticalGap = 8f,
        // Projection may be wider than the local movement segment.
        MaximumLocalSegmentLength = RoadMaximumProjectionDistance
    });
#endif

    public static BotTravelRoute Plan(Character bot, Vector3 destination)
    {
        if (bot?.ParentWorld == null || bot.Transform?.World == null ||
            !IsFinite(destination))
        {
            return Direct(destination, "world_unavailable");
        }

        var start = bot.Transform.World.Position;
        if (!IsFinite(start))
            return Direct(destination, "start_invalid");

        var directDistance = Vector3.Distance(start, destination);
#if !PLAYERBOTS_AAEMU_3_0
        if (directDistance >= RoadMinimumTravelDistance)
        {
            try
            {
                var graph = RoadGraphs.Capture();
                var worldId = bot.ParentWorld.Template.Id;
                var road = RoadPlanner.Plan(
                    graph,
                    new RoadRouteEndpoint(worldId, start),
                    new RoadRouteEndpoint(worldId, destination));
                if (road.IsSuccess && road.Waypoints.Count > 0)
                {
                    var points = new List<Vector3>();
                    AppendBai(bot, points, start, road.StartProjection.Position);
                    AppendBounded(points, start, road.Waypoints, RoadWaypointSegmentLength);
                    AppendBai(bot, points, road.DestinationProjection.Position, destination);
                    Append(points, [destination]);
                    RemoveOrigin(points, start);

                    var routeLength = Length(start, points);
                    var maximumReasonableLength = Math.Max(100f, directDistance * MaximumRouteStretch);
                    if (points.Count > 0 && routeLength <= maximumReasonableLength)
                    {
                        return new BotTravelRoute(
                            "road+bai",
                            points,
                            road.Steps.Count,
                            $"graph={road.GraphGenerationId} component={road.ComponentId}");
                    }
                }
            }
            catch
            {
                // Fall back to local navigation when road data is unavailable.
            }
        }
#endif

        try
        {
            var local = new List<Vector3>();
            AppendBai(bot, local, start, destination);
            Append(local, [destination]);
            RemoveOrigin(local, start);
            if (local.Count > 0)
                return new BotTravelRoute("bai", local, 0, "native_local_path");
        }
        catch
        {
            // Keep direct local movement as the compatibility fallback.
        }

        return Direct(destination, "native_navigation_unavailable");
    }

    private static void AppendBai(Character bot, ICollection<Vector3> destination, Vector3 start, Vector3 goal)
    {
        if (Vector3.Distance(start, goal) <= DuplicatePointTolerance)
            return;

        IReadOnlyList<Vector3> path;
#if PLAYERBOTS_AAEMU_3_0
        var startPoint = new Point(start.X, start.Y, start.Z);
        var goalPoint = new Point(goal.X, goal.Y, goal.Z);
        var oldPath = new PathNode
        {
            ZoneKey = bot.Transform.ZoneId,
            pos1 = startPoint,
            pos2 = goalPoint
        }.FindPath(startPoint, goalPoint);
        path = oldPath?.Select(point => new Vector3(point.X, point.Y, point.Z)).ToArray();
#else
        path = new PathNode { ZoneKey = bot.Transform.ZoneId }
            .FindPath(bot.ParentWorld, start, goal);
#endif
        if (path == null || path.Count == 0)
            return;
        Append(destination, path);
    }

    private static void Append(ICollection<Vector3> destination, IEnumerable<Vector3> source)
    {
        foreach (var point in source)
        {
            if (!IsFinite(point))
                continue;
            if (destination.LastOrDefault() is var previous && destination.Count > 0 &&
                Vector3.Distance(previous, point) <= DuplicatePointTolerance)
            {
                continue;
            }
            destination.Add(point);
        }
    }

    private static void AppendBounded(
        ICollection<Vector3> destination,
        Vector3 origin,
        IEnumerable<Vector3> source,
        float maximumSegmentLength)
    {
        var previous = destination.Count > 0 ? destination.Last() : origin;
        foreach (var point in source)
        {
            if (!IsFinite(point))
                continue;

            var distance = Vector3.Distance(previous, point);
            var divisions = Math.Max(1, (int)Math.Ceiling(distance / maximumSegmentLength));
            for (var part = 1; part <= divisions; part++)
                Append(destination, [Vector3.Lerp(previous, point, part / (float)divisions)]);
            previous = point;
        }
    }

    private static void RemoveOrigin(IList<Vector3> points, Vector3 origin)
    {
        while (points.Count > 0 && Vector3.Distance(points[0], origin) <= DuplicatePointTolerance)
            points.RemoveAt(0);
    }

    private static float Length(Vector3 start, IReadOnlyList<Vector3> points)
    {
        var length = 0f;
        var previous = start;
        foreach (var point in points)
        {
            length += Vector3.Distance(previous, point);
            previous = point;
        }
        return length;
    }

    private static BotTravelRoute Direct(Vector3 destination, string detail) =>
        new("direct", [destination], 0, detail);

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
