using System.Numerics;
using AAEmu.Game.Bots.Navigation;

namespace AAEmu.UnitTests.Bots.Navigation;

public class WorldRoadNavigatorTests
{
    [Test]
    public async Task Session_DispatchesBoundedRoadSegmentsThenFinalBaiApproach()
    {
        var graph = Graph(1);
        var traversal = new RecordingTraversal();
        var session = Session(graph, traversal);

        var first = session.Advance(new Vector3(0, 0, 0));
        var waiting = session.Advance(new Vector3(0, 0, 0));
        var second = session.Advance(new Vector3(5, 0, 0));
        var final = session.Advance(new Vector3(10, 0, 0));
        var completed = session.Advance(new Vector3(10, 2, 0));

        await Assert.That(first.Status).IsEqualTo(WorldRoadNavigationStatus.RoadSegmentDispatched);
        await Assert.That(waiting.Status).IsEqualTo(WorldRoadNavigationStatus.AwaitingLocalTraversal);
        await Assert.That(second.Status).IsEqualTo(WorldRoadNavigationStatus.RoadSegmentDispatched);
        await Assert.That(final.Status).IsEqualTo(WorldRoadNavigationStatus.FinalApproachDispatched);
        await Assert.That(completed.Status).IsEqualTo(WorldRoadNavigationStatus.Completed);
        await Assert.That(traversal.RoadCalls.Count).IsEqualTo(2);
        await Assert.That(traversal.FinalCalls).IsEquivalentTo(new[]
        {
            (new Vector3(10, 0, 0), new Vector3(10, 2, 0))
        });
    }

    [Test]
    public async Task Session_DoesNotTreatAcceptedOrRejectedLocalSegmentAsTraversed()
    {
        var traversal = new RecordingTraversal(
            LocalRouteTraversalStatus.Rejected,
            LocalRouteTraversalStatus.Rejected);
        var session = Session(Graph(1), traversal);

        var replanned = session.Advance(Vector3.Zero);
        var failed = session.Advance(Vector3.Zero);

        await Assert.That(replanned.Status).IsEqualTo(WorldRoadNavigationStatus.Replanned);
        await Assert.That(session.ReplanCount).IsEqualTo(1);
        await Assert.That(failed.Status).IsEqualTo(WorldRoadNavigationStatus.Failed);
        await Assert.That(failed.Failure).IsEqualTo(WorldRoadNavigationFailure.LocalSegmentRejected);
        await Assert.That(traversal.RoadCalls.Count).IsEqualTo(2);
    }

