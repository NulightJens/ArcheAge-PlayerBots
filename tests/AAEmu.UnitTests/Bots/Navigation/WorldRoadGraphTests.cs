using System.Numerics;
using AAEmu.Game.Bots.Navigation;

namespace AAEmu.UnitTests.Bots.Navigation;

public class WorldRoadGraphTests
{
    [Test]
    public async Task Build_PreservesCoordinatesMetadataAndPolylineLength()
    {
        var points = new[]
        {
            new RoadPoint(101.25f, 202.5f, 3.75f, 7),
            new RoadPoint(104.25f, 206.5f, 3.75f, 7)
        };
        var graph = Build(Road("cart-alpha", 41, points, pathType: 17, cellX: 4, cellY: 9));

        await Assert.That(graph.Edges.Count).IsEqualTo(1);
        var edge = graph.Edges[0];
        await Assert.That(edge.WorldId).IsEqualTo(1u);
        await Assert.That(edge.ZoneId).IsEqualTo(41u);
        await Assert.That(edge.PathName).IsEqualTo("cart-alpha");
        await Assert.That(edge.PathType).IsEqualTo(17);
        await Assert.That(edge.CellX).IsEqualTo(4);
        await Assert.That(edge.CellY).IsEqualTo(9);
        await Assert.That(edge.Points).IsEquivalentTo(points);
        await Assert.That(edge.Weight).IsEqualTo(5d);
    }

    [Test]
    public async Task Build_SnapsCompatibleCrossZoneEndpointsDeterministically()
    {
        var first = Road("a", 10, [new RoadPoint(0, 0, 0), new RoadPoint(5, 0, 0)]);
        var second = Road("b", 11, [new RoadPoint(5.5f, 0, 0), new RoadPoint(10, 0, 0)]);
        var builder = new WorldRoadGraphBuilder(new WorldRoadGraphBuildOptions
        {
            EndpointSnapTolerance = 1f
        });

        var graph = builder.Build(new TransferRoadNetworkSnapshot(9, [second, first]));
        var reordered = builder.Build(new TransferRoadNetworkSnapshot(9, [first, second]));

        await Assert.That(graph.Nodes.Count).IsEqualTo(3);
        await Assert.That(graph.ComponentCount).IsEqualTo(1);
        await Assert.That(graph.GenerationId).IsEqualTo(reordered.GenerationId);
        await Assert.That(graph.Edges.Select(edge => edge.PathName)).IsEquivalentTo(new[] { "a", "b" });
        var join = graph.Nodes.Single(node => node.Endpoints.Count == 2);
        await Assert.That(join.Endpoints.Select(endpoint => endpoint.ZoneId)).IsEquivalentTo(new uint[] { 10, 11 });
    }

    [Test]
    public async Task Build_RejectsNonFiniteDegenerateGapVerticalAndSlopeInputs()
    {
        var graph = new WorldRoadGraphBuilder(new WorldRoadGraphBuildOptions
        {
            MaximumPointGap = 10f,
            MaximumVerticalStep = 5f,
            MaximumSlope = 2f
        }).Build(new TransferRoadNetworkSnapshot(1,
        [
            Road("nan", 1, [new RoadPoint(0, 0, 0), new RoadPoint(float.NaN, 0, 0)]),
            Road("zero", 1, [new RoadPoint(0, 0, 0), new RoadPoint(0, 0, 0)]),
            Road("gap", 1, [new RoadPoint(0, 0, 0), new RoadPoint(20, 0, 0)]),
            Road("vertical", 1, [new RoadPoint(0, 0, 0), new RoadPoint(1, 0, 8)]),
            Road("slope", 1, [new RoadPoint(0, 0, 0), new RoadPoint(1, 0, 3)])
        ]));

        await Assert.That(graph.Edges.Count).IsEqualTo(0);
        await Assert.That(graph.Issues.Select(issue => issue.Code)).IsEquivalentTo(new[]
        {
            RoadGraphIssueCode.NonFinitePoint,
            RoadGraphIssueCode.DegeneratePolyline,
            RoadGraphIssueCode.ExcessivePointGap,
            RoadGraphIssueCode.ExcessiveVerticalStep,
            RoadGraphIssueCode.ExcessiveSlope
        });
    }

