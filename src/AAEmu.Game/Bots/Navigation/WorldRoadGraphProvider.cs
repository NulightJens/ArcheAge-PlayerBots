namespace AAEmu.Game.Bots.Navigation;

public interface IWorldRoadGraphProvider
{
    WorldRoadGraph Capture();
}

/// <summary>Caches the road graph until AAEmu's source data changes.</summary>
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
