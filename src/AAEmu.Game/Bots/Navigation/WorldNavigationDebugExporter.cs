using System.Text.Json;
using System.Text.Json.Serialization;

namespace AAEmu.Game.Bots.Navigation;

/// <summary>
/// Produces a deterministic read-only JSON representation. Deliberately exposes no file
/// writer, so callers choose a server-side diagnostics destination and cannot mutate client data.
/// </summary>
public static class WorldNavigationDebugExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public static string Serialize(WorldRoadGraph graph, RoadRouteResult route = null)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var document = new DebugDocument
        {
            SourceRevision = graph.SourceRevision,
            GenerationId = graph.GenerationId,
            ComponentCount = graph.ComponentCount,
            Nodes = graph.Nodes.OrderBy(node => node.Id).Select(node => new DebugNode
            {
                Id = node.Id,
                WorldId = node.WorldId,
                ComponentId = node.ComponentId,
                SurfaceId = node.SurfaceId,
                Position = new DebugPoint(node.Position.X, node.Position.Y, node.Position.Z),
                Endpoints = node.Endpoints.Select(endpoint => new DebugEndpoint
                {
                    EdgeId = endpoint.EdgeId,
                    IsStart = endpoint.IsStart,
                    ZoneId = endpoint.ZoneId,
                    PathName = endpoint.PathName
                }).ToArray()
            }).ToArray(),
            Edges = graph.Edges.OrderBy(edge => edge.Id, StringComparer.Ordinal).Select(edge => new DebugEdge
            {
                Id = edge.Id,
                StartNodeId = edge.StartNodeId,
                EndNodeId = edge.EndNodeId,
                WorldId = edge.WorldId,
                ZoneId = edge.ZoneId,
                PathName = edge.PathName,
                PathType = edge.PathType,
                CellX = edge.CellX,
                CellY = edge.CellY,
                Direction = edge.Direction.ToString(),
                Weight = edge.Weight,
                Points = edge.Points.Select(point => new DebugPoint(point.X, point.Y, point.Z)).ToArray()
            }).ToArray(),
            Issues = graph.Issues.Select(issue => new DebugIssue
            {
                Code = issue.Code.ToString(),
                Severity = issue.Severity.ToString(),
                RoadKey = issue.RoadKey,
                Detail = issue.Detail
            }).ToArray(),
            Route = route == null ? null : new DebugRoute
            {
                Status = route.Status.ToString(),
                Detail = route.Detail,
                GraphGenerationId = route.GraphGenerationId,
                ComponentId = route.ComponentId,
                TotalWeight = route.TotalWeight,
                Waypoints = route.Waypoints.Select(point => new DebugPoint(point.X, point.Y, point.Z)).ToArray(),
                Steps = route.Steps.Select(step => new DebugStep
                {
                    EdgeId = step.EdgeId,
                    ZoneId = step.ZoneId,
                    PathName = step.PathName,
                    PathType = step.PathType,
                    Forward = step.Forward,
                    FromDistance = step.FromDistance,
                    ToDistance = step.ToDistance,
                    Weight = step.Weight
                }).ToArray()
            }
        };

        return JsonSerializer.Serialize(document, JsonOptions).ReplaceLineEndings("\n") + "\n";
    }

    private sealed class DebugDocument
    {
        public long SourceRevision { get; init; }
        public string GenerationId { get; init; }
        public int ComponentCount { get; init; }
        public IReadOnlyList<DebugNode> Nodes { get; init; }
        public IReadOnlyList<DebugEdge> Edges { get; init; }
        public IReadOnlyList<DebugIssue> Issues { get; init; }
        public DebugRoute Route { get; init; }
    }

    private sealed class DebugNode
    {
        public int Id { get; init; }
        public uint WorldId { get; init; }
        public int ComponentId { get; init; }
        public int SurfaceId { get; init; }
        public DebugPoint Position { get; init; }
        public IReadOnlyList<DebugEndpoint> Endpoints { get; init; }
    }

    private sealed class DebugEndpoint
    {
        public string EdgeId { get; init; }
        public bool IsStart { get; init; }
        public uint ZoneId { get; init; }
        public string PathName { get; init; }
    }

    private sealed class DebugEdge
    {
        public string Id { get; init; }
        public int StartNodeId { get; init; }
        public int EndNodeId { get; init; }
        public uint WorldId { get; init; }
        public uint ZoneId { get; init; }
        public string PathName { get; init; }
        public int PathType { get; init; }
        public int CellX { get; init; }
        public int CellY { get; init; }
        public string Direction { get; init; }
        public double Weight { get; init; }
        public IReadOnlyList<DebugPoint> Points { get; init; }
    }

    private sealed class DebugIssue
    {
        public string Code { get; init; }
        public string Severity { get; init; }
        public string RoadKey { get; init; }
        public string Detail { get; init; }
    }

    private sealed class DebugRoute
    {
        public string Status { get; init; }
        public string Detail { get; init; }
        public string GraphGenerationId { get; init; }
        public int ComponentId { get; init; }
        public double TotalWeight { get; init; }
        public IReadOnlyList<DebugPoint> Waypoints { get; init; }
        public IReadOnlyList<DebugStep> Steps { get; init; }
    }

    private sealed class DebugStep
    {
        public string EdgeId { get; init; }
        public uint ZoneId { get; init; }
        public string PathName { get; init; }
        public int PathType { get; init; }
        public bool Forward { get; init; }
        public double FromDistance { get; init; }
        public double ToDistance { get; init; }
        public double Weight { get; init; }
    }

    private sealed record DebugPoint(float X, float Y, float Z);
}
