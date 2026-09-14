using System.Numerics;
using AAEmu.Game.Models.Game.AI.AStar;
using AAEmu.Game.Models.Game.Char;

namespace AAEmu.Game.Bots.Navigation;

public sealed record BotTravelRoute(
    string Mode,
    IReadOnlyList<Vector3> Waypoints,
    int RoadSteps,
    string Detail);

public enum BotTravelIntent
{
    Transit,
    QuestObjective,
    Interaction,
    PartyRendezvous
}

/// <summary>
/// Composes a behavior-owned destination from authoritative transfer roads and
/// AAEmu's native BAI pathfinder. No point is teleported or applied directly.
/// </summary>
public static class BotTravelRoutePlanner
{
    private const float RoadMinimumTravelDistance = 18f;
    private const float RoadMaximumProjectionDistance = 200f;
    private const float RoadWaypointSegmentLength = 8f;
    private const float DuplicatePointTolerance = 0.35f;
    private const float MaximumRouteStretch = 5f;
    internal const float TerminalDirectDistance = 15f;
    internal const float NearbyConnectorDistance = 30f;
    private const float TerminalGroundSampleLength = 1f;
    private const float TerminalSurfaceTolerance = 1f;
    private const float MaximumTerminalGroundStep = 1.25f;
    private static readonly IWorldRoadGraphProvider RoadGraphs =
        new TransferRoadGraphProvider(new CompositeTransferRoadSnapshotProvider(
            new AaemuTransferRoadSnapshotProvider(),
            new RecordedTransferRoadSnapshotProvider()));
    private static readonly WorldRoadRoutePlanner RoadPlanner = new(new WorldRoadRoutePlannerOptions
    {
        MaximumProjectionDistance = RoadMaximumProjectionDistance,
        MaximumProjectionVerticalGap = 8f,
        // The route planner requires this bound to cover endpoint projection.
        // We re-bound emitted road points to the tighter movement segment below.
        MaximumLocalSegmentLength = RoadMaximumProjectionDistance
    });

