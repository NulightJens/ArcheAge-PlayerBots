using System.Numerics;
using AAEmu.Game.Bots.Navigation;

namespace AAEmu.UnitTests.Bots.Navigation;

public class WorldRoadRoutePlannerTests
{
    [Test]
    public async Task ProjectNearest_ReturnsDeterministicSafePolylineProjection()
    {
        var graph = Build(Road("road", 1, (0, 0), (10, 0)));
        var projection = new WorldRoadRoutePlanner().ProjectNearest(
            graph,
            new RoadRouteEndpoint(1, new Vector3(3, 2, 0)));

        await Assert.That(projection).IsNotNull();
        await Assert.That(projection.Position).IsEqualTo(new Vector3(3, 0, 0));
        await Assert.That(projection.Distance).IsEqualTo(2f);
        await Assert.That(Math.Abs(projection.AlongDistance - 3d) < 0.00001d).IsTrue();
        await Assert.That(projection.PathName).IsEqualTo("road");
    }

    [Test]
    public async Task ProjectNearest_RejectsExcessiveVerticalAndKnownSurfaceGaps()
    {
        var graph = Build(new RoadPolylineSnapshot(
            1,
            1,
            "surface-road",
            0,
            0,
            0,
            RoadTravelDirection.Bidirectional,
            [new RoadPoint(0, 0, 0, 7), new RoadPoint(10, 0, 0, 7)]));
        var planner = new WorldRoadRoutePlanner(new WorldRoadRoutePlannerOptions
        {
            MaximumProjectionDistance = 10f,
            MaximumProjectionVerticalGap = 5f,
            MaximumLocalSegmentLength = 10f
        });

        var vertical = planner.ProjectNearest(graph, new RoadRouteEndpoint(1, new Vector3(5, 0, 6), 7));
        var surface = planner.ProjectNearest(graph, new RoadRouteEndpoint(1, new Vector3(5, 0, 0), 8));

        await Assert.That(vertical).IsNull();
        await Assert.That(surface).IsNull();
    }

    [Test]
    public async Task Plan_UsesDistanceWeightedShortestCrossZoneRoute()
    {
        var graph = Build(
            Road("start", 10, (0, 0), (2, 0)),
            Road("short", 11, (2, 0), (8, 0)),
            Road("long-a", 12, (2, 0), (5, 3)),
            Road("long-b", 13, (5, 3), (8, 0)),
            Road("end", 14, (8, 0), (10, 0)));

        var route = new WorldRoadRoutePlanner().Plan(
            graph,
            Endpoint(0.1f, 0),
            Endpoint(9.9f, 0));

        await Assert.That(route.Status).IsEqualTo(RoadRouteStatus.Success);
        await Assert.That(route.Steps.Select(step => step.PathName)).Contains("short");
        await Assert.That(route.Steps.Select(step => step.PathName)).DoesNotContain("long-a");
        await Assert.That(route.Steps.Select(step => step.ZoneId).Distinct().Count()).IsEqualTo(3);
        await Assert.That(Math.Abs(route.TotalWeight - 9.8d) < 0.00001d).IsTrue();
    }

    [Test]
    public async Task Plan_EqualCostTieSelectsStableLexicalEdge()
    {
        var graph = Build(
            Road("start", 1, (0, 0), (2, 0)),
            Road("alpha", 2, (2, 0), (5, 2), (8, 0)),
            Road("beta", 3, (2, 0), (5, -2), (8, 0)),
            Road("end", 4, (8, 0), (10, 0)));
        var planner = new WorldRoadRoutePlanner();

        var first = planner.Plan(graph, Endpoint(0.1f, 0), Endpoint(9.9f, 0));
        var second = planner.Plan(graph, Endpoint(0.1f, 0), Endpoint(9.9f, 0));

        await Assert.That(first.Steps.Select(step => step.PathName)).Contains("alpha");
        await Assert.That(first.Steps.Select(step => step.PathName)).DoesNotContain("beta");
        await Assert.That(second.Steps).IsEquivalentTo(first.Steps);
        await Assert.That(second.Waypoints).IsEquivalentTo(first.Waypoints);
    }

    [Test]
    public async Task Plan_DisconnectedComponentsFailClosed()
    {
        var graph = Build(
            Road("west", 1, (0, 0), (10, 0)),
            Road("east", 2, (100, 0), (110, 0)));

        var route = new WorldRoadRoutePlanner().Plan(graph, Endpoint(0, 0), Endpoint(110, 0));

        await Assert.That(route.Status).IsEqualTo(RoadRouteStatus.Disconnected);
        await Assert.That(route.Waypoints.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Plan_OneWayReverseRequestFailsClosed()
    {
        var graph = Build(Road(
            "one-way",
            1,
            RoadTravelDirection.ForwardOnly,
            (0, 0),
            (10, 0)));

        var route = new WorldRoadRoutePlanner().Plan(graph, Endpoint(9, 0), Endpoint(1, 0));

        await Assert.That(route.Status).IsEqualTo(RoadRouteStatus.NoDirectedRoute);
        await Assert.That(route.IsSuccess).IsFalse();
    }

    [Test]
    public async Task Plan_BoundsEveryWaypointSegmentForLocalNavigation()
    {
        var graph = Build(Road("long", 1, (0, 0), (50, 0)));
        var route = new WorldRoadRoutePlanner(new WorldRoadRoutePlannerOptions
        {
            MaximumProjectionDistance = 10f,
            MaximumLocalSegmentLength = 10f
        }).Plan(graph, Endpoint(0, 0), Endpoint(50, 0));

        await Assert.That(route.Status).IsEqualTo(RoadRouteStatus.Success);
        await Assert.That(route.Waypoints.Count).IsEqualTo(6);
        await Assert.That(route.Waypoints.Zip(route.Waypoints.Skip(1), Vector3.Distance)
            .All(distance => distance <= 10.0001f)).IsTrue();
    }

    [Test]
    public async Task DebugExport_IsDeterministicAndContainsGraphComponentsAndRoute()
    {
        var graph = Build(Road("debug", 77, (0, 0), (5, 0)));
        var route = new WorldRoadRoutePlanner().Plan(graph, Endpoint(0, 0), Endpoint(5, 0));

        var first = WorldNavigationDebugExporter.Serialize(graph, route);
        var second = WorldNavigationDebugExporter.Serialize(graph, route);

        await Assert.That(second).IsEqualTo(first);
        await Assert.That(first).Contains("\"componentCount\": 1");
        await Assert.That(first).Contains("\"pathName\": \"debug\"");
        await Assert.That(first).Contains("\"status\": \"Success\"");
    }

    private static RoadRouteEndpoint Endpoint(float x, float y) =>
        new(1, new Vector3(x, y, 0));

    private static WorldRoadGraph Build(params RoadPolylineSnapshot[] roads) =>
        new WorldRoadGraphBuilder(new WorldRoadGraphBuildOptions
        {
            EndpointSnapTolerance = 0.1f
        }).Build(new TransferRoadNetworkSnapshot(1, roads));

    private static RoadPolylineSnapshot Road(
        string name,
        uint zoneId,
        params (float X, float Y)[] points) =>
        Road(name, zoneId, RoadTravelDirection.Bidirectional, points);

    private static RoadPolylineSnapshot Road(
        string name,
        uint zoneId,
        RoadTravelDirection direction,
        params (float X, float Y)[] points) =>
        new(1, zoneId, name, 0, 0, 0, direction, points.Select(point => new RoadPoint(point.X, point.Y, 0)));
}
