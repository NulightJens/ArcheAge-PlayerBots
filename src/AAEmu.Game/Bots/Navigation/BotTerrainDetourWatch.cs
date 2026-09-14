#if !PLAYERBOTS_AAEMU_3_0
using System.Numerics;
using AAEmu.Game.Models.Game.Bots;

namespace AAEmu.Game.Bots.Navigation;

/// <summary>Bounds contour recovery for one local route segment, including later retries.</summary>
internal sealed class BotTerrainDetourWatch
{
    private readonly record struct Segment(object World, uint Instance, BotMovementOwner Owner,
        Vector3 Goal, Vector3 Waypoint)
    {
        internal bool Matches(Segment? candidate) => candidate is { } other &&
            ReferenceEquals(World, other.World) && Instance == other.Instance && Owner == other.Owner &&
            Vector3.DistanceSquared(Goal, other.Goal) <= .25f &&
            Vector3.DistanceSquared(Waypoint, other.Waypoint) <= 1f;
    }

    private sealed record Failure(Segment Segment, Vector3 Origin, DateTime Until);
    private readonly Queue<Failure> _failures = new();
    private Segment? _segment;
    private float _bestDistance;
    private DateTime _progressAt;
    private DateTime _loggedAt;
    internal int Detours { get; private set; }

    internal bool Observe(object world, uint instance, BotMovementState state, Vector3 position,
        DateTime now, double minimumProgress, double stuckSeconds)
    {
        var segment = Current(world, instance, state);
        // Preserve failed segments across a controller's ordinary route cooldown.
        // A different surface/world, goal or connector remains free to make progress.
        foreach (var failure in _failures)
            if (now <= failure.Until && failure.Until - now <= TimeSpan.FromMinutes(5) &&
                failure.Segment.Matches(segment) && Vector3.DistanceSquared(position, failure.Origin) <= 64f)
                return true;

        if (_segment is not { } active)
            return false;
        if (!active.Matches(segment) || now < _progressAt)
        {
            _segment = null;
            return false;
        }

        // Count a new best approach to the committed waypoint, not distance walked
        // around it. A real waypoint transition begins a distinct segment episode.
        var distance = PlanarDistance(position, active.Waypoint);
        var progressThreshold = double.IsFinite(minimumProgress) ? Math.Clamp(minimumProgress, .3, 5) : .3;
        if (_bestDistance - distance >= (float)progressThreshold)
        {
            _bestDistance = distance;
            _progressAt = now;
        }
        var timeoutSeconds = double.IsFinite(stuckSeconds) ? Math.Clamp(stuckSeconds * 5, 15, 120) : 15;
        if (now - _progressAt < TimeSpan.FromSeconds(timeoutSeconds))
            return false;

        _failures.Enqueue(new Failure(active, position, now.AddMinutes(5)));
        while (_failures.Count > 4)
            _failures.Dequeue();
        _segment = null;
        return true;
    }

    internal bool Detour(object world, uint instance, BotMovementState state, Vector3 position, DateTime now)
    {
        var segment = Current(world, instance, state);
        if (segment is not { } current)
            return false;
        if (_segment is not { } active || !active.Matches(current))
        {
            _segment = segment;
            _bestDistance = PlanarDistance(position, current.Waypoint);
            _progressAt = now;
            _loggedAt = DateTime.MinValue;
            Detours = 0;
        }
        Detours++;
        if (_loggedAt != DateTime.MinValue && now >= _loggedAt && now - _loggedAt < TimeSpan.FromSeconds(5))
            return false;
        _loggedAt = now;
        return true;
    }

    private static Segment? Current(object world, uint instance, BotMovementState state) =>
        state.TravelDestination is { } goal && state.Destination is { } waypoint
            ? new Segment(world, instance, state.TravelOwner, goal, waypoint)
            : null;

    private static float PlanarDistance(Vector3 left, Vector3 right) =>
        Vector2.Distance(new Vector2(left.X, left.Y), new Vector2(right.X, right.Y));
}
#endif
