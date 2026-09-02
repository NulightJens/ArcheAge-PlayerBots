using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;

namespace AAEmu.Game.Bots.Navigation;

public sealed class WorldRoadGraphBuilder
{
    private readonly WorldRoadGraphBuildOptions _options;

    public WorldRoadGraphBuilder(WorldRoadGraphBuildOptions options = null)
    {
        _options = options ?? new WorldRoadGraphBuildOptions();
        ValidateOptions(_options);
    }

    public WorldRoadGraph Build(TransferRoadNetworkSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var issues = new List<RoadGraphIssue>();
        var candidates = snapshot.Roads
            .Select((road, index) => road == null
                ? RejectNullRoad(index, issues)
                : ValidateRoad(road, issues))
            .Where(candidate => candidate != null)
            .OrderBy(candidate => candidate.StableKey, StringComparer.Ordinal)
            .ToList();

        var unique = new List<RoadCandidate>();
        var geometryKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            if (!geometryKeys.Add(candidate.DuplicateKey))
            {
                issues.Add(new RoadGraphIssue(
                    RoadGraphIssueCode.DuplicatePolyline,
                    RoadGraphIssueSeverity.Warning,
                    candidate.StableKey,
                    "An equivalent road geometry and direction was already retained."));
                continue;
            }

            unique.Add(candidate);
        }

        var endpoints = CreateEndpoints(unique);
        var ambiguous = FindAmbiguousEndpoints(endpoints, issues);
        var clusters = ClusterEndpoints(endpoints, ambiguous, issues);
        var endpointToNode = new int[endpoints.Count];
        Array.Fill(endpointToNode, -1);

        var orderedClusters = clusters
            .OrderBy(cluster => cluster.Select(index => endpoints[index].StableKey).Min(StringComparer.Ordinal),
                StringComparer.Ordinal)
            .ToList();

        var nodeSeeds = new List<NodeSeed>();
        for (var nodeId = 0; nodeId < orderedClusters.Count; nodeId++)
        {
            var members = orderedClusters[nodeId].OrderBy(index => endpoints[index].StableKey, StringComparer.Ordinal).ToArray();
            foreach (var endpointIndex in members)
                endpointToNode[endpointIndex] = nodeId;

            var positions = members.Select(index => endpoints[index].Point.Position).ToArray();
            var position = new Vector3(
                (float)positions.Average(value => (double)value.X),
                (float)positions.Average(value => (double)value.Y),
                (float)positions.Average(value => (double)value.Z));
            var surfaceId = members.Select(index => endpoints[index].Point.SurfaceId).FirstOrDefault(id => id != 0);
            nodeSeeds.Add(new NodeSeed(nodeId, endpoints[members[0]].WorldId, position, surfaceId, members));
        }

        var edges = new List<RoadGraphEdge>(unique.Count);
        for (var index = 0; index < unique.Count; index++)
        {
            var edgeId = $"e{index:D6}";
            var startEndpoint = index * 2;
            var endEndpoint = startEndpoint + 1;
            edges.Add(new RoadGraphEdge(
                edgeId,
                endpointToNode[startEndpoint],
                endpointToNode[endEndpoint],
                unique[index].Source,
                unique[index].Weight));
        }

        var adjacency = CreateAdjacency(nodeSeeds, edges);
        var components = FindComponents(nodeSeeds.Count, adjacency);
        var nodes = nodeSeeds.Select(seed => new RoadGraphNode(
            seed.Id,
            seed.WorldId,
            seed.Position,
            seed.SurfaceId,
            components[seed.Id],
            seed.EndpointIndices.Select(endpointIndex =>
            {
                var endpoint = endpoints[endpointIndex];
                var edge = edges[endpoint.RoadIndex];
                return new RoadEndpointReference(edge.Id, endpoint.IsStart, edge.ZoneId, edge.PathName);
            }))).ToArray();

