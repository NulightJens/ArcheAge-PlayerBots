using System.Numerics;

namespace AAEmu.Game.Bots.Navigation;

public interface IWorldRoadGraphProvider
{
    WorldRoadGraph Capture();
}

public sealed class TransferRoadGraphProvider : IWorldRoadGraphProvider
{
    private readonly ITransferRoadSnapshotProvider _snapshotProvider;
    private readonly WorldRoadGraphBuilder _builder;
    private readonly object _sync = new();
    private WorldRoadGraph _cached;

    public TransferRoadGraphProvider(
        ITransferRoadSnapshotProvider snapshotProvider,
        WorldRoadGraphBuilder builder = null)
    {
        _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
        _builder = builder ?? new WorldRoadGraphBuilder();
    }

    public WorldRoadGraph Capture()
    {
        var snapshot = _snapshotProvider.Capture() ??
                       throw new InvalidOperationException("The transfer-road provider returned no snapshot.");
        lock (_sync)
        {
            if (_cached == null || _cached.SourceRevision != snapshot.Revision)
                _cached = _builder.Build(snapshot);
            return _cached;
        }
    }
}

public enum LocalRouteTraversalStatus
{
    Accepted,
    Rejected,
    Unavailable
}

public interface ILocalRouteTraversal
{
    LocalRouteTraversalStatus BeginRoadSegment(Vector3 start, Vector3 destination);
    LocalRouteTraversalStatus BeginFinalApproach(Vector3 start, Vector3 destination);
}

/// <summary>
/// Bridges route waypoints to the existing heightmap/BAI navigation decision seam and
/// a caller-supplied normal movement publisher. It has no teleport path.
/// </summary>
public sealed class NavigationBoundaryLocalRouteTraversal : ILocalRouteTraversal
{
    private readonly INavigationDecisionBoundary _roadBoundary;
    private readonly INavigationDecisionBoundary _finalApproachBoundary;
    private readonly Action<Vector3> _publishDestination;

    public NavigationBoundaryLocalRouteTraversal(
        INavigationDecisionBoundary roadBoundary,
        INavigationDecisionBoundary finalApproachBoundary,
        Action<Vector3> publishDestination)
    {
        _roadBoundary = roadBoundary ?? throw new ArgumentNullException(nameof(roadBoundary));
        _finalApproachBoundary = finalApproachBoundary ??
                                 throw new ArgumentNullException(nameof(finalApproachBoundary));
        _publishDestination = publishDestination ?? throw new ArgumentNullException(nameof(publishDestination));
    }

    public LocalRouteTraversalStatus BeginRoadSegment(Vector3 start, Vector3 destination) =>
        Begin(_roadBoundary, start, destination);

    public LocalRouteTraversalStatus BeginFinalApproach(Vector3 start, Vector3 destination) =>
        Begin(_finalApproachBoundary, start, destination);

    private LocalRouteTraversalStatus Begin(
        INavigationDecisionBoundary boundary,
        Vector3 start,
        Vector3 destination)
    {
        NavigationDecision decision;
        try
        {
            decision = boundary.Evaluate(start, destination);
        }
        catch
        {
            return LocalRouteTraversalStatus.Unavailable;
        }

        if (!decision.IsAccepted)
        {
            return decision.Status == NavigationDecisionStatus.Unavailable
                ? LocalRouteTraversalStatus.Unavailable
                : LocalRouteTraversalStatus.Rejected;
        }

        try
        {
            _publishDestination(destination);
            return LocalRouteTraversalStatus.Accepted;
        }
        catch
        {
            return LocalRouteTraversalStatus.Unavailable;
        }
    }
}

public sealed class WorldRoadNavigationOptions
{
    public float WaypointArrivalTolerance { get; init; } = 0.75f;
    public float FinalArrivalTolerance { get; init; } = 0.75f;
    public float MaximumLocalSegmentLength { get; init; } = 20f;
    public int MaximumReplans { get; init; } = 1;
}

public enum WorldRoadNavigationStatus
{
    Planned,
    RoadSegmentDispatched,
    AwaitingLocalTraversal,
    Replanned,
    FinalApproachDispatched,
    Completed,
    Failed
}

public enum WorldRoadNavigationFailure
{
    None,
    GraphUnavailable,
    RoutePlanningFailed,
    WorldChangeReplanFailed,
    InvalidLocalSegment,
    LocalSegmentRejected,
    LocalNavigationUnavailable,
    FinalApproachRejected,
    FinalApproachUnavailable
}

public sealed record WorldRoadNavigationUpdate(
    WorldRoadNavigationStatus Status,
    WorldRoadNavigationFailure Failure,
    Vector3? ActiveDestination,
    string Detail);

