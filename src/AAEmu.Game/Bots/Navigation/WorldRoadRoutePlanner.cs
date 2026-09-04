using System.Collections.ObjectModel;
using System.Globalization;
using System.Numerics;

namespace AAEmu.Game.Bots.Navigation;

public sealed class WorldRoadRoutePlannerOptions
{
    public float MaximumProjectionDistance { get; init; } = 20f;
    public float MaximumProjectionVerticalGap { get; init; } = 5f;
    public float MaximumLocalSegmentLength { get; init; } = 20f;
    public double CostTieTolerance { get; init; } = 0.00001d;
}

public readonly record struct RoadRouteEndpoint(uint WorldId, Vector3 Position, int SurfaceId = 0);

public sealed record RoadProjection(
    string EdgeId,
    int EdgeIndex,
    int SegmentIndex,
    float SegmentFraction,
    double AlongDistance,
    Vector3 Position,
    float Distance,
    uint WorldId,
    uint ZoneId,
    string PathName,
    int SurfaceId);

public sealed record RoadRouteStep(
    string EdgeId,
    uint ZoneId,
    string PathName,
    int PathType,
    bool Forward,
    double FromDistance,
    double ToDistance,
    double Weight);

public enum RoadRouteStatus
{
    Success,
    InvalidRequest,
    GraphUnavailable,
    StartProjectionUnavailable,
    DestinationProjectionUnavailable,
    Disconnected,
    NoDirectedRoute
}

public sealed class RoadRouteResult
{
    private RoadRouteResult(
        RoadRouteStatus status,
        string detail,
        string graphGenerationId,
        long sourceRevision,
        RoadProjection startProjection,
        RoadProjection destinationProjection,
        IEnumerable<Vector3> waypoints,
        IEnumerable<RoadRouteStep> steps,
        double totalWeight,
        int componentId)
    {
        Status = status;
        Detail = detail;
        GraphGenerationId = graphGenerationId;
        SourceRevision = sourceRevision;
        StartProjection = startProjection;
        DestinationProjection = destinationProjection;
        Waypoints = new ReadOnlyCollection<Vector3>(waypoints.ToArray());
        Steps = new ReadOnlyCollection<RoadRouteStep>(steps.ToArray());
        TotalWeight = totalWeight;
        ComponentId = componentId;
    }

    public RoadRouteStatus Status { get; }
    public string Detail { get; }
    public string GraphGenerationId { get; }
    public long SourceRevision { get; }
    public RoadProjection StartProjection { get; }
    public RoadProjection DestinationProjection { get; }
    public IReadOnlyList<Vector3> Waypoints { get; }
    public IReadOnlyList<RoadRouteStep> Steps { get; }
    public double TotalWeight { get; }
    public int ComponentId { get; }
    public bool IsSuccess => Status == RoadRouteStatus.Success;

    internal static RoadRouteResult Success(
        WorldRoadGraph graph,
        RoadProjection startProjection,
        RoadProjection destinationProjection,
        IEnumerable<Vector3> waypoints,
        IEnumerable<RoadRouteStep> steps,
        double totalWeight,
        int componentId)
    {
        return new RoadRouteResult(
            RoadRouteStatus.Success,
            "Route selected.",
            graph.GenerationId,
            graph.SourceRevision,
            startProjection,
            destinationProjection,
            waypoints,
            steps,
            totalWeight,
            componentId);
    }

    internal static RoadRouteResult Failure(WorldRoadGraph graph, RoadRouteStatus status, string detail)
    {
        return new RoadRouteResult(
            status,
            detail,
            graph?.GenerationId ?? string.Empty,
            graph?.SourceRevision ?? 0,
            null,
            null,
            Array.Empty<Vector3>(),
            Array.Empty<RoadRouteStep>(),
            0d,
            -1);
    }
}

public sealed class WorldRoadRoutePlanner
{
    private readonly WorldRoadRoutePlannerOptions _options;

    public WorldRoadRoutePlanner(WorldRoadRoutePlannerOptions options = null)
    {
        _options = options ?? new WorldRoadRoutePlannerOptions();
        ValidateOptions(_options);
    }