        var generationId = CreateGenerationId(snapshot.Revision, nodes, edges);
        return new WorldRoadGraph(
            snapshot.Revision,
            generationId,
            nodes,
            edges,
            issues.OrderBy(issue => issue.RoadKey, StringComparer.Ordinal).ThenBy(issue => issue.Code),
            adjacency,
            _options.EndpointSnapTolerance);
    }

    private RoadCandidate ValidateRoad(RoadPolylineSnapshot road, ICollection<RoadGraphIssue> issues)
    {
        var stableKey = StableRoadKey(road);
        if (road.Points.Count < 2)
        {
            Reject(RoadGraphIssueCode.DegeneratePolyline, stableKey, "A road requires at least two points.", issues);
            return null;
        }

        double weight = 0d;
        for (var index = 0; index < road.Points.Count; index++)
        {
            var point = road.Points[index];
            if (!IsFinite(point.Position))
            {
                Reject(RoadGraphIssueCode.NonFinitePoint, stableKey, $"Point {index} is non-finite.", issues);
                return null;
            }

            if (index == 0)
                continue;

            var previous = road.Points[index - 1].Position;
            var delta = point.Position - previous;
            var length = delta.Length();
            if (length < _options.MinimumSegmentLength)
            {
                Reject(RoadGraphIssueCode.DegeneratePolyline, stableKey, $"Segment {index - 1} has zero or negligible length.", issues);
                return null;
            }

            if (length > _options.MaximumPointGap)
            {
                Reject(RoadGraphIssueCode.ExcessivePointGap, stableKey, $"Segment {index - 1} exceeds the point-gap limit.", issues);
                return null;
            }

            var vertical = MathF.Abs(delta.Z);
            if (vertical > _options.MaximumVerticalStep)
            {
                Reject(RoadGraphIssueCode.ExcessiveVerticalStep, stableKey, $"Segment {index - 1} exceeds the vertical-step limit.", issues);
                return null;
            }

            var planar = MathF.Sqrt(delta.X * delta.X + delta.Y * delta.Y);
            if (vertical / MathF.Max(planar, _options.MinimumSegmentLength) > _options.MaximumSlope)
            {
                Reject(RoadGraphIssueCode.ExcessiveSlope, stableKey, $"Segment {index - 1} exceeds the slope limit.", issues);
                return null;
            }

            weight += length;
        }

        var duplicateKey = DuplicateRoadKey(road);
        return new RoadCandidate(road, stableKey, duplicateKey, weight);
    }

    private static RoadCandidate RejectNullRoad(int index, ICollection<RoadGraphIssue> issues)
    {
        issues.Add(new RoadGraphIssue(
            RoadGraphIssueCode.InvalidPolyline,
            RoadGraphIssueSeverity.Error,
            $"input:{index:D6}",
            "The road snapshot entry is null."));
        return null;
    }

    private static List<Endpoint> CreateEndpoints(IReadOnlyList<RoadCandidate> roads)
    {
        var endpoints = new List<Endpoint>(roads.Count * 2);
        for (var index = 0; index < roads.Count; index++)
        {
            var road = roads[index];
            var points = road.Source.Points;
            endpoints.Add(new Endpoint(
                index,
                true,
                road.Source.WorldId,
                points[0],
                Vector3.Normalize(points[1].Position - points[0].Position),
                road.StableKey + ":0"));
            endpoints.Add(new Endpoint(
                index,
                false,
                road.Source.WorldId,
                points[^1],
                Vector3.Normalize(points[^1].Position - points[^2].Position),
                road.StableKey + ":1"));
        }

        return endpoints;
    }

    private HashSet<int> FindAmbiguousEndpoints(
        IReadOnlyList<Endpoint> endpoints,
        ICollection<RoadGraphIssue> issues)
    {
        var ambiguous = new HashSet<int>();
        for (var index = 0; index < endpoints.Count; index++)
        {
            var matches = Enumerable.Range(0, endpoints.Count)
                .Where(other => other != index && CanSnap(endpoints[index], endpoints[other]))
                .Select(other => new
                {
                    Index = other,
                    Distance = Vector3.Distance(endpoints[index].Point.Position, endpoints[other].Point.Position)
                })
                .OrderBy(match => match.Distance)
                .ThenBy(match => endpoints[match.Index].StableKey, StringComparer.Ordinal)
                .ToArray();

            for (var left = 0; left < matches.Length; left++)
            {
                for (var right = left + 1; right < matches.Length; right++)
                {
                    if (MathF.Abs(matches[left].Distance - matches[right].Distance) > _options.EndpointAmbiguityTolerance)
                        continue;

                    var leftEndpoint = endpoints[matches[left].Index];
                    var rightEndpoint = endpoints[matches[right].Index];
                    if (Vector3.Distance(leftEndpoint.Point.Position, rightEndpoint.Point.Position) <=
                        _options.ExactEndpointTolerance)
                    {
                        continue;
                    }

                    if (MathF.Abs(Vector3.Dot(leftEndpoint.Tangent, rightEndpoint.Tangent)) <
                        _options.NearParallelDotThreshold)
                    {
                        continue;
                    }

                    ambiguous.Add(index);
                    ambiguous.Add(matches[left].Index);
                    ambiguous.Add(matches[right].Index);
                }
            }
        }

        foreach (var endpointIndex in ambiguous.OrderBy(value => endpoints[value].StableKey, StringComparer.Ordinal))
        {
            issues.Add(new RoadGraphIssue(
                RoadGraphIssueCode.AmbiguousEndpoint,
                RoadGraphIssueSeverity.Error,
                endpoints[endpointIndex].StableKey,
                "Endpoint has equally near, non-coincident, near-parallel snap candidates and was left disconnected."));
        }

        return ambiguous;
    }

    private List<int[]> ClusterEndpoints(
        IReadOnlyList<Endpoint> endpoints,
        HashSet<int> ambiguous,
        ICollection<RoadGraphIssue> issues)
    {
        var union = new UnionFind(endpoints.Count);
        for (var left = 0; left < endpoints.Count; left++)
        {
            if (ambiguous.Contains(left))
                continue;

            for (var right = left + 1; right < endpoints.Count; right++)
            {
                if (!ambiguous.Contains(right) && CanSnap(endpoints[left], endpoints[right]))
                    union.Join(left, right);
            }
        }

        var provisional = Enumerable.Range(0, endpoints.Count)
            .Where(index => !ambiguous.Contains(index))
            .GroupBy(union.Find)
            .Select(group => group.ToArray())
            .ToArray();
        foreach (var group in provisional)
        {
            if (AllPairsCompatible(group, endpoints))
                continue;

            foreach (var endpointIndex in group)
            {
                ambiguous.Add(endpointIndex);
                issues.Add(new RoadGraphIssue(
                    RoadGraphIssueCode.AmbiguousEndpoint,
                    RoadGraphIssueSeverity.Error,
                    endpoints[endpointIndex].StableKey,
                    "Transitive endpoint snapping exceeded the configured compatibility bounds and was rejected."));
            }
        }

        union = new UnionFind(endpoints.Count);
        for (var left = 0; left < endpoints.Count; left++)
        {
            if (ambiguous.Contains(left))
                continue;
            for (var right = left + 1; right < endpoints.Count; right++)
            {
                if (!ambiguous.Contains(right) && CanSnap(endpoints[left], endpoints[right]))
                    union.Join(left, right);
            }
        }

        return Enumerable.Range(0, endpoints.Count)
            .GroupBy(index => ambiguous.Contains(index) ? -(index + 1) : union.Find(index))
            .Select(group => group.ToArray())
            .ToList();
    }

    private bool AllPairsCompatible(IReadOnlyList<int> group, IReadOnlyList<Endpoint> endpoints)
    {
        for (var left = 0; left < group.Count; left++)
        for (var right = left + 1; right < group.Count; right++)
        {
            if (!CanSnap(endpoints[group[left]], endpoints[group[right]]))
                return false;
        }

        return true;
    }

    private bool CanSnap(Endpoint left, Endpoint right)
    {
        if (left.WorldId != right.WorldId)
            return false;
        if (left.RoadIndex == right.RoadIndex &&
            Vector3.Distance(left.Point.Position, right.Point.Position) > _options.ExactEndpointTolerance)
        {
            return false;
        }
        if (left.Point.SurfaceId != 0 && right.Point.SurfaceId != 0 &&
            left.Point.SurfaceId != right.Point.SurfaceId)
        {
            return false;
        }

        var delta = left.Point.Position - right.Point.Position;
        var planar = MathF.Sqrt(delta.X * delta.X + delta.Y * delta.Y);
        return planar <= _options.EndpointSnapTolerance &&
               MathF.Abs(delta.Z) <= _options.EndpointVerticalTolerance;
    }

    private static IReadOnlyList<RoadGraphArc>[] CreateAdjacency(
        IReadOnlyList<NodeSeed> nodes,
        IReadOnlyList<RoadGraphEdge> edges)
    {
        var adjacency = Enumerable.Range(0, nodes.Count).Select(_ => new List<RoadGraphArc>()).ToArray();
        for (var edgeIndex = 0; edgeIndex < edges.Count; edgeIndex++)
        {
            var edge = edges[edgeIndex];
            var startConnector = Vector3.Distance(nodes[edge.StartNodeId].Position, edge.Points[0].Position);
            var endConnector = Vector3.Distance(nodes[edge.EndNodeId].Position, edge.Points[^1].Position);
            var cost = edge.Weight + startConnector + endConnector;
            if (edge.Direction is RoadTravelDirection.Bidirectional or RoadTravelDirection.ForwardOnly)
                adjacency[edge.StartNodeId].Add(new RoadGraphArc(edge.StartNodeId, edge.EndNodeId, edgeIndex, true, cost));
            if (edge.Direction is RoadTravelDirection.Bidirectional or RoadTravelDirection.ReverseOnly)
                adjacency[edge.EndNodeId].Add(new RoadGraphArc(edge.EndNodeId, edge.StartNodeId, edgeIndex, false, cost));
        }

        return adjacency.Select(arcs => (IReadOnlyList<RoadGraphArc>)arcs
            .OrderBy(arc => edges[arc.EdgeIndex].Id, StringComparer.Ordinal)
            .ThenBy(arc => arc.Forward ? 0 : 1)
            .ToArray()).ToArray();
    }

    private static int[] FindComponents(int nodeCount, IReadOnlyList<RoadGraphArc>[] adjacency)
    {
        var undirected = Enumerable.Range(0, nodeCount).Select(_ => new SortedSet<int>()).ToArray();
        foreach (var arcs in adjacency)
        foreach (var arc in arcs)
        {
            undirected[arc.FromNodeId].Add(arc.ToNodeId);
            undirected[arc.ToNodeId].Add(arc.FromNodeId);
        }

        var components = Enumerable.Repeat(-1, nodeCount).ToArray();
        var component = 0;
        for (var start = 0; start < nodeCount; start++)
        {
            if (components[start] >= 0)
                continue;

            var queue = new Queue<int>();
            queue.Enqueue(start);
            components[start] = component;
            while (queue.Count > 0)
            {
                var node = queue.Dequeue();
                foreach (var neighbor in undirected[node])
                {
                    if (components[neighbor] >= 0)
                        continue;
                    components[neighbor] = component;
                    queue.Enqueue(neighbor);
                }
            }

            component++;
        }

        return components;
    }

    private static string CreateGenerationId(
        long revision,
        IEnumerable<RoadGraphNode> nodes,
        IEnumerable<RoadGraphEdge> edges)
    {
        var canonical = new StringBuilder();
        foreach (var node in nodes)
        {
            canonical.Append(node.Id).Append(':').Append(node.WorldId).Append(':')
                .Append(FloatBits(node.Position.X)).Append(':')
                .Append(FloatBits(node.Position.Y)).Append(':')
                .Append(FloatBits(node.Position.Z)).Append(':')
                .Append(node.ComponentId).Append(';');
        }

        foreach (var edge in edges)
        {
            canonical.Append(edge.Id).Append(':').Append(edge.StartNodeId).Append(':').Append(edge.EndNodeId)
                .Append(':').Append(StableRoadKey(edge)).Append(';');
        }

        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(canonical.ToString()));
        return revision.ToString(CultureInfo.InvariantCulture) + ":" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string StableRoadKey(RoadPolylineSnapshot road)
    {
        return string.Join(":",
            road.WorldId.ToString(CultureInfo.InvariantCulture),
            road.ZoneId.ToString(CultureInfo.InvariantCulture),
            Escape(road.PathName),
            road.PathType.ToString(CultureInfo.InvariantCulture),
            road.CellX.ToString(CultureInfo.InvariantCulture),
            road.CellY.ToString(CultureInfo.InvariantCulture),
            ((int)road.Direction).ToString(CultureInfo.InvariantCulture),
            GeometryKey(road.Points));
    }

    private static string StableRoadKey(RoadGraphEdge edge)
    {
        return string.Join(":",
            edge.WorldId.ToString(CultureInfo.InvariantCulture),
            edge.ZoneId.ToString(CultureInfo.InvariantCulture),
            Escape(edge.PathName),
            edge.PathType.ToString(CultureInfo.InvariantCulture),
            edge.CellX.ToString(CultureInfo.InvariantCulture),
            edge.CellY.ToString(CultureInfo.InvariantCulture),
            ((int)edge.Direction).ToString(CultureInfo.InvariantCulture),
            GeometryKey(edge.Points));
    }

    private static string DuplicateRoadKey(RoadPolylineSnapshot road)
    {
        var forward = GeometryKey(road.Points);
        var reverse = GeometryKey(road.Points.Reverse());
        var geometry = road.Direction == RoadTravelDirection.Bidirectional &&
                       StringComparer.Ordinal.Compare(reverse, forward) < 0
            ? reverse
            : forward;
        return road.WorldId.ToString(CultureInfo.InvariantCulture) + ":" + (int)road.Direction + ":" + geometry;
    }

    private static string GeometryKey(IEnumerable<RoadPoint> points)
    {
        return string.Join("|", points.Select(point =>
            $"{FloatBits(point.X)},{FloatBits(point.Y)},{FloatBits(point.Z)},{point.SurfaceId}"));
    }

    private static string FloatBits(float value) => BitConverter.SingleToInt32Bits(value).ToString("x8", CultureInfo.InvariantCulture);

    private static string Escape(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

    private static void Reject(
        RoadGraphIssueCode code,
        string roadKey,
        string detail,
        ICollection<RoadGraphIssue> issues)
    {
        issues.Add(new RoadGraphIssue(code, RoadGraphIssueSeverity.Error, roadKey, detail));
    }

    private static void ValidateOptions(WorldRoadGraphBuildOptions options)
    {
        ValidatePositive(options.EndpointSnapTolerance, nameof(options.EndpointSnapTolerance));
        ValidatePositive(options.EndpointVerticalTolerance, nameof(options.EndpointVerticalTolerance));
        ValidateNonNegative(options.EndpointAmbiguityTolerance, nameof(options.EndpointAmbiguityTolerance));
        ValidateNonNegative(options.ExactEndpointTolerance, nameof(options.ExactEndpointTolerance));
        ValidatePositive(options.MinimumSegmentLength, nameof(options.MinimumSegmentLength));
        ValidatePositive(options.MaximumPointGap, nameof(options.MaximumPointGap));
        ValidatePositive(options.MaximumVerticalStep, nameof(options.MaximumVerticalStep));
        ValidatePositive(options.MaximumSlope, nameof(options.MaximumSlope));
        if (!float.IsFinite(options.NearParallelDotThreshold) ||
            options.NearParallelDotThreshold < 0f || options.NearParallelDotThreshold > 1f)
        {
            throw new ArgumentOutOfRangeException(nameof(options.NearParallelDotThreshold));
        }
    }

    private static void ValidatePositive(float value, string name)
    {
        if (!float.IsFinite(value) || value <= 0f)
            throw new ArgumentOutOfRangeException(name);
    }

    private static void ValidateNonNegative(float value, string name)
    {
        if (!float.IsFinite(value) || value < 0f)
            throw new ArgumentOutOfRangeException(name);
    }

    private sealed record RoadCandidate(
        RoadPolylineSnapshot Source,
        string StableKey,
        string DuplicateKey,
        double Weight);

    private sealed record Endpoint(
        int RoadIndex,
        bool IsStart,
        uint WorldId,
        RoadPoint Point,
        Vector3 Tangent,
        string StableKey);

    private sealed record NodeSeed(
        int Id,
        uint WorldId,
        Vector3 Position,
        int SurfaceId,
        IReadOnlyList<int> EndpointIndices);

    private sealed class UnionFind
    {
        private readonly int[] _parent;

        public UnionFind(int count)
        {
            _parent = Enumerable.Range(0, count).ToArray();
        }

        public int Find(int value)
        {
            while (_parent[value] != value)
            {
                _parent[value] = _parent[_parent[value]];
                value = _parent[value];
            }

            return value;
        }

        public void Join(int left, int right)
        {
            var leftRoot = Find(left);
            var rightRoot = Find(right);
            if (leftRoot == rightRoot)
                return;
            if (leftRoot < rightRoot)
                _parent[rightRoot] = leftRoot;
            else
                _parent[leftRoot] = rightRoot;
        }
    }
}
