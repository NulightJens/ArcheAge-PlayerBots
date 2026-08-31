using System;
using System.Numerics;

namespace AAEmu.Game.Bots.Navigation;

public enum NavigationDecisionStatus
{
    Accepted,
    Unavailable,
    InvalidSurface,
    Unreachable
}

public enum NavigationDiagnosticReason
{
    ReachabilityConfirmed,
    SameSurfaceDirectTraversalCompatibility,
    NonFiniteGeometry,
    NavigationDataUnavailable,
    StartSurfaceInvalid,
    DestinationSurfaceInvalid,
    DestinationHeightSurfaceDisagreement,
    SurfaceMismatch,
    ReachabilityRejected
}

public readonly struct NavigationDecision
{
    public NavigationDecision(NavigationDecisionStatus status, NavigationDiagnosticReason reason)
    {
        Status = status;
        Reason = reason;
    }

    public NavigationDecisionStatus Status { get; }
    public NavigationDiagnosticReason Reason { get; }
    public bool IsAccepted => Status == NavigationDecisionStatus.Accepted;
}

public enum NavigationSurfaceStatus
{
    Available,
    Unavailable,
    Invalid
}

public readonly struct NavigationSurfaceSample
{
    public NavigationSurfaceSample(NavigationSurfaceStatus status, float height = 0f, int surfaceId = 0)
    {
        Status = status;
        Height = height;
        SurfaceId = surfaceId;
    }

    public NavigationSurfaceStatus Status { get; }
    public float Height { get; }
    public int SurfaceId { get; }
}

public enum NavigationReachabilityStatus
{
    Reachable,
    Unreachable,
    Unavailable
}

public enum NavigationCompatibilityPolicy
{
    FailClosed,
    SameSurfaceDirectTraversal
}

public interface INavigationSurfaceProvider
{
    NavigationSurfaceSample Sample(Vector3 position);
}

public interface INavigationReachabilityProbe
{
    NavigationReachabilityStatus Probe(Vector3 start, Vector3 destination, int surfaceId);
}

public interface INavigationDecisionBoundary
{
    NavigationDecision Evaluate(Vector3 start, Vector3 destination);
}

/// <summary>
/// Validates a destination before movement is published. The same-surface policy is a
/// deliberately narrow compatibility seam; it does not claim wall or terrain avoidance.
/// </summary>
public sealed class NavigationDecisionBoundary : INavigationDecisionBoundary
{
    public const float DefaultSurfaceHeightTolerance = 1f;

    private readonly INavigationSurfaceProvider _surfaceProvider;
    private readonly INavigationReachabilityProbe _reachabilityProbe;
    private readonly NavigationCompatibilityPolicy _compatibilityPolicy;
    private readonly float _surfaceHeightTolerance;

    public NavigationDecisionBoundary(
        INavigationSurfaceProvider surfaceProvider,
        NavigationCompatibilityPolicy compatibilityPolicy = NavigationCompatibilityPolicy.FailClosed,
        INavigationReachabilityProbe reachabilityProbe = null,
        float surfaceHeightTolerance = DefaultSurfaceHeightTolerance)
    {
        _surfaceProvider = surfaceProvider ?? throw new ArgumentNullException(nameof(surfaceProvider));
        if (!IsFinite(surfaceHeightTolerance) || surfaceHeightTolerance < 0f)
            throw new ArgumentOutOfRangeException(nameof(surfaceHeightTolerance));

        _compatibilityPolicy = compatibilityPolicy;
        _reachabilityProbe = reachabilityProbe;
        _surfaceHeightTolerance = surfaceHeightTolerance;
    }