    public RoadProjection ProjectNearest(WorldRoadGraph graph, RoadRouteEndpoint endpoint)
    {
        if (graph == null || !IsFinite(endpoint.Position))
            return null;

        ProjectionCandidate best = null;
        for (var edgeIndex = 0; edgeIndex < graph.Edges.Count; edgeIndex++)
        {
            var edge = graph.Edges[edgeIndex];
            if (edge.WorldId != endpoint.WorldId)
                continue;

            var cumulative = 0d;
            for (var segmentIndex = 0; segmentIndex < edge.Points.Count - 1; segmentIndex++)
            {
                var start = edge.Points[segmentIndex];
                var end = edge.Points[segmentIndex + 1];
                var delta = end.Position - start.Position;
                var lengthSquared = delta.LengthSquared();
                var fraction = lengthSquared <= 0f
                    ? 0f
                    : Math.Clamp(Vector3.Dot(endpoint.Position - start.Position, delta) / lengthSquared, 0f, 1f);
                var position = start.Position + delta * fraction;
                var distance = Vector3.Distance(endpoint.Position, position);
                var verticalGap = MathF.Abs(endpoint.Position.Z - position.Z);
                var surfaceId = start.SurfaceId == end.SurfaceId ? start.SurfaceId : 0;
                var surfaceCompatible = endpoint.SurfaceId == 0 || surfaceId == 0 || endpoint.SurfaceId == surfaceId;
                if (distance <= _options.MaximumProjectionDistance &&
                    verticalGap <= _options.MaximumProjectionVerticalGap && surfaceCompatible)
                {
                    var segmentLength = Math.Sqrt(lengthSquared);
                    var candidate = new ProjectionCandidate(
                        edgeIndex,
                        segmentIndex,
                        fraction,
                        cumulative + segmentLength * fraction,
                        position,
                        distance,
                        surfaceId);
                    if (best == null || CompareProjection(candidate, best, graph) < 0)
                        best = candidate;
                }

                cumulative += Math.Sqrt(lengthSquared);
            }
        }

        if (best == null)
            return null;

        var selected = graph.Edges[best.EdgeIndex];
        return new RoadProjection(
            selected.Id,
            best.EdgeIndex,
            best.SegmentIndex,
            best.SegmentFraction,
            best.AlongDistance,
            best.Position,
            best.Distance,
            selected.WorldId,
            selected.ZoneId,
            selected.PathName,
            best.SurfaceId);
    }

