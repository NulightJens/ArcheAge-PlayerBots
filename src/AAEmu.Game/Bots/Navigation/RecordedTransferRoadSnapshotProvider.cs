using System.Buffers;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AAEmu.Game.Bots.Navigation;

/// <summary>Loads reviewed player-recorded roads shipped with the module.</summary>
public sealed class RecordedTransferRoadSnapshotProvider : ITransferRoadSnapshotProvider
{
    public const string SchemaVersion = "playerbots.recorded-transfer-road-snapshot.v1";

    private readonly string _directory;
    private readonly object _sync = new();
    private Dictionary<string, CachedRoadFile> _files = new(StringComparer.Ordinal);
    private TransferRoadNetworkSnapshot _snapshot;
    private byte[] _digest;

    private sealed record CachedRoadFile(byte[] Bytes, IReadOnlyList<RoadPolylineSnapshot> Roads);

    public RecordedTransferRoadSnapshotProvider(string directory = null)
    {
        _directory = directory ?? Path.Combine(AppContext.BaseDirectory, "Data", "BotNavigation");
    }

    public TransferRoadNetworkSnapshot Capture()
    {
        lock (_sync)
            return CaptureLocked();
    }

    private TransferRoadNetworkSnapshot CaptureLocked()
    {
        if (!Directory.Exists(_directory))
        {
            _files.Clear();
            _snapshot = null;
            _digest = null;
            return new TransferRoadNetworkSnapshot(0, Array.Empty<RoadPolylineSnapshot>());
        }

        var roads = new List<RoadPolylineSnapshot>();
        var files = new Dictionary<string, CachedRoadFile>(StringComparer.Ordinal);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in Directory.EnumerateFiles(_directory, "*.json", SearchOption.TopDirectoryOnly)
                     .OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
        {
            // Check actual bytes, including same-size edits with preserved timestamps.
            // Pool the read buffer so unchanged large recordings do not allocate or reparse.
            using var stream = File.OpenRead(file);
            var length = checked((int)stream.Length);
            var buffer = ArrayPool<byte>.Shared.Rent(length);
            try
            {
                stream.ReadExactly(buffer.AsSpan(0, length));
                var bytes = buffer.AsSpan(0, length);
                hash.AppendData(Encoding.UTF8.GetBytes(Path.GetFileName(file).ToLowerInvariant()));
                hash.AppendData(bytes);
                if (!_files.TryGetValue(file, out var cached) || !bytes.SequenceEqual(cached.Bytes))
                    cached = new CachedRoadFile(bytes.ToArray(), Parse(bytes, file));
                files.Add(file, cached);
                roads.AddRange(cached.Roads);
            }
            finally { ArrayPool<byte>.Shared.Return(buffer); }
        }

        var digest = hash.GetHashAndReset();
        // Publish only after every file validates. A bad replacement must fail visibly.
        _files = files;
        if (_snapshot != null && digest.AsSpan().SequenceEqual(_digest))
            return _snapshot;
        _digest = digest;
        return _snapshot = new TransferRoadNetworkSnapshot(BitConverter.ToInt64(digest, 0), roads);
    }

    internal static IReadOnlyList<RoadPolylineSnapshot> Parse(ReadOnlySpan<byte> json, string source = "recorded-road")
    {
        RecordedRoadDocument document;
        try
        {
            document = JsonSerializer.Deserialize<RecordedRoadDocument>(json, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"{source}: malformed recorded-road document", exception);
        }
        if (document == null)
            throw new InvalidDataException($"{source}: empty recorded-road document");
        if (!string.Equals(document.SchemaVersion, SchemaVersion, StringComparison.Ordinal))
            throw new InvalidDataException($"{source}: unsupported schema '{document.SchemaVersion}'");
        if (document.Roads == null)
            throw new InvalidDataException($"{source}: roads are required");

        var roads = new List<RoadPolylineSnapshot>(document.Roads.Count);
        foreach (var road in document.Roads)
        {
            if (road == null || road.WorldId == null || string.IsNullOrWhiteSpace(road.PathName) ||
                road.Points is not { Count: >= 2 } ||
                !Enum.TryParse<RoadTravelDirection>(road.Direction, true, out var direction))
            {
                throw new InvalidDataException($"{source}: invalid recorded road");
            }

            var points = road.Points.Select(point =>
            {
                if (point == null || !float.IsFinite(point.X) || !float.IsFinite(point.Y) || !float.IsFinite(point.Z))
                    throw new InvalidDataException($"{source}: road '{road.PathName}' contains a non-finite point");
                return new RoadPoint(point.X, point.Y, point.Z, point.SurfaceId);
            });
            roads.Add(new RoadPolylineSnapshot(
                road.WorldId.Value,
                road.ZoneId,
                road.PathName,
                road.PathType,
                road.CellX,
                road.CellY,
                direction,
                points));
        }

        return new ReadOnlyCollection<RoadPolylineSnapshot>(roads);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class RecordedRoadDocument
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; init; }

        public List<RecordedRoad> Roads { get; init; }
    }

    private sealed class RecordedRoad
    {
        public uint? WorldId { get; init; }
        public uint ZoneId { get; init; }
        public string PathName { get; init; }
        public int PathType { get; init; }
        public int CellX { get; init; }
        public int CellY { get; init; }
        public string Direction { get; init; }
        public List<RecordedPoint> Points { get; init; }
    }

    private sealed class RecordedPoint
    {
        public float X { get; init; }
        public float Y { get; init; }
        public float Z { get; init; }
        public int SurfaceId { get; init; }
    }
}

/// <summary>Combines host roads with module-owned reviewed roads.</summary>
public sealed class CompositeTransferRoadSnapshotProvider : ITransferRoadSnapshotProvider
{
    private readonly IReadOnlyList<ITransferRoadSnapshotProvider> _providers;

    public CompositeTransferRoadSnapshotProvider(params ITransferRoadSnapshotProvider[] providers)
    {
        _providers = providers ?? throw new ArgumentNullException(nameof(providers));
    }

    public TransferRoadNetworkSnapshot Capture()
    {
        var roads = new List<RoadPolylineSnapshot>();
        var revision = 1469598103934665603L;
        foreach (var provider in _providers)
        {
            var snapshot = provider?.Capture() ?? throw new InvalidOperationException("Road provider returned no snapshot.");
            roads.AddRange(snapshot.Roads);
            revision = unchecked((revision ^ snapshot.Revision) * 1099511628211L);
            revision = unchecked((revision ^ snapshot.Roads.Count) * 1099511628211L);
        }
        return new TransferRoadNetworkSnapshot(revision, roads);
    }
}