    public NavigationDecision Evaluate(Vector3 start, Vector3 destination)
    {
        if (!IsFinite(start) || !IsFinite(destination))
            return Reject(NavigationDecisionStatus.InvalidSurface, NavigationDiagnosticReason.NonFiniteGeometry);

        NavigationSurfaceSample startSurface;
        NavigationSurfaceSample destinationSurface;
        try
        {
            startSurface = _surfaceProvider.Sample(start);
            destinationSurface = _surfaceProvider.Sample(destination);
        }
        catch
        {
            return Reject(NavigationDecisionStatus.Unavailable, NavigationDiagnosticReason.NavigationDataUnavailable);
        }

        if (startSurface.Status == NavigationSurfaceStatus.Unavailable ||
            destinationSurface.Status == NavigationSurfaceStatus.Unavailable)
        {
            return Reject(NavigationDecisionStatus.Unavailable, NavigationDiagnosticReason.NavigationDataUnavailable);
        }

        if (startSurface.Status != NavigationSurfaceStatus.Available || !IsFinite(startSurface.Height))
            return Reject(NavigationDecisionStatus.InvalidSurface, NavigationDiagnosticReason.StartSurfaceInvalid);

        if (destinationSurface.Status != NavigationSurfaceStatus.Available || !IsFinite(destinationSurface.Height))
            return Reject(NavigationDecisionStatus.InvalidSurface, NavigationDiagnosticReason.DestinationSurfaceInvalid);

        if (MathF.Abs(destination.Z - destinationSurface.Height) > _surfaceHeightTolerance)
        {
            return Reject(
                NavigationDecisionStatus.InvalidSurface,
                NavigationDiagnosticReason.DestinationHeightSurfaceDisagreement);
        }

        if (startSurface.SurfaceId != destinationSurface.SurfaceId)
            return Reject(NavigationDecisionStatus.Unreachable, NavigationDiagnosticReason.SurfaceMismatch);

        if (_reachabilityProbe != null)
        {
            NavigationReachabilityStatus reachability;
            try
            {
                reachability = _reachabilityProbe.Probe(start, destination, startSurface.SurfaceId);
            }
            catch
            {
                return Reject(NavigationDecisionStatus.Unavailable, NavigationDiagnosticReason.NavigationDataUnavailable);
            }

            return reachability switch
            {
                NavigationReachabilityStatus.Reachable =>
                    Accept(NavigationDiagnosticReason.ReachabilityConfirmed),
                NavigationReachabilityStatus.Unreachable =>
                    Reject(NavigationDecisionStatus.Unreachable, NavigationDiagnosticReason.ReachabilityRejected),
                _ => Reject(NavigationDecisionStatus.Unavailable, NavigationDiagnosticReason.NavigationDataUnavailable)
            };
        }

        if (_compatibilityPolicy == NavigationCompatibilityPolicy.SameSurfaceDirectTraversal)
            return Accept(NavigationDiagnosticReason.SameSurfaceDirectTraversalCompatibility);

        return Reject(NavigationDecisionStatus.Unavailable, NavigationDiagnosticReason.NavigationDataUnavailable);
    }

    internal static INavigationDecisionBoundary CreateSameSurfaceGroundCompatibility(
        Func<float, float, float> groundHeight)
    {
        return new NavigationDecisionBoundary(
            new GroundHeightNavigationSurfaceProvider(groundHeight),
            NavigationCompatibilityPolicy.SameSurfaceDirectTraversal);
    }

    private static NavigationDecision Accept(NavigationDiagnosticReason reason)
    {
        return new NavigationDecision(NavigationDecisionStatus.Accepted, reason);
    }

    private static NavigationDecision Reject(NavigationDecisionStatus status, NavigationDiagnosticReason reason)
    {
        return new NavigationDecision(status, reason);
    }

    private static bool IsFinite(Vector3 value)
    {
        return IsFinite(value.X) && IsFinite(value.Y) && IsFinite(value.Z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private sealed class GroundHeightNavigationSurfaceProvider : INavigationSurfaceProvider
    {
        private readonly Func<float, float, float> _groundHeight;

        public GroundHeightNavigationSurfaceProvider(Func<float, float, float> groundHeight)
        {
            _groundHeight = groundHeight ?? throw new ArgumentNullException(nameof(groundHeight));
        }

        public NavigationSurfaceSample Sample(Vector3 position)
        {
            var height = _groundHeight(position.X, position.Y);
            return IsFinite(height)
                ? new NavigationSurfaceSample(NavigationSurfaceStatus.Available, height)
                : new NavigationSurfaceSample(NavigationSurfaceStatus.Invalid);
        }
    }
}