    public RoadRouteResult Plan(
        WorldRoadGraph graph,
        RoadRouteEndpoint start,
        RoadRouteEndpoint destination)
    {
        if (graph == null || graph.Edges.Count == 0)
            return RoadRouteResult.Failure(graph, RoadRouteStatus.GraphUnavailable, "The graph has no usable roads.");
        if (!IsFinite(start.Position) || !IsFinite(destination.Position) || start.WorldId != destination.WorldId)
            return RoadRouteResult.Failure(graph, RoadRouteStatus.InvalidRequest, "Endpoints must be finite and in one world.");

        var startProjection = ProjectNearest(graph, start);
        if (startProjection == null)
        {
            return RoadRouteResult.Failure(
                graph,
                RoadRouteStatus.StartProjectionUnavailable,
                "No safe road projection exists for the start endpoint.");
        }

        var destinationProjection = ProjectNearest(graph, destination);
        if (destinationProjection == null)
        {
            return RoadRouteResult.Failure(
                graph,
                RoadRouteStatus.DestinationProjectionUnavailable,
                "No safe road projection exists for the destination endpoint.");
        }

        var candidates = new List<RouteCandidate>();
        AddDirectCandidate(graph, startProjection, destinationProjection, candidates);

        var starts = CreateStartConnections(graph, startProjection);
        var destinations = CreateDestinationConnections(graph, destinationProjection);
        var hasSharedComponent = false;
        foreach (var startConnection in starts)
        foreach (var destinationConnection in destinations)
        {
            var startComponent = graph.Nodes[startConnection.NodeId].ComponentId;
            if (startComponent != graph.Nodes[destinationConnection.NodeId].ComponentId)
                continue;

            hasSharedComponent = true;
            var middle = FindPath(graph, startConnection.NodeId, destinationConnection.NodeId);
            if (middle == null)
                continue;

            var waypoints = new List<Vector3>();
            Append(waypoints, startConnection.Waypoints);
            Append(waypoints, middle.Waypoints);
            Append(waypoints, destinationConnection.Waypoints);

            var steps = startConnection.Steps.Concat(middle.Steps).Concat(destinationConnection.Steps).ToArray();
            var signature = startConnection.Signature + ">" + middle.Signature + ">" + destinationConnection.Signature;
            candidates.Add(new RouteCandidate(
                startConnection.Cost + middle.Cost + destinationConnection.Cost,
                signature,
                waypoints,
                steps,
                startComponent));
        }

        if (candidates.Count == 0)
        {
            return RoadRouteResult.Failure(
                graph,
                hasSharedComponent ? RoadRouteStatus.NoDirectedRoute : RoadRouteStatus.Disconnected,
                hasSharedComponent
                    ? "The weak component is connected, but one-way constraints reject every route."
                    : "The projected roads are in disconnected components.");
        }

        var selectedRoute = candidates.Aggregate(SelectBetterRoute);
        var bounded = BoundSegments(selectedRoute.Waypoints, _options.MaximumLocalSegmentLength);
        return RoadRouteResult.Success(
            graph,
            startProjection,
            destinationProjection,
            bounded,
            selectedRoute.Steps,
            selectedRoute.Cost,
            selectedRoute.ComponentId);
    }

    private void AddDirectCandidate(
        WorldRoadGraph graph,
        RoadProjection start,
        RoadProjection destination,
        ICollection<RouteCandidate> candidates)
    {
        if (start.EdgeIndex != destination.EdgeIndex)
            return;

        var edge = graph.Edges[start.EdgeIndex];
        var forward = destination.AlongDistance >= start.AlongDistance;
        if (forward && edge.Direction == RoadTravelDirection.ReverseOnly)
            return;
        if (!forward && edge.Direction == RoadTravelDirection.ForwardOnly)
            return;

        var points = Slice(edge, start.AlongDistance, destination.AlongDistance);
        var weight = Math.Abs(destination.AlongDistance - start.AlongDistance);
        candidates.Add(new RouteCandidate(
            weight,
            edge.Id + (forward ? ":F:direct" : ":R:direct"),
            points,
            new[] { CreateStep(edge, forward, start.AlongDistance, destination.AlongDistance) },
            graph.Nodes[edge.StartNodeId].ComponentId));
    }

    private static IReadOnlyList<RouteConnection> CreateStartConnections(WorldRoadGraph graph, RoadProjection projection)
    {
        var edge = graph.Edges[projection.EdgeIndex];
        var result = new List<RouteConnection>();
        if (edge.Direction is RoadTravelDirection.Bidirectional or RoadTravelDirection.ForwardOnly)
        {
            result.Add(CreateConnection(
                graph,
                edge,
                projection.AlongDistance,
                edge.Weight,
                edge.EndNodeId,
                true,
                startConnection: true));
        }

        if (edge.Direction is RoadTravelDirection.Bidirectional or RoadTravelDirection.ReverseOnly)
        {
            result.Add(CreateConnection(
                graph,
                edge,
                projection.AlongDistance,
                0d,
                edge.StartNodeId,
                false,
                startConnection: true));
        }

        return result;
    }

    private static IReadOnlyList<RouteConnection> CreateDestinationConnections(
        WorldRoadGraph graph,
        RoadProjection projection)
    {
        var edge = graph.Edges[projection.EdgeIndex];
        var result = new List<RouteConnection>();
        if (edge.Direction is RoadTravelDirection.Bidirectional or RoadTravelDirection.ForwardOnly)
        {
            result.Add(CreateConnection(
                graph,
                edge,
                0d,
                projection.AlongDistance,
                edge.StartNodeId,
                true,
                startConnection: false));
        }

        if (edge.Direction is RoadTravelDirection.Bidirectional or RoadTravelDirection.ReverseOnly)
        {
            result.Add(CreateConnection(
                graph,
                edge,
                edge.Weight,
                projection.AlongDistance,
                edge.EndNodeId,
                false,
                startConnection: false));
        }

        return result;
    }