    public static BotTravelRoute Plan(
        Character bot,
        Vector3 destination,
        BotTravelIntent intent = BotTravelIntent.Transit)
    {
        if (bot?.Transform?.World == null || !IsFinite(destination))
            return Unavailable("world_unavailable");

        if (bot.ParentWorld == null)
            return intent == BotTravelIntent.Transit
                ? Direct(destination, "world_unavailable_transit_compatibility")
                : Unavailable("world_unavailable");

        var start = bot.Transform.World.Position;
        if (!IsFinite(start))
            return Unavailable("start_invalid");

        var directDistance = Vector3.Distance(start, destination);
        if (intent == BotTravelIntent.PartyRendezvous &&
            IsSafeNearbyConnector(start, destination, bot.ParentWorld.GetHeight))
            return Direct(destination, "intent=partyrendezvous checked_nearby_ground_connector");
        var roadFailure = "not_needed";
        if (directDistance >= RoadMinimumTravelDistance)
        {
            try
            {
                var graph = RoadGraphs.Capture();
                var worldId = bot.ParentWorld.Template.Id;
                var road = RoadPlanner.Plan(
                    graph,
                    new RoadRouteEndpoint(worldId, start),
                    new RoadRouteEndpoint(worldId, destination)
#if !PLAYERBOTS_AAEMU_3_0
                    // Use the connector's existing surface requirement during
                    // selection, so an unusable nearest point cannot hide a
                    // compatible entry on another segment. BAI and terminal
                    // checks still validate the complete approach afterwards.
                    , point => IsGroundCompatiblePoint(point, bot.ParentWorld.GetHeight)
#endif
                    );
                roadFailure = road.Status.ToString();
                if (road.IsSuccess && road.Waypoints.Count > 0)
                {
                    var points = new List<Vector3>();
                    var usedBai = false;
                    var usedTerminalDirect = false;
                    var startConnected = Vector3.Distance(start, road.StartProjection.Position) <=
                                         DuplicatePointTolerance;
                    if (!startConnected)
                    {
                        usedBai = AppendBai(bot, points, start, road.StartProjection.Position, out usedTerminalDirect);
                        startConnected = usedBai;
                        if (!startConnected &&
                            IsSafeNearbyConnector(
                                start,
                                road.StartProjection.Position,
                                bot.ParentWorld.GetHeight))
                        {
                            Append(points, [road.StartProjection.Position]);
                            startConnected = true;
                            usedTerminalDirect = true;
                        }
                    }
                    if (!startConnected) roadFailure = "start_connector_unavailable";

                    if (startConnected)
                    {
                        AppendBounded(
                            points,
                            start,
                            road.Waypoints,
                            RoadWaypointSegmentLength,
                            (x, y) => bot.ParentWorld.GetHeight(x, y));
                        var connectorStart = points.Count;
                        var endpointUsedBai = AppendLocalConnector(
                            bot,
                            points,
                            road.DestinationProjection.Position,
                            destination,
                            intent,
                            out var endpointUsedTerminalDirect);
                        usedBai |= endpointUsedBai;
                        usedTerminalDirect |= endpointUsedTerminalDirect;
                        var destinationConnected =
                            Vector3.Distance(road.DestinationProjection.Position, destination) <=
                            DuplicatePointTolerance || points.Count > connectorStart;
                        if (!destinationConnected) roadFailure = "destination_connector_unavailable";

                        if (destinationConnected)
                        {
                            Append(points, [destination]);
                            RemoveOrigin(points, start);

                            var routeLength = Length(start, points);
                            var maximumReasonableLength = Math.Max(100f, directDistance * MaximumRouteStretch);
                            roadFailure = $"road_stretch_{routeLength:F0}_limit_{maximumReasonableLength:F0}";
                            if (points.Count > 0 && routeLength <= maximumReasonableLength)
                            {
                                return new BotTravelRoute(
                                    ComposeMode(road: true, usedBai, usedTerminalDirect),
                                    points,
                                    road.Steps.Count,
                                    $"intent={IntentName(intent)} graph={road.GraphGenerationId} " +
                                    $"component={road.ComponentId} length={routeLength:F1} start_road={road.StartProjection.PathName} " +
                                    $"end_road={road.DestinationProjection.PathName} z=live_ground");
                            }
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                roadFailure = "road_exception_" + exception.GetType().Name;
                // The road layer is optional at runtime. BAI remains the safe local fallback.
            }
        }

        try
        {
            var local = new List<Vector3>();
            var usedBai = AppendLocalConnector(
                bot,
                local,
                start,
                destination,
                intent,
                out var usedTerminalDirect);
            var destinationConnected = directDistance <= DuplicatePointTolerance || local.Count > 0;
            if (destinationConnected)
            {
                Append(local, [destination]);
                RemoveOrigin(local, start);
                return new BotTravelRoute(
                    ComposeMode(road: false, usedBai, usedTerminalDirect),
                    local,
                    0,
                    $"intent={IntentName(intent)} native_local_path road_rejected={roadFailure} " +
                    $"length={Length(start, local):F1} direct={directDistance:F1}");
            }
        }
        catch
        {
            // The guarded fallback below decides whether direct travel is safe.
        }

        if (IsSafeTerminalDirect(start, destination, bot.ParentWorld.GetHeight))
            return Direct(destination, $"intent={IntentName(intent)} safe_terminal_fallback");

        // Explicit transit commands retain their compatibility fallback. Quest
        // ownership must never turn missing navigation into a long straight line.
        return intent == BotTravelIntent.Transit
            ? Direct(destination, $"intent={IntentName(intent)} native_navigation_unavailable")
            : Unavailable($"intent={IntentName(intent)} native_navigation_unavailable road_rejected={roadFailure}");
    }

    private static bool AppendLocalConnector(
        Character bot,
        ICollection<Vector3> destination,
        Vector3 start,
        Vector3 goal,
        BotTravelIntent intent,
        out bool usedTerminalDirect)
    {
        usedTerminalDirect = false;
        if (intent != BotTravelIntent.Transit &&
            IsSafeTerminalDirect(start, goal, bot.ParentWorld.GetHeight))
        {
            Append(destination, [goal]);
            usedTerminalDirect = true;
            return false;
        }

        var path = FindBaiPath(bot, start, goal, out usedTerminalDirect);
        if (path.Count == 0)
            return false;

        if (intent == BotTravelIntent.Interaction)
        {
            var handoffIndex = FindTerminalHandoff(path, goal, bot.ParentWorld.GetHeight);
            if (handoffIndex >= 0)
            {
                Append(destination, path.Take(handoffIndex + 1));
                Append(destination, [goal]);
                usedTerminalDirect = true;
                return true;
            }
        }

        Append(destination, path);
        return true;
    }

    private static bool AppendBai(Character bot, ICollection<Vector3> destination, Vector3 start, Vector3 goal,
        out bool usedTerminalDirect)
    {
        usedTerminalDirect = false;
        if (Vector3.Distance(start, goal) <= DuplicatePointTolerance)
            return false;

        var path = FindBaiPath(bot, start, goal, out usedTerminalDirect);
        if (path.Count == 0)
            return false;
        Append(destination, path);
        return true;
    }

    private static IReadOnlyList<Vector3> FindBaiPath(Character bot, Vector3 start, Vector3 goal,
        out bool usedTerminalDirect)
    {
        var path = new PathNode { ZoneKey = bot.Transform.ZoneId }.FindPath(bot.ParentWorld, start, goal) ?? [];
#if !PLAYERBOTS_AAEMU_3_0
        var connected = CompleteNativeConnector(path, goal, bot.ParentWorld.GetHeight, out usedTerminalDirect);
        if (path.Count > 0 && connected.Count == 0)
        {
            var invalidIndex = -1;
            for (var index = 0; index < path.Count; index++)
                if (!IsGroundCompatiblePoint(path[index], bot.ParentWorld.GetHeight))
                { invalidIndex = index; break; }
            var invalidPoint = invalidIndex >= 0 ? path[invalidIndex] : (Vector3?)null;
            NLog.LogManager.GetCurrentClassLogger().Warn(
                $"BOT id={bot.Id} ev=bai_route_rejected reason=invalid_ground_or_terminal_connector " +
                $"points={path.Count} endpoint={path[^1]} goal={goal} " +
                $"endpoint_ground={SampleGround(path[^1].X, path[^1].Y, bot.ParentWorld.GetHeight):R} " +
                $"goal_ground={SampleGround(goal.X, goal.Y, bot.ParentWorld.GetHeight):R} " +
                $"invalid_index={invalidIndex} invalid_point={invalidPoint} " +
                $"invalid_ground={(invalidPoint is { } bad ? SampleGround(bad.X, bad.Y, bot.ParentWorld.GetHeight) : float.NaN):R}");
        }
        return connected;
#else
        usedTerminalDirect = false;
        var classified = BotLocalPathResult.Classify(path, goal);
        if (classified.Status != BotLocalPathStatus.Complete)
            return [];
        return path;
#endif
    }

#if !PLAYERBOTS_AAEMU_3_0
    internal static bool IsGroundCompatiblePoint(Vector3 point, Func<float, float, float> groundHeight)
    {
        if (!IsFinite(point) || groundHeight == null) return false;
        var ground = SampleGround(point.X, point.Y, groundHeight);
        return float.IsFinite(ground) && MathF.Abs(point.Z - ground) <= TerminalSurfaceTolerance;
    }

    internal static IReadOnlyList<Vector3> CompleteNativeConnector(IReadOnlyList<Vector3> path,
        Vector3 goal, Func<float, float, float> groundHeight, out bool usedTerminalDirect)
    {
        usedTerminalDirect = false;
        var classified = BotLocalPathResult.Classify(path, goal);
        if (classified.Status == BotLocalPathStatus.Missing ||
            !HasGroundCompatibleWaypoints(path, groundHeight)) return [];
        if (classified.Status == BotLocalPathStatus.Complete) return classified.Waypoints;

        // Native navigation can snap to a nearby node. Keep that actual endpoint
        // and validate the remaining leg before treating the connector as complete.
        // A partial path alone never authorizes appending an arbitrary destination.
        if (!IsSafeTerminalDirect(classified.ActualEndpoint.Value, goal, groundHeight)) return [];
        usedTerminalDirect = true;
        return [.. classified.Waypoints, goal];
    }

    internal static bool HasGroundCompatibleWaypoints(IReadOnlyList<Vector3> path,
        Func<float, float, float> groundHeight)
    {
        if (path == null || path.Count == 0 || path.Count > 4096) return false;
        try
        {
            foreach (var point in path)
            {
                var ground = groundHeight(point.X, point.Y);
                if (!IsFinite(point) || !float.IsFinite(ground) || MathF.Abs(point.Z - ground) > 1f)
                    return false;
            }
            return true;
        }
        catch { return false; }
    }
#endif

    internal static int FindTerminalHandoff(
        IReadOnlyList<Vector3> baiPath,
        Vector3 destination,
        Func<float, float, float> groundHeight)
    {
        if (baiPath == null)
            return -1;

        for (var index = 0; index < baiPath.Count; index++)
        {
            var candidate = baiPath[index];
            if (Vector3.Distance(candidate, destination) <= DuplicatePointTolerance)
                continue;
            if (IsSafeTerminalDirect(candidate, destination, groundHeight))
                return index;
        }

        return -1;
    }

    internal static bool IsSafeTerminalDirect(
        Vector3 start,
        Vector3 destination,
        Func<float, float, float> groundHeight)
        => IsSafeGroundConnector(start, destination, groundHeight, TerminalDirectDistance);

    internal static bool IsSafeNearbyConnector(Vector3 start, Vector3 destination,
        Func<float, float, float> groundHeight)
        => IsSafeGroundConnector(start, destination, groundHeight, NearbyConnectorDistance);

    private static bool IsSafeGroundConnector(Vector3 start, Vector3 destination,
        Func<float, float, float> groundHeight, float maximumDistance)
    {
        if (!IsFinite(start) || !IsFinite(destination) || groundHeight == null)
            return false;

        var planarDistance = Vector2.Distance(
            new Vector2(start.X, start.Y),
            new Vector2(destination.X, destination.Y));
        if (!float.IsFinite(planarDistance) || planarDistance > maximumDistance)
            return false;

        var samples = Math.Max(1, (int)Math.Ceiling(planarDistance / TerminalGroundSampleLength));
        var previousHeight = SampleGround(start.X, start.Y, groundHeight);
        var destinationHeight = SampleGround(destination.X, destination.Y, groundHeight);
        if (!float.IsFinite(previousHeight) || !float.IsFinite(destinationHeight) ||
            MathF.Abs(start.Z - previousHeight) > TerminalSurfaceTolerance ||
            MathF.Abs(destination.Z - destinationHeight) > TerminalSurfaceTolerance)
        {
            return false;
        }

        for (var sample = 1; sample <= samples; sample++)
        {
            var amount = sample / (float)samples;
            var x = start.X + (destination.X - start.X) * amount;
            var y = start.Y + (destination.Y - start.Y) * amount;
            var height = SampleGround(x, y, groundHeight);
            if (!float.IsFinite(height) || MathF.Abs(height - previousHeight) > MaximumTerminalGroundStep)
                return false;
            previousHeight = height;
        }

        return true;
    }

    private static float SampleGround(float x, float y, Func<float, float, float> groundHeight)
    {
        try
        {
            return groundHeight(x, y);
        }
        catch
        {
            return float.NaN;
        }
    }

    private static string ComposeMode(bool road, bool bai, bool terminalDirect)
    {
        var parts = new List<string>(3);
        if (road)
            parts.Add("road");
        if (bai)
            parts.Add("bai");
        if (terminalDirect)
            parts.Add("terminal-direct");
        if (parts.Count == 0)
            parts.Add("direct");
        return string.Join('+', parts);
    }

    private static string IntentName(BotTravelIntent intent) =>
        intent.ToString().ToLowerInvariant();

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
        float maximumSegmentLength,
        Func<float, float, float> groundHeight)
    {
        var previous = destination.Count > 0 ? destination.Last() : origin;
        foreach (var point in source)
        {
            if (!IsFinite(point))
                continue;

            var distance = Vector3.Distance(previous, point);
            var divisions = Math.Max(1, (int)Math.Ceiling(distance / maximumSegmentLength));
            for (var part = 1; part <= divisions; part++)
            {
                var waypoint = Vector3.Lerp(previous, point, part / (float)divisions);
                Append(destination, [ProjectRecordedWaypointToGround(waypoint, groundHeight)]);
            }
            previous = point;
        }
    }

    internal static Vector3 ProjectRecordedWaypointToGround(
        Vector3 waypoint,
        Func<float, float, float> groundHeight)
    {
        var liveHeight = groundHeight(waypoint.X, waypoint.Y);
        return float.IsFinite(liveHeight)
            ? new Vector3(waypoint.X, waypoint.Y, liveHeight)
            : waypoint;
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

    private static BotTravelRoute Unavailable(string detail) =>
        new("unavailable", [], 0, detail);

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