/// <summary>
/// Drives one long-range route without assuming that a published local destination was
/// reached. A waypoint advances only after the caller reports a position within tolerance.
/// </summary>
public sealed class WorldRoadNavigationSession
{
    private readonly IWorldRoadGraphProvider _graphProvider;
    private readonly WorldRoadRoutePlanner _planner;
    private readonly ILocalRouteTraversal _localTraversal;
    private readonly RoadRouteEndpoint _destination;
    private readonly WorldRoadNavigationOptions _options;
    private RoadRouteResult _route;
    private int _nextWaypoint;
    private Vector3? _activeDestination;
    private bool _activeIsFinal;
    private int _replans;
    private WorldRoadNavigationUpdate _terminal;

    public WorldRoadNavigationSession(
        IWorldRoadGraphProvider graphProvider,
        WorldRoadRoutePlanner planner,
        ILocalRouteTraversal localTraversal,
        RoadRouteEndpoint start,
        RoadRouteEndpoint destination,
        WorldRoadNavigationOptions options = null)
    {
        _graphProvider = graphProvider ?? throw new ArgumentNullException(nameof(graphProvider));
        _planner = planner ?? throw new ArgumentNullException(nameof(planner));
        _localTraversal = localTraversal ?? throw new ArgumentNullException(nameof(localTraversal));
        _destination = destination;
        _options = options ?? new WorldRoadNavigationOptions();
        ValidateOptions(_options);

        WorldRoadGraph graph;
        try
        {
            graph = _graphProvider.Capture();
        }
        catch
        {
            _terminal = Failed(WorldRoadNavigationFailure.GraphUnavailable, "The initial graph snapshot is unavailable.");
            return;
        }

        _route = _planner.Plan(graph, start, destination);
        if (!_route.IsSuccess)
        {
            _terminal = Failed(
                WorldRoadNavigationFailure.RoutePlanningFailed,
                $"Initial route failed: {_route.Status}.");
        }
    }

    public RoadRouteResult Route => _route;
    public int ReplanCount => _replans;

    public WorldRoadNavigationUpdate Advance(Vector3 currentPosition)
    {
        if (_terminal != null)
            return _terminal;
        if (!IsFinite(currentPosition))
            return SetTerminal(WorldRoadNavigationFailure.InvalidLocalSegment, "Current position is non-finite.");

        WorldRoadGraph graph;
        try
        {
            graph = _graphProvider.Capture();
        }
        catch
        {
            return SetTerminal(WorldRoadNavigationFailure.GraphUnavailable, "The current graph snapshot is unavailable.");
        }

        if (!string.Equals(graph.GenerationId, _route.GraphGenerationId, StringComparison.Ordinal))
        {
            return TryReplan(
                currentPosition,
                graph,
                WorldRoadNavigationFailure.WorldChangeReplanFailed,
                "The road snapshot changed; a new route was selected.");
        }

        if (_activeDestination is { } active)
        {
            var tolerance = _activeIsFinal
                ? _options.FinalArrivalTolerance
                : _options.WaypointArrivalTolerance;
            if (Vector3.Distance(currentPosition, active) > tolerance)
            {
                return new WorldRoadNavigationUpdate(
                    WorldRoadNavigationStatus.AwaitingLocalTraversal,
                    WorldRoadNavigationFailure.None,
                    active,
                    "The active local segment has not been reported as reached.");
            }

            if (_activeIsFinal)
            {
                _terminal = new WorldRoadNavigationUpdate(
                    WorldRoadNavigationStatus.Completed,
                    WorldRoadNavigationFailure.None,
                    null,
                    "The final local approach reached its destination.");
                return _terminal;
            }

            _nextWaypoint++;
            _activeDestination = null;
        }

        while (_nextWaypoint < _route.Waypoints.Count &&
               Vector3.Distance(currentPosition, _route.Waypoints[_nextWaypoint]) <=
               _options.WaypointArrivalTolerance)
        {
            _nextWaypoint++;
        }

        if (_nextWaypoint < _route.Waypoints.Count)
        {
            var next = _route.Waypoints[_nextWaypoint];
            if (!IsFinite(next) || Vector3.Distance(currentPosition, next) > _options.MaximumLocalSegmentLength + 0.001f)
            {
                return SetTerminal(
                    WorldRoadNavigationFailure.InvalidLocalSegment,
                    "A planned road segment is non-finite or exceeds the local bound.");
            }

            var localStatus = SafeBeginRoadSegment(currentPosition, next);
            if (localStatus == LocalRouteTraversalStatus.Accepted)
            {
                _activeDestination = next;
                _activeIsFinal = false;
                return new WorldRoadNavigationUpdate(
                    WorldRoadNavigationStatus.RoadSegmentDispatched,
                    WorldRoadNavigationFailure.None,
                    next,
                    "A bounded road segment was accepted by local navigation.");
            }

            var failure = localStatus == LocalRouteTraversalStatus.Rejected
                ? WorldRoadNavigationFailure.LocalSegmentRejected
                : WorldRoadNavigationFailure.LocalNavigationUnavailable;
            return TryReplan(currentPosition, graph, failure, "Local navigation rejected the road waypoint; route replanned.");
        }

        if (Vector3.Distance(currentPosition, _destination.Position) <= _options.FinalArrivalTolerance)
        {
            _terminal = new WorldRoadNavigationUpdate(
                WorldRoadNavigationStatus.Completed,
                WorldRoadNavigationFailure.None,
                null,
                "The destination was already within final-approach tolerance.");
            return _terminal;
        }

        if (Vector3.Distance(currentPosition, _destination.Position) > _options.MaximumLocalSegmentLength + 0.001f)
        {
            return SetTerminal(
                WorldRoadNavigationFailure.InvalidLocalSegment,
                "The final off-road approach exceeds the local bound.");
        }

        var finalStatus = SafeBeginFinalApproach(currentPosition, _destination.Position);
        if (finalStatus != LocalRouteTraversalStatus.Accepted)
        {
            return SetTerminal(
                finalStatus == LocalRouteTraversalStatus.Rejected
                    ? WorldRoadNavigationFailure.FinalApproachRejected
                    : WorldRoadNavigationFailure.FinalApproachUnavailable,
                "The BAI/local final-approach seam rejected or could not evaluate the destination.");
        }

        _activeDestination = _destination.Position;
        _activeIsFinal = true;
        return new WorldRoadNavigationUpdate(
            WorldRoadNavigationStatus.FinalApproachDispatched,
            WorldRoadNavigationFailure.None,
            _destination.Position,
            "The final off-road approach was accepted by local navigation.");
    }