    private static RouteConnection CreateConnection(
        WorldRoadGraph graph,
        RoadGraphEdge edge,
        double fromDistance,
        double toDistance,
        int nodeId,
        bool forward,
        bool startConnection)
    {
        var roadPoints = Slice(edge, fromDistance, toDistance);
        var endpoint = startConnection
            ? forward ? edge.Points[^1].Position : edge.Points[0].Position
            : forward ? edge.Points[0].Position : edge.Points[^1].Position;
        var nodePosition = graph.Nodes[nodeId].Position;
        var connectorCost = Vector3.Distance(endpoint, nodePosition);
        var waypoints = new List<Vector3>();
        if (startConnection)
        {
            Append(waypoints, roadPoints);
            Append(waypoints, new[] { nodePosition });
        }
        else
        {
            Append(waypoints, new[] { nodePosition, endpoint });
            Append(waypoints, roadPoints);
        }

        var signature = edge.Id + (forward ? ":F:" : ":R:") + (startConnection ? "exit" : "entry");
        return new RouteConnection(
            nodeId,
            Math.Abs(toDistance - fromDistance) + connectorCost,
            signature,
            waypoints,
            new[] { CreateStep(edge, forward, fromDistance, toDistance) });
    }

    private PathCandidate FindPath(WorldRoadGraph graph, int startNodeId, int destinationNodeId)
    {
        if (startNodeId == destinationNodeId)
        {
            return new PathCandidate(
                0d,
                "same-node",
                new[] { graph.Nodes[startNodeId].Position },
                Array.Empty<RoadRouteStep>());
        }

        var bestCost = Enumerable.Repeat(double.PositiveInfinity, graph.Nodes.Count).ToArray();
        var bestSignature = Enumerable.Repeat<string>(null, graph.Nodes.Count).ToArray();
        var previous = new RoadGraphArc?[graph.Nodes.Count];
        var open = new SortedSet<OpenState>(OpenStateComparer.Instance);
        long sequence = 0;
        bestCost[startNodeId] = 0d;
        bestSignature[startNodeId] = string.Empty;
        open.Add(new OpenState(
            Heuristic(graph, startNodeId, destinationNodeId),
            0d,
            string.Empty,
            startNodeId,
            sequence++));

        while (open.Count > 0)
        {
            var current = open.Min;
            open.Remove(current);
            if (current.Cost > bestCost[current.NodeId] + _options.CostTieTolerance ||
                !string.Equals(current.Signature, bestSignature[current.NodeId], StringComparison.Ordinal))
            {
                continue;
            }

            if (current.NodeId == destinationNodeId)
                break;

            foreach (var arc in graph.ArcsFrom(current.NodeId))
            {
                var edge = graph.Edges[arc.EdgeIndex];
                var signature = current.Signature + ">" + edge.Id + (arc.Forward ? ":F" : ":R");
                var cost = current.Cost + arc.Cost;
                var costComparison = cost.CompareTo(bestCost[arc.ToNodeId]);
                var improves = costComparison < 0 ||
                               Math.Abs(cost - bestCost[arc.ToNodeId]) <= _options.CostTieTolerance &&
                               StringComparer.Ordinal.Compare(signature, bestSignature[arc.ToNodeId]) < 0;
                if (!improves)
                    continue;

                bestCost[arc.ToNodeId] = cost;
                bestSignature[arc.ToNodeId] = signature;
                previous[arc.ToNodeId] = arc;
                open.Add(new OpenState(
                    cost + Heuristic(graph, arc.ToNodeId, destinationNodeId),
                    cost,
                    signature,
                    arc.ToNodeId,
                    sequence++));
            }
        }

        if (!double.IsFinite(bestCost[destinationNodeId]))
            return null;

        var reversed = new List<RoadGraphArc>();
        var nodeId = destinationNodeId;
        while (nodeId != startNodeId)
        {
            if (previous[nodeId] is not { } arc)
                return null;
            reversed.Add(arc);
            nodeId = arc.FromNodeId;
        }

        reversed.Reverse();
        var waypoints = new List<Vector3> { graph.Nodes[startNodeId].Position };
        var steps = new List<RoadRouteStep>();
        foreach (var arc in reversed)
        {
            var edge = graph.Edges[arc.EdgeIndex];
            var roadStart = arc.Forward ? edge.Points[0].Position : edge.Points[^1].Position;
            Append(waypoints, new[] { roadStart });
            Append(waypoints, arc.Forward ? edge.Points.Select(point => point.Position) : edge.Points.Reverse().Select(point => point.Position));
            Append(waypoints, new[] { graph.Nodes[arc.ToNodeId].Position });
            steps.Add(CreateStep(
                edge,
                arc.Forward,
                arc.Forward ? 0d : edge.Weight,
                arc.Forward ? edge.Weight : 0d));
        }

        return new PathCandidate(bestCost[destinationNodeId], bestSignature[destinationNodeId], waypoints, steps);
    }