    [Test]
    public async Task Session_ReplansWhenPublishedGraphGenerationChanges()
    {
        var provider = new MutableGraphProvider { Graph = Graph(1) };
        var traversal = new RecordingTraversal();
        var session = Session(provider, traversal);
        var initialGeneration = session.Route.GraphGenerationId;
        session.Advance(Vector3.Zero);
        provider.Graph = Graph(2);

        var update = session.Advance(Vector3.Zero);

        await Assert.That(update.Status).IsEqualTo(WorldRoadNavigationStatus.Replanned);
        await Assert.That(session.Route.GraphGenerationId == initialGeneration).IsFalse();
        await Assert.That(session.Route.SourceRevision).IsEqualTo(2);
        await Assert.That(session.ReplanCount).IsEqualTo(1);
        await Assert.That(traversal.RoadCalls.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Session_FinalApproachRejectionFailsClosed()
    {
        var traversal = new RecordingTraversal
        {
            FinalStatus = LocalRouteTraversalStatus.Rejected
        };
        var session = Session(Graph(1), traversal);
        session.Advance(Vector3.Zero);
        session.Advance(new Vector3(5, 0, 0));

        var failed = session.Advance(new Vector3(10, 0, 0));

        await Assert.That(failed.Status).IsEqualTo(WorldRoadNavigationStatus.Failed);
        await Assert.That(failed.Failure).IsEqualTo(WorldRoadNavigationFailure.FinalApproachRejected);
        await Assert.That(traversal.FinalCalls.Count).IsEqualTo(1);
    }

    [Test]
    public async Task NavigationBoundaryAdapter_UsesDistinctRoadAndFinalSeamsWithoutTeleport()
    {
        var published = new List<Vector3>();
        var adapter = new NavigationBoundaryLocalRouteTraversal(
            new FixedBoundary(new NavigationDecision(
                NavigationDecisionStatus.Accepted,
                NavigationDiagnosticReason.ReachabilityConfirmed)),
            new FixedBoundary(new NavigationDecision(
                NavigationDecisionStatus.Unreachable,
                NavigationDiagnosticReason.ReachabilityRejected)),
            published.Add);

        var road = adapter.BeginRoadSegment(Vector3.Zero, Vector3.UnitX);
        var final = adapter.BeginFinalApproach(Vector3.UnitX, Vector3.One);

        await Assert.That(road).IsEqualTo(LocalRouteTraversalStatus.Accepted);
        await Assert.That(final).IsEqualTo(LocalRouteTraversalStatus.Rejected);
        await Assert.That(published).IsEquivalentTo(new[] { Vector3.UnitX });
    }

    [Test]
    public async Task TransferRoadGraphProvider_CachesOnlyOneGraphPerSourceRevision()
    {
        var snapshots = new CountingSnapshotProvider(new TransferRoadNetworkSnapshot(7,
        [
            new RoadPolylineSnapshot(1, 1, "road", 0, 0, 0, RoadTravelDirection.Bidirectional,
                [new RoadPoint(0, 0, 0), new RoadPoint(10, 0, 0)])
        ]));
        var provider = new TransferRoadGraphProvider(snapshots);

        var first = provider.Capture();
        var second = provider.Capture();

        await Assert.That(second).IsSameReferenceAs(first);
        await Assert.That(snapshots.Captures).IsEqualTo(2);
    }

    private static WorldRoadNavigationSession Session(WorldRoadGraph graph, RecordingTraversal traversal) =>
        Session(new MutableGraphProvider { Graph = graph }, traversal);

    private static WorldRoadNavigationSession Session(
        IWorldRoadGraphProvider provider,
        RecordingTraversal traversal)
    {
        return new WorldRoadNavigationSession(
            provider,
            new WorldRoadRoutePlanner(new WorldRoadRoutePlannerOptions
            {
                MaximumProjectionDistance = 5f,
                MaximumLocalSegmentLength = 5f
            }),
            traversal,
            new RoadRouteEndpoint(1, Vector3.Zero),
            new RoadRouteEndpoint(1, new Vector3(10, 2, 0)),
            new WorldRoadNavigationOptions
            {
                MaximumLocalSegmentLength = 5f,
                MaximumReplans = 1
            });
    }

    private static WorldRoadGraph Graph(long revision)
    {
        return new WorldRoadGraphBuilder().Build(new TransferRoadNetworkSnapshot(revision,
        [
            new RoadPolylineSnapshot(1, 1, "road", 0, 0, 0, RoadTravelDirection.Bidirectional,
                [new RoadPoint(0, 0, 0), new RoadPoint(10, 0, 0)])
        ]));
    }

    private sealed class MutableGraphProvider : IWorldRoadGraphProvider
    {
        public WorldRoadGraph Graph { get; set; }
        public WorldRoadGraph Capture() => Graph;
    }

    private sealed class RecordingTraversal : ILocalRouteTraversal
    {
        private readonly Queue<LocalRouteTraversalStatus> _roadStatuses;

        public RecordingTraversal(params LocalRouteTraversalStatus[] roadStatuses)
        {
            _roadStatuses = new Queue<LocalRouteTraversalStatus>(roadStatuses);
        }

        public List<(Vector3 Start, Vector3 Destination)> RoadCalls { get; } = [];
        public List<(Vector3 Start, Vector3 Destination)> FinalCalls { get; } = [];
        public LocalRouteTraversalStatus FinalStatus { get; init; } = LocalRouteTraversalStatus.Accepted;

        public LocalRouteTraversalStatus BeginRoadSegment(Vector3 start, Vector3 destination)
        {
            RoadCalls.Add((start, destination));
            return _roadStatuses.Count == 0 ? LocalRouteTraversalStatus.Accepted : _roadStatuses.Dequeue();
        }

        public LocalRouteTraversalStatus BeginFinalApproach(Vector3 start, Vector3 destination)
        {
            FinalCalls.Add((start, destination));
            return FinalStatus;
        }
    }

    private sealed class FixedBoundary(NavigationDecision decision) : INavigationDecisionBoundary
    {
        public NavigationDecision Evaluate(Vector3 start, Vector3 destination) => decision;
    }

    private sealed class CountingSnapshotProvider(TransferRoadNetworkSnapshot snapshot)
        : ITransferRoadSnapshotProvider
    {
        public int Captures { get; private set; }

        public TransferRoadNetworkSnapshot Capture()
        {
            Captures++;
            return snapshot;
        }
    }
}