    private WorldRoadNavigationUpdate TryReplan(
        Vector3 currentPosition,
        WorldRoadGraph graph,
        WorldRoadNavigationFailure exhaustedFailure,
        string successDetail)
    {
        if (_replans >= _options.MaximumReplans)
            return SetTerminal(exhaustedFailure, "The bounded replan budget was exhausted.");

        _replans++;
        var next = _planner.Plan(
            graph,
            new RoadRouteEndpoint(_destination.WorldId, currentPosition),
            _destination);
        if (!next.IsSuccess)
            return SetTerminal(exhaustedFailure, $"Replan failed: {next.Status}.");

        _route = next;
        _nextWaypoint = 0;
        _activeDestination = null;
        _activeIsFinal = false;
        return new WorldRoadNavigationUpdate(
            WorldRoadNavigationStatus.Replanned,
            WorldRoadNavigationFailure.None,
            null,
            successDetail);
    }

    private LocalRouteTraversalStatus SafeBeginRoadSegment(Vector3 start, Vector3 destination)
    {
        try
        {
            return _localTraversal.BeginRoadSegment(start, destination);
        }
        catch
        {
            return LocalRouteTraversalStatus.Unavailable;
        }
    }

    private LocalRouteTraversalStatus SafeBeginFinalApproach(Vector3 start, Vector3 destination)
    {
        try
        {
            return _localTraversal.BeginFinalApproach(start, destination);
        }
        catch
        {
            return LocalRouteTraversalStatus.Unavailable;
        }
    }

    private WorldRoadNavigationUpdate SetTerminal(WorldRoadNavigationFailure failure, string detail)
    {
        _terminal = Failed(failure, detail);
        _activeDestination = null;
        return _terminal;
    }

    private static WorldRoadNavigationUpdate Failed(WorldRoadNavigationFailure failure, string detail) =>
        new(WorldRoadNavigationStatus.Failed, failure, null, detail);

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static void ValidateOptions(WorldRoadNavigationOptions options)
    {
        ValidatePositive(options.WaypointArrivalTolerance, nameof(options.WaypointArrivalTolerance));
        ValidatePositive(options.FinalArrivalTolerance, nameof(options.FinalArrivalTolerance));
        ValidatePositive(options.MaximumLocalSegmentLength, nameof(options.MaximumLocalSegmentLength));
        if (options.MaximumReplans < 0)
            throw new ArgumentOutOfRangeException(nameof(options.MaximumReplans));
    }

    private static void ValidatePositive(float value, string name)
    {
        if (!float.IsFinite(value) || value <= 0f)
            throw new ArgumentOutOfRangeException(name);
    }
}