    private static double Heuristic(WorldRoadGraph graph, int nodeId, int destinationNodeId)
    {
        return Vector3.Distance(graph.Nodes[nodeId].Position, graph.Nodes[destinationNodeId].Position);
    }

    private static IReadOnlyList<Vector3> Slice(RoadGraphEdge edge, double fromDistance, double toDistance)
    {
        var cumulative = CumulativeDistances(edge);
        var from = PointAtDistance(edge, cumulative, fromDistance);
        var to = PointAtDistance(edge, cumulative, toDistance);
        var forward = toDistance >= fromDistance;
        var points = new List<Vector3> { from };
        if (forward)
        {
            for (var index = 1; index < edge.Points.Count - 1; index++)
            {
                if (cumulative[index] > fromDistance && cumulative[index] < toDistance)
                    points.Add(edge.Points[index].Position);
            }
        }
        else
        {
            for (var index = edge.Points.Count - 2; index > 0; index--)
            {
                if (cumulative[index] < fromDistance && cumulative[index] > toDistance)
                    points.Add(edge.Points[index].Position);
            }
        }

        Append(points, new[] { to });
        return points;
    }

    private static double[] CumulativeDistances(RoadGraphEdge edge)
    {
        var result = new double[edge.Points.Count];
        for (var index = 1; index < edge.Points.Count; index++)
            result[index] = result[index - 1] + Vector3.Distance(edge.Points[index - 1].Position, edge.Points[index].Position);
        return result;
    }

    private static Vector3 PointAtDistance(RoadGraphEdge edge, IReadOnlyList<double> cumulative, double distance)
    {
        distance = Math.Clamp(distance, 0d, edge.Weight);
        for (var index = 0; index < cumulative.Count - 1; index++)
        {
            if (distance > cumulative[index + 1])
                continue;
            var segmentLength = cumulative[index + 1] - cumulative[index];
            var fraction = segmentLength <= 0d ? 0f : (float)((distance - cumulative[index]) / segmentLength);
            return Vector3.Lerp(edge.Points[index].Position, edge.Points[index + 1].Position, fraction);
        }

        return edge.Points[^1].Position;
    }

    private static RoadRouteStep CreateStep(
        RoadGraphEdge edge,
        bool forward,
        double fromDistance,
        double toDistance)
    {
        return new RoadRouteStep(
            edge.Id,
            edge.ZoneId,
            edge.PathName,
            edge.PathType,
            forward,
            fromDistance,
            toDistance,
            Math.Abs(toDistance - fromDistance));
    }

    private static IReadOnlyList<Vector3> BoundSegments(IReadOnlyList<Vector3> source, float maximumLength)
    {
        if (source.Count == 0)
            return Array.Empty<Vector3>();

        var result = new List<Vector3> { source[0] };
        for (var index = 1; index < source.Count; index++)
        {
            var start = result[^1];
            var end = source[index];
            var distance = Vector3.Distance(start, end);
            var divisions = Math.Max(1, (int)Math.Ceiling(distance / maximumLength));
            for (var part = 1; part <= divisions; part++)
                Append(result, new[] { Vector3.Lerp(start, end, part / (float)divisions) });
        }

        return result;
    }

