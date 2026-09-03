using System.Collections.ObjectModel;
using System.Numerics;

namespace AAEmu.Game.Bots.Navigation;

public enum RoadTravelDirection
{
    Bidirectional,
    ForwardOnly,
    ReverseOnly
}

public readonly record struct RoadPoint(float X, float Y, float Z, int SurfaceId = 0)
{
    public Vector3 Position => new(X, Y, Z);
}

/// <summary>
/// Module-owned, immutable copy of one authoritative transfer-road polyline.
/// Point order is the source order and is never normalized or rounded.
/// </summary>
public sealed class RoadPolylineSnapshot
{
    public RoadPolylineSnapshot(
        uint worldId,
        uint zoneId,
        string pathName,
        int pathType,
        int cellX,
        int cellY,
        RoadTravelDirection direction,
        IEnumerable<RoadPoint> points)
    {
        WorldId = worldId;
        ZoneId = zoneId;
        PathName = pathName ?? string.Empty;
        PathType = pathType;
        CellX = cellX;
        CellY = cellY;
        Direction = direction;
        Points = new ReadOnlyCollection<RoadPoint>((points ?? throw new ArgumentNullException(nameof(points))).ToArray());
    }

    public uint WorldId { get; }
    public uint ZoneId { get; }
    public string PathName { get; }
    public int PathType { get; }
    public int CellX { get; }
    public int CellY { get; }
    public RoadTravelDirection Direction { get; }
    public IReadOnlyList<RoadPoint> Points { get; }
}

public sealed class TransferRoadNetworkSnapshot
{
    public TransferRoadNetworkSnapshot(long revision, IEnumerable<RoadPolylineSnapshot> roads)
    {
        Revision = revision;
        Roads = new ReadOnlyCollection<RoadPolylineSnapshot>(
            (roads ?? throw new ArgumentNullException(nameof(roads))).ToArray());
    }

    public long Revision { get; }
    public IReadOnlyList<RoadPolylineSnapshot> Roads { get; }
}

public interface ITransferRoadSnapshotProvider
{
    TransferRoadNetworkSnapshot Capture();
}

#if !PLAYERBOTS_AAEMU_3_0
/// <summary>
/// AAEmu 1.2 adapter for the compatibility-patch snapshot seam. The host does not
/// retain one-way metadata, so its roads are explicitly mapped as bidirectional.
/// </summary>
public sealed class AaemuTransferRoadSnapshotProvider : ITransferRoadSnapshotProvider
{
    public TransferRoadNetworkSnapshot Capture()
    {
        var source = GameData.TransferGameData.Instance.GetTransferRoadsSnapshot();
        return new TransferRoadNetworkSnapshot(
            source.Revision,
            source.Roads.Select(road => new RoadPolylineSnapshot(
                road.WorldId,
                road.ZoneId,
                road.Name,
                road.Type,
                road.CellX,
                road.CellY,
                RoadTravelDirection.Bidirectional,
                road.Points.Select(point => new RoadPoint(
                    point.X,
                    point.Y,
                    point.Z)))));
    }
}
#endif
