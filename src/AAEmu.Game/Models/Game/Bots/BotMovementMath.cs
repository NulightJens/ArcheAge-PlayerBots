using System.Numerics;

namespace AAEmu.Game.Models.Game.Bots;

internal static class BotMovementMath
{
    private const float Gravity = 9.81f;
    private const float GroundTolerance = 0.1f;
    private const float FollowRunThreshold = 10.0f;
    private const float FallbackRunSpeed = 5.4f;
    private const float FallbackWalkSpeed = 1.8f;
    private const float BackwardMultiplier = 0.65f;

    internal static (Vector3? Destination, bool Run) ComputeFollowDestination(
        Vector3 botPosition,
        Vector3 targetPosition,
        float followDistance)
    {
        var direction = targetPosition - botPosition;
        var distance = direction.Length();
        if (distance > followDistance + 0.1f)
            return (targetPosition - direction * (followDistance / distance), distance > FollowRunThreshold);

        return (null, false);
    }

    internal static (Vector3 Next, bool Arrived) StepTowards(Vector3 current, Vector3 destination, float speed, float dt)
    {
        var direction = destination - current;
        var distance = direction.Length();

        if (distance < 0.5f)
            return (destination, true);

        var moveDistance = speed * dt;
        if (moveDistance >= distance)
            return (destination, true);

        return (current + Vector3.Normalize(direction) * moveDistance, false);
    }

    internal static float ComputeFacingDegrees(Vector3 from, Vector3 to)
    {
        var angleRad = MathF.Atan2(to.Y - from.Y, to.X - from.X);
        return angleRad * 180f / MathF.PI - 90f;
    }

    internal static float DirectionalMultiplier(Vector3 moveDirection, Vector3 directionToTarget)
    {
        if (directionToTarget.LengthSquared() < 0.001f || moveDirection.LengthSquared() < 0.001f)
            return 1f;

        var normalizedTargetDirection = Vector3.Normalize(directionToTarget);
        var normalizedMoveDirection = Vector3.Normalize(moveDirection);
        var dot = Vector3.Dot(normalizedMoveDirection, normalizedTargetDirection);

        if (dot < -0.3f)
            return BackwardMultiplier;
        return 1f;
    }

    internal static Vector3 ComputeVelocity(Vector3 moveDirection, float rotationZRadians, float speed, bool combatWithTarget)
    {
        if (!combatWithTarget)
            return moveDirection * speed;

        var forward = BotMath.Forward(rotationZRadians);
        var dot = Vector3.Dot(moveDirection, forward);
        if (dot < -0.3f)
            return -forward * speed;

        return moveDirection * speed;
    }

    internal static (float NewZ, float NewFallVelocity, bool Landed, bool Falling) ApplyGravity(
        float z,
        float groundZ,
        float fallVelocity,
        float dt)
    {
        if (z <= groundZ + GroundTolerance && fallVelocity <= 0f)
            return (z, fallVelocity, false, false);

        var newFallVelocity = fallVelocity + Gravity * dt;
        var newZ = z - newFallVelocity * dt;
        if (newZ <= groundZ)
            return (groundZ, 0f, true, false);

        return (newZ, newFallVelocity, false, true);
    }

    internal static (float NewZ, float NewVerticalVelocity, bool Landed) ApplyJump(
        float z,
        float groundZ,
        float verticalVelocity,
        float dt)
    {
        var newVerticalVelocity = verticalVelocity - Gravity * dt;
        var newZ = z + newVerticalVelocity * dt;
        if (newZ <= groundZ)
        {
            if (newVerticalVelocity <= 0f)
                return (groundZ, 0f, true);

            // Rising terrain may advance beneath the arc during a horizontal jump.
            // Never publish a bot below the terrain surface while it is ascending.
            newZ = groundZ;
        }

        return (newZ, newVerticalVelocity, false);
    }

    internal static (float Run, float Walk) ResolveSpeed(float aiRun, float aiWalk, float max)
    {
        var run = MathF.Min(aiRun, max);
        var walk = MathF.Min(aiWalk, max);
        if (run < 0.1f)
            run = FallbackRunSpeed;
        if (walk < 0.1f)
            walk = FallbackWalkSpeed;

        return (run, walk);
    }
}