    private static int CompareProjection(ProjectionCandidate left, ProjectionCandidate right, WorldRoadGraph graph)
    {
        var result = left.Distance.CompareTo(right.Distance);
        if (result != 0)
            return result;
        result = StringComparer.Ordinal.Compare(graph.Edges[left.EdgeIndex].Id, graph.Edges[right.EdgeIndex].Id);
        if (result != 0)
            return result;
        result = left.SegmentIndex.CompareTo(right.SegmentIndex);
        return result != 0 ? result : left.SegmentFraction.CompareTo(right.SegmentFraction);
    }

    private RouteCandidate SelectBetterRoute(RouteCandidate left, RouteCandidate right)
    {
        if (left.Cost < right.Cost - _options.CostTieTolerance)
            return left;
        if (right.Cost < left.Cost - _options.CostTieTolerance)
            return right;
        return StringComparer.Ordinal.Compare(left.Signature, right.Signature) <= 0 ? left : right;
    }

    private static void Append(ICollection<Vector3> destination, IEnumerable<Vector3> source)
    {
        foreach (var point in source)
        {
            if (destination.Count > 0 && destination.Last() == point)
                continue;
            destination.Add(point);
        }
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static void ValidateOptions(WorldRoadRoutePlannerOptions options)
    {
        ValidatePositive(options.MaximumProjectionDistance, nameof(options.MaximumProjectionDistance));
        ValidatePositive(options.MaximumProjectionVerticalGap, nameof(options.MaximumProjectionVerticalGap));
        ValidatePositive(options.MaximumLocalSegmentLength, nameof(options.MaximumLocalSegmentLength));
        if (!double.IsFinite(options.CostTieTolerance) || options.CostTieTolerance < 0d)
            throw new ArgumentOutOfRangeException(nameof(options.CostTieTolerance));
        if (options.MaximumProjectionDistance > options.MaximumLocalSegmentLength)
        {
            throw new ArgumentException(
                "Projection distance cannot exceed the bounded local-segment length.",
                nameof(options));
        }
    }

    private static void ValidatePositive(float value, string name)
    {
        if (!float.IsFinite(value) || value <= 0f)
            throw new ArgumentOutOfRangeException(name);
    }

    private sealed record ProjectionCandidate(
        int EdgeIndex,
        int SegmentIndex,
        float SegmentFraction,
        double AlongDistance,
        Vector3 Position,
        float Distance,
        int SurfaceId);

    private sealed record RouteConnection(
        int NodeId,
        double Cost,
        string Signature,
        IReadOnlyList<Vector3> Waypoints,
        IReadOnlyList<RoadRouteStep> Steps);

    private sealed record PathCandidate(
        double Cost,
        string Signature,
        IReadOnlyList<Vector3> Waypoints,
        IReadOnlyList<RoadRouteStep> Steps);

    private sealed record RouteCandidate(
        double Cost,
        string Signature,
        IReadOnlyList<Vector3> Waypoints,
        IReadOnlyList<RoadRouteStep> Steps,
        int ComponentId);

    private sealed record OpenState(double Priority, double Cost, string Signature, int NodeId, long Sequence);

    private sealed class OpenStateComparer : IComparer<OpenState>
    {
        public static OpenStateComparer Instance { get; } = new();

        public int Compare(OpenState left, OpenState right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            var result = left.Priority.CompareTo(right.Priority);
            if (result != 0)
                return result;
            result = left.Cost.CompareTo(right.Cost);
            if (result != 0)
                return result;
            result = StringComparer.Ordinal.Compare(left.Signature, right.Signature);
            if (result != 0)
                return result;
            result = left.NodeId.CompareTo(right.NodeId);
            return result != 0 ? result : left.Sequence.CompareTo(right.Sequence);
        }
    }
}
