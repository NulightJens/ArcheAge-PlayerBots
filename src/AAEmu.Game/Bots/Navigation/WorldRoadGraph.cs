using System.Collections.ObjectModel;
using System.Numerics;

namespace AAEmu.Game.Bots.Navigation;

public sealed class WorldRoadGraphBuildOptions
{
    public float EndpointSnapTolerance { get; init; } = 2f;
    public float EndpointVerticalTolerance { get; init; } = 1.5f;
    public float EndpointAmbiguityTolerance { get; init; } = 0.05f;
    public float ExactEndpointTolerance { get; init; } = 0.01f;
    public float NearParallelDotThreshold { get; init; } = 0.98f;
    public float MinimumSegmentLength { get; init; } = 0.01f;
    public float MaximumPointGap { get; init; } = 100f;
    public float MaximumVerticalStep { get; init; } = 15f;
    public float MaximumSlope { get; init; } = 2f;
}

public enum RoadGraphIssueCode
{
    InvalidPolyline,
    NonFinitePoint,
    DegeneratePolyline,
    ExcessivePointGap,
    ExcessiveVerticalStep,
    ExcessiveSlope,
    DuplicatePolyline,
    AmbiguousEndpoint
}

public enum RoadGraphIssueSeverity
{
    Warning,
    Error
}

public sealed record RoadGraphIssue(
    RoadGraphIssueCode Code,
    RoadGraphIssueSeverity Severity,
    string RoadKey,
    string Detail);

public sealed record RoadEndpointReference(
    string EdgeId,
    bool IsStart,
    uint ZoneId,
    string PathName);

public sealed class RoadGraphNode
{
    internal RoadGraphNode(
        int id,
        uint worldId,
        Vector3 position,
        int surfaceId,
        int componentId,
        IEnumerable<RoadEndpointReference> endpoints)
    {
        Id = id;
        WorldId = worldId;
        Position = position;
        SurfaceId = surfaceId;
        ComponentId = componentId;
        Endpoints = new ReadOnlyCollection<RoadEndpointReference>(endpoints.ToArray());
    }

    public int Id { get; }
    public uint WorldId { get; }
    public Vector3 Position { get; }
    public int SurfaceId { get; }
    public int ComponentId { get; }
    public IReadOnlyList<RoadEndpointReference> Endpoints { get; }
}

public sealed class RoadGraphEdge
{
    internal RoadGraphEdge(
        string id,
        int startNodeId,
        int endNodeId,
        RoadPolylineSnapshot source,
        double weight)
    {
        Id = id;
        StartNodeId = startNodeId;
        EndNodeId = endNodeId;
        WorldId = source.WorldId;
        ZoneId = source.ZoneId;
        PathName = source.PathName;
        PathType = source.PathType;
        CellX = source.CellX;
        CellY = source.CellY;
        Direction = source.Direction;
        Points = source.Points;
        Weight = weight;
    }

    public string Id { get; }
    public int StartNodeId { get; }
    public int EndNodeId { get; }
    public uint WorldId { get; }
    public uint ZoneId { get; }
    public string PathName { get; }
    public int PathType { get; }
    public int CellX { get; }
    public int CellY { get; }
    public RoadTravelDirection Direction { get; }
    public IReadOnlyList<RoadPoint> Points { get; }
    public double Weight { get; }
}

internal readonly record struct RoadGraphArc(
    int FromNodeId,
    int ToNodeId,
    int EdgeIndex,
    bool Forward,
    double Cost);

public sealed class WorldRoadGraph
{
    private readonly IReadOnlyList<RoadGraphArc>[] _adjacency;

    internal WorldRoadGraph(
        long sourceRevision,
        string generationId,
        IEnumerable<RoadGraphNode> nodes,
        IEnumerable<RoadGraphEdge> edges,
        IEnumerable<RoadGraphIssue> issues,
        IReadOnlyList<RoadGraphArc>[] adjacency,
        float endpointSnapTolerance)
    {
        SourceRevision = sourceRevision;
        GenerationId = generationId;
        Nodes = new ReadOnlyCollection<RoadGraphNode>(nodes.ToArray());
        Edges = new ReadOnlyCollection<RoadGraphEdge>(edges.ToArray());
        Issues = new ReadOnlyCollection<RoadGraphIssue>(issues.ToArray());
        _adjacency = adjacency;
        EndpointSnapTolerance = endpointSnapTolerance;
        ComponentCount = Nodes.Count == 0 ? 0 : Nodes.Max(node => node.ComponentId) + 1;
    }

    public long SourceRevision { get; }
    public string GenerationId { get; }
    public IReadOnlyList<RoadGraphNode> Nodes { get; }
    public IReadOnlyList<RoadGraphEdge> Edges { get; }
    public IReadOnlyList<RoadGraphIssue> Issues { get; }
    public int ComponentCount { get; }
    public float EndpointSnapTolerance { get; }

    internal IReadOnlyList<RoadGraphArc> ArcsFrom(int nodeId) => _adjacency[nodeId];
}
