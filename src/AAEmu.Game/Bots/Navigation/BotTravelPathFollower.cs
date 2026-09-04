using System.Numerics;

namespace AAEmu.Game.Bots.Navigation;

/// <summary>Steers along an approved route with a short lookahead.</summary>
internal static class BotTravelPathFollower
{
    internal const float FinalArrivalRadius = 0.5f;
    internal const float IntermediateArrivalRadius = 0.75f;

    private const float MinimumLookAheadDistance = 1.25f;
    private const float MaximumLookAheadDistance = 3f;
    private const float LookAheadSpeedFactor = 0.32f;
    private const float MaximumCornerLookAheadRadians = 100f * MathF.PI / 180f;
    private const float Acceleration = 12f;
    private const float Deceleration = 16f;
    private const float MinimumApproachSpeed = 0.75f;

    internal static Vector3 SelectSteeringTarget(
        Vector3 current,
        Vector3 activeWaypoint,
        Queue<Vector3> remainingWaypoints,
        float speed)
    {
        var lookAhead = Math.Clamp(
            MinimumLookAheadDistance + Math.Max(0f, speed) * LookAheadSpeedFactor,
            MinimumLookAheadDistance,
            MaximumLookAheadDistance);
        var segmentStart = current;
        var segmentEnd = activeWaypoint;
        var distanceLeft = lookAhead;

        foreach (var next in remainingWaypoints)
        {
            var segmentLength = PlanarDistance(segmentStart, segmentEnd);
            if (segmentLength >= distanceLeft)
                return PlanarLerp(segmentStart, segmentEnd, distanceLeft / segmentLength);

            distanceLeft -= segmentLength;
            if (IsSharpCorner(segmentStart, segmentEnd, next))
                return segmentEnd;

            segmentStart = segmentEnd;
            segmentEnd = next;
        }

        var lastSegmentLength = PlanarDistance(segmentStart, segmentEnd);
        if (lastSegmentLength <= distanceLeft || lastSegmentLength < 1e-5f)
            return segmentEnd;

        return PlanarLerp(segmentStart, segmentEnd, distanceLeft / lastSegmentLength);
    }

    internal static Vector3 TurnTowards(Vector3 currentDirection, Vector3 desiredDirection, float maximumTurnRadians)
    {
        var desired = PlanarUnit(desiredDirection);
        if (desired == Vector3.Zero)
            return Vector3.Zero;

        var current = PlanarUnit(currentDirection);
        if (current == Vector3.Zero || maximumTurnRadians <= 0f)
            return desired;

        var dot = Math.Clamp(Vector3.Dot(current, desired), -1f, 1f);
        var angle = MathF.Acos(dot);
        if (angle <= maximumTurnRadians)
            return desired;

        var crossZ = current.X * desired.Y - current.Y * desired.X;
        var turnSign = crossZ < 0f ? -1f : 1f;
        var currentAngle = MathF.Atan2(current.Y, current.X);
        var nextAngle = currentAngle + turnSign * maximumTurnRadians;
        return new Vector3(MathF.Cos(nextAngle), MathF.Sin(nextAngle), 0f);
    }

    internal static float AdvanceSpeed(float currentSpeed, float maximumSpeed, float remainingDistance, float deltaTime)
    {
        if (!float.IsFinite(maximumSpeed) || maximumSpeed <= 0f || deltaTime <= 0f)
            return 0f;

        var usableDistance = Math.Max(0f, remainingDistance - FinalArrivalRadius);
        var brakingSpeed = MathF.Sqrt(2f * Deceleration * usableDistance);
        var desiredSpeed = Math.Min(maximumSpeed, brakingSpeed);
        if (usableDistance > 0f)
            desiredSpeed = Math.Max(Math.Min(MinimumApproachSpeed, maximumSpeed), desiredSpeed);

        var change = desiredSpeed >= currentSpeed
            ? Acceleration * deltaTime
            : Deceleration * deltaTime;
        return Math.Clamp(desiredSpeed, Math.Max(0f, currentSpeed - change), currentSpeed + change);
    }

    internal static float MeasureRemaining(
        Vector3 current,
        Vector3 activeWaypoint,
        Queue<Vector3> remainingWaypoints)
    {
        var total = PlanarDistance(current, activeWaypoint);
        var previous = activeWaypoint;
        foreach (var waypoint in remainingWaypoints)
        {
            total += PlanarDistance(previous, waypoint);
            previous = waypoint;
        }

        return total;
    }

    internal static bool ShouldAdvance(Vector3 current, Vector3 waypoint, bool hasAnotherWaypoint)
    {
        var radius = hasAnotherWaypoint ? IntermediateArrivalRadius : FinalArrivalRadius;
        return PlanarDistance(current, waypoint) < radius;
    }

    private static bool IsSharpCorner(Vector3 from, Vector3 corner, Vector3 to)
    {
        var incoming = PlanarUnit(corner - from);
        var outgoing = PlanarUnit(to - corner);
        if (incoming == Vector3.Zero || outgoing == Vector3.Zero)
            return false;

        var angle = MathF.Acos(Math.Clamp(Vector3.Dot(incoming, outgoing), -1f, 1f));
        return angle > MaximumCornerLookAheadRadians;
    }

    private static Vector3 PlanarUnit(Vector3 value)
    {
        value.Z = 0f;
        return value.LengthSquared() < 1e-8f ? Vector3.Zero : Vector3.Normalize(value);
    }

    private static float PlanarDistance(Vector3 from, Vector3 to)
    {
        var x = to.X - from.X;
        var y = to.Y - from.Y;
        return MathF.Sqrt(x * x + y * y);
    }

    private static Vector3 PlanarLerp(Vector3 from, Vector3 to, float amount) =>
        new(
            from.X + (to.X - from.X) * amount,
            from.Y + (to.Y - from.Y) * amount,
            from.Z + (to.Z - from.Z) * amount);
}
