using System.Numerics;
using AAEmu.Game.Bots.Navigation;

namespace AAEmu.UnitTests.Bots.Navigation;

public class NavigationDecisionBoundaryTests
{
    [Test]
    public async Task Evaluate_NonFiniteGeometry_IsInvalidWithoutSamplingSurface()
    {
        var samples = 0;
        var boundary = new NavigationDecisionBoundary(
            new DelegateSurfaceProvider(_ =>
            {
                samples++;
                return AvailableSurface();
            }),
            NavigationCompatibilityPolicy.SameSurfaceDirectTraversal);

        var decision = boundary.Evaluate(Vector3.Zero, new Vector3(float.NaN, 2f, 0f));

        await Assert.That(decision.Status).IsEqualTo(NavigationDecisionStatus.InvalidSurface);
        await Assert.That(decision.Reason).IsEqualTo(NavigationDiagnosticReason.NonFiniteGeometry);
        await Assert.That(samples).IsEqualTo(0);
    }

    [Test]
    public async Task Evaluate_DestinationSurfaceInvalid_ReturnsInvalidSurface()
    {
        var boundary = new NavigationDecisionBoundary(
            new DelegateSurfaceProvider(position => position.X == 0f
                ? AvailableSurface()
                : new NavigationSurfaceSample(NavigationSurfaceStatus.Invalid)),
            NavigationCompatibilityPolicy.SameSurfaceDirectTraversal);

        var decision = boundary.Evaluate(Vector3.Zero, new Vector3(5f, 0f, 0f));

        await Assert.That(decision.Status).IsEqualTo(NavigationDecisionStatus.InvalidSurface);
        await Assert.That(decision.Reason).IsEqualTo(NavigationDiagnosticReason.DestinationSurfaceInvalid);
    }

    [Test]
    public async Task Evaluate_DestinationHeightDisagreesWithSurface_ReturnsInvalidSurface()
    {
        var boundary = new NavigationDecisionBoundary(
            new DelegateSurfaceProvider(_ => AvailableSurface(height: 2f)),
            NavigationCompatibilityPolicy.SameSurfaceDirectTraversal,
            surfaceHeightTolerance: 0.5f);

        var decision = boundary.Evaluate(new Vector3(0f, 0f, 2f), new Vector3(5f, 0f, 3f));

        await Assert.That(decision.Status).IsEqualTo(NavigationDecisionStatus.InvalidSurface);
        await Assert.That(decision.Reason)
            .IsEqualTo(NavigationDiagnosticReason.DestinationHeightSurfaceDisagreement);
    }

    [Test]
    public async Task Evaluate_SurfaceDataUnavailable_ReturnsUnavailable()
    {
        var boundary = new NavigationDecisionBoundary(
            new DelegateSurfaceProvider(_ => new NavigationSurfaceSample(NavigationSurfaceStatus.Unavailable)),
            NavigationCompatibilityPolicy.SameSurfaceDirectTraversal);

        var decision = boundary.Evaluate(Vector3.Zero, new Vector3(5f, 0f, 0f));

        await Assert.That(decision.Status).IsEqualTo(NavigationDecisionStatus.Unavailable);
        await Assert.That(decision.Reason).IsEqualTo(NavigationDiagnosticReason.NavigationDataUnavailable);
    }

    [Test]
    public async Task Evaluate_DifferentSurfaces_ReturnsUnreachable()
    {
        var boundary = new NavigationDecisionBoundary(
            new DelegateSurfaceProvider(position => AvailableSurface(surfaceId: position.X == 0f ? 10 : 11)),
            NavigationCompatibilityPolicy.SameSurfaceDirectTraversal);

        var decision = boundary.Evaluate(Vector3.Zero, new Vector3(5f, 0f, 0f));

        await Assert.That(decision.Status).IsEqualTo(NavigationDecisionStatus.Unreachable);
        await Assert.That(decision.Reason).IsEqualTo(NavigationDiagnosticReason.SurfaceMismatch);
    }

    [Test]
    public async Task Evaluate_ReachabilityProbeRejects_ReturnsUnreachable()
    {
        var boundary = new NavigationDecisionBoundary(
            new DelegateSurfaceProvider(_ => AvailableSurface(surfaceId: 10)),
            NavigationCompatibilityPolicy.SameSurfaceDirectTraversal,
            new DelegateReachabilityProbe((_, _, _) => NavigationReachabilityStatus.Unreachable));

        var decision = boundary.Evaluate(Vector3.Zero, new Vector3(5f, 0f, 0f));

        await Assert.That(decision.Status).IsEqualTo(NavigationDecisionStatus.Unreachable);
        await Assert.That(decision.Reason).IsEqualTo(NavigationDiagnosticReason.ReachabilityRejected);
    }

    [Test]
    public async Task Evaluate_ReachabilityProbeAccepts_ReturnsReachabilityConfirmed()
    {
        var boundary = new NavigationDecisionBoundary(
            new DelegateSurfaceProvider(_ => AvailableSurface(surfaceId: 10)),
            NavigationCompatibilityPolicy.FailClosed,
            new DelegateReachabilityProbe((_, _, _) => NavigationReachabilityStatus.Reachable));

        var decision = boundary.Evaluate(Vector3.Zero, new Vector3(5f, 0f, 0f));

        await Assert.That(decision.Status).IsEqualTo(NavigationDecisionStatus.Accepted);
        await Assert.That(decision.Reason).IsEqualTo(NavigationDiagnosticReason.ReachabilityConfirmed);
    }

    [Test]
    public async Task Evaluate_SameSurfaceWithoutProbe_UsesNamedCompatibilityPolicy()
    {
        var boundary = new NavigationDecisionBoundary(
            new DelegateSurfaceProvider(_ => AvailableSurface(surfaceId: 10)),
            NavigationCompatibilityPolicy.SameSurfaceDirectTraversal);

        var decision = boundary.Evaluate(Vector3.Zero, new Vector3(5f, 0f, 0f));

        await Assert.That(decision.Status).IsEqualTo(NavigationDecisionStatus.Accepted);
        await Assert.That(decision.Reason)
            .IsEqualTo(NavigationDiagnosticReason.SameSurfaceDirectTraversalCompatibility);
    }

    [Test]
    public async Task Evaluate_NoProbeAndNoCompatibilityPolicy_FailsClosed()
    {
        var boundary = new NavigationDecisionBoundary(
            new DelegateSurfaceProvider(_ => AvailableSurface(surfaceId: 10)));

        var decision = boundary.Evaluate(Vector3.Zero, new Vector3(5f, 0f, 0f));

        await Assert.That(decision.Status).IsEqualTo(NavigationDecisionStatus.Unavailable);
        await Assert.That(decision.Reason).IsEqualTo(NavigationDiagnosticReason.NavigationDataUnavailable);
    }

    private static NavigationSurfaceSample AvailableSurface(float height = 0f, int surfaceId = 0)
    {
        return new NavigationSurfaceSample(NavigationSurfaceStatus.Available, height, surfaceId);
    }

    private sealed class DelegateSurfaceProvider(Func<Vector3, NavigationSurfaceSample> sample)
        : INavigationSurfaceProvider
    {
        public NavigationSurfaceSample Sample(Vector3 position) => sample(position);
    }

    private sealed class DelegateReachabilityProbe(
        Func<Vector3, Vector3, int, NavigationReachabilityStatus> probe) : INavigationReachabilityProbe
    {
        public NavigationReachabilityStatus Probe(Vector3 start, Vector3 destination, int surfaceId) =>
            probe(start, destination, surfaceId);
    }
}