    [Test]
    public async Task Build_DeduplicatesEquivalentBidirectionalGeometryByStableMetadataOrder()
    {
        var points = new[] { new RoadPoint(0, 0, 0), new RoadPoint(4, 0, 0) };
        var graph = Build(
            Road("z-last", 1, points),
            Road("a-first", 1, points.Reverse().ToArray()));

        await Assert.That(graph.Edges.Count).IsEqualTo(1);
        await Assert.That(graph.Edges[0].PathName).IsEqualTo("a-first");
        await Assert.That(graph.Issues.Single().Code).IsEqualTo(RoadGraphIssueCode.DuplicatePolyline);
        await Assert.That(graph.Issues.Single().Severity).IsEqualTo(RoadGraphIssueSeverity.Warning);
    }

    [Test]
    public async Task Build_NearParallelEqualDistanceEndpointsRemainDisconnected()
    {
        var graph = new WorldRoadGraphBuilder(new WorldRoadGraphBuildOptions
        {
            EndpointSnapTolerance = 1.1f,
            EndpointAmbiguityTolerance = 0.01f
        }).Build(new TransferRoadNetworkSnapshot(1,
        [
            Road("approach", 1, [new RoadPoint(-5, 0, 0), new RoadPoint(0, 0, 0)]),
            Road("lane-left", 2, [new RoadPoint(-1, 0, 0), new RoadPoint(4, 0, 0)]),
            Road("lane-right", 3, [new RoadPoint(1, 0, 0), new RoadPoint(6, 0, 0)])
        ]));

        await Assert.That(graph.ComponentCount).IsEqualTo(3);
        await Assert.That(graph.Issues.Count(issue => issue.Code == RoadGraphIssueCode.AmbiguousEndpoint))
            .IsEqualTo(3);
    }

    [Test]
    public async Task Build_SurfaceMismatchPreventsEndpointSnap()
    {
        var graph = Build(
            Road("surface-a", 1, [new RoadPoint(0, 0, 0, 10), new RoadPoint(5, 0, 0, 10)]),
            Road("surface-b", 1, [new RoadPoint(5, 0, 0, 11), new RoadPoint(10, 0, 0, 11)]));

        await Assert.That(graph.ComponentCount).IsEqualTo(2);
        await Assert.That(graph.Nodes.Count).IsEqualTo(4);
    }

    [Test]
    public async Task Snapshot_CopiesCallerOwnedCollections()
    {
        var points = new List<RoadPoint> { new(0, 0, 0), new(2, 0, 0) };
        var roads = new List<RoadPolylineSnapshot> { Road("fixed", 1, points) };
        var snapshot = new TransferRoadNetworkSnapshot(3, roads);
        points[0] = new RoadPoint(99, 99, 99);
        roads.Clear();

        await Assert.That(snapshot.Roads.Count).IsEqualTo(1);
        await Assert.That(snapshot.Roads[0].Points[0]).IsEqualTo(new RoadPoint(0, 0, 0));
    }

    private static WorldRoadGraph Build(params RoadPolylineSnapshot[] roads) =>
        new WorldRoadGraphBuilder().Build(new TransferRoadNetworkSnapshot(1, roads));

    internal static RoadPolylineSnapshot Road(
        string name,
        uint zoneId,
        IEnumerable<RoadPoint> points,
        RoadTravelDirection direction = RoadTravelDirection.Bidirectional,
        int pathType = 0,
        int cellX = 0,
        int cellY = 0) =>
        new(1, zoneId, name, pathType, cellX, cellY, direction, points);
}
